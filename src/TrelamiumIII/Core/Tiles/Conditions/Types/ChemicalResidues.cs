using ReLogic.Content;
using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core.Gas;
using TrelamiumIII.Core.Gas.Layers;
using TrelamiumIII.Core.Graphics.Fields;

namespace TrelamiumIII.Core.Tiles.Conditions.Types;

// Palette, affliction rate, reaction odds, contact debuff and light all come from ElementProfile via
// the base, so each residue below only states what makes it distinct: which layer it breathes, how it
// moves underfoot, how long it lasts.

public sealed class ThermalResidueCondition : ChemicalReactionTileCondition
{
    public override ChemicalDamageType ChemicalType => ChemicalDamageType.Thermal;

    public override float Duration => 9f * 60f;

    public override TileConditionRenderMode RenderMode => TileConditionRenderMode.None;

    protected override Vector3 EmittedLight => Vector3.Zero;

    public override void OnApplied(ref TileConditionInstance instance)
        => BurnedCondition.Ignite(instance.X, instance.Y, BurnProfile.Thermal, instance.Intensity, instance.Owner);
}

public sealed class CryonicResidueCondition : ChemicalReactionTileCondition
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

    public override TileCanvasSample EncodeCanvas(in TileConditionInstance instance) => FrostFilm.Encode(in instance);

    public override void ApplyShaderParameters(Effect effect) => FrostFilm.Apply(effect, EdgeColor, Threshold);

    protected override float LingerRadius => 6f;

    protected override int LingerInterval => 24;

    protected override float LingerRise => 0.2f;

    public override void OnPlayerContact(ref TileConditionInstance instance, Player player)
    {
        FrostFilm.Slide(player, ref player.velocity, player.oldVelocity, instance.Strength);

        // slippy is what vanilla sets from an icy tile, and the collision code reads it on the next
        // pass
        player.slippy = true;
        player.slippy2 = true;
    }

    public override void OnNPCContact(ref TileConditionInstance instance, NPC npc)
    {
        base.OnNPCContact(ref instance, npc);

        if (!npc.noTileCollide && !npc.noGravity)
            FrostFilm.Slide(npc, ref npc.velocity, npc.oldVelocity, instance.Strength);
    }
}

public sealed class CausticResidueCondition : ChemicalReactionTileCondition
{
    public override ChemicalDamageType ChemicalType => ChemicalDamageType.Caustic;

    /// <inheritdoc cref="NecroticResidueCondition.RenderMode" />
    public override TileConditionRenderMode RenderMode => TileConditionRenderMode.None;

    protected override GasField? LingerLayer => FieldSystem.GetLayer<PhytoncideSporeField>();
}

public sealed class NecroticResidueCondition : ChemicalReactionTileCondition
{
    public override ChemicalDamageType ChemicalType => ChemicalDamageType.Necrotic;

    public override TileConditionRenderMode RenderMode => TileConditionRenderMode.None;

    protected override int ContactBuff => BuffID.Bleeding;

    protected override GasField? LingerLayer => FieldSystem.GetLayer<VilesporeField>();
}
