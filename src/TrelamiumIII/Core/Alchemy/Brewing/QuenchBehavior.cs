using System;
using System.Reflection;

namespace TrelamiumIII.Core.Alchemy.Brewing;

public sealed class QuenchBehavior : IEffectBehavior
{
    private readonly float vanillaBonus;   // extra buff duration the stock potion grants (.25f = +25%)

    public QuenchBehavior(float vanillaDurationBonus) => vanillaBonus = vanillaDurationBonus;

    /// <summary>Total duration multiplier a brew of this strength should apply (1.25 at stock strength).</summary>
    public static float MultiplierAt(float vanillaBonus, int strengthPercent) =>
        1f + vanillaBonus * (strengthPercent / 100f);

    public void Apply(Player player, in AlchemicalEffect fx) { }

    public string Describe(in AlchemicalEffect fx) =>
        $"+{(int)MathF.Round((MultiplierAt(vanillaBonus, fx.StrengthPercent) - 1f) * 100f)}% increased buff duration";
}

/// <summary>
/// Owns our subscription to SpiritReforged's <c>BuffPlayer.ModifyBuffTime</c>. That type is
/// <see langword="internal"/> to their assembly and we build without a reference to it, so the event and
/// its delegate type are resolved by name; failure degrades to "brews behave like the stock potion"
/// rather than throwing on every <c>AddBuff</c>.
/// </summary>
internal static class QuenchHook
{
    private static EventInfo? _event;
    private static Delegate? _handler;
    private static int _quenchBuff;
    private static float _vanillaBonus;

    /// <summary>
    /// Subscribe the rescaling handler. Must be called from PostSetupContent, not Load: their
    /// <c>QuenchPotion.Load</c> subscribes <c>QuenchifyBuff</c>, and a multicast delegate invokes in
    /// subscription order, so subscribing later is exactly what puts us downstream of their multiply.
    /// </summary>
    public static bool TryHook(int quenchBuffType, float vanillaBonus)
    {
        if (_handler is not null) return true;

        if (!ModLoader.TryGetMod("SpiritReforged", out Mod spirit)
            || spirit.Code.GetType("SpiritReforged.Common.BuffCommon.BuffPlayer") is not { } buffPlayer
            || buffPlayer.GetNestedType("ModifyBuffTimeDelegate",
                   BindingFlags.Public | BindingFlags.NonPublic) is not { } delegateType
            || buffPlayer.GetEvent("ModifyBuffTime", BindingFlags.Public | BindingFlags.Static) is not { } evt)
        {
            TrelamiumIII.Instance.Logger.Warn(
                "Alchemy: could not resolve SpiritReforged BuffPlayer.ModifyBuffTime | " +
                "brewed Quench potions will fall back to that mod's flat 1.25x.");
            return false;
        }

        var method = typeof(QuenchHook).GetMethod(nameof(Rescale), BindingFlags.NonPublic | BindingFlags.Static)!;
        _handler = Delegate.CreateDelegate(delegateType, method);
        _event = evt;
        _quenchBuff = quenchBuffType;
        _vanillaBonus = vanillaBonus;

        evt.AddEventHandler(null, _handler);
        return true;
    }

    /// <summary>Drop the subscription. Their static event outlives our assembly otherwise.</summary>
    public static void Unhook()
    {
        if (_event is not null && _handler is not null)
            _event.RemoveEventHandler(null, _handler);

        _event = null;
        _handler = null;
    }

    private static void Rescale(int buffType, ref int buffTime, Player player, bool quickBuff)
    {
        // Mirror QuenchifyBuff's own guard: if it skipped this buff, there is nothing to rescale.
        if (Main.debuff[buffType] || buffType == _quenchBuff || !player.HasBuff(_quenchBuff)) return;

        // Only a brewed Quench rescales. A stock SpiritReforged potion keeps their flat 1.25x.
        if (!player.GetModPlayer<AlchemyEffectPlayer>().TryGetActiveBrew(_quenchBuff, out var fx)) return;

        float applied = 1f + _vanillaBonus;                                   // what they just multiplied by
        float brewed = QuenchBehavior.MultiplierAt(_vanillaBonus, fx.StrengthPercent);
        if (MathF.Abs(brewed - applied) < 0.0001f) return;                    // stock-strength brew, leave it

        // buffTime already holds (int)(original * applied), so only the residual is ours to apply.
        // Re-deriving the original costs a tick of precision at most, well under a frame.
        buffTime = (int)(buffTime * (brewed / applied));
    }
}
