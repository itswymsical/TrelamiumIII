using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core.Tiles.Conditions;
using TrelamiumIII.Core.Graphics.Fields;

namespace TrelamiumIII.Core.Gas;

/// <summary>
/// Gas layer that emits chemistry on contact. A node carrying a payload exposes what touches it to that
/// payload's chemistry; the layer's own numbers are for ambient gas nobody released.
/// </summary>
public abstract class ChemicalGasField : GasField, IChemicalSource
{
    public const int CosmeticOwnerID = -999;
    public abstract ChemicalDamageType ChemicalType { get; }

    public virtual int ChemicalDamage => 1;

    public virtual int AfflictionDuration => 60;
    public virtual float AfflictionRate => 30f;
    public virtual int ReactionChance => 3;

    public virtual int ReactionDamage => 0;

    public virtual int Concentration => 6;

    public virtual float Catalysis => 0f;

    /// <summary>What this gas does when it clings, for sources that name none. Null is the default cling.</summary>
    public virtual ClingBehaviour? ClingBehaviour => null;

    /// <summary>
    /// The residue this layer leaves on tiles it pools on, or null for a layer that leaves nothing.
    /// </summary>
    protected virtual ChemicalReactionTileCondition? Residue => null;

    protected override bool UsesTileField => Residue is not null;

    /// <summary>
    /// Emitter hits are controller damage, like everything else this layer does.
    /// </summary>
    /// <remarks>
    /// The burst projectiles this replaces each declared the class by hand in their own
    /// <c>SetDefaults</c>, six times over. Answering it once here is what makes an arm of a burst
    /// count toward the same accessories, stats and tooltips the flask that threw it does.
    /// </remarks>
    protected override DamageClass EmitterDamageClass => ModContent.GetInstance<ChemicalDamageClass>();

    /// <summary>
    /// Exposes the target to the node's chemistry. Targets already passed <see cref="GasField.CanAffect(NPC)"/>,
    /// so overrides need not re-check them.
    /// </summary>
    protected override void OnNPCContact(ref FieldNode node, NPC npc)
        => ChemicalPayload.Contact(npc, this, node.Payload, node.Owner, this);

    public void SpawnClingNode(Vector2 position, Vector2 velocity, float radius, float life)
    {
        SpawnNode(position, velocity, radius, life, owner: CosmeticOwnerID);
    }

    /// <summary>One cosmetic wisp over <paramref name="host" />, spreading wider and living longer with <paramref name="intensity" />.</summary>
    public void ShedOnto(NPC host, float intensity)
    {
        if (Main.dedServ)
            return;

        intensity = MathHelper.Clamp(intensity, 0f, 1f);

        float spreadX = host.width * MathHelper.Lerp(0.25f, 0.55f, intensity);
        float spreadY = host.height * MathHelper.Lerp(0.25f, 0.55f, intensity);

        Vector2 position = host.Center + Main.rand.NextVector2Circular(spreadX, spreadY);
        Vector2 velocity = host.velocity * 0.85f + new Vector2(0f, -0.2f) + Main.rand.NextVector2Circular(0.4f, 0.4f);

        SpawnClingNode(
            position,
            velocity,
            radius: MathHelper.Lerp(2f, 4f, intensity) * Main.rand.NextFloat(0.75f, 1.25f),
            life: MathHelper.Lerp(10f, 20f, intensity));
    }

    protected override void UpdateNode(ref FieldNode node, int index)
    {
        if (node.Owner == CosmeticOwnerID)
        {
            node.Velocity.Y += MathHelper.Lerp(-BuoyancyForce, GravityScale, node.Density);
            node.Velocity *= MathHelper.Lerp(Drag, Drag * 0.96f, node.Viscosity);

            node.Position += node.Velocity;
            node.Life -= LifeDecay;
            return;
        }

        base.UpdateNode(ref node, index);
    }

    protected override void OnLiquidContact(ref FieldNode node, int tileX, int tileY, Tile tile)
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

    protected override void OnTileContact(ref FieldNode node, int tileX, int tileY, Tile tile)
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
    protected override void OnTileField(int tileX, int tileY, Tile tile, float density, int owner, ChemicalPayload? payload)
    {
        if (owner == CosmeticOwnerID) return;

        Residue?.Linger(tileX, tileY, density / Math.Max(TileFieldThreshold, 0.01f), owner, payload);
    }

    protected override void OnLiquidField(int tileX, int tileY, Tile tile, float density, int owner, ChemicalPayload? payload)
    {
        if (owner == CosmeticOwnerID) return;
        base.OnLiquidField(tileX, tileY, tile, density, owner, payload);
    }

    public virtual void OnChemicalReaction(in ChemicalReactionEvent e) { }
}