using System.Collections.Generic;
using Terraria.Audio;
using Terraria.GameContent;
using TrelamiumIII.Core.Graphics.Interfaces;
using TrelamiumIII.Core.Graphics.Primitives;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Content.Items.FarmingOverhaul.Seedbeds;

/// <summary>
///     A vanilla herb seed fired as dart ammo, drawn with a two-layer pixel trail. One instance per <see cref="Seeds" /> row;
///     <see cref="Common.Globals.Items.SeedAmmoGlobalItem" /> reads the ammo stats from the same row.
/// </summary>
public sealed class SeedProjectile : ModProjectile, IDrawPrimitive, ICustomAutoload
{
    /// <param name="Item">The vanilla seed item fired as ammo.</param>
    /// <param name="Herb">Name prefix and <c>SeedAmmo</c> localization key.</param>
    /// <param name="FavouriteSoil">Seed bed item type shown in the tooltip.</param>
    /// <param name="OnCrit">Applied on a critical hit, to the owner or the target.</param>
    public sealed record Seed(
        int Item,
        string Herb,
        int Damage,
        int Crit,
        Func<int> FavouriteSoil,
        Color TrailColor,
        int PointCount,
        float OuterWidth,
        float InnerWidth,
        string TrailTexture,
        float TimeScale,
        Action<Player, NPC> OnCrit);

    private static readonly Seed[] Seeds =
    [
        new(ItemID.DaybloomSeeds, "Daybloom", 4, 10, () => SeedBedItem.Dirt,
            new(244, 212, 0), 11, 20f, 8f, Assets.Textures.VFX.EnergyTrail.KEY, 0.07f,
            static (owner, _) => owner.AddBuff(BuffID.Ironskin, 5 * 60)),

        new(ItemID.BlinkrootSeeds, "Blinkroot", 4, 13, () => SeedBedItem.Dirt,
            new(212, 151, 32), 11, 20f, 8f, Assets.Textures.VFX.EnergyTrail.KEY, 0.05f,
            static (owner, _) =>
            {
                owner.AddBuff(BuffID.Swiftness, 4 * 60);
                owner.AddBuff(BuffID.Hunter, 5 * 60);
            }),

        new(ItemID.FireblossomSeeds, "Fireblossom", 5, 9, () => SeedBedItem.Ash,
            new(215, 68, 29), 13, 24f, 18f, Assets.Textures.VFX.FireTrail.KEY, 0.07f,
            static (_, target) => target.AddBuff(BuffID.OnFire, 4 * 60)),

        // TODO: peat or snow, once there is a bed for either.
        new(ItemID.ShiverthornSeeds, "Shiverthorn", 4, 10, () => SeedBedItem.Mud,
            new(92, 183, 255), 11, 17f, 8f, Assets.Textures.VFX.EnergyTrail.KEY, 0.07f,
            static (_, target) => target.AddBuff(BuffID.Frostburn, 4 * 60)),

        new(ItemID.MoonglowSeeds, "Moonglow", 4, 11, () => SeedBedItem.Mud,
            new(52, 196, 187), 11, 20f, 8f, Assets.Textures.VFX.BeamTrail.KEY, 0.07f,
            static (owner, _) => owner.AddBuff(BuffID.Regeneration, 8 * 60)),

        new(ItemID.DeathweedSeeds, "Deathweed", 3, 11, () => SeedBedItem.Ash,
            new(177, 92, 182), 11, 20f, 8f, Assets.Textures.VFX.BeamTrail.KEY, 0.07f,
            static (_, target) => target.AddBuff(BuffID.Poisoned, 5 * 60)),

        new(ItemID.WaterleafSeeds, "Waterleaf", 4, 11, () => SeedBedItem.Sand,
            new(100, 150, 8), 11, 12f, 5f, Assets.Textures.VFX.RibbonTrail.KEY, 0.07f,
            static (_, target) => target.AddBuff(BuffID.Wet, 5 * 60)),
    ];

    private static readonly Dictionary<int, SeedProjectile> bySeedItem = [];

