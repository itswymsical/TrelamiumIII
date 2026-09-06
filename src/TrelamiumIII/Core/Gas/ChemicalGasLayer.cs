using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core.Tiles.Conditions;

namespace TrelamiumIII.Core.Gas;

/// <summary>
/// Gas layer that emits chemistry on contact. Bridges the gas system and the
/// chemical system: gas nodes apply afflictions and roll reactions through the
/// same path as projectile-delivered chemistry, with the layer itself acting
/// as the IChemicalSource.
/// </summary>
public abstract class ChemicalGasLayer : GasLayer, IChemicalSource
{
    public abstract ChemicalDamageType ChemicalType { get; }

    /// <summary>
    /// Affliction tick damage from a single gas contact, before composition matchup.
    /// Gas applies continuously while the node remains in contact (this obviously does less damage).
    /// </summary>
    public virtual float ChemicalDamage => 0.5f;

    public virtual int AfflictionDuration => 60;
    public virtual float AfflictionRate => 30f;
    public virtual float ReactionChance => 0.03f;

    public virtual float ReactionDamage => 0f;

    /// <summary>
    /// The residue this layer leaves on tiles it pools on, or null for a layer that leaves nothing.
    /// </summary>
    /// <remarks>
    /// Resolved through <see cref="TileConditionSystem"/> rather than constructed here, because
    /// conditions are flyweights owned by that system -- this is a reference to the registered one,
    /// not a second copy of it.
    /// </remarks>
    protected virtual ChemicalReactionTileCondition? Residue => null;

    protected override bool UsesTileField => Residue is not null;

    /// <summary>
    /// Applies chemistry. Friendlies, dummies and anything that cannot take damage were already
    /// filtered by <see cref="GasLayer.CanAffect(NPC)"/> before this was called -- overrides do not
    /// need to re-check, and more to the point, an override that forgot used to be able to reach
    /// past a guard that lived here.
    /// </summary>
    protected override void OnNPCContact(ref GasNode node, NPC npc)
    {
        var chem = npc.GetGlobalNPC<ChemicalGlobalNPC>();
        chem.ApplyAffliction(ChemicalDamage, AfflictionDuration, AfflictionRate, ChemicalType);

        if (Main.rand.NextFloat() < ReactionChance)
            ChemicalReaction.Trigger(npc, this);
    }

    protected override void OnLiquidContact(ref GasNode node, int tileX, int tileY, Tile tile)
    {
        if (GasType == GasTypeID.Liquid)
        {
            if (tile.LiquidType == LiquidID.Water)
            {
                node.Velocity *= 0.9f;
                node.Viscosity *= 1.05f;
            }
            else if (tile.LiquidType == LiquidID.Lava)
            {
                node.Velocity *= 0.8f;
                node.Viscosity *= 1.1f;
            }
        }
        base.OnLiquidContact(ref node, tileX, tileY, tile);
    }

    protected override void OnTileContact(ref GasNode node, int tileX, int tileY, Tile tile)
    {
        if (GasType == GasTypeID.Liquid)
        {
            node.Viscosity *= 1.03f;
            node.Velocity *= 0.92f;
        }
        else if (GasType == GasTypeID.Gas)
        {
            node.Viscosity *= 1.01f;
            node.Velocity *= 0.95f;
        }
        base.OnTileContact(ref node, tileX, tileY, tile);
    }

    /// <summary>
    /// Leaves the layer's residue wherever it has actually pooled.
    /// </summary>
    /// <remarks>
    /// Intensity is carried over from the coverage that cleared the threshold, so a thin film sets
    /// a faint, short-lived patch and a deep pool sets a strong one. The residue then applies the
    /// same chemistry the nodes did, through the same <see cref="IChemicalSource"/> path -- the
    /// puddle outlives the cloud that made it, which is the point.
    /// <para>
    /// The owner rides along with it. The puddle spends the rest of its life breathing this same
    /// layer back out, and those nodes have to know whose they are for the same reason the ones
    /// that made it did.
    /// </para>
    /// </remarks>
    protected override void OnTileField(int tileX, int tileY, Tile tile, float density, int owner)
        => Residue?.Linger(tileX, tileY, density / Math.Max(TileFieldThreshold, 0.01f), owner);

    public virtual void OnChemicalReaction(in ChemicalReactionEvent e) { }
}