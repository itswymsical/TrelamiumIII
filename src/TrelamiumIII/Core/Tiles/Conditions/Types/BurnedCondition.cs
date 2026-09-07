using ReLogic.Content;

namespace TrelamiumIII.Core.Tiles.Conditions.Types;

/// <summary>
///     Terrain on fire, and terrain that has been: a heat curve per tile, rendered as a coverage mask
///     that carries its own temperature.
/// </summary>
/// <remarks>
///     <para>
///         The mask is still the point. Every burning tile stamps its own silhouette into a field and
///         the shader erodes the whole field at once with noise locked to the world, so a run of
///         blocks burns as one patch with holes and licking edges rather than as eight copies of a
///         16x16 texture. Slopes and half blocks come out right for free, because the stamp is the
///         tile's own sprite.
///     </para>
///     <para>
///         What is new is that the stamp carries data. The field's alpha is coverage as before, and
///         its red and green channels now hold the tile's heat and how far the char has crept over
///         it -- see <c>TileSilhouette.fx</c> for the mechanism and <c>TileBurnMask.fx</c> for what
///         is done with it. That is what makes a fire legible as a process: the same tile is ember
///         red as it catches, yellow-white at its peak, and black afterwards, and the tile next to it
///         is somewhere else in that sequence. One <c>BodyColor</c> for the whole screen -- which is
///         all the shared condition shader can offer -- cannot say any of that.
///     </para>
///     <para>
///         The rules a given fire runs under live in a <see cref="BurnProfile" />, chosen at the
///         moment of ignition and remembered on the tile. A torch scuff, a thermite pour and a
///         wildfire are all this condition.
///     </para>
/// </remarks>
public sealed class BurnedCondition : TileCondition
{
    private const float QUENCH_PER_PROBE = 16f;

    /// <summary>Heat below which the fire is out and the tile is only waiting to stop being sooty.</summary>
    private const float SPENT_HEAT = 0.02f;

    /// <summary>
    ///     Fraction of remaining life over which the mark thins out and disappears.
    /// </summary>
    /// <remarks>
    ///     The last quarter. Before it the char sits at full coverage so the blackening is legible;
    ///     inside it the coverage falls, the shader's noise erosion eats the mark from its thinnest
    ///     places first, and the scorch recedes into scattered specks rather than blinking out.
    /// </remarks>
    private const float DISSOLVE_FRACTION = 0.25f;

    /// <summary>Heat below which the fire pass fades out and leaves the tile to the char.</summary>
    private const float FIRE_FADE_HEAT = 0.15f;

    /// <summary>
    ///     How dark a fully charred block gets.
    /// </summary>
    /// <remarks>
    ///     Short of opaque on purpose. Taken all the way to black the tile stops reading as burnt
    ///     stone and starts reading as a hole in the terrain, which is a much louder thing to have
    ///     scattered across a battlefield than scorch marks.
    /// </remarks>
    private const float MAX_CHAR = 0.82f;

    public override float Duration => BurnProfile.Standard.Duration;

    public override int MaxTiles => 1200;

    /// <summary>
    ///     A field for the fire, and a per-tile pass for the char.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The two halves of a burn want opposite geometry, which is why this is the one
    ///         condition that runs both passes. Flame is continuous: it crosses tile seams, it has to
    ///         be eroded as one region, and a run of burning blocks should read as one fire. Char is
    ///         not continuous at all -- it is a specific block that has gone dark.
    ///     </para>
    ///     <para>
    ///         The char was a mask until it was looked at. A black layer composited over terrain
    ///         reads as a sticker on top of the block rather than as the block itself being burnt,
    ///         because it lands in the same plane as the fire and shares its eroded, noisy edge.
    ///         Darkening the tile's own sprite instead is both simpler and truer: the terrain keeps
    ///         its own outline and detail and just loses its light, which is what scorched stone
    ///         actually looks like.
    ///     </para>
    /// </remarks>
    public override TileConditionRenderMode RenderMode
        => TileConditionRenderMode.Field | TileConditionRenderMode.Overlay;

    /// <summary>
    ///     Premultiplied alpha, which is the one blend that leaves the field's channels linear in the
    ///     stamp's own. The shader reads coverage from alpha and heat and char from red and green, so
    ///     all three have to mean what the stamp meant; additive would square them, and no two tiles
    ///     overlap anyway.
    /// </summary>
    public override BlendState FieldBlend => BlendState.AlphaBlend;

    public override Asset<Effect>? Shader => Assets.Shaders.TileBurnMask.Asset;

