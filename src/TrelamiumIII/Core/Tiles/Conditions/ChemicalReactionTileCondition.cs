using ReLogic.Content;
using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core.Gas;
using TrelamiumIII.Core.Tiles.Conditions.Reactions;

namespace TrelamiumIII.Core.Tiles.Conditions;

/// <summary>
/// functions as the tile condition for chemical fields that derive from a chemical weapons.
/// <para>
/// Example: a thermite field leaving a 'burning' TileCondition with the tiles it intersects with. 
/// <see cref="ChemicalReactionTileCondition"/> instances also have mutable chemical stat shares with the emitter field that creates it.
/// </para>
/// </summary>
public abstract class ChemicalReactionTileCondition : FilmCondition, IChemicalSource
{
    public abstract ChemicalDamageType ChemicalType { get; }

    protected ElementProfile Element => ElementProfile.For(ChemicalType);

    public virtual int ChemicalDamage => Element.ChemicalDamage;

    public virtual int AfflictionDuration => Element.AfflictionDuration;

    public virtual float AfflictionRate => Element.AfflictionRate;

    public virtual int ReactionChance => Element.ReactionChance;

    public virtual int ReactionDamage => Element.ReactionDamage;

    public virtual int Concentration => Element.Concentration;

    public virtual float Catalysis => Element.Catalysis;

    public virtual ClingBehaviour? ClingBehaviour => Element.ClingBehaviour;

    public virtual void OnChemicalReaction(in ChemicalReactionEvent e) { }

    public override TileElement ElementsOf(in TileConditionInstance instance) => TileElements.Of(ChemicalType);

    protected virtual int ContactBuff => Element.ContactBuff;

    protected virtual int ContactBuffTicks => Element.ContactBuffTicks;

    protected virtual bool HarmsPlayers => Element.HarmsPlayers;

    protected virtual GasField? LingerLayer => null;

    protected virtual int LingerInterval => 16;

    protected virtual float LingerRadius => 7f;

    protected virtual float LingerLife => 42f;

    protected virtual float LingerRise => 0.5f;

    protected virtual float ActiveStrength => 0.25f;

    public override float Duration => 14f * 60f;

    public override int MaxTiles => 700;

    protected override Asset<Texture2D> NoiseTexture => Assets.Textures.Noise.Turbulence_06.Asset;

    protected virtual Vector3 BodyColor => Element.BodyColor;

    protected virtual Vector3 EdgeColor => Element.EdgeColor;

    protected virtual Vector3 EmittedLight => Element.EmittedLight;

    protected virtual float Threshold => Element.Threshold;

    /// <summary>FBM cycles per tile. <see cref="ElementProfile.NoiseScale" /> is per screen height at 1080p.</summary>
    protected virtual float NoiseScale => Element.NoiseScale * 16f / 1080f;

    protected virtual Vector2 NoiseDrift => Element.NoiseDrift;

    private TileFilmLook? look;

    /// <summary>The coat's look, built from the element palette on first use.</summary>
    protected override TileFilmLook Look => look ??= new TileFilmLook
    {
        Threshold = Threshold,
        NoiseScale = NoiseScale,
        NoiseStrength = 0.16f,
        NoiseDrift = NoiseDrift,
        Bands = 8f,
        ShadowColor = BodyColor * 0.7f,
        BodyColor = BodyColor,
        CoreColor = Vector3.Lerp(BodyColor, EdgeColor, 0.3f),
        BodyAlpha = 0.78f,
        OutlineColor = EdgeColor,
        OutlineAlpha = 0.92f,
        EmissiveEdge = 1f,
        SurfaceLip = 1f,
    };

    public override void Unload()
    {
        base.Unload();
        look = null;
    }

    /// <summary>
    ///     Deposits or refreshes the residue on a tile.
    /// </summary>
    /// <param name="coverage">
    ///     How far past its layer's threshold the field was, as a multiple of it. 1 is "just barely
    ///     pooled"; larger values are a deeper puddle and set a stronger, not a longer, mark.
    /// </param>
    /// <param name="owner">The player whose gas pooled here, or -1.</param>
    /// <param name="payload">The chemical release whose gas pooled here, or null.</param>
    public void Linger(int i, int j, float coverage, int owner = TileConditionInstance.NoOwner, ChemicalPayload? payload = null)
        => TileConditionSystem.Apply(this, i, j, MathHelper.Clamp(coverage * 0.55f, 0.25f, 1f), owner: owner, sync: false, payload: payload);

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
    ///         Emitted from just above the tile rather than its centre, which is inside solid rock: a
    ///         node born there registers as enclosed, takes the enclosure life penalty, and gets
    ///         ejected. Half a tile of clearance starts it in open air.
    ///     </para>
    ///     <para>
    ///         Scaled by strength on both size and lifetime, so a fading patch does not keep
    ///         producing full-bodied clouds.
    ///     </para>
    ///     <para>
    ///         The visibility gate is needed as a large pool keeps a
    ///         four-figure node count alive permanently, each swept against every NPC. The chemistry
    ///         runs in <see cref="OnNPCContact" /> regardless, so skipping off-screen clouds loses
    ///         nothing.
    ///     </para>
    /// </remarks>
    private void Exhale(in TileConditionInstance instance)
    {
        if (LingerLayer is not { } layer || instance.Strength < ActiveStrength || instance.Payload is { IsActive: false })
            return;

        int interval = Math.Max(LingerInterval, 1);

        if ((instance.Age + instance.X * 7 + instance.Y * 13) % interval != 0)
            return;

        float strength = instance.Strength;

        Vector2 origin = instance.WorldCenter - new Vector2(0f, 10f) + Main.rand.NextVector2Circular(5f, 3f);
        Vector2 drift = new(Main.rand.NextFloat(-0.35f, 0.35f), -LingerRise * Main.rand.NextFloat(0.6f, 1.2f));

        // Re-emitted, so the exhaled gas can't cling and keep the residue's chemistry going on its own.
        layer.SpawnNode(
            origin,
            drift,
            radius: LingerRadius * MathHelper.Lerp(0.6f, 1f, strength),
            life: LingerLife * MathHelper.Lerp(0.5f, 1f, strength),
            owner: instance.Owner,
            payload: instance.Payload?.Derived
        );
    }

    public override void OnNPCContact(ref TileConditionInstance instance, NPC npc)
    {
        if (instance.Strength < ActiveStrength || !CanAffect(npc))
            return;

        // The gas a residue exhales is what clings from it are made of.
        if (ChemicalPayload.Contact(npc, this, instance.Payload, instance.Owner, LingerLayer as ChemicalGasField, instance.Strength) && ContactBuff >= 0)
            npc.AddBuff(ContactBuff, ContactBuffTicks);
    }

    public override void OnPlayerContact(ref TileConditionInstance instance, Player player)
    {
        if (!HarmsPlayers || instance.Strength < ActiveStrength || ContactBuff < 0)
            return;

        player.AddBuff(ContactBuff, ContactBuffTicks);
    }

    protected static bool CanAffect(NPC npc)
        => !npc.friendly && !npc.dontTakeDamage && !npc.immortal;
}
