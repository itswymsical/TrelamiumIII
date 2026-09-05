using ReLogic.Content;
using TrelamiumIII.Common.ChemicalClass;

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
    ///     Affliction tick damage from standing in the residue.
    /// </summary>
    /// <remarks>
    ///     Lower than the cloud that left it by default. The residue is a much larger window of
    ///     exposure -- it sits there for twenty seconds where the cloud passed in two -- so matching
    ///     the cloud's rate would make the leftovers hit harder than the weapon.
    /// </remarks>
    public virtual float ChemicalDamage => 0.35f;

    public virtual int AfflictionDuration => 60;

    public virtual float AfflictionRate => 30f;

    public virtual float ReactionChance => 0.01f;

    public virtual float ReactionDamage => 0f;

    public virtual void OnChemicalReaction(in ChemicalReactionEvent e) { }

    /// <summary>A debuff put on anything standing in it, or -1 for none.</summary>
    protected virtual int ContactBuff => -1;

    protected virtual int ContactBuffTicks => 90;

    /// <summary>Residue below this strength is a stain rather than a hazard, and does nothing.</summary>
    protected virtual float ActiveStrength => 0.25f;

    public override float Duration => 14f * 60f;

    public override int MaxTiles => 700;

    public override TileConditionRenderMode RenderMode => TileConditionRenderMode.Field;

    /// <summary>Premultiplied alpha, so the field's alpha channel stays linear in the stamp's.</summary>
    public override BlendState FieldBlend => BlendState.AlphaBlend;

    public override Asset<Effect>? Shader => Assets.Shaders.TileConditionMask.Asset;

    protected abstract Vector3 BodyColor { get; }

    protected abstract Vector3 EdgeColor { get; }

    protected virtual Vector3 EmittedLight => Vector3.Zero;

    /// <summary>How much of the mask survives the shader's noise erosion. Higher reads wetter.</summary>
    protected virtual float Threshold => 0.42f;

    protected virtual float NoiseScale => 5.5f;

    protected virtual Vector2 NoiseDrift => new(0.01f, -0.014f);

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
    public void Linger(int i, int j, float coverage)
        => TileConditionSystem.Apply(this, i, j, MathHelper.Clamp(coverage * 0.55f, 0.25f, 1f), sync: false);

    public override void UpdateTile(ref TileConditionInstance instance)
    {
        if (Main.dedServ || EmittedLight == Vector3.Zero || !OnScreen(in instance))
            return;

        Lighting.AddLight(instance.WorldCenter, EmittedLight * instance.Strength);
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

    public override void OnPlayerContact(ref TileConditionInstance instance, Player player)
    {
        if (instance.Strength < ActiveStrength || ContactBuff < 0)
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
