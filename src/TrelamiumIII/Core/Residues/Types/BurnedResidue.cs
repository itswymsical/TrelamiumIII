using ReLogic.Content;
using TrelamiumIII.Core.Residues.Reactions;

namespace TrelamiumIII.Core.Residues.Types;

public sealed class BurnedResidue : Residue
{
    private const float QUENCH_PER_PROBE = 16f;

    private const float SPENT_HEAT = 0.02f;

    private const float DISSOLVE_FRACTION = 0.25f;

    private const float FIRE_FADE_HEAT = 0.15f;

    private const float MAX_CHAR = 0.82f;

    /// <summary>What's left at a coat's top edge on a body, of the fuel and heat it has at the feet.</summary>
    private const float BODY_TOP_FUEL = 0.2f;

    private const float BODY_TOP_HEAT = 0.4f;

    private const int BODY_EMBER_ODDS = 5;

    public override float Duration => BurnProfile.Standard.Duration;

    public override int MaxTiles => 1200;

    public override ResidueRenderMode RenderMode => ResidueRenderMode.Canvas;

    public override Asset<Effect>? Shader => Assets.Shaders.TileBurnMask.Asset;

    public override int FadeOutTicks => 20;

    private Asset<Texture2D>? noiseTexture;

    public override void Load()
    {
        if (Main.dedServ)
            return;

        noiseTexture = Assets.Textures.Noise.Turbulence_05.Asset;
    }

    public override void Unload() => noiseTexture = null;

    public override void OnApplied(ref ResidueInstance instance)
    {
        BurnProfile profile = BurnProfile.Get(instance.Variant);

        instance.Level = MathF.Max(instance.Level, profile.Ignition * instance.Intensity);
        instance.Stage = (byte)BurnProfile.StageOf(instance.Level);
    }

    public override void UpdateTile(ref ResidueInstance instance)
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
            if (profile.ConsumesTiles && !fuelled)
                Consume(ref instance, profile);

