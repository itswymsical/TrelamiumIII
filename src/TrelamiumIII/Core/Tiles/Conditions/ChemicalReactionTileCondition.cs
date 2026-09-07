using ReLogic.Content;
using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core.Gas;

namespace TrelamiumIII.Core.Tiles.Conditions;

/// <summary>
///     Chemistry left behind on the ground: the residue a <c>ChemicalGasLayer</c> deposits wherever
///     its field pooled long enough to soak in.
/// </summary>
/// <remarks>
///     <para>
///         The condition is itself an <see cref="IChemicalSource" />, so a puddle afflicts through
///         exactly the same path a node does -- same matchup table, same reaction roll, same
///         <see cref="IChemicalSource.OnChemicalReaction" /> dispatch. A flask that lands and
///         disperses therefore keeps working after its cloud is gone, and the difference between the
///         cloud and the residue is duration and delivery, not a second parallel set of rules.
///     </para>
///     <para>
///         Rendered as a coverage mask, which is what lets one puddle span several tiles without a
///         seam at every boundary. Subclasses supply the palette and the chemistry; the machinery is
///         all here.
///     </para>
/// </remarks>
public abstract class ChemicalReactionTileCondition : TileCondition, IChemicalSource
{
    public abstract ChemicalDamageType ChemicalType { get; }

    /// <summary>
    ///     The universal definition of whatever element this residue is made of.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Every default below comes from here, which is the point. A residue's element already
    ///         determines what it should do to something standing in it and roughly what it should
    ///         look like doing so, and there is no reason for a caustic pool to restate the caustic
    ///         palette and the caustic affliction rate when it has already said it is caustic.
    ///     </para>
    ///     <para>
    ///         So a residue is now a name, an element, and only the handful of things that make it
    ///         that residue rather than the generic form of its element. What is left overridden in
    ///         a subclass is, by construction, the part that is actually distinctive.
    ///     </para>
    /// </remarks>
    protected ElementProfile Element => ElementProfile.For(ChemicalType);

    /// <summary>
    ///     Affliction tick damage from standing in the residue.
    /// </summary>
    /// <remarks>
    ///     Lower than the cloud that left it. The residue is a much larger window of exposure -- it
    ///     sits there for twenty seconds where the cloud passed in two -- so matching the cloud's
    ///     rate would make the leftovers hit harder than the weapon. The element carries that
    ///     already-reduced figure.
    /// </remarks>
    public virtual float ChemicalDamage => Element.ChemicalDamage;

    public virtual int AfflictionDuration => Element.AfflictionDuration;

    public virtual float AfflictionRate => Element.AfflictionRate;

    public virtual float ReactionChance => Element.ReactionChance;

    public virtual float ReactionDamage => Element.ReactionDamage;

    public virtual void OnChemicalReaction(in ChemicalReactionEvent e) { }

    /// <summary>A debuff put on anything hostile standing in it, or -1 for none.</summary>
    /// <remarks>
    ///     Players are not "anything" -- see <see cref="HarmsPlayers" />.
    /// </remarks>
    protected virtual int ContactBuff => Element.ContactBuff;

    protected virtual int ContactBuffTicks => Element.ContactBuffTicks;

    /// <summary>
    ///     Whether the residue is a hazard to players, or only to what they threw it at.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Off by default, and that default is the whole shape of the feature. A flask marks a
    ///         patch of ground for the better part of twenty seconds and the thrower has to walk
    ///         through it to press the advantage; a residue that debuffs on contact turns every one
    ///         of these weapons into a thing you throw and then retreat from, which is the opposite
    ///         of what an area denial tool is for.
    ///     </para>
    ///     <para>
    ///         What the residue does instead is keep breathing -- see <see cref="LingerLayer" /> --
    ///         so the ground stays visibly and mechanically active without the player having to
    ///         fight their own leftovers. The exception is a residue that is meant to be a hazard
    ///         to everyone, thermite being the obvious one: molten metal does not check who poured
    ///         it.
    ///     </para>
    /// </remarks>
    protected virtual bool HarmsPlayers => Element.HarmsPlayers;

