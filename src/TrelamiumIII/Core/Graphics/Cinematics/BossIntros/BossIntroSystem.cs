using System.Collections.Generic;
using Terraria.GameContent;
using Terraria.UI;
using TrelamiumIII.Core.Graphics.Cinematics.Camera;
using TrelamiumIII.Core.Graphics.ScreenEffects;
using TrelamiumIII.Utilities;
using static TrelamiumIII.Utilities.EaseFunction;

namespace TrelamiumIII.Core.Graphics.Cinematics.BossIntros;

/// <summary>
///     Runs a boss's entrance: clears the interface, rumbles the camera, drops the bars in, and puts
///     the name and the soundtrack credit on screen.
/// </summary>
/// <remarks>
///     <para>
///         <b>Client-side and cosmetic, start to finish.</b> Nothing here touches the boss, the world
///         or the player, so it is started independently on every client that can see the fight
///         rather than synced -- a player who walks in halfway does not get a title card for a fight
///         already in progress, which is the correct behaviour and comes for free.
///     </para>
///     <para>
///         <b>Why the interface is cleared rather than faded.</b> Vanilla's layers have no opacity to
///         animate; the only lever tModLoader hands out is <see cref="GameInterfaceLayer.Active" />,
///         which is a switch. A true alpha fade means capturing the whole interface into a render
///         target and compositing it, and binding a target that late in the frame risks discarding
///         the scene behind it (see <see cref="Shockwaves.TileShockwaveSystem" /> for the same
///         hazard). So the elements are dropped on a stagger instead, outermost first, which reads as
///         the screen clearing itself rather than as a cut.
///     </para>
/// </remarks>
internal sealed class BossIntroSystem : ModSystem
{
    /// <summary>
    ///     Interface layers that stand down for the entrance.
    /// </summary>
    /// <remarks>
    ///     <c>Out</c> is frames from the start of the intro, ordered outside-in so the clear sweeps
    ///     toward the middle of the screen. <c>Back</c> is frames from the start of the outro.
    ///     <para>
    ///         The restore stagger is usually invisible: <see cref="LetterboxSystem" /> independently
    ///         hides most of this list for as long as the bars have any height, so in practice the
    ///         interface returns as they finish retracting. The offsets still matter for a boss whose
    ///         intro runs without a letterbox.
    ///     </para>
    /// </remarks>
    private static readonly (string Layer, int Out, int Back)[] Teardown =
    [
        ("Vanilla: Inventory", 0, 30),
        ("Vanilla: Mouse Over", 0, 30),
        ("Vanilla: Laser Ruler", 0, 30),
        ("Vanilla: Tile Grid Option", 0, 30),
        ("Vanilla: Info Accessories Bar", 0, 26),
        ("Vanilla: Builder Accessories Bar", 3, 22),
        ("Vanilla: Map / Minimap", 6, 16),
        ("Vanilla: Resource Bars", 12, 6),
        ("Vanilla: Hotbar", 18, 0),
        ("Vanilla: Radial Hotbars", 18, 0),
    ];

    /// <summary>Longest <c>Back</c> above. The intro cannot finish before the last layer is home.</summary>
    private const int RestoreTail = 30;

    private const int CardFade = 20;

    /// <summary>Shake floor during the approach, before it ramps toward the intro's own figure.</summary>
    private const int RumbleFloor = 2;

    private static BossIntro? current;

    private static int timer;

    /// <summary>Tick <see cref="Settle" /> was called on, or -1 while the entrance is still running.</summary>
    private static int outroStart = -1;

    public static bool IsPlaying => current is not null;

    /// <summary>True once the fight has started and the furniture is on its way out.</summary>
    public static bool IsSettling => current is not null && outroStart >= 0;

    /// <summary>
    ///     Starts <paramref name="intro" />, replacing anything already on screen.
    /// </summary>
    /// <remarks>
    ///     Two bosses arriving together is not a case worth queueing for: the second card would play
    ///     to an empty room minutes after the fight it belongs to began.
    /// </remarks>
    public static void Play(BossIntro intro)
    {
        if (Main.dedServ)
            return;

        current = intro;
        timer = 0;
        outroStart = -1;
    }

    /// <summary>
    ///     The boss has arrived and the fight is live. Takes the bars, the lines and the interface
    ///     back to normal; the track card keeps its own schedule and usually outlives them.
    /// </summary>
    public static void Settle()
    {
        if (current is not { } intro || outroStart >= 0)
            return;

        outroStart = timer;

        Letterbox.Hide(intro.OutroTicks, EaseQuadInOut);
        ActionLinesSystem.Stop(intro.OutroTicks);
    }

