using System.Reflection;

namespace TrelamiumIII.Core.NPCs.Actors;

/// <summary>
/// Bound delegates for the private half of <see cref="Player.ItemCheck"/>.
///
/// <para>
/// <see cref="PlayerPuppet"/> reproduces the visual slice of ItemCheck for
/// entities that are not players. Most of that slice is public
/// (<see cref="Player.ItemCheck_ApplyUseStyle"/>, <see cref="Item.GetDrawHitbox"/>),
/// but hold styles, melee hitboxes and use visuals are not. Rather than
/// reimplementing them - and drifting from vanilla every time Re-Logic tweaks
/// an offset - we bind the originals once and call them.
/// </para>
///
/// <para>
/// Every binding is optional. If a tModLoader update renames a member, the
/// corresponding delegate stays null, a warning is logged once, and the puppet
/// degrades to "animates but doesn't swing" rather than crashing the world.
/// </para>
/// </summary>
internal static class PlayerInternals
{
    internal delegate void ApplyHoldStyleDelegate(Player player, float mountOffset, Item item, Rectangle heldItemFrame);
    internal delegate void GetMeleeHitboxDelegate(Player player, Item item, Rectangle heldItemFrame, out bool dontAttack, out Rectangle itemRectangle);
    internal delegate Rectangle EmitUseVisualsDelegate(Player player, Item item, Rectangle itemRectangle);
    internal delegate void EmitHeldItemLightDelegate(Player player, Item item);

    /// <summary>Positions a held (not swung) item. Mirrors <c>ItemCheck_ApplyHoldStyle</c>.</summary>
    public static readonly ApplyHoldStyleDelegate ApplyHoldStyle;

    /// <summary>Builds the world-space swing rectangle. Mirrors <c>ItemCheck_GetMeleeHitbox</c>.</summary>
    public static readonly GetMeleeHitboxDelegate GetMeleeHitbox;

    /// <summary>Spawns per-weapon swing dust and returns the adjusted hitbox.</summary>
    public static readonly EmitUseVisualsDelegate EmitUseVisuals;

    /// <summary>Emits light for torches, glowsticks and similar held items.</summary>
    public static readonly EmitHeldItemLightDelegate EmitHeldItemLight;

    /// <summary>True when every binding resolved. False means some puppet features are inert.</summary>
    public static bool FullyBound { get; }

    static PlayerInternals()
    {
        ApplyHoldStyle = Bind<ApplyHoldStyleDelegate>("ItemCheck_ApplyHoldStyle");
        GetMeleeHitbox = Bind<GetMeleeHitboxDelegate>("ItemCheck_GetMeleeHitbox");
        EmitUseVisuals = Bind<EmitUseVisualsDelegate>("ItemCheck_EmitUseVisuals");
        EmitHeldItemLight = Bind<EmitHeldItemLightDelegate>("ItemCheck_EmitHeldItemLight");

        FullyBound = ApplyHoldStyle != null
                  && GetMeleeHitbox != null
                  && EmitUseVisuals != null
                  && EmitHeldItemLight != null;
    }

    private static T Bind<T>(string methodName) where T : Delegate
    {
        MethodInfo method = typeof(Player).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (method == null)
        {
            Warn(methodName, "not found");
            return null;
        }

        // Open instance delegate: the Player becomes the first parameter.
        if (Delegate.CreateDelegate(typeof(T), method, throwOnBindFailure: false) is T bound)
            return bound;

        Warn(methodName, "signature changed");
        return null;
    }

    private static void Warn(string methodName, string reason)
    {
        TrelamiumIII.Instance?.Logger.Warn(
            $"[PlayerInternals] Could not bind Player.{methodName} ({reason}). " +
            "WorldNPC item animation will be partially disabled until this is updated.");
    }
}