    /// <summary>
    ///     The layer this residue exhales while it sits there, or null for one that just stains.
    /// </summary>
    /// <remarks>
    ///     A puddle that emits its own cloud is what replaces contact damage for the residues that
    ///     no longer have any: the patch is still doing something, and what it is doing is the same
    ///     thing the flask did, delivered by the same nodes with the same ownership. Enemies that
    ///     wander into it are afflicted by the gas exactly as they would be by a fresh throw.
    /// </remarks>
    protected virtual GasField? LingerLayer => null;

    /// <summary>Ticks between exhalations, per tile.</summary>
    /// <remarks>
    ///     Staggered by tile coordinate rather than fired in lockstep, so a wide patch simmers
    ///     unevenly instead of pulsing as one organism.
    /// </remarks>
    protected virtual int LingerInterval => 16;

    protected virtual float LingerRadius => 7f;

    protected virtual float LingerLife => 42f;

    /// <summary>Upward drift given to an exhaled node, before the layer's own buoyancy takes over.</summary>
    protected virtual float LingerRise => 0.5f;

    /// <summary>Residue below this strength is a stain rather than a hazard, and does nothing.</summary>
    protected virtual float ActiveStrength => 0.25f;

    public override float Duration => 14f * 60f;

    public override int MaxTiles => 700;

    public override TileConditionRenderMode RenderMode => TileConditionRenderMode.Field;

    /// <summary>Premultiplied alpha, so the field's alpha channel stays linear in the stamp's.</summary>
    public override BlendState FieldBlend => BlendState.AlphaBlend;

    public override Asset<Effect>? Shader => Assets.Shaders.TileConditionMask.Asset;

    protected virtual Vector3 BodyColor => Element.BodyColor;

    protected virtual Vector3 EdgeColor => Element.EdgeColor;

    protected virtual Vector3 EmittedLight => Element.EmittedLight;

    /// <summary>How much of the mask survives the shader's noise erosion. Higher reads wetter.</summary>
    protected virtual float Threshold => Element.Threshold;

    protected virtual float NoiseScale => Element.NoiseScale;

    protected virtual Vector2 NoiseDrift => Element.NoiseDrift;

    private Asset<Texture2D>? noiseTexture;

    public override void Load()
    {
        if (Main.dedServ)
            return;

        noiseTexture = Assets.Textures.Noise.Turbulence_06.Asset;
    }

    public override void Unload() => noiseTexture = null;

    /// <summary>
    ///     Deposits or refreshes the residue on a tile.
    /// </summary>
    /// <param name="coverage">
    ///     How far past its layer's threshold the field was, as a multiple of it. 1 is "just barely
    ///     pooled"; larger values are a deeper puddle and set a stronger, not a longer, mark.
    /// </param>
    /// <param name="owner">The player whose gas pooled here, or -1.</param>
    public void Linger(int i, int j, float coverage, int owner = TileConditionInstance.NoOwner)
        => TileConditionSystem.Apply(this, i, j, MathHelper.Clamp(coverage * 0.55f, 0.25f, 1f), owner: owner, sync: false);

    public override void UpdateTile(ref TileConditionInstance instance)
    {
        if (Main.dedServ || !OnScreen(in instance))
            return;

        Exhale(in instance);

        if (EmittedLight == Vector3.Zero)
            return;

        Lighting.AddLight(instance.WorldCenter, EmittedLight * instance.Strength);
    }

    /// <summary>
    ///     Breathes a node of the residue's own layer back out, on this tile's turn in the cycle.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Emitted from just above the tile rather than from its centre, because the centre is
    ///         inside solid rock: a node born there registers as enclosed on its first tick, takes
    ///         the enclosure life penalty, and gets ejected in whichever direction its density
    ///         happens to point. Half a tile of clearance is enough to start it in open air.
    ///     </para>
    ///     <para>
    ///         Scaled by strength on both size and lifetime, so a fading patch does not keep
    ///         producing full-bodied clouds right up to the frame it disappears.
    ///     </para>
    ///     <para>
    ///         Gated on being visible, and that gate is load-bearing rather than an optimisation.
    ///         A residue runs to <see cref="TileCondition.MaxTiles" /> tiles and each one emits on
    ///         its own cycle; ungated, a large enough pool keeps a four-figure node count alive
    ///         permanently, and every one of those nodes is swept against every NPC in the world.
    ///         The cloud is a reading of the patch rather than the patch itself -- the chemistry is
    ///         in <see cref="OnNPCContact" />, which runs everywhere regardless -- so declining to
    ///         render one nobody is looking at costs nothing that matters.
    ///     </para>
    /// </remarks>
    private void Exhale(in TileConditionInstance instance)
    {
        if (LingerLayer is not { } layer || instance.Strength < ActiveStrength)
            return;

        int interval = Math.Max(LingerInterval, 1);

        if ((instance.Age + instance.X * 7 + instance.Y * 13) % interval != 0)
            return;

        float strength = instance.Strength;

        Vector2 origin = instance.WorldCenter - new Vector2(0f, 10f) + Main.rand.NextVector2Circular(5f, 3f);
        Vector2 drift = new(Main.rand.NextFloat(-0.35f, 0.35f), -LingerRise * Main.rand.NextFloat(0.6f, 1.2f));

        layer.SpawnNode(
            origin,
            drift,
            radius: LingerRadius * MathHelper.Lerp(0.6f, 1f, strength),
            life: LingerLife * MathHelper.Lerp(0.5f, 1f, strength),
            owner: instance.Owner
        );
    }