    /// <summary>Tears the whole thing down now. For world unload and for a boss that never arrived.</summary>
    public static void Stop()
    {
        if (current is null)
            return;

        current = null;
        timer = 0;
        outroStart = -1;

        Letterbox.Hide(20);
        ActionLinesSystem.Stop(20);
    }

    public override void OnWorldUnload() => Stop();

    public override void PostUpdateEverything()
    {
        if (Main.dedServ || current is not { } intro)
            return;

        timer++;

        if (timer < intro.RevealTick)
            Rumble(intro);
        else if (timer == intro.RevealTick)
            Reveal(intro);

        // Backstop only. The boss itself is what normally ends the entrance, and it does that by
        // landing -- which is a distance, not a duration.
        if (outroStart < 0 && timer >= intro.MaxLength)
            Settle();

        if (timer >= EndTick(intro))
            current = null;
    }

    /// <summary>Ground rumble, building as whatever is under the floor gets closer to the surface.</summary>
    private static void Rumble(BossIntro intro)
    {
        float progress = timer / (float)Math.Max(intro.RevealTick, 1);
        int strength = (int)MathHelper.Lerp(RumbleFloor, intro.RumbleShake, EaseQuadIn.Ease(progress));

        CameraSystem.shake = Math.Max(CameraSystem.shake, strength);
    }

    private static void Reveal(BossIntro intro)
    {
        Letterbox.Show(
            intro.LetterboxTicks,
            intro.LetterboxHeight,
            opacity: 1f,
            EaseCubicOut,
            borderSize: 26,
            borderIntensity: 0.45f
        );

        ActionLinesSystem.Density = intro.LineDensity;
        ActionLinesSystem.Play(intro.LineIntensity, intro.LineRamp);

        CameraSystem.shake = Math.Max(CameraSystem.shake, intro.RevealShake);
    }

    /// <summary>
    ///     Frame the whole sequence is finally done on.
    /// </summary>
    /// <remarks>
    ///     The card outlasting the bars is the normal case, not an edge one, so this is the later of
    ///     the two schedules rather than the end of the outro.
    /// </remarks>
    private static int EndTick(BossIntro intro)
    {
        int cardEnd = intro.RevealTick + intro.CardDelay + intro.CardHold + CardFade;

        if (outroStart < 0)
            return int.MaxValue;

        return Math.Max(outroStart + intro.OutroTicks + RestoreTail, cardEnd);
    }

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        if (current is not { } intro)
            return;

        foreach (var layer in layers)
        {
            if (IsHidden(intro, layer.Name))
                layer.Active = false;
        }

        // Above everything except tooltips, which should still be able to sit over a title card.
        int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");

