using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core.Gas;
using TrelamiumIII.Core.Graphics.Fields;
using TrelamiumIII.Core.Tiles.Conditions;

namespace TrelamiumIII.Core.Graphics.Vfx;

/// <summary> Everything about a <see cref="ModularField" /> that isn't code: the numbers its physics reads, what it's made of, and what it leaves behind. </summary>
internal sealed class VfxFieldSpec
{
    /// <summary> A <c>GasField.GasTypeID</c>: 0 liquid, 1 gas, 2 plasma. </summary>
    public int Kind { get; set; } = 1;

    public float Density { get; set; } = 0.25f;

    public float Viscosity { get; set; } = 0.15f;

    public float Temperature { get; set; } = 20f;

    public float Buoyancy { get; set; } = 0.055f;

    public float Gravity { get; set; } = 0.07f;

    public float Drag { get; set; } = 0.984f;

    public float LifeDecay { get; set; } = 1f;

    public float EnclosedPenalty { get; set; } = 2f;

    public int MaxNodes { get; set; } = 400;

    public float FlowReference { get; set; } = 6f;

    public float StampStretch { get; set; }

    public Vector2 TemperatureRange { get; set; } = new(0f, 1200f);

    public VfxMaterial? Material { get; set; }

    public FieldModule[] Modules { get; set; } = [];

    /// <summary> The condition this field leaves on tiles it pools on, or null to leave nothing. A type, as fields can load before conditions do. </summary>
    public Type? Residue { get; set; }

    public float ResidueThreshold { get; set; } = 0.9f;

    public int ResidueInterval { get; set; } = 5;
}

/// <summary>
///     A field that's all data: its physics are numbers, its behaviour is a list of <see cref="FieldModule" />s, and its look is a
///     <see cref="VfxMaterial" />. A lab draft and an exported field are this same class, so what's previewed is what ships.
/// </summary>
internal abstract class ModularField : GasField
{
    /// <summary> The look's stamp shape, shared by everything drawn from a recipe. </summary>
    protected override string TexturePath => Assets.Textures.VFX.RadialGradient.KEY;

    /// <summary> Read fresh every time, so a draft can be retuned while it runs. </summary>
    public abstract VfxFieldSpec Spec { get; }

    protected override int GasType => Spec.Kind;

    protected override float DefaultDensity => Spec.Density;

    protected override float DefaultViscosity => Spec.Viscosity;

    protected override float DefaultTemperature => Spec.Temperature;

    protected override float BuoyancyForce => Spec.Buoyancy;

    protected override float GravityScale => Spec.Gravity;

    protected override float Drag => Spec.Drag;

    protected override float LifeDecay => Spec.LifeDecay;

    protected override float EnclosedLifePenalty => Spec.EnclosedPenalty;

    protected override int BaseMaxNodes => Spec.MaxNodes;

    protected override float FlowReferenceSpeed => Spec.FlowReference;

    protected override float StampStretch => Spec.StampStretch;

    protected override Vector2 TemperatureRange => Spec.TemperatureRange;

    protected override bool UsesTileField => Spec.Residue is not null;

    protected override float TileFieldThreshold => Spec.ResidueThreshold;

    protected override int TileFieldInterval => Math.Max(Spec.ResidueInterval, 1);

    public override void Update()
    {
        if (NodeCount > 0)
        {
            foreach (FieldModule module in Spec.Modules)
                module.BeginTick(WorldFieldSpace.Instance);
        }

        base.Update();
    }

    protected override void UpdateNode(ref FieldNode node, int index)
    {
        foreach (FieldModule module in Spec.Modules)
            module.Apply(ref node, WorldFieldSpace.Instance);

        base.UpdateNode(ref node, index);
    }

    protected override void OnTileContact(ref FieldNode node, int tileX, int tileY, Tile tile)
    {
        foreach (FieldModule module in Spec.Modules)
            module.OnTileContact(ref node);

        base.OnTileContact(ref node, tileX, tileY, tile);
    }

    /// <summary> The same strength a chemical field's residue lingers at. </summary>
    protected override void OnTileField(int tileX, int tileY, Tile tile, float density, int owner, ChemicalPayload? payload)
    {
        if (Spec.Residue is { } type && TileConditionSystem.Get(type) is { } residue)
            TileConditionSystem.Apply(residue, tileX, tileY, MathHelper.Clamp(density / MathF.Max(TileFieldThreshold, 0.01f) * 0.55f, 0.25f, 1f), owner: owner, sync: false, payload: payload);
    }

    protected override void ApplyShaderParameters(Effect effect) => Spec.Material?.Apply(effect);
}
