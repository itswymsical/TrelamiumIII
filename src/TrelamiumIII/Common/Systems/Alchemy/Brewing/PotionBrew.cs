using System;
using System.Collections.Generic;
using System.Linq;

namespace TrelamiumIII.Common.Systems.Alchemy.Brewing;

/// <summary>
/// A potion's brew: its guiding <see cref="PrincipleType"/> plus the list of effects it applies. v1
/// recipes stamp a single effect, but the list shape is what lets recipes "intermix" several (the
/// augmentation "add/combine" case). Reference type, so <see cref="BrewGlobalItem"/> must deep-Clone
/// it; equality is structural so only identical brews stack.
/// </summary>
public sealed class PotionBrew
{
    public PrincipleType Domain { get; init; }

    /// <summary>The effects this potion applies on consume. Never empty for a branded item.</summary>
    public List<AlchemicalEffect> Effects { get; init; } = new();

    /// <summary>The headline effect drives the tooltip's potency stars and the buff-line rewrite.</summary>
    public AlchemicalEffect Primary => Effects[0];

    /// <summary>Deep copy (effects are value types, so a fresh list is a complete clone).</summary>
    public PotionBrew Clone() => new() { Domain = Domain, Effects = new List<AlchemicalEffect>(Effects) };

    public override bool Equals(object? obj) =>
        obj is PotionBrew o && Domain == o.Domain && Effects.SequenceEqual(o.Effects);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Domain);
        foreach (var e in Effects) hash.Add(e);
        return hash.ToHashCode();
    }
}
