namespace TrelamiumIII.Core.Gas;

public struct GasNode
{
    public Vector2 Position;
    public Vector2 Velocity;
    public float Radius;
    public float Life;
    public float MaxLife;

    /// <summary>
    /// 0.0 = very buoyant, 1.0 = very dense.
    /// GasLayer.ApplyPhysics interpolates between BuoyancyForce and GravityScale.
    /// </summary>
    public float Density;

    /// <summary>
    /// 0.0 = free flowy, 1.0 = sluggish. Scales lateral spread on collision.
    /// </summary>
    public float Viscosity;

    /// <summary>
    /// Temperature in Celsius. Passed to reaction hooks; not used by base physics.
    /// </summary>
    public float Temperature;

    /// <summary>
    /// Identifies which gas type this node belongs to.
    /// Set automatically by GasLayer when spawning.
    /// </summary>
    public int GasTypeId;

    public bool OnCeiling;
    public bool OnFloor;
    public bool OnWall;

    /// <summary>
    /// Incremented each tick the node is colliding with geometry.
    /// </summary>
    public int EnclosedTicks;

    public bool Friendly;
    public int Damage;
    public float Knockback;

    /// <summary>
    /// The player whose weapon produced this node, or <see cref="NoOwner" />.
    /// </summary>
    /// <remarks>
    /// Read by the hooks that decide what the gas may do to a player, and carried onward into any
    /// residue the node pools into, so a flask's leftovers stay harmless to the person who threw
    /// them for as long as they sit there.
    /// </remarks>
    public int Owner;

    /// <summary>
    /// Ticks remaining before the next entity contact sweep.
    /// </summary>
    public int ContactCooldown;

    public float Alpha => Life / MaxLife;
    public bool Active => Life > 0f;

    public readonly bool IsOwnedBy(Player player) => Owner == player.whoAmI;

    /// <summary>Gas nobody fired: ambient emitters, world hazards, anything spawned by terrain.</summary>
    public const int NoOwner = -1;
}