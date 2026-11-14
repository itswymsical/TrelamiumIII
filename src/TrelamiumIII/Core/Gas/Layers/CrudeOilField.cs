using ReLogic.Content;
using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Content.Liquids;
using TrelamiumIII.Core.Graphics.Fields;
using TrelamiumIII.Core.Residues;
using TrelamiumIII.Core.Residues.Types;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Core.Gas.Layers;

public sealed class CrudeOilField : ChemicalGasField
{
    public override ChemicalDamageType ChemicalType => ChemicalDamageType.None;

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
        Shadow = ColorUtils.Rgb(5, 5, 5),
        Body = ColorUtils.Rgb(43, 33, 56),
        Core = ColorUtils.Rgb(5, 5, 5),
        Outline = ColorUtils.Rgb(43, 34, 40),
        OutlineAlpha = 0.95f,
        Highlight = ColorUtils.Rgb(72, 39, 75),
        HighlightAlpha = 0.75f,
    };

    protected override void OnTileField(int tileX, int tileY, Tile tile, float density, int owner, ChemicalPayload? payload)
    {
        if (owner == CosmeticOwnerID || payload is { IsActive: false })
            return;

        float intensity = MathHelper.Clamp(density / Math.Max(TileFieldThreshold, 0.01f) * 0.5f, 0.3f, 1f);

        if (ChemicalPayload.IsAuthority(owner))
            ResidueSystem.Apply<CrudeOilResidue>(tileX, tileY, intensity, owner: owner);
    }

    //protected override void OnTileContact(ref FieldNode node, int tileX, int tileY, Tile tile)
    //{
    //    base.OnTileContact(ref node, tileX, tileY, tile);
    //    node.Viscosity *= 1.12f;
    //}

    protected override void OnLiquidContact(ref FieldNode node, int tileX, int tileY, Tile tile)
    {
        base.OnLiquidContact(ref node, tileX, tileY, tile);

        if (tile.LiquidType == LavaLiquids.Instance.Molten.Index)
        {
            BurnedResidue.Ignite(tileX, tileY, BurnProfile.Fuse, 1, node.Owner);
        }
    }

    protected override void OnPlayerContact(ref FieldNode node, Player player)
    {
        ResidueSystem.Apply<CrudeOilResidue>((int)player.position.X, (int)player.position.Y, 1, 
            TimeConversion.SetTime(secs: 3), -1, ResidueOrigin.Shared);
    }
}