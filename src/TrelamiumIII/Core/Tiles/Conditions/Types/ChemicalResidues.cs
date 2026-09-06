using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core.Gas;
using TrelamiumIII.Core.Gas.Layers;

namespace TrelamiumIII.Core.Tiles.Conditions.Types;

/// <summary>
///     Thermite that has run off its target and pooled on the terrain, still burning.
/// </summary>
/// <remarks>
///     One of the two residues that is still a hazard to the player who made it. Molten metal on
///     the floor is the clearest case there is of an effect that should not be reading a name tag
///     before it decides whether to burn someone, and a thermite pool you can wade through is not
///     a thermite pool.
/// </remarks>
public sealed class ThermalResidueCondition : ChemicalReactionTileCondition
{
    public override ChemicalDamageType ChemicalType => ChemicalDamageType.Thermal;

    public override float ChemicalDamage => 0.5f;

    public override float ReactionChance => 0.015f;

    public override float ReactionDamage => 1f;

    /// <summary>Shorter than the other residues: thermite is spending itself as it burns.</summary>
    public override float Duration => 9f * 60f;

    protected override int ContactBuff => BuffID.OnFire;

    /// <inheritdoc />
    protected override bool HarmsPlayers => true;

    protected override Vector3 BodyColor => new(0.52f, 0.14f, 0.02f);

    protected override Vector3 EdgeColor => new(1.00f, 0.62f, 0.16f);

    protected override Vector3 EmittedLight => new(0.34f, 0.16f, 0.03f);

    /// <summary>Churns faster than the rest -- it is molten, not merely wet.</summary>
    protected override Vector2 NoiseDrift => new(0.014f, -0.032f);

    /// <summary>
    ///     Burns down to char.
    /// </summary>
    /// <remarks>
    ///     Handing off on expiry rather than applying both at once keeps the two marks from
    ///     compositing over each other -- there is molten thermite here, and then there is a scorch
    ///     where it used to be, which is also the sequence that reads correctly.
    /// </remarks>
    public override void OnExpired(in TileConditionInstance instance)
        => TileConditionSystem.Apply<BurnedCondition>(instance.X, instance.Y, instance.Intensity * 0.8f, sync: false);
}

/// <summary>Hyperborean frost, settled into a rime on whatever it drained across.</summary>
/// <remarks>
///     <para>
///         The other residue that does something to the player, and the one that does not hurt them
///         to do it. Rime used to hand out frostburn, which made the flask's own landing zone a
///         place its thrower could not follow into -- and frostburn on terrain is a strange reading
///         of ice anyway. What ice actually does is take the friction away.
///     </para>
///     <para>
///         So the patch is slippery, for everything: the player slides across their own frost, and
///         so does whatever they threw it at. That reads immediately, costs no health, and turns
///         the flask into a control tool rather than a second damage-over-time -- an enemy that
///         charges across a frozen floor overshoots.
///     </para>
/// </remarks>
public sealed class CryonicResidueCondition : ChemicalReactionTileCondition
{
    /// <summary>
    ///     How much of last tick's horizontal momentum is handed back each tick of contact.
    /// </summary>
    /// <remarks>
    ///     Friction and player input both act by scrubbing velocity during the entity's own update;
    ///     restoring part of what was scrubbed is the same thing a slippery tile does, expressed as
    ///     the difference between the two velocities rather than as a friction constant we would
    ///     have to reproduce. Scaled by strength, so thin rime is only faintly slick.
    /// </remarks>
    private const float SLIP_RECOVERY = 0.72f;

    /// <summary>Below this, the ice cannot get anyone moving who was not already moving.</summary>
    private const float SLIDE_THRESHOLD = 0.35f;

    public override ChemicalDamageType ChemicalType => ChemicalDamageType.Cryonic;

    public override float ChemicalDamage => 0.28f;

    /// <summary>No debuff at all: the whole effect is mechanical. See the type remarks.</summary>
    protected override int ContactBuff => -1;

    protected override GasField? LingerLayer => GasSystem.GetLayer<IceMistField>();

    /// <summary>Barely there: frost gives off a thin ground-hugging mist, not a cloud.</summary>
    protected override float LingerRadius => 6f;

    protected override int LingerInterval => 24;

    protected override float LingerRise => 0.2f;

    protected override Vector3 BodyColor => new(0.58f, 0.74f, 0.92f);

