using System;
using System.Reflection;

namespace TrelamiumIII.Common.Systems.Alchemy.Brewing;

/// <summary>
/// A <see cref="DeltaBehavior"/> for a stat that lives on <em>another mod's</em> ModPlayer. We build
/// against no reference to that mod, so its ModPlayer and the field are resolved by name once at
/// registration and cached; anything that fails to resolve degrades to a no-op (with a log line)
/// rather than throwing sixty times a second.
/// <para>The write lands in <see cref="ApplyPreEquips"/>, not <see cref="Apply"/>: these fields are
/// accumulators the owning mod reads during <c>UpdateEquips</c> and resets in <c>ResetEffects</c>, so
/// the post-equips pass would be a frame late and then be wiped.</para>
/// </summary>
public sealed class CrossModStatBehavior : IEffectBehavior
{
    private readonly float _vanillaValue;
    private readonly string _noun;
    private readonly bool _asPercent;

    private readonly ModPlayer? _template;
    private readonly FieldInfo? _field;

    /// <param name="modName">Internal name of the owning mod, e.g. "SpiritReforged".</param>
    /// <param name="modPlayerName">Class name of its ModPlayer, e.g. "FlightPlayer".</param>
    /// <param name="fieldName">Public float field its buff accumulates into, e.g. "wingTimeMult".</param>
    /// <param name="vanillaValue">
    /// What that mod's own buff already contributes each tick. We only ever add the difference the
    /// brew's strength asks for, so a 100%-strength brew is exactly the unmodded potion.
    /// </param>
    /// <param name="noun">Tooltip noun, e.g. "wing flight time".</param>
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
