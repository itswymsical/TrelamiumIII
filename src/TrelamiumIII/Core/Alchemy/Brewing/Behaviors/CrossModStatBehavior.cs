using System;
using System.Reflection;
using TrelamiumIII.Core.Alchemy.Brewing.Behaviors;

namespace TrelamiumIII.Core.Alchemy.Brewing;

/// <summary>
/// A stat on another mod's ModPlayer, resolved by name once and cached (failures log and no-op). Writes in <see cref="ApplyPreEquips"/>,
/// since the owning mod reads it during UpdateEquips and resets it in ResetEffects.
/// </summary>
public sealed class CrossModStatBehavior : IEffectBehavior
{
    private readonly float _vanillaValue;
    private readonly string _noun;
    private readonly bool _asPercent;

    private readonly ModPlayer? _template;
    private readonly FieldInfo? _field;

    /// <param name="modName">Owning mod's internal name, e.g. "SpiritReforged".</param>
    /// <param name="modPlayerName">Its ModPlayer class name, e.g. "FlightPlayer".</param>
    /// <param name="fieldName">Public float field its buff accumulates into, e.g. "wingTimeMult".</param>
    /// <param name="vanillaValue">What that mod's buff already adds per tick; only the difference is added, so 100% is the unmodded potion.</param>
    public CrossModStatBehavior(string modName, string modPlayerName, string fieldName,
                                float vanillaValue, string noun, bool asPercent = true)
    {
        _vanillaValue = vanillaValue;
        _noun = noun;
        _asPercent = asPercent;

        if (ModContent.TryFind(modName, modPlayerName, out ModPlayer template)
            && template.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance) is { } field
            && field.FieldType == typeof(float))
        {
            _template = template;
            _field = field;
        }
        else
        {
            TrelamiumIII.Instance.Logger.Warn(
                $"Alchemy: could not resolve {modName}/{modPlayerName}.{fieldName} (float) | " +
                $"brewed \"{noun}\" will fall back to that mod's own values.");
        }
    }

    /// <summary>Nothing post-equips; see the class remarks for why this behaviour runs earlier.</summary>
    public void Apply(Player player, in AlchemicalEffect fx) { }

    public void ApplyPreEquips(Player player, in AlchemicalEffect fx)
    {
        if (_template is not { } template || _field is not { } field) return;

        // Same delta convention as DeltaBehavior: the other mod's buff has already added its own
        // value this tick, so a brew at StrengthPercent S contributes only the shortfall/surplus.
        ModPlayer instance = player.GetModPlayer(template);
        float delta = _vanillaValue * (fx.StrengthPercent / 100f - 1f);
        field.SetValue(instance, (float)field.GetValue(instance)! + delta);
    }

    public string Describe(in AlchemicalEffect fx)
    {
        float value = _vanillaValue * fx.StrengthPercent / 100f;
        return _asPercent
            ? $"+{(int)MathF.Round(value * 100)}% increased {_noun}"
            : $"increases {_noun} by {(int)MathF.Round(value)}";
    }
}
