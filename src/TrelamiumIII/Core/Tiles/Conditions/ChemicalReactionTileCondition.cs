using ReLogic.Content;
using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core.Gas;

namespace TrelamiumIII.Core.Tiles.Conditions;

public abstract class ChemicalReactionTileCondition : TileCondition, IChemicalSource
{
    public abstract ChemicalDamageType ChemicalType { get; }

    protected ElementProfile Element => ElementProfile.For(ChemicalType);

    public virtual int ChemicalDamage => Element.ChemicalDamage;

    public virtual int AfflictionDuration => Element.AfflictionDuration;

    public virtual float AfflictionRate => Element.AfflictionRate;

    public virtual int ReactionChance => Element.ReactionChance;

    public virtual int ReactionDamage => Element.ReactionDamage;

    public virtual void OnChemicalReaction(in ChemicalReactionEvent e) { }

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

    public override TileConditionRenderMode RenderMode => TileConditionRenderMode.Field;

    public override BlendState FieldBlend => BlendState.Additive;

    protected virtual float PoolSpread => 2.4f;

    public override Asset<Effect>? Shader => Assets.Shaders.TileConditionMask.Asset;

    protected virtual Vector3 BodyColor => Element.BodyColor;

    protected virtual Vector3 EdgeColor => Element.EdgeColor;

    protected virtual Vector3 EmittedLight => Element.EmittedLight;

    protected virtual float Threshold => Element.Threshold;

    protected virtual float NoiseScale => Element.NoiseScale;

    protected virtual Vector2 NoiseDrift => Element.NoiseDrift;

    private Asset<Texture2D>? noiseTexture;

    private Asset<Texture2D>? poolTexture;

    public override void Load()
    {
        if (Main.dedServ)
            return;

        noiseTexture = Assets.Textures.Noise.Turbulence_06.Asset;

        poolTexture = Assets.Textures.VFX.RadialGradient.Asset;
    }

    public override void Unload()
    {
        noiseTexture = null;
        poolTexture = null;
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
    {
        if (poolTexture is not { Value: { } blob })
            return;

        // The same integer snap DrawAsTile applies, and for the same reason: vanilla draws terrain
        // against a truncated screen position, so anything painted on terrain has to as well or it
        // crawls by up to a pixel as the camera moves.
        var pixelSnap = new Vector2(
            Main.screenPosition.X - (int)Main.screenPosition.X,
            Main.screenPosition.Y - (int)Main.screenPosition.Y);

        float size = PoolSpread * 16f;

        spriteBatch.Draw(
            blob,
            instance.WorldCenter - Main.screenPosition + pixelSnap,
            null,
            Color.White * instance.Strength,
            0f,
            blob.Size() / 2f,
            size / blob.Width,
            SpriteEffects.None,
            0f);
    }

    public override void ApplyShaderParameters(Effect effect)
    {
        ApplyFieldFrameParameters(effect);

        if (noiseTexture is { Value: { } noise })
            effect.Parameters["uNoiseTex"]?.SetValue(noise);

        effect.Parameters["WorldLight"]?.SetValue(Vector3.Max(AverageScreenLight(), new Vector3(0.2f)));

        effect.Parameters["Threshold"]?.SetValue(Threshold);
        // a metaball boundary is already smooth, so the noise only keeps the rim from looking
        // machined. Turned up, it shreds the pool into disconnected specks.
        effect.Parameters["NoiseStrength"]?.SetValue(0.16f);
        effect.Parameters["NoiseScale"]?.SetValue(NoiseScale);
        effect.Parameters["NoiseDrift"]?.SetValue(NoiseDrift);
        effect.Parameters["EdgeWidth"]?.SetValue(0.25f);

        effect.Parameters["BodyColor"]?.SetValue(BodyColor);
        effect.Parameters["EdgeColor"]?.SetValue(EdgeColor);
        effect.Parameters["BodyAlpha"]?.SetValue(0.78f);
        effect.Parameters["EdgeAlpha"]?.SetValue(0.92f);
    }

    public override void OnNPCContact(ref TileConditionInstance instance, NPC npc)
    {
        if (instance.Strength < ActiveStrength || !CanAffect(npc))
            return;

        if (ContactBuff >= 0)
            npc.AddBuff(ContactBuff, ContactBuffTicks);

        // The reaction is rolled per tile of contact rather than per tick of standing in it.
        ChemicalPayload.Contact(npc, this, instance.Payload, instance.Strength);
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
