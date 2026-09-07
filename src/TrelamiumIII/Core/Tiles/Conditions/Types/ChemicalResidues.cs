using ReLogic.Content;
using Terraria.GameContent;
using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core.Gas;
using TrelamiumIII.Core.Gas.Layers;

namespace TrelamiumIII.Core.Tiles.Conditions.Types;

// Every residue in this file is now mostly its element and a little of itself. Palette, affliction
// rate, reaction odds, contact debuff and light all come from ElementProfile via the base, so what
// is written out below is only the part that makes each one distinct from the generic form of the
// element it is made of -- which layer it breathes, how it moves underfoot, how long it lasts.

/// <summary>
///     Thermite that has run off its target and pooled on the terrain, still burning.
/// </summary>
/// <remarks>
///     One of the two residues that is still a hazard to the player who made it. Molten metal on
///     the floor is the clearest case there is of an effect that should not be reading a name tag
///     before it decides whether to burn someone, and a thermite pool you can wade through is not
///     a thermite pool. It inherits that from <see cref="ElementProfile.Thermal" /> rather than
///     declaring it, because it is true of thermal chemistry generally and not of this pool alone.
/// </remarks>
public sealed class ThermalResidueCondition : ChemicalReactionTileCondition
{
    public override ChemicalDamageType ChemicalType => ChemicalDamageType.Thermal;

    /// <summary>Shorter than the other residues: thermite is spending itself as it burns.</summary>
    public override float Duration => 9f * 60f;

    /// <summary>
    ///     Draws nothing of its own. <see cref="BurnedCondition" /> is what a thermite pool looks
    ///     like.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This residue used to draw itself as a coverage mask with a fixed molten palette and
    ///         then hand off to a burn on expiry. That split made sense while the burn was a flat
    ///         char stain with one colour for the whole screen and could not depict anything hot. It
    ///         no longer is: a burn carries per-tile heat through a blackbody ramp, and running a
    ///         fire is precisely what it is for. Two masks over the same tiles would only composite
    ///         two fires on top of each other.
    ///     </para>
    ///     <para>
    ///         So the pair splits by what each is actually good at. The burn is the fire -- the heat
    ///         curve, the colour, the light, the char left behind. This is the chemistry, which the
    ///         burn has no notion of: a thermal affliction, a matchup against the target's
    ///         composition, and a reaction roll, all on the same <see cref="IChemicalSource" /> path
    ///         a thermite node uses. A pool of the stuff is both things at once and always was.
    ///     </para>
    /// </remarks>
    public override TileConditionRenderMode RenderMode => TileConditionRenderMode.None;

    /// <summary>None: the fire on this tile lights it, and two sources would double the glow.</summary>
    protected override Vector3 EmittedLight => Vector3.Zero;

    /// <summary>
    ///     Sets the tile alight, at thermite's own ferocity.
    /// </summary>
    /// <remarks>
    ///     Runs on every refresh as well as on the first application, which is what keeps a pool
    ///     that is still being fed burning at full heat: <see cref="BurnedCondition.OnApplied" />
    ///     takes incoming heat as a floor rather than a reset, so a live pour stokes the fire under
    ///     it while one that has stopped being fed is allowed to cool.
    /// </remarks>
    public override void OnApplied(ref TileConditionInstance instance)
        => BurnedCondition.Ignite(instance.X, instance.Y, BurnProfile.Thermal, instance.Intensity, instance.Owner);

    // No expiry handoff any more. It used to light a char where the pool had been, which is what
    // the burn is already doing from the moment the pool lands -- and re-applying here would now
    // actively hurt, because a plain Apply carries the default variant and would demote a
    // still-raging thermite fire to an ordinary one at the moment its chemistry ran out.
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
///         charges across a frozen floor overshoots. The absence of a debuff now lives on
///         <see cref="ElementProfile.Cryonic" />, since it is true of cold in general.
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