    [CloneByReference]
    private readonly Seed seed;

    // Null on the registered template, so every clone starts with its own trail.
    [CloneByReference]
    private TrailPositionCache? cache;

    [CloneByReference]
    private Vector2[]? positions;

    [CloneByReference]
    private VertexTrail? outer;

    [CloneByReference]
    private VertexTrail? inner;

    private SeedProjectile(Seed seed) => this.seed = seed;

    public Seed Info => seed;

    protected override bool CloneNewInstances => true;

    public override string Name => $"{seed.Herb}SeedProjectile";

    /// <summary>The projectile fired by the seed item <paramref name="itemType" />, or null when it is not seed ammo.</summary>
    public static SeedProjectile? ForSeed(int itemType) => bySeedItem.GetValueOrDefault(itemType);

    public static void Autoload(Mod mod)
    {
        foreach (Seed seed in Seeds)
        {
            var projectile = new SeedProjectile(seed);

            mod.AddContent(projectile);
            bySeedItem[seed.Item] = projectile;
        }
    }

    public override void Unload() => bySeedItem.Clear();

    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 8;
        Projectile.aiStyle = ProjAIStyleID.Arrow;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.timeLeft = 600;
        AIType = ProjectileID.Seed;
    }

    public override void AI()
    {
        base.AI();

        Color color = seed.TrailColor;

        cache ??= new TrailPositionCache(seed.PointCount);
        positions ??= new Vector2[seed.PointCount];
        outer ??= new VertexTrail(seed.PointCount,
            f => f * seed.OuterWidth,
            f => color * 0.6f * MathF.Pow(f, 2f));
        inner ??= new VertexTrail(seed.PointCount,
            f => f * seed.InnerWidth,
            f => Color.Lerp(color, Color.White, 0.6f) * 0.7f * MathF.Pow(f, 2f));

        cache.Update(Projectile.Center);
        cache.Fill(positions);
        outer.Positions = positions;
        inner.Positions = positions;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        if (hit.Crit)
            seed.OnCrit(Main.player[Projectile.owner], target);
    }

    public void DrawPrimitives()
    {
        if (outer is null || inner is null || Assets.Shaders.pixelTrail.Asset.Value is not { } effect)
            return;

        SetTrailParameters(effect, 4f);
        outer.Render(effect);

        SetTrailParameters(effect, 6f);
        inner.Render(effect);
    }

    private void SetTrailParameters(Effect effect, float pixelation)
    {
        var world = Matrix.CreateTranslation(-Main.screenPosition.ToVector3());
        var view = Main.GameViewMatrix.TransformationMatrix;
        var projection = Matrix.CreateOrthographicOffCenter(0, Main.screenWidth, Main.screenHeight, 0, -1, 1);

        effect.Parameters["time"]?.SetValue(Main.GameUpdateCount * seed.TimeScale);
        effect.Parameters["repeats"]?.SetValue(8f);
        effect.Parameters["pixelation"]?.SetValue(pixelation);
        effect.Parameters["resolution"]?.SetValue(new Vector2(Main.screenWidth, Main.screenHeight));
        effect.Parameters["transformMatrix"]?.SetValue(world * view * projection);
        effect.Parameters["sampleTexture"]?.SetValue(ModContent.Request<Texture2D>(seed.TrailTexture).Value);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        Texture2D texture = TextureAssets.Projectile[Type].Value;
        Vector2 offset = Projectile.velocity.SafeNormalize(Vector2.Zero) * 10f;

        Main.EntitySpriteDraw(texture, Projectile.Center - offset - Main.screenPosition, null, lightColor,
            Projectile.rotation, texture.Size() / 2f, Projectile.scale, SpriteEffects.None, 0);

        return false;
    }

    public override void OnKill(int timeLeft)
    {
        Collision.HitTiles(Projectile.position + Projectile.velocity, Projectile.velocity, Projectile.width, Projectile.height);
        SoundEngine.PlaySound(SoundID.Dig, Projectile.position);
    }
}
