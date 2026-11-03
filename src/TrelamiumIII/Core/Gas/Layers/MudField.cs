using ReLogic.Content;
using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core.Graphics.Fields;
using TrelamiumIII.Core.Residues;
using TrelamiumIII.Core.Residues.Types;

namespace TrelamiumIII.Core.Gas.Layers;

/// <summary>Wet mud: a heavy liquid that slumps and pools, coating what it settles on in <see cref="MuddiedResidue" />.</summary>
public sealed class MudField : ChemicalGasField
{
    public override ChemicalDamageType ChemicalType => ChemicalDamageType.Solute;

    public override int ChemicalDamage => 1;

    public override int AfflictionDuration => 90;

    public override float AfflictionRate => 30f;

    public override int ReactionChance => 0;

    public override int ReactionDamage => 0;

    /// <summary>Under the other pooling liquids.</summary>
    public override int LoadOrder => 15;

    protected override Asset<Effect> Shader => Assets.Shaders.GasFbm.Asset;

    protected override string TexturePath => Assets.Textures.VFX.RadialGradient.KEY;

    protected override int GasType => GasTypeID.Liquid;

    protected override float DefaultDensity => 1f;

    protected override float DefaultViscosity => 0.88f;

    protected override float GravityScale => 0.28f;

    protected override float BuoyancyForce => 0.02f;

    protected override float Drag => 0.982f;

    protected override float EnclosedLifePenalty => 0.5f;

    protected override bool UsesTileField => true;

    protected override float TileFieldThreshold => 0.5f;

    protected override int TileFieldInterval => 4;

    protected override int ContactBuff => ModContent.BuffType<Mired>();

    protected override int ContactBuffTicks => 60;

    protected override GasLook Look { get; } = new()
    {
        Noise = static () => Assets.Textures.Noise.LiquidNoise.Asset,
        WorldLight = new Vector3(0.3f),
        Threshold = 0.15f,
        NoiseScale = 1.2f,
        NoiseStrength = 0.2f,
        Drift = new Vector2(0.004f, 0.012f),
        NoiseStretch = new Vector2(1f, 0.7f),
        FlowStrength = 0.1f,
        Bands = 8f,
        BodyAlpha = 0.96f,
        BodyAlphaFloor = 0.85f,
        Shadow = new Vector3(0.22f, 0.14f, 0.08f),
        Body = new Vector3(0.34f, 0.22f, 0.13f),
        Core = new Vector3(0.44f, 0.3f, 0.18f),
        Outline = new Vector3(0.12f, 0.08f, 0.05f),
        OutlineAlpha = 0.95f,
        Highlight = new Vector3(0.6f, 0.47f, 0.33f),
        HighlightAlpha = 0.75f,
    };

    protected override void OnTileField(int tileX, int tileY, Tile tile, float density, int owner, ChemicalPayload? payload)
    {
        if (owner == CosmeticOwnerID || payload is { IsActive: false })
            return;

        float intensity = MathHelper.Clamp(density / Math.Max(TileFieldThreshold, 0.01f) * 0.5f, 0.3f, 1f);

        ResidueSystem.Apply<MuddiedResidue>(tileX, tileY, intensity, owner: owner, sync: false);
    }
    //protected override void OnTileContact(ref FieldNode node, int tileX, int tileY, Tile tile)
    //{
    //    base.OnTileContact(ref node, tileX, tileY, tile);
    //    node.Viscosity *= 1.12f;
    //}
    protected override void OnPlayerContact(ref FieldNode node, Player player) { }
}
