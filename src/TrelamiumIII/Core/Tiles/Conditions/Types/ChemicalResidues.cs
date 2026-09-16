using ReLogic.Content;
using Terraria.GameContent;
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
    private const float SLIP_RECOVERY = 0.72f;

    private const float SLIDE_THRESHOLD = 0.35f;

    public override ChemicalDamageType ChemicalType => ChemicalDamageType.Cryonic;

    public override TileConditionRenderMode RenderMode => TileConditionRenderMode.Overlay;

    public override Asset<Effect>? Shader => null;

    protected override GasField? LingerLayer => FieldSystem.GetLayer<IceMistField>();

    private const float RIME_ALPHA = 0.5f;

    public override void DrawTile(SpriteBatch spriteBatch, in TileConditionInstance instance, Vector2 drawPosition)
    {
        Main.instance.LoadTiles(TileID.BreakableIce);

        if (TextureAssets.Tile[TileID.BreakableIce] is not { Value: { } ice })
            return;

        DrawAsTile(in instance, ice, Color.White * (RIME_ALPHA * instance.Strength));
    }

    protected override float LingerRadius => 6f;

    protected override int LingerInterval => 24;

    protected override float LingerRise => 0.2f;

    public override void OnPlayerContact(ref TileConditionInstance instance, Player player)
    {
        Slide(player, ref player.velocity, player.oldVelocity, instance.Strength);

        // slippy is what vanilla sets from an icy tile, and the collision code reads it on the next
        // pass
        player.slippy = true;
        player.slippy2 = true;
    }

    public override void OnNPCContact(ref TileConditionInstance instance, NPC npc)
    {
        base.OnNPCContact(ref instance, npc);

        if (!npc.noTileCollide && !npc.noGravity)
            Slide(npc, ref npc.velocity, npc.oldVelocity, instance.Strength);
    }

    /// <summary>
    ///     Hands back the horizontal momentum the entity's own update just scrubbed off.
    /// </summary>
    private static void Slide(Entity entity, ref Vector2 velocity, Vector2 oldVelocity, float strength)
    {
        if (velocity.Y != 0f || MathF.Abs(oldVelocity.X) < SLIDE_THRESHOLD)
            return;

        velocity.X = MathHelper.Lerp(velocity.X, oldVelocity.X, SLIP_RECOVERY * MathHelper.Clamp(strength, 0f, 1f));

        if (Main.dedServ || !Main.rand.NextBool(20))
            return;

        Dust dust = Dust.NewDustPerfect(
            entity.Bottom + new Vector2(Main.rand.NextFloat(-6f, 6f), -2f),
            DustID.SnowflakeIce,
            new Vector2(-MathF.Sign(velocity.X) * Main.rand.NextFloat(0.4f, 1.1f), -Main.rand.NextFloat(0.2f, 0.6f)),
            120,
            default,
            Main.rand.NextFloat(0.5f, 0.9f)
        );

        dust.noGravity = true;
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