    protected override Vector3 EdgeColor => new(0.90f, 0.98f, 1.00f);

    protected override Vector3 EmittedLight => new(0.05f, 0.09f, 0.14f);

    /// <summary>Nearly still: frost creeps, it does not boil.</summary>
    protected override Vector2 NoiseDrift => new(0.003f, -0.004f);

    /// <summary>Finer than the others, so rime reads as crystalline rather than as a puddle.</summary>
    protected override float NoiseScale => 8f;

    public override void OnPlayerContact(ref TileConditionInstance instance, Player player)
    {
        Slide(player, ref player.velocity, player.oldVelocity, instance.Strength);

        // slippy is what vanilla sets from an icy tile, and the collision code reads it on the next
        // pass. Setting it alongside the momentum restore is belt and braces -- the restore is what
        // does the visible work, this is what keeps the feel consistent with real ice underfoot.
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
    /// <remarks>
    ///     Only for something already on the ground and already moving. Airborne entities are not
    ///     touching the ice, and applying this to a standing one would have the floor pushing people
    ///     around, which is a different effect entirely and not a good one.
    /// </remarks>
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

/// <summary>Phytoncide spores, settled as a caustic film that keeps giving off spores.</summary>
public sealed class CausticResidueCondition : ChemicalReactionTileCondition
{
    public override ChemicalDamageType ChemicalType => ChemicalDamageType.Caustic;

    public override float ChemicalDamage => 0.4f;

    /// <summary>Poison for whatever hostile is standing in it; nothing at all for the thrower.</summary>
    protected override int ContactBuff => BuffID.Poisoned;

    protected override GasField? LingerLayer => GasSystem.GetLayer<PhytoncideSporeLayer>();

    protected override Vector3 BodyColor => new(0.24f, 0.34f, 0.06f);

    protected override Vector3 EdgeColor => new(0.72f, 0.92f, 0.22f);

    protected override Vector3 EmittedLight => new(0.05f, 0.10f, 0.02f);
}

/// <summary>Corrupt spores, settled as a necrotic film that keeps seeding itself.</summary>
public sealed class NecroticResidueCondition : ChemicalReactionTileCondition
{
    public override ChemicalDamageType ChemicalType => ChemicalDamageType.Necrotic;

    public override float ChemicalDamage => 0.3f;

    protected override int ContactBuff => BuffID.Bleeding;

    protected override GasField? LingerLayer => GasSystem.GetLayer<CorruptedSporeLayer>();

    protected override Vector3 BodyColor => new(0.26f, 0.04f, 0.10f);

    protected override Vector3 EdgeColor => new(0.74f, 0.12f, 0.28f);

    /// <summary>Thicker than the rest, so it holds a wet, clotted edge rather than a crisp one.</summary>
    protected override float Threshold => 0.36f;

    protected override float NoiseScale => 4.5f;
}

/// <summary>
///     Blood, pooled where the effusion landed and still steaming off it.
/// </summary>
/// <remarks>
///     Split out from <see cref="NecroticResidueCondition" />, which both this and the corrupt
///     spores used to share. That was harmless while a residue was only a palette and a debuff, and
///     stopped being harmless the moment residues started exhaling their own layer: one condition
///     cannot breathe out two different gases, and a blood pool venting corrupt spores is not a
///     thing anybody asked for. Same chemistry, same reading; different lungs.
/// </remarks>
public sealed class SanguineResidueCondition : ChemicalReactionTileCondition
{
    public override ChemicalDamageType ChemicalType => ChemicalDamageType.Necrotic;

    public override float ChemicalDamage => 0.3f;

    protected override int ContactBuff => BuffID.Bleeding;

    protected override GasField? LingerLayer => GasSystem.GetLayer<BloodLayer>();

    /// <summary>Heavier and slower than a spore cloud: blood seeps, it does not billow.</summary>
    protected override int LingerInterval => 22;

    protected override float LingerRise => 0.15f;

    protected override Vector3 BodyColor => new(0.30f, 0.03f, 0.06f);

    protected override Vector3 EdgeColor => new(0.78f, 0.14f, 0.20f);

    /// <summary>Thicker than the rest, so it holds a wet, clotted edge rather than a crisp one.</summary>
    protected override float Threshold => 0.36f;

    protected override float NoiseScale => 4.5f;
}