    /// <summary>
    ///     Draws vanilla's breakable ice over the tile, at half opacity, instead of a mask.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         A noise-eroded coverage mask is the right tool for something that seeps, spreads and
    ///         has no natural boundary. Ice is the opposite: it is a solid with facets, and vanilla
    ///         already owns a sprite of exactly that, framed on the same layout every standard block
    ///         uses. Borrowing it costs one draw, matches the tile's own edges and slope for free,
    ///         and reads unmistakably as ice in a way a blue-tinted blob never quite did.
    ///     </para>
    ///     <para>
    ///         Half opacity is what keeps it a coating rather than a substitution -- the terrain
    ///         underneath has to stay legible, or a frozen floor looks like someone replaced the
    ///         blocks.
    ///     </para>
    /// </remarks>
    public override TileConditionRenderMode RenderMode => TileConditionRenderMode.Overlay;

    /// <summary>Plain: the ice sprite is already the effect, and needs nothing done to it.</summary>
    public override Asset<Effect>? Shader => null;

    protected override GasField? LingerLayer => GasSystem.GetLayer<IceMistField>();

    /// <summary>How much of the ice sheet shows through at full strength.</summary>
    private const float RIME_ALPHA = 0.5f;

    public override void DrawTile(SpriteBatch spriteBatch, in TileConditionInstance instance, Vector2 drawPosition)
    {
        Main.instance.LoadTiles(TileID.BreakableIce);

        if (TextureAssets.Tile[TileID.BreakableIce] is not { Value: { } ice })
            return;

        DrawAsTile(in instance, ice, Color.White * (RIME_ALPHA * instance.Strength));
    }

    /// <summary>Barely there: frost gives off a thin ground-hugging mist, not a cloud.</summary>
    protected override float LingerRadius => 6f;

    protected override int LingerInterval => 24;

    protected override float LingerRise => 0.2f;

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
/// <remarks>
///     Entirely the generic form of its element apart from what it breathes, which is exactly what
///     the element defaults are for -- a caustic film should look and act like caustic chemistry,
///     and saying so twice would only create somewhere for the two to disagree.
/// </remarks>
public sealed class CausticResidueCondition : ChemicalReactionTileCondition
{
    public override ChemicalDamageType ChemicalType => ChemicalDamageType.Caustic;

    /// <inheritdoc cref="NecroticResidueCondition.RenderMode" />
    public override TileConditionRenderMode RenderMode => TileConditionRenderMode.None;

    protected override GasField? LingerLayer => GasSystem.GetLayer<PhytoncideSporeLayer>();
}

/// <summary>Corrupt spores, settled as a necrotic film that keeps seeding itself.</summary>
/// <remarks>
///     Asks for bleeding by name, where it used to inherit it. Corruption working its way into a
///     wound is a reading that holds up; necrotic chemistry causing bleeding in general is not, so
///     the debuff belongs to this residue rather than to the element -- see
///     <see cref="ElementProfile.Necrotic" />.
/// </remarks>
public sealed class NecroticResidueCondition : ChemicalReactionTileCondition
{
    public override ChemicalDamageType ChemicalType => ChemicalDamageType.Necrotic;

    /// <summary>
    ///     Marks no terrain. The cloud it breathes is the whole of what the patch looks like.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Spores are not a substance that coats a floor. They hang, they drift, and they are
    ///         already being drawn -- as gas nodes, in the layer this exhales, through a metaball
    ///         field built for exactly that. Painting a second mask onto the blocks underneath added
    ///         a hard-edged stain to an effect whose whole character is that it has no edges, and
    ///         put a per-tile square outline under a cloud.
    ///     </para>
    ///     <para>
    ///         Nothing mechanical is lost. The chemistry, the contact debuff and the exhalation all
    ///         run on the tile batch regardless of render mode -- this only declines to draw.
    ///     </para>
    /// </remarks>
    public override TileConditionRenderMode RenderMode => TileConditionRenderMode.None;

    protected override int ContactBuff => BuffID.Bleeding;

    protected override GasField? LingerLayer => GasSystem.GetLayer<CorruptedSporeLayer>();
}
