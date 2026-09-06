using System.Collections.Generic;
using Terraria.GameContent;
using Terraria.UI;
using TrelamiumIII.Common.Configs;

namespace TrelamiumIII.Core.UI;

/// <summary>
/// The floating "+N Exp" that rises off the player when Exp lands.
///
/// <para>
/// Drawn rather than handed to vanilla's <see cref="PopupText"/> so it can carry the glow treatment.
/// Exp is the one currency in the mod that is supposed to feel like it came from somewhere, and vanilla
/// popup text renders it as loot text - the same weight as a coin pickup.
/// </para>
/// </summary>
public sealed class ExpPopup
{
    /// <summary>Ticks the popup lives for.</summary>
    private const float LIFETIME = 90f;

    /// <summary>Ticks the arrival flourish runs for.</summary>
    private const float PULSE_FRAMES = 18f;

    /// <summary>How far it drifts upward over its life, in world pixels.</summary>
    private const float RISE = 46f;

    private static readonly Color Core = new(236, 255, 236);
    private static readonly Color Glow = new(150, 245, 120);

    private float timer;
    private float pulseTimer;

    /// <summary>World position the text is anchored to, captured so it does not chase the player around.</summary>
    private Vector2 anchor;

    public int Amount { get; private set; }

    public bool Expired => timer >= LIFETIME;

    public ExpPopup(Vector2 anchor, int amount)
    {
        this.anchor = anchor;
        Amount = amount;
    }

    /// <summary>
    /// Folds a further gain into this popup and restarts the flourish, so a burst of Exp reads as one
    /// growing number rather than a stack of competing labels.
    /// </summary>
    public void Add(int amount)
    {
        Amount += amount;
        pulseTimer = 0f;

        // Give the merged popup its life back; it just became newsworthy again.
        timer = Math.Min(timer, LIFETIME * 0.35f);
    }

    public void Update()
    {
        timer++;

        if (pulseTimer < PULSE_FRAMES)
            pulseTimer++;
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        float life = Math.Clamp(timer / LIFETIME, 0f, 1f);

        // Holds full strength for most of its life and fades over the last third, so the number is
        // readable for as long as it is on screen.
        float alpha = life < 0.66f ? 1f : 1f - (life - 0.66f) / 0.34f;

        if (alpha <= 0f)
            return;

        float pulse = 1f - Math.Clamp(pulseTimer / PULSE_FRAMES, 0f, 1f);

        Vector2 world = anchor - new Vector2(0f, RISE * EaseOutCubic(life));
        Vector2 screen = world - Main.screenPosition;

        // The popup lives in world space but draws on the UI layer, so it has to be taken through the
        // game view transform by hand - otherwise it drifts away from the player under zoom.
        screen = Vector2.Transform(screen, Main.GameViewMatrix.ZoomMatrix);

        float scale = 0.9f + 0.35f * pulse;

        GlowText.Draw(spriteBatch, FontAssets.MouseText.Value,
            $"+{Amount} Exp", screen, Core, Glow, scale, alpha, pulse);
    }

    private static float EaseOutCubic(float t)
    {
        float u = 1f - t;
        return 1f - u * u * u;
    }
}

/// <summary>
/// Holds the live Exp popups and draws them. Kept to a small pool: Exp arrives in bursts, and the merge
/// in <see cref="ExpPopup.Add"/> means a burst usually collapses into one number anyway.
/// </summary>
public sealed class ExpPopupSystem : ModSystem
{
    /// <summary>Ticks within which a fresh gain merges into the newest popup instead of starting its own.</summary>
    private const float MERGE_WINDOW = 45f;

    private static readonly List<ExpPopup> Active = [];

    private static float sinceLast = float.MaxValue;

    /// <summary>Reports Exp landing on the local player. Silently does nothing when popups are switched off.</summary>
    public static void Show(int amount)
    {
        if (amount <= 0 || Main.dedServ || Main.gameMenu || !ExperientialConfig.ExpPopups)
            return;

        if (Main.LocalPlayer is not { active: true } player)
            return;

        if (sinceLast <= MERGE_WINDOW && Active.Count > 0 && !Active[^1].Expired)
        {
            Active[^1].Add(amount);
            sinceLast = 0f;

            return;
        }

        Active.Add(new ExpPopup(player.Top - new Vector2(0f, 12f), amount));
        sinceLast = 0f;
    }

    public static void Clear() => Active.Clear();

    public override void OnWorldUnload() => Clear();

    public override void PostUpdateEverything()
    {
        if (Main.dedServ)
            return;

        sinceLast++;

        for (int i = Active.Count - 1; i >= 0; i--)
        {
            Active[i].Update();

            if (Active[i].Expired)
                Active.RemoveAt(i);
        }
    }

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int index = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Resource Bars"));

        if (index == -1)
            return;

        layers.Insert(index + 1, new LegacyGameInterfaceLayer(
            "TrelamiumIII: Exp Popups",
            delegate
            {
                if (!Main.gameMenu)
                {
                    foreach (ExpPopup popup in Active)
                        popup.Draw(Main.spriteBatch);
                }

                return true;
            },
            InterfaceScaleType.UI));
    }
}
