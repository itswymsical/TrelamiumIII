using System.Collections.Generic;
using ReLogic.Graphics;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.Localization;
using Terraria.ModLoader.IO;
using Terraria.UI;
using TrelamiumIII.Core.Dialogue;

namespace TrelamiumIII.Core.UI.Jobs;

/// <summary>
///     The one-time walkthrough after the Guide hands out his opening job: ask the player to open
///     their inventory, then point at the job tracker that comes up with it.
/// </summary>
/// <remarks>
///     <para>
///         Started when the Home Forager greeting is played through, whichever answer it ends on; see
///         <c>GuideDialogue</c>. Shown once per player, which <see cref="JobTrackerHintPlayer" />
///         remembers.
///     </para>
///     <para>
///         Closing the inventory straight away puts it back to the first step rather than counting
///         as read: the tracker step only finishes once it has been on screen long enough to read,
///         or the player says they have it.
///     </para>
/// </remarks>
public sealed class JobTrackerHint : ModSystem
{
    private enum Step
    {
        None,
        OpenInventory,
        ShowTracker,
    }

    /// <summary>Ticks the tracker callout has to have been up before closing the inventory counts as having read it.</summary>
    private const int READ_TICKS = 90;

    private const float FADE_SPEED = 0.08f;

    private const int PADDING = 12;

    /// <summary>Gap between the callout's arrow and the tracker's highlight.</summary>
    private const int GAP = 6;

    private const int ARROW = 9;

    private static readonly Color PanelColor = new Color(63, 65, 151) * 0.9f;

    private static readonly Color HighlightColor = new(255, 214, 90);

    private static Step step;

    private static int stepTicks;

    private static float fade;

    public static bool IsActive => step != Step.None;

    /// <summary>Starts the walkthrough for the local player, unless they have already been through it.</summary>
    public static void Begin()
    {
        if (Main.dedServ || Main.LocalPlayer.GetModPlayer<JobTrackerHintPlayer>().Seen)
            return;

        GoTo(Main.playerInventory ? Step.ShowTracker : Step.OpenInventory);
    }

    private static void GoTo(Step next)
    {
        step = next;
        stepTicks = 0;
    }

    private static void Finish()
    {
        Main.LocalPlayer.GetModPlayer<JobTrackerHintPlayer>().Seen = true;
        step = Step.None;
    }

    public override void OnWorldUnload()
    {
        step = Step.None;
        fade = 0f;
    }