            return;
        }

        if (Main.dedServ || !OnScreen(in instance))
            return;

        Embers(in instance, stage);
    }

    private static void Spread(in ResidueInstance instance, BurnProfile profile)
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

        if (ResidueSystem.Has<BurnedResidue>(x, y))
            return;

        if (profile.SpreadsAlongFuel && !ResidueSystem.HasElement(x, y, ResidueElement.Flammable))
            return;

        Ignite(x, y, profile, instance.Intensity * profile.SpreadFalloff, instance.Owner);
    }

    /// <summary>
    ///     The fire has burned out: it takes the tile if it can, and either way it is over. A tile that stands up to it
    ///     just keeps the char.
    /// </summary>
    private static void Consume(ref ResidueInstance instance, BurnProfile profile)
    {
        if (profile.ConsumesAnyTile || IsFlammable(instance.Tile.TileType))
            TryDestroyTile(ref instance);

        instance.Variant = BurnProfile.Doused.Id;
    }

    /// <summary>Puts the fire out. Its char stays to fade, and being out it no longer takes the tile.</summary>
    private static void Douse(ref ResidueInstance instance)
    {
        instance.Level = 0f;
        instance.Stage = (byte)BurnStage.BurntOut;
        instance.Variant = BurnProfile.Doused.Id;
    }

    public override ResidueElement ElementsOf(in ResidueInstance instance) => ResidueElement.Heat;

    public override float PotencyOf(in ResidueInstance instance) => instance.Level;

    public override void Diminish(ref ResidueInstance instance, float share)
    {
        instance.Level *= 1f - share;
        instance.Stage = (byte)BurnProfile.StageOf(instance.Level);

        if (instance.Level <= SPENT_HEAT)
            Douse(ref instance);
    }

    private static bool IsFlammable(ushort type) => type
        is TileID.LivingFire or TileID.Trees or TileID.PalmTree or TileID.LeafBlock
        or TileID.WoodBlock or TileID.LivingWood or TileID.Plants or TileID.Plants2
        or TileID.Vines or TileID.JungleVines or TileID.HayBlock or TileID.Cobweb
        or TileID.Books or TileID.Bookcases or TileID.WorkBenches or TileID.Chairs;

    private static void Embers(in ResidueInstance instance, BurnStage stage)
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

    /// <summary>Heat as an average, char and flame as amounts scaled by the mark's coverage.</summary>
    public override TileCanvasSample EncodeCanvas(in ResidueInstance instance)
    {
        float coverage = Coverage(in instance);
        float fire = MathHelper.Clamp(instance.Level / FIRE_FADE_HEAT, 0f, 1f);

        return new TileCanvasSample(coverage, new Vector3(instance.Level, coverage * CharProgress(in instance) * MAX_CHAR, coverage * fire));
    }

    private static float Coverage(in ResidueInstance instance)
        => instance.Intensity * MathHelper.Clamp(instance.Fade / DISSOLVE_FRACTION, 0f, 1f);

    /// <summary>Counted from the first ignition, so re-lighting a tile does not clean its char off.</summary>
    private static float CharProgress(in ResidueInstance instance)
        => MathHelper.Clamp(instance.TotalAge / 60f * BurnProfile.Get(instance.Variant).CharRate, 0f, 1f);

    public override void ApplyShaderParameters(Effect effect)
    {
        if (noiseTexture is { Value: { } noise })
            effect.Parameters["uNoiseTex"]?.SetValue(noise);

        effect.Parameters["FireThreshold"]?.SetValue(0.4f);
        effect.Parameters["FireNoiseScale"]?.SetValue(0.09f);
        effect.Parameters["FireStretch"]?.SetValue(1f);
        effect.Parameters["FireNoiseStrength"]?.SetValue(0.8f);
        effect.Parameters["FireDrift"]?.SetValue(new Vector2(0.012f, 0.06f));
        effect.Parameters["FireEdgeWidth"]?.SetValue(0.22f);
        effect.Parameters["FireBodyAlpha"]?.SetValue(0.86f);
        effect.Parameters["FireEdgeAlpha"]?.SetValue(0.95f);

        effect.Parameters["EmberColor"]?.SetValue(new Vector3(0.45f, 0.06f, 0.01f));
        effect.Parameters["CoolColor"]?.SetValue(new Vector3(0.88f, 0.26f, 0.03f));
        effect.Parameters["HotColor"]?.SetValue(new Vector3(1.00f, 0.62f, 0.14f));
        effect.Parameters["FlashColor"]?.SetValue(new Vector3(1.00f, 0.94f, 0.74f));

        effect.Parameters["CharColor"]?.SetValue(new Vector3(0.05f, 0.045f, 0.05f));
        effect.Parameters["CharAlpha"]?.SetValue(0.85f);
        effect.Parameters["CharThreshold"]?.SetValue(0.05f);
        effect.Parameters["CharSoftness"]?.SetValue(0.6f);
        effect.Parameters["CharNoiseScale"]?.SetValue(0.2f);
        effect.Parameters["CharNoiseStrength"]?.SetValue(0.6f);
        effect.Parameters["CharBands"]?.SetValue(5f);
    }

    /// <summary>Flames stand a little proud of the outline and more so upward. Never down, where they'd sit on the ground under the feet.</summary>
    private static readonly Point[] FlameReach =
    [
        new(0, 0),
        new(-2, 0),
        new(2, 0),
        new(0, -2),
        new(-2, -2),
        new(2, -2),
        new(0, -4),
    ];

    public override Point[] BodyReach => FlameReach;

    /// <summary>Solid and yellow at the feet, breaking up into thin red tongues at the top. No char: at this size soot only reads as noise.</summary>
    public override TileCanvasSample EncodeBody(float rise, int age)
    {
        var instance = new ResidueInstance
        {
            Life = 1f,
            MaxLife = 1f,
            Intensity = MathHelper.Lerp(BODY_TOP_FUEL, 1f, rise),
            Level = MathHelper.Lerp(BODY_TOP_HEAT, 1f, rise),
        };

        return EncodeCanvas(in instance);
    }

    /// <summary>
    ///     The ground's look is scaled for fire many tiles wide, so a body's flames are finer, taller and faster. Strength and
    ///     threshold are both high so that fuel decides where fire shows: this noise runs dark, and with less the whole shape
    ///     fills in up to a flat line where the stamps end.
    /// </summary>
    public override void ApplyBodyParameters(Effect effect, float gravDir, float height)
    {
        ApplyShaderParameters(effect);

        float detail = BodyDetail(height);

        effect.Parameters["FireThreshold"]?.SetValue(1.9f);
        effect.Parameters["FireNoiseScale"]?.SetValue(0.55f * detail);
        effect.Parameters["FireStretch"]?.SetValue(3f);
        effect.Parameters["FireNoiseStrength"]?.SetValue(5f);
        effect.Parameters["FireDrift"]?.SetValue(new Vector2(0.02f, 0.7f * detail * gravDir));
        effect.Parameters["FireEdgeWidth"]?.SetValue(0.5f);
        effect.Parameters["FireBodyAlpha"]?.SetValue(0.92f);
        effect.Parameters["FireEdgeAlpha"]?.SetValue(1f);
    }

    public override void UpdateBody(Entity body, float fill)
    {
        if (Main.dedServ)
            return;

        float gravDir = body is Player player ? player.gravDir : 1f;
        Vector2 feet = gravDir < 0f ? body.Top : body.Bottom;
        float height = body.height * fill;
        Vector2 flames = feet - new Vector2(0f, height * 0.5f * gravDir);
        float heat = MathHelper.Clamp(fill * 2f, 0f, 1f);

        Lighting.AddLight(flames, 0.42f * heat, 0.17f * heat, 0.03f * heat);

        if (!Main.rand.NextBool(BODY_EMBER_ODDS))
            return;

        Dust ember = Dust.NewDustPerfect(
            feet + new Vector2(Main.rand.NextFloat(-body.width * 0.5f, body.width * 0.5f), -Main.rand.NextFloat(height) * gravDir),
            DustID.Torch,
            body.velocity * 0.4f + new Vector2(Main.rand.NextFloat(-0.25f, 0.25f), Main.rand.NextFloat(-1.8f, -0.6f) * gravDir),
            0,
            default,
            Main.rand.NextFloat(0.8f, 1.3f));

        ember.noGravity = true;
    }

    public override void OnPlayerContact(ref ResidueInstance instance, Player player)
    {
        if (instance.Level < 0.22f)
            return;

        player.AddBuff(BuffID.OnFire, (int)MathHelper.Lerp(60f, 180f, instance.Level));
    }

    public override void OnNPCContact(ref ResidueInstance instance, NPC npc)
    {
        if (npc.friendly || instance.Level < 0.22f)
            return;

        npc.AddBuff(BuffID.OnFire, (int)MathHelper.Lerp(80f, 220f, instance.Level));
    }

    public override void OnLiquidContact(ref ResidueInstance instance, int liquidType, int amount)
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

        if (instance.Level <= SPENT_HEAT)
            Douse(ref instance);

        if (!Main.dedServ)
            ReactionEffects.Steam(instance.WorldCenter, 1);
    }

    /// <summary>Lights one tile under a named set of rules.</summary>
    /// <param name="profile">Which kind of fire this is. See <see cref="BurnProfile" />.</param>
    public static bool Ignite(int i, int j, BurnProfile profile, float intensity = 1f, int owner = ResidueInstance.NoOwner)
        => ResidueSystem.Apply<BurnedResidue>(i, j, intensity, profile.Duration, owner, sync: false, variant: profile.Id);

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