    /// <summary>
    ///     None. The char is a plain darkening of the block, with nothing done to it.
    /// </summary>
    /// <remarks>
    ///     Explicitly null rather than inherited, because the inherited answer is
    ///     <see cref="Shader" /> and that one reads a finished field out of register s0. Handed a
    ///     batch of tile sprites it would take each block's own alpha for coverage and run the
    ///     blackbody ramp over it, which is a second fire drawn on top of the first.
    /// </remarks>
    public override Asset<Effect>? OverlayShader => null;

    /// <summary>
    ///     Stamps the tile's shape without its colour, so the colour channels are free to carry heat.
    /// </summary>
    /// <remarks>
    ///     Without this the field's red and green would be whatever pigment the block's sprite
    ///     happens to have -- dirt brown, stone grey -- and the shader would read that back as a
    ///     temperature.
    /// </remarks>
    public override Asset<Effect>? StampShader => Assets.Shaders.TileSilhouette.Asset;

    private Asset<Texture2D>? noiseTexture;

    public override void Load()
    {
        if (Main.dedServ)
            return;

        noiseTexture = Assets.Textures.Noise.Turbulence_05.Asset;
    }

    public override void Unload() => noiseTexture = null;

    /// <summary>Fire cannot take on a tile that is still wet; it spends itself boiling the water off.</summary>
    public override bool CanApplyTo(int i, int j)
    {
        if (!base.CanApplyTo(i, j))
            return false;

        if (!TileConditionSystem.Has<SoakedCondition>(i, j))
            return true;

        TileConditionSystem.Remove<SoakedCondition>(i, j);

        if (!Main.dedServ)
            Steam(new Vector2(i * 16f + 8f, j * 16f + 8f), 2);

        return false;
    }

    /// <summary>
    ///     Lights the tile at its profile's ignition heat.
    /// </summary>
    /// <remarks>
    ///     Runs on a fresh application and on a re-application alike, which is what lets a second
    ///     hit on an already burning tile stoke it: the heat carried in is taken as a floor rather
    ///     than as a reset, so pouring more thermite onto a dying fire revives it and pouring a
    ///     scuff onto a raging one does nothing.
    /// </remarks>
    public override void OnApplied(ref TileConditionInstance instance)
    {
        BurnProfile profile = BurnProfile.Get(instance.Variant);

        instance.Level = MathF.Max(instance.Level, profile.Ignition * instance.Intensity);
        instance.Stage = (byte)BurnProfile.StageOf(instance.Level);
    }

    /// <summary>
    ///     Advances the fire: heat first, then what the heat is hot enough to do.
    /// </summary>
    /// <remarks>
    ///     Heat climbs while the tile still has life to spend and falls once it is past its fuel
    ///     fraction, which is where the curve comes from. There is nothing on a tile to model as
    ///     actual fuel, and the instance's own remaining life is a serviceable stand-in -- a fire
    ///     that has been burning a while is a fire that is running out.
    /// </remarks>
    public override void UpdateTile(ref TileConditionInstance instance)
    {
        BurnProfile profile = BurnProfile.Get(instance.Variant);

        bool fuelled = instance.Fade > 1f - profile.FuelFraction;

        // Intensity caps the fire as well as starting it, so the thin rim of a scorch smoulders and
        // never catches while its centre rages.
        float ceiling = profile.Peak * instance.Intensity;

        float delta = fuelled ? profile.HeatGain : -profile.HeatDecay;

        instance.Level = MathHelper.Clamp(instance.Level + delta, 0f, ceiling);

        var stage = BurnProfile.StageOf(instance.Level);

        instance.Stage = (byte)stage;

        if (stage >= BurnStage.Burning && profile.SpreadChance > 0f)
            Spread(in instance, profile);

        // A fire whose heat is gone has nothing left to do but finish blackening, so it hands the
        // rest of its life to the char and stops costing anything but a draw.
        if (instance.Level <= SPENT_HEAT)
        {
            if (profile.ConsumesTiles)
                Consume(ref instance);

            return;
        }

        if (Main.dedServ || !OnScreen(in instance))
            return;

        Embers(in instance, stage);
    }

    /// <summary>
    ///     Rolls to light one of the four neighbours.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         One neighbour per roll rather than all four, chosen at random. Spreading to everything
    ///         at once turns a fire into a flood fill with a delay -- it advances as a perfect
    ///         expanding diamond -- where picking one gives the front a ragged edge that finds its
    ///         way around obstacles the way fire does.
    ///     </para>
    ///     <para>
    ///         Applied through the system rather than by reaching into the batch, so it is deferred
    ///         to the end of the tick. This runs mid-walk over the very dictionary it is adding to.
    ///     </para>
    /// </remarks>
    private static void Spread(in TileConditionInstance instance, BurnProfile profile)
    {
        if (!Main.rand.NextBool() || Main.rand.NextFloat() > profile.SpreadChance * instance.Level)
            return;

        (int dx, int dy) = Main.rand.Next(4) switch
        {
            0 => (-1, 0),
            1 => (1, 0),
            2 => (0, -1),
            _ => (0, 1),
        };

        int x = instance.X + dx;
        int y = instance.Y + dy;

        if (TileConditionSystem.Has<BurnedCondition>(x, y))
            return;

        Ignite(x, y, profile, instance.Intensity * profile.SpreadFalloff, instance.Owner);
    }

    /// <summary>
    ///     Destroys a flammable tile the fire has burnt out on.
    /// </summary>
    /// <remarks>
    ///     Only ever reached from a profile that opted in -- see <see cref="BurnProfile.ConsumesTiles" />
    ///     -- and only on tiles in <see cref="IsFlammable" />. Runs on the server or in single player
    ///     alone, because <see cref="WorldGen.KillTile" /> on a client desynchronises the world.
    /// </remarks>
    private static void Consume(ref TileConditionInstance instance)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || !IsFlammable(instance.Tile.TileType))
            return;

        WorldGen.KillTile(instance.X, instance.Y, noItem: true);

        if (Main.netMode == NetmodeID.Server)
            NetMessage.SendTileSquare(-1, instance.X, instance.Y, 1);

        instance.Life = 0f;
    }

    /// <summary>
    ///     Tiles a consuming fire is allowed to destroy.
    /// </summary>
    /// <remarks>
    ///     Written out rather than taken from a vanilla set, because the vanilla notion of flammable
    ///     covers things this should not touch and the cost of being wrong here is a player's build.
    ///     Wood, growth and cloth; never stone, never ore, never anything structural.
    /// </remarks>
    private static bool IsFlammable(ushort type) => type
        is TileID.LivingFire or TileID.Trees or TileID.PalmTree or TileID.LeafBlock
        or TileID.WoodBlock or TileID.LivingWood or TileID.Plants or TileID.Plants2
        or TileID.Vines or TileID.JungleVines or TileID.HayBlock or TileID.Cobweb
        or TileID.Books or TileID.Bookcases or TileID.WorkBenches or TileID.Chairs;

    /// <summary>Light and embers, scaled by how hot the tile actually is.</summary>
    /// <remarks>
    ///     Driven by heat rather than by <see cref="TileConditionInstance.Strength" />, which is the
    ///     substantive change here. Strength only ever falls, so the old version was at its
    ///     brightest on the frame of ignition and dimmed monotonically -- a fire that was over before
    ///     it started. Heat climbs first, so a tile now visibly catches.
    /// </remarks>
    private static void Embers(in TileConditionInstance instance, BurnStage stage)
    {
        float heat = instance.Level;

        Lighting.AddLight(instance.WorldCenter, 0.42f * heat, 0.17f * heat, 0.03f * heat);

        if (!Main.rand.NextBool((int)MathHelper.Lerp(340f, 60f, heat)))
            return;

        Dust ember = Dust.NewDustPerfect(
            instance.WorldPosition + new Vector2(Main.rand.NextFloat(16f), Main.rand.NextFloat(16f)),
            // Fire throws sparks; something merely smouldering throws ash, which is the visible
            // difference between a tile that is alight and one that is only still warm.
            stage >= BurnStage.Burning ? DustID.Torch : DustID.Ash,
            new Vector2(Main.rand.NextFloat(-0.25f, 0.25f), Main.rand.NextFloat(-1.4f, -0.4f)),
            0,
            default,
            Main.rand.NextFloat(0.7f, 1.3f) * MathHelper.Lerp(0.5f, 1.2f, heat)
        );

        ember.noGravity = true;
    }

    /// <summary>
    ///     Stamps the tile's shape, carrying its heat and char in the colour channels.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Red is heat and green is how black the tile has gone; alpha stays coverage. The whole
    ///         thing is scaled by strength, which premultiplies it -- the shader divides by alpha to
    ///         get the two payload values back. See <c>TileSilhouette.fx</c>, which is what stops the
    ///         block's own pigment landing in those channels instead.
    ///     </para>
    ///     <para>
    ///         Char is derived from age rather than stored. It only ever advances, so elapsed time
    ///         times the profile's rate is the whole of it, and the tile is spared a second float.
    ///     </para>
    ///     <para>
    ///         Coverage is scaled by heat as well as by the mark's own strength, so the fire fades
    ///         out as the fire dies rather than sitting there as a cold red stain. That is what
    ///         hands the tile over to the char pass: by the time the block is fully blackened, the
    ///         field is contributing nothing to it.
    ///     </para>
    ///     <para>
    ///         Scaling alpha does not corrupt the payload, because the shader recovers heat as
    ///         <c>r / a</c> and both were scaled by the same number.
    ///     </para>
    /// </remarks>
    public override void DrawTile(SpriteBatch spriteBatch, in TileConditionInstance instance, Vector2 drawPosition)
    {
        float fire = MathHelper.Clamp(instance.Level / FIRE_FADE_HEAT, 0f, 1f);

        if (fire <= 0f)
            return;

        var payload = new Color(instance.Level, 0f, 0f, 1f);

        DrawSilhouette(in instance, payload * (Coverage(in instance) * fire));
    }

    /// <summary>
    ///     Darkens the block itself, by however far the char has crept over it.
    /// </summary>
    /// <remarks>
    ///     Black at partial alpha over the tile's own silhouette, which multiplies the terrain down
    ///     toward nothing while leaving its shape, its slope and its internal detail intact. Capped
    ///     short of opaque so a burnt block still reads as that block rather than as a hole.
    /// </remarks>
    public override void DrawTileOverlay(SpriteBatch spriteBatch, in TileConditionInstance instance, Vector2 drawPosition)
    {
        float soot = CharProgress(in instance) * Coverage(in instance) * MAX_CHAR;

        if (soot <= 0.004f)
            return;

        DrawSilhouette(in instance, Color.Black * soot);
    }

    /// <summary>
    ///     How much of the tile the mark covers, before anything is done with it.
    /// </summary>
    /// <remarks>
    ///     Deliberately <em>not</em> <see cref="TileConditionInstance.Strength" />, which is what
    ///     every other condition stamps. Strength falls from the moment of application, so a burn
    ///     drawn with it thins out over the same seconds the char is supposed to be creeping across
    ///     it -- the tile would fade before it ever went black. Holding it flat until the last
    ///     quarter of the life puts the sequence back in the order it should read: it burns, it
    ///     blackens, and only then does the mark lift.
    /// </remarks>
    private static float Coverage(in TileConditionInstance instance)
        => instance.Intensity * MathHelper.Clamp(instance.Fade / DISSOLVE_FRACTION, 0f, 1f);

    /// <summary>
    ///     How far the block has blackened, 0..1.
    /// </summary>
    /// <remarks>
    ///     Derived from age rather than stored. Char only ever advances, so elapsed time times the
    ///     profile's rate is the whole of it, and a burning tile is spared a second float.
    /// </remarks>
    private static float CharProgress(in TileConditionInstance instance)
        => MathHelper.Clamp(instance.Age / 60f * BurnProfile.Get(instance.Variant).CharRate, 0f, 1f);

    public override void ApplyShaderParameters(Effect effect)
    {
        ApplyFieldFrameParameters(effect);

        if (noiseTexture is { Value: { } noise })
            effect.Parameters["uNoiseTex"]?.SetValue(noise);

        // Floored low: soot in an unlit cave should stay unlit. The fire does not read this at all,
        // so a burning tile lights itself regardless.
        effect.Parameters["WorldLight"]?.SetValue(Vector3.Max(AverageScreenLight(), new Vector3(0.12f)));

        // Threshold and strength are tuned as a pair. Erosion of roughly twice the threshold means
        // a fully covered tile still dips under the cut wherever the noise troughs, which is what
        // punches holes through the middle of a burn instead of only nibbling its outline.
        effect.Parameters["Threshold"]?.SetValue(0.55f);
        effect.Parameters["NoiseStrength"]?.SetValue(0.95f);
        effect.Parameters["NoiseScale"]?.SetValue(6f);

        // Faster and more upward than the old char pass, because this one is drawing fire rather
        // than the mark fire left. Still slow enough to read as heat coming off a surface.
        effect.Parameters["NoiseDrift"]?.SetValue(new Vector2(0.012f, -0.06f));

        effect.Parameters["EdgeWidth"]?.SetValue(0.22f);

        // The blackbody ramp, coolest to hottest.
        effect.Parameters["EmberColor"]?.SetValue(new Vector3(0.45f, 0.06f, 0.01f));
        effect.Parameters["CoolColor"]?.SetValue(new Vector3(0.88f, 0.26f, 0.03f));
        effect.Parameters["HotColor"]?.SetValue(new Vector3(1.00f, 0.62f, 0.14f));
        effect.Parameters["FlashColor"]?.SetValue(new Vector3(1.00f, 0.94f, 0.74f));

        effect.Parameters["CharColor"]?.SetValue(new Vector3(0.05f, 0.045f, 0.05f));

        // Heat wins over char by a wide margin, so a raging tile is fire with soot at its rim rather
        // than soot with fire showing through.
        effect.Parameters["CharSuppression"]?.SetValue(0.85f);

        effect.Parameters["BodyAlpha"]?.SetValue(0.86f);
        effect.Parameters["EdgeAlpha"]?.SetValue(0.95f);
    }

    /// <summary>Standing in fire sets you alight, and the hotter it is the longer it sticks.</summary>
    public override void OnPlayerContact(ref TileConditionInstance instance, Player player)
    {
        if (instance.Level < 0.22f)
            return;

        player.AddBuff(BuffID.OnFire, (int)MathHelper.Lerp(60f, 180f, instance.Level));
    }

    /// <inheritdoc cref="OnPlayerContact" />
    public override void OnNPCContact(ref TileConditionInstance instance, NPC npc)
    {
        if (npc.friendly || instance.Level < 0.22f)
            return;

        npc.AddBuff(BuffID.OnFire, (int)MathHelper.Lerp(80f, 220f, instance.Level));
    }

    /// <summary>Water puts it out and leaves the tile soaked; lava keeps it going indefinitely.</summary>
    /// <remarks>
    ///     Water takes the heat down as well as the life, so a doused fire visibly dies rather than
    ///     staying just as bright until the moment it disappears.
    /// </remarks>
    public override void OnLiquidContact(ref TileConditionInstance instance, int liquidType, int amount)
    {
        if (liquidType == LiquidID.Lava)
        {
            instance.Life = instance.MaxLife;
            instance.Level = MathF.Min(instance.Level + 0.1f, 1f);

            return;
        }

        float share = amount / 255f;

        instance.Life -= QUENCH_PER_PROBE * share;
        instance.Level = MathF.Max(instance.Level - share * 0.25f, 0f);
        instance.Stage = (byte)BurnProfile.StageOf(instance.Level);

        if (!Main.dedServ)
            Steam(instance.WorldCenter, 1);

        if (instance.Life <= 0f)
            TileConditionSystem.Apply<SoakedCondition>(instance.X, instance.Y, share, sync: false);
    }

    private static void Steam(Vector2 position, int count)
    {
        for (int i = 0; i < count; i++)
        {
            Dust steam = Dust.NewDustPerfect(
                position + Main.rand.NextVector2Circular(7f, 7f),
                DustID.Cloud,
                new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(-1.4f, -0.6f)),
                120,
                Color.White,
                Main.rand.NextFloat(0.7f, 1.2f)
            );

            steam.noGravity = true;
        }
    }

    /// <summary>Lights one tile under a named set of rules.</summary>
    /// <param name="profile">Which kind of fire this is. See <see cref="BurnProfile" />.</param>
    public static bool Ignite(int i, int j, BurnProfile profile, float intensity = 1f, int owner = TileConditionInstance.NoOwner)
        => TileConditionSystem.Apply<BurnedCondition>(i, j, intensity, profile.Duration, owner, sync: false, variant: profile.Id);

    /// <summary>
    ///     Lights everything solid within <paramref name="radius" /> pixels, thinning toward the edge.
    /// </summary>
    /// <remarks>
    ///     Defaults to <see cref="BurnProfile.Standard" />, so the call sites that predate profiles
    ///     keep the behaviour they were tuned against.
    /// </remarks>
    public static void Scorch(Vector2 center, float radius, float intensity = 1f, BurnProfile? profile = null)
    {
        BurnProfile rules = profile ?? BurnProfile.Standard;

        int reach = (int)MathF.Ceiling(radius / 16f);
        int centerX = (int)(center.X / 16f);
        int centerY = (int)(center.Y / 16f);

        for (int x = centerX - reach; x <= centerX + reach; x++)
        {
            for (int y = centerY - reach; y <= centerY + reach; y++)
            {
                float distance = Vector2.Distance(new Vector2(x * 16f + 8f, y * 16f + 8f), center);

                if (distance > radius)
                    continue;

                float falloff = 1f - distance / MathF.Max(radius, 1f);

                Ignite(x, y, rules, intensity * MathHelper.Lerp(0.4f, 1f, falloff));
            }
        }
    }
}