    public override void UpdateUI(GameTime gameTime)
    {
        if (step == Step.None)
        {
            fade = 0f;
            return;
        }

        // Held back while anyone is talking: the hint follows the Guide's line, it does not talk over it.
        bool held = DialogueManager.Instance.IsAnyActive();
        fade = MathHelper.Clamp(fade + (held ? -FADE_SPEED : FADE_SPEED), 0f, 1f);

        if (held)
            return;

        stepTicks++;

        switch (step)
        {
            case Step.OpenInventory when Main.playerInventory:
                GoTo(Step.ShowTracker);
                SoundEngine.PlaySound(SoundID.MenuTick);
                break;

            case Step.ShowTracker when !Main.playerInventory:
                if (stepTicks >= READ_TICKS)
                    Finish();
                else
                    GoTo(Step.OpenInventory);

                break;
        }
    }

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        // Under the mouse text rather than next to the tracker's own layer. The tracker has to have
        // drawn this frame for its bounds to be current, and every interface layer it could sit in
        // comes before this one, whichever order the two systems happened to load in.
        int index = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));

        if (index == -1)
            return;

        layers.Insert(index, new LegacyGameInterfaceLayer(
            "TrelamiumIII: Job Tracker Hint",
            delegate
            {
                Draw(Main.spriteBatch);
                return true;
            },
            InterfaceScaleType.UI));
    }

    private static void Draw(SpriteBatch spriteBatch)
    {
        if (fade <= 0f)
            return;

        switch (step)
        {
            case Step.OpenInventory:
                DrawOpenInventory(spriteBatch);
                break;

            case Step.ShowTracker:
                DrawTracker(spriteBatch);
                break;
        }
    }

    /// <summary>The first step: a box at the top of the screen naming the key that opens the inventory.</summary>
    private static void DrawOpenInventory(SpriteBatch spriteBatch)
    {
        string text = Text("OpenInventory").Format(InventoryKey());
        Vector2 size = FontAssets.MouseText.Value.MeasureString(text);

        var box = new Rectangle(
            (int)(Main.screenWidth / 2f - size.X / 2f) - PADDING,
            110 + (int)(MathF.Sin(Main.GlobalTimeWrappedHourly * 3f) * 3f),
            (int)size.X + PADDING * 2,
            (int)size.Y + PADDING * 2 - 8);

        Utils.DrawInvBG(spriteBatch, box, PanelColor * fade);
        Utils.DrawBorderString(spriteBatch, text, new Vector2(box.X + PADDING, box.Y + PADDING - 2), Color.White * fade);
    }

    /// <summary>The second step: the tracker outlined, and a callout beside it saying what it is.</summary>
    private static void DrawTracker(SpriteBatch spriteBatch)
    {
        // Nothing to point at until the panel has drawn. It fades in with the inventory, so the hint does too.
        if (JobSidebarManager.Instance?.TrackerBounds is not { } tracker)
            return;

        float shown = fade * JobSidebarManager.Instance.TrackerVisibility;

        if (shown <= 0f)
            return;

        float pulse = 0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * 5f);
        Color glow = Color.Lerp(HighlightColor, Color.White, pulse * 0.4f) * shown;

        Fill(spriteBatch, tracker, HighlightColor * (0.06f + 0.06f * pulse) * shown);
        Outline(spriteBatch, tracker, glow);

        DynamicSpriteFont font = FontAssets.MouseText.Value;

        string title = Text("TrackerTitle").Value;
        string body = Text("TrackerBody").Value;
        string dismiss = Text("Dismiss").Value;

        const float BODY_SCALE = 0.85f;

        Vector2 titleSize = font.MeasureString(title);
        Vector2 bodySize = font.MeasureString(body) * BODY_SCALE;
        Vector2 dismissSize = font.MeasureString(dismiss) * BODY_SCALE;

        int width = (int)MathF.Max(titleSize.X, MathF.Max(bodySize.X, dismissSize.X)) + PADDING * 2;
        int height = (int)(titleSize.Y + bodySize.Y + dismissSize.Y) + PADDING;

        // Left of the tracker, nudging toward it, never off the top of the screen.
        int nudge = (int)(MathF.Sin(Main.GlobalTimeWrappedHourly * 3f) * 3f);

        var box = new Rectangle(
            tracker.Left - GAP - ARROW - width + nudge,
            Math.Max(tracker.Center.Y - height / 2, 10),
            width,
            height);

        Utils.DrawInvBG(spriteBatch, box, PanelColor * shown);
        Arrow(spriteBatch, box.Right, Math.Clamp(tracker.Center.Y, box.Top + ARROW + 4, box.Bottom - ARROW - 4), PanelColor * shown);

        float y = box.Y + PADDING / 2f;

        Utils.DrawBorderString(spriteBatch, title, new Vector2(box.X + PADDING, y), HighlightColor * shown);
        y += titleSize.Y - 4f;

        Utils.DrawBorderString(spriteBatch, body, new Vector2(box.X + PADDING, y), Color.White * shown, BODY_SCALE);
        y += bodySize.Y;

        var dismissArea = new Rectangle((int)(box.Right - PADDING - dismissSize.X), (int)y, (int)dismissSize.X, (int)dismissSize.Y);
        bool hovering = dismissArea.Contains(Main.MouseScreen.ToPoint()) && !PlayerInput.IgnoreMouseInterface;

        Utils.DrawBorderString(spriteBatch, dismiss, dismissArea.TopLeft(), (hovering ? HighlightColor : Color.LightGray) * shown, BODY_SCALE);

        if (box.Contains(Main.MouseScreen.ToPoint()))
            Main.LocalPlayer.mouseInterface = true;

        if (hovering && shown > 0.9f && Main.mouseLeft && Main.mouseLeftRelease)
        {
            Main.mouseLeftRelease = false;
            SoundEngine.PlaySound(SoundID.MenuClose);
            Finish();
        }
    }

    private static LocalizedText Text(string key) => TrelamiumIII.Instance.GetLocalization($"Jobs.TrackerHint.{key}");

    /// <summary>The first key bound to opening the inventory, for whichever input the player is using.</summary>
    private static string InventoryKey()
    {
        InputMode mode = PlayerInput.UsingGamepad ? InputMode.XBoxGamepad : InputMode.Keyboard;

        return PlayerInput.CurrentProfile.InputModes[mode].KeyStatus.TryGetValue(TriggerNames.Inventory, out List<string>? keys) && keys.Count > 0
            ? keys[0]
            : "Escape";
    }

    private static void Fill(SpriteBatch spriteBatch, Rectangle area, Color color)
        => spriteBatch.Draw(TextureAssets.MagicPixel.Value, area, color);

    private static void Outline(SpriteBatch spriteBatch, Rectangle area, Color color, int thickness = 2)
    {
        Fill(spriteBatch, new Rectangle(area.Left, area.Top, area.Width, thickness), color);
        Fill(spriteBatch, new Rectangle(area.Left, area.Bottom - thickness, area.Width, thickness), color);
        Fill(spriteBatch, new Rectangle(area.Left, area.Top + thickness, thickness, area.Height - thickness * 2), color);
        Fill(spriteBatch, new Rectangle(area.Right - thickness, area.Top + thickness, thickness, area.Height - thickness * 2), color);
    }

    /// <summary>A small triangle off the right edge of the callout, pointing at the tracker.</summary>
    private static void Arrow(SpriteBatch spriteBatch, int left, int centerY, Color color)
    {
        for (int k = 0; k < ARROW; k++)
        {
            int half = ARROW - k;
            Fill(spriteBatch, new Rectangle(left + k, centerY - half, 1, half * 2), color);
        }
    }
}

/// <summary>Remembers that a player has been through <see cref="JobTrackerHint" />, so it is shown once per character.</summary>
public sealed class JobTrackerHintPlayer : ModPlayer
{
    public bool Seen;

    public override void SaveData(TagCompound tag)
    {
        if (Seen)
            tag["jobTrackerHintSeen"] = true;
    }

    public override void LoadData(TagCompound tag) => Seen = tag.GetBool("jobTrackerHintSeen");
}