    public override void DrawTile(SpriteBatch spriteBatch, in TileConditionInstance instance, Vector2 drawPosition)
        => DrawSilhouette(in instance, Color.White * instance.Strength);

    public override void ApplyShaderParameters(Effect effect)
    {
        ApplyFieldFrameParameters(effect);

        if (noiseTexture is { Value: { } noise })
            effect.Parameters["uNoiseTex"]?.SetValue(noise);

        effect.Parameters["WorldLight"]?.SetValue(Vector3.Max(AverageScreenLight(), new Vector3(0.2f)));

        effect.Parameters["Threshold"]?.SetValue(Threshold);
        effect.Parameters["NoiseStrength"]?.SetValue(0.8f);
        effect.Parameters["NoiseScale"]?.SetValue(NoiseScale);
        effect.Parameters["NoiseDrift"]?.SetValue(NoiseDrift);
        effect.Parameters["EdgeWidth"]?.SetValue(0.25f);

        effect.Parameters["BodyColor"]?.SetValue(BodyColor);
        effect.Parameters["EdgeColor"]?.SetValue(EdgeColor);
        effect.Parameters["BodyAlpha"]?.SetValue(0.78f);
        effect.Parameters["EdgeAlpha"]?.SetValue(0.92f);
    }

    /// <summary>
    ///     Afflicts anything hostile standing in it.
    /// </summary>
    /// <remarks>
    ///     The same friendly, dummy and invulnerable filter the gas sweep applies, for the same
    ///     reason: a puddle left in a town should be scenery, not a hazard to the people living
    ///     there.
    /// </remarks>
    public override void OnNPCContact(ref TileConditionInstance instance, NPC npc)
    {
        if (instance.Strength < ActiveStrength || !CanAffect(npc))
            return;

        npc.GetGlobalNPC<ChemicalGlobalNPC>()
           .ApplyAffliction(ChemicalDamage * instance.Strength, AfflictionDuration, AfflictionRate, ChemicalType);

        if (ContactBuff >= 0)
            npc.AddBuff(ContactBuff, ContactBuffTicks);

        // Rolled per tile of contact rather than per tick of standing in it, which is already how
        // the gas layers roll -- residue is just a slower emitter of the same events.
        if (Main.rand.NextFloat() < ReactionChance * instance.Strength)
            ChemicalReaction.Trigger(npc, this);
    }

    /// <summary>
    ///     Does nothing to players unless the residue is explicitly a hazard to them.
    /// </summary>
    /// <remarks>
    ///     See <see cref="HarmsPlayers" />. A residue that stays out of the player's way is not
    ///     inert -- it is still afflicting everything hostile that walks through it, and still
    ///     exhaling its own cloud on top of that.
    /// </remarks>
    public override void OnPlayerContact(ref TileConditionInstance instance, Player player)
    {
        if (!HarmsPlayers || instance.Strength < ActiveStrength || ContactBuff < 0)
            return;

        player.AddBuff(ContactBuff, ContactBuffTicks);
    }

    /// <summary>
    ///     The same test the gas sweep uses, so a residue and the cloud that left it agree about
    ///     what counts as a target.
    /// </summary>
    protected static bool CanAffect(NPC npc)
        => !npc.friendly && !npc.dontTakeDamage && !npc.immortal;
}
