using TrelamiumIII.Common.ChemicalClass;

namespace TrelamiumIII.Core.Tiles.Conditions.Types;

/// <summary>
///     Thermite that has run off its target and pooled on the terrain, still burning.
/// </summary>
public sealed class ThermalResidueCondition : ChemicalReactionTileCondition
{
    public override ChemicalDamageType ChemicalType => ChemicalDamageType.Thermal;

    public override float ChemicalDamage => 0.5f;

    public override float ReactionChance => 0.015f;

    public override float ReactionDamage => 1f;

    /// <summary>Shorter than the other residues: thermite is spending itself as it burns.</summary>
    public override float Duration => 9f * 60f;

    protected override int ContactBuff => BuffID.OnFire;

    protected override Vector3 BodyColor => new(0.52f, 0.14f, 0.02f);

    protected override Vector3 EdgeColor => new(1.00f, 0.62f, 0.16f);

    protected override Vector3 EmittedLight => new(0.34f, 0.16f, 0.03f);

    /// <summary>Churns faster than the rest -- it is molten, not merely wet.</summary>
    protected override Vector2 NoiseDrift => new(0.014f, -0.032f);

    /// <summary>
    ///     Burns down to char.
    /// </summary>
    /// <remarks>
    ///     Handing off on expiry rather than applying both at once keeps the two marks from
    ///     compositing over each other -- there is molten thermite here, and then there is a scorch
    ///     where it used to be, which is also the sequence that reads correctly.
    /// </remarks>
    public override void OnExpired(in TileConditionInstance instance)
        => TileConditionSystem.Apply<BurnedCondition>(instance.X, instance.Y, instance.Intensity * 0.8f, sync: false);
}

/// <summary>Hyperborean frost, settled into a rime on whatever it drained across.</summary>
public sealed class CryonicResidueCondition : ChemicalReactionTileCondition
{
    public override ChemicalDamageType ChemicalType => ChemicalDamageType.Cryonic;

    public override float ChemicalDamage => 0.28f;

    protected override int ContactBuff => BuffID.Frostburn;

    protected override Vector3 BodyColor => new(0.58f, 0.74f, 0.92f);

    protected override Vector3 EdgeColor => new(0.90f, 0.98f, 1.00f);

    protected override Vector3 EmittedLight => new(0.05f, 0.09f, 0.14f);

    /// <summary>Nearly still: frost creeps, it does not boil.</summary>
    protected override Vector2 NoiseDrift => new(0.003f, -0.004f);

    /// <summary>Finer than the others, so rime reads as crystalline rather than as a puddle.</summary>
    protected override float NoiseScale => 8f;
}

/// <summary>Phytoncide spores, settled as a caustic film.</summary>
public sealed class CausticResidueCondition : ChemicalReactionTileCondition
{
    public override ChemicalDamageType ChemicalType => ChemicalDamageType.Caustic;

    public override float ChemicalDamage => 0.4f;

    protected override int ContactBuff => BuffID.Poisoned;

    protected override Vector3 BodyColor => new(0.24f, 0.34f, 0.06f);

    protected override Vector3 EdgeColor => new(0.72f, 0.92f, 0.22f);

    protected override Vector3 EmittedLight => new(0.05f, 0.10f, 0.02f);
}

/// <summary>Blood and corrupt spore alike: what necrotic chemistry leaves on the floor.</summary>
public sealed class NecroticResidueCondition : ChemicalReactionTileCondition
{
    public override ChemicalDamageType ChemicalType => ChemicalDamageType.Necrotic;

    public override float ChemicalDamage => 0.3f;

    protected override int ContactBuff => BuffID.Bleeding;

    protected override Vector3 BodyColor => new(0.26f, 0.04f, 0.10f);

    protected override Vector3 EdgeColor => new(0.74f, 0.12f, 0.28f);

    /// <summary>Thicker than the rest, so it holds a wet, clotted edge rather than a crisp one.</summary>
    protected override float Threshold => 0.36f;

    protected override float NoiseScale => 4.5f;
}