        layers.Insert(
            index < 0 ? layers.Count : index,
            new LegacyGameInterfaceLayer(
                $"{TrelamiumIII.NAME_PREFIX}Boss Intro",
                static () =>
                {
                    Draw(Main.spriteBatch);

                    return true;
                },
                // None, not UI: the bars have to cover the screen exactly and the card is composed
                // against the screen, not against the player's interface scale.
                InterfaceScaleType.None
            )
        );
    }

    private static bool IsHidden(BossIntro intro, string layerName)
    {
        foreach (var (layer, hideAt, restoreAt) in Teardown)
        {
            if (layer != layerName)
                continue;

            if (timer < hideAt)
                return false;

            return outroStart < 0 || timer < outroStart + restoreAt;
        }

        return false;
    }

    private static void Draw(SpriteBatch spriteBatch)
    {
        if (current is not { } intro || Main.gameMenu)
            return;

        // Skipped while a cutscene owns the screen: Cutscene.Draw puts the same bars up, and two
        // passes of the same black is twice as dark.
        if (CutsceneSystem.CurrentCutscene is null)
            Letterbox.Draw(spriteBatch);

        DrawNamePlate(spriteBatch, intro);
        DrawTrackCard(spriteBatch, intro);
    }

    private static void DrawNamePlate(SpriteBatch spriteBatch, BossIntro intro)
    {
        float exit = ExitFade(intro);

        if (exit <= 0f)
            return;

        float appear = Rise(intro.RevealTick + 4, 24, EaseCubicOut);
        float alpha = appear * exit;

        if (alpha <= 0f)
            return;

        var font = FontAssets.DeathText.Value;
        float scale = NameScale;

        Vector2 size = font.MeasureString(intro.Name) * scale;

        // Anchored to the screen rather than to the bar. Riding the letterbox would have the name
        // sliding down with it for the whole reveal, and jumping again when it retracts.
        float centreY = Main.screenHeight * 0.045f + MathHelper.Lerp(-16f, 0f, appear);
        var centre = new Vector2(Main.screenWidth * 0.5f, centreY);

        Utils.DrawBorderStringBig(spriteBatch, intro.Name, centre, intro.Accent * alpha, scale, 0.5f, 0.5f);

        // Rule under the name, wiping outward from the middle a beat behind the text.
        float wipe = Rise(intro.RevealTick + 10, 30, EaseCubicOut);

        if (wipe > 0f)
        {
            float width = (size.X + 96f) * wipe;

            var rule = new Rectangle(
                (int)(centre.X - width * 0.5f),
                (int)(centreY + size.Y * 0.5f + 4f),
                (int)width,
                2
            );

            spriteBatch.Draw(TextureAssets.MagicPixel.Value, rule, intro.Accent * (0.8f * alpha));
        }

        if (intro.Epithet is not { Length: > 0 } epithet)
            return;

        float epithetAlpha = Rise(intro.RevealTick + 18, 26, EaseCubicOut) * exit;

        Utils.DrawBorderString(
            spriteBatch,
            epithet,
            new Vector2(centre.X, centreY + size.Y * 0.5f + 18f),
            Color.White * (0.7f * epithetAlpha),
            0.95f,
            0.5f,
            0.5f
        );
    }

    private static void DrawTrackCard(SpriteBatch spriteBatch, BossIntro intro)
    {
        int start = intro.RevealTick + intro.CardDelay;

        float appear = Rise(start, CardFade, EaseCubicOut);
        float leave = 1f - Rise(start + intro.CardHold, CardFade, EaseQuadIn);
        float alpha = appear * leave;

        if (alpha <= 0f)
            return;

        var font = FontAssets.MouseText.Value;

        Vector2 titleSize = font.MeasureString(intro.TrackTitle) * TrackTitleScale;
        Vector2 artistSize = font.MeasureString(intro.TrackArtist) * TrackArtistScale;

        // Slides in from off the right edge, and settles on a fixed line rather than on top of the
        // bottom bar -- otherwise it walks back down the screen as the bar retracts.
        float right = Main.screenWidth - 46f + MathHelper.Lerp(44f, 0f, appear);
        float baseline = Main.screenHeight * 0.88f;

        Utils.DrawBorderString(spriteBatch, intro.TrackArtist, new Vector2(right, baseline), Color.White * (0.62f * alpha), TrackArtistScale, 1f, 1f);
        Utils.DrawBorderString(spriteBatch, intro.TrackTitle, new Vector2(right, baseline - artistSize.Y - 1f), intro.Accent * alpha, TrackTitleScale, 1f, 1f);

        float height = titleSize.Y + artistSize.Y + 3f;

        var marker = new Rectangle((int)right + 10, (int)(baseline - height), 2, (int)height);

        spriteBatch.Draw(TextureAssets.MagicPixel.Value, marker, intro.Accent * (0.85f * alpha));
    }

    /// <summary>1 while the entrance holds, easing to 0 across the outro.</summary>
    private static float ExitFade(BossIntro intro)
    {
        if (outroStart < 0)
            return 1f;

        return 1f - Math.Clamp((timer - outroStart) / (float)Math.Max(intro.OutroTicks, 1), 0f, 1f);
    }

    /// <summary>0 before <paramref name="start" />, easing to 1 over the next <paramref name="ticks" />.</summary>
    private static float Rise(int start, int ticks, EaseFunction? ease = null)
    {
        float progress = Math.Clamp((timer - start) / (float)Math.Max(ticks, 1), 0f, 1f);

        return (ease ?? Linear).Ease(progress);
    }

    /// <summary>
    ///     Scaled off the window height so the card reads the same on a 720p window as on a 4K one.
    /// </summary>
    /// <remarks>
    ///     Screen-relative rather than UI-scale-relative on purpose: this is composed against the
    ///     frame, alongside the letterbox, and should not move when a player changes their interface
    ///     scale.
    /// </remarks>
    private static float NameScale => Math.Clamp(Main.screenHeight / 1080f, 0.65f, 1.5f) * 0.95f;

    private static float TrackTitleScale => Math.Clamp(Main.screenHeight / 1080f, 0.75f, 1.4f) * 1.05f;

    private static float TrackArtistScale => Math.Clamp(Main.screenHeight / 1080f, 0.75f, 1.4f) * 0.85f;
}
