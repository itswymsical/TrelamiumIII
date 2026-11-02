using TrelamiumIII.Common.ChemicalClass;

namespace TrelamiumIII.Core.Tiles.Conditions.Reactions;

/// <summary>
///     What a mark on a tile is made of, as far as reactions care. Reactions are written against these rather than against
///     condition types, so a new kind of frost or fire reacts like the others by saying what it is.
/// </summary>
[Flags]
public enum TileElement : byte
{
    None = 0,

    /// <summary>Gel, and anything else that goes up when heat reaches it.</summary>
    Flammable = 1 << 0,

    /// <summary>Fire, magma and thermal residue.</summary>
    Heat = 1 << 1,

    /// <summary>A soaked tile.</summary>
    Water = 1 << 2,

    Cold = 1 << 3,

    Corrosive = 1 << 4,
}

public static class TileElements
{
    /// <summary>What a chemical residue of <paramref name="type" /> counts as on the ground.</summary>
    public static TileElement Of(ChemicalDamageType type) => type switch
    {
        ChemicalDamageType.Thermal => TileElement.Heat,
        ChemicalDamageType.Cryonic => TileElement.Cold,
        ChemicalDamageType.Caustic => TileElement.Corrosive,
        _ => TileElement.None,
    };
}
