using ReLogic.Content;
using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core.Gas;
using TrelamiumIII.Core.Gas.Layers;
using TrelamiumIII.Core.Graphics.Fields;

namespace TrelamiumIII.Core.Residues.Types;

// Palette, affliction rate, reaction odds, contact debuff and light all come from ElementProfile via
// the base, so each residue below only states what makes it distinct: which layer it breathes, how it
// moves underfoot, how long it lasts.

public sealed class ThermalResidue : ChemicalResidue
{
    public override ChemicalDamageType ChemicalType => ChemicalDamageType.Thermal;

    public override float Duration => 9f * 60f;

    public override ResidueRenderMode RenderMode => ResidueRenderMode.None;

    protected override Vector3 EmittedLight => Vector3.Zero;

    public override void OnApplied(ref ResidueInstance instance)
        => BurnedResidue.Ignite(instance.X, instance.Y, BurnProfile.Thermal, instance.Intensity, instance.Owner);
}

public sealed class CryonicResidue : ChemicalResidue
{
    public override ChemicalDamageType ChemicalType => ChemicalDamageType.Cryonic;

    public override Asset<Effect>? Shader => Assets.Shaders.TileFrostMask.Asset;

    public override int FadeInTicks => 16;

    public override int FadeOutTicks => 24;

    protected override GasField? LingerLayer => FieldSystem.GetLayer<IceMistField>();

    public override void Load()
    {
        base.Load();
        FrostFilm.Load();
    }

    public override void Unload()
    {
        base.Unload();
        FrostFilm.Unload();
    }

    public override TileCanvasSample EncodeCanvas(in ResidueInstance instance) => FrostFilm.Encode(in instance);

    public override void ApplyShaderParameters(Effect effect) => FrostFilm.Apply(effect, EdgeColor, Threshold);

    /// <summary>Creeps outward at the same slow pace every way, and stays where it's got to.</summary>
    public override Bodies.BodyFlow BodyFlow => new(Rise: 0.015f, Fall: 0.015f, Creep: 0.015f, Decay: 0.0012f, Reach: 0.5f);

    /// <summary>Flakes that come away and drift down.</summary>
    public override Bodies.BodyShed? BodyShed => new(Rate: 0.06f, Radius: 4f, Life: 44f, Launch: new Vector2(0f, 0.05f), Scatter: 0.25f, Pull: new Vector2(0f, 0.015f), Drag: 0.985f, Wind: 0.01f, Cling: 0.3f);

    public override IResidue? BodyPickup(in ResidueInstance instance, out float amount)
    {
        amount = instance.Strength * 0.05f;

        return this;
    }

    /// <summary>The ground's cells and veins are broader than a body is tall.</summary>
    public override void ApplyBodyParameters(Effect effect, float gravDir, float height)
    {
        ApplyShaderParameters(effect);

        float detail = BodyDetail(height);

        effect.Parameters["CellScale"]?.SetValue(0.8f * detail);
        effect.Parameters["VeinScale"]?.SetValue(0.7f * detail);
    }

    protected override float LingerRadius => 6f;

    protected override int LingerInterval => 24;

    protected override float LingerRise => 0.2f;

    public override void OnPlayerContact(ref ResidueInstance instance, Player player)
    {
        FrostFilm.Slide(player, ref player.velocity, player.oldVelocity, instance.Strength);

        // slippy is what vanilla sets from an icy tile, and the collision code reads it on the next
        // pass
        player.slippy = true;
        player.slippy2 = true;
    }

    public override void OnNPCContact(ref ResidueInstance instance, NPC npc)
    {
        base.OnNPCContact(ref instance, npc);

        if (!npc.noTileCollide && !npc.noGravity)
            FrostFilm.Slide(npc, ref npc.velocity, npc.oldVelocity, instance.Strength);
    }
}

public sealed class CausticResidue : ChemicalResidue
{
    public override ChemicalDamageType ChemicalType => ChemicalDamageType.Caustic;

    /// <inheritdoc cref="NecroticResidue.RenderMode" />
    public override ResidueRenderMode RenderMode => ResidueRenderMode.None;

    protected override GasField? LingerLayer => FieldSystem.GetLayer<PhytoncideSporeField>();

    /// <summary>Wears the acid canvas on a body, as it has no canvas of its own.</summary>
    public override IResidue? BodyPickup(in ResidueInstance instance, out float amount)
    {
        amount = instance.Strength * 0.08f;

        return ResidueSystem.Get<AcidResidue>();
    }
}

public sealed class NecroticResidue : ChemicalResidue
{
    public override ChemicalDamageType ChemicalType => ChemicalDamageType.Necrotic;

    public override ResidueRenderMode RenderMode => ResidueRenderMode.None;

    protected override int ContactBuff => BuffID.Bleeding;

    protected override GasField? LingerLayer => FieldSystem.GetLayer<VilesporeField>();

    /// <summary>Runs like blood.</summary>
    public override Bodies.BodyFlow BodyFlow => new(Rise: 0f, Fall: 0.06f, Creep: 0.01f, Sag: 0.03f, Decay: 0.002f, Reach: 0.3f);

    public override Bodies.BodyShed? BodyShed => new(Rate: 0.1f, Radius: 3.5f, Life: 30f, Launch: new Vector2(0f, 0.1f), Scatter: 0.1f, Pull: new Vector2(0f, 0.11f), Drag: 0.99f, Cling: 0.2f);

    public override IResidue? BodyPickup(in ResidueInstance instance, out float amount)
    {
        amount = instance.Strength * 0.08f;

        return this;
    }
}
