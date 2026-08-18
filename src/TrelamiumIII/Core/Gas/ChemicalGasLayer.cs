using TrelamiumIII.Common.ChemicalClass;

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

    protected override void OnNPCContact(ref GasNode node, NPC npc)
    {
        if (npc.friendly) return;

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

    public virtual void OnChemicalReaction(in ChemicalReactionEvent e) { }
}