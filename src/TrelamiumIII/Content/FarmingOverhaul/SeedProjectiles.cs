using TrelamiumIII.Core.Graphics.Interfaces;
using TrelamiumIII.Core.Graphics.Primitives;
using TrelamiumIII.Utilities;
using Terraria.Audio;
using Terraria.GameContent;

namespace TrelamiumIII.Content.FarmingOverhaul;

// Shared helper so DrawPrimitives isn't duplicated seven times.
// Each seed passes its own effect params and trails.
file static class SeedTrailHelper
{
    public static void SetEffectParams(Effect effect, string texturePath, float timeScale, float pixelation)
    {
        var world = Matrix.CreateTranslation(-Main.screenPosition.ToVector3());
        var view = Main.GameViewMatrix.TransformationMatrix;
        var projection = Matrix.CreateOrthographicOffCenter(
            0, Main.screenWidth, Main.screenHeight, 0, -1, 1);

        effect.Parameters["time"]?.SetValue(Main.GameUpdateCount * timeScale);
        effect.Parameters["repeats"]?.SetValue(8f);
        effect.Parameters["pixelation"]?.SetValue(pixelation);
        effect.Parameters["resolution"]?.SetValue(new Vector2(Main.screenWidth, Main.screenHeight));
        effect.Parameters["transformMatrix"]?.SetValue(world * view * projection);
        effect.Parameters["sampleTexture"]?.SetValue(
            ModContent.Request<Texture2D>(texturePath).Value);
    }

    public static bool PreDrawDefault(ModProjectile mp, Color lightColor)
    {
        var texture = TextureAssets.Projectile[mp.Projectile.type].Value;
        var offset = mp.Projectile.velocity.SafeNormalize(Vector2.Zero) * 10f;
        var drawPos = mp.Projectile.Center - offset - Main.screenPosition;
        var source = new Rectangle(0, 0, texture.Width, texture.Height);
        var origin = source.Size() / 2f;

        Main.EntitySpriteDraw(texture, drawPos, source, lightColor,
            mp.Projectile.rotation, origin, mp.Projectile.scale,
            SpriteEffects.None, 0);

        return false;
    }
}

public class DaybloomSeedProjectile : ModProjectile, IDrawPrimitive
{
    static readonly Color TrailColor = new(244, 212, 0);
    const int PointCount = 11;

    TrailPositionCache cache;
    Vector2[] positions;
    VertexTrail trail;
    VertexTrail trail2;

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

        cache ??= new TrailPositionCache(PointCount);
        positions ??= new Vector2[PointCount];
        trail ??= new VertexTrail(PointCount,
            f => f * 20f,
            f => TrailColor * 0.6f * MathF.Pow(f, 2f));
        trail2 ??= new VertexTrail(PointCount,
            f => f * 8f,
            f => Color.Lerp(TrailColor, Color.White, 0.6f) * 0.7f * MathF.Pow(f, 2f));

        cache.Update(Projectile.Center);
        cache.Fill(positions);
        trail.Positions = positions;
        trail2.Positions = positions;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        if (hit.Crit)
            Main.player[Projectile.owner].AddBuff(BuffID.Ironskin, 5 * 60);
    }

    public void DrawPrimitives()
    {
        var effect = Assets.Shaders.pixelTrail.Asset.Value;
        if (effect == null) return;

        SeedTrailHelper.SetEffectParams(effect, $"{Assets.Textures.VFX.EnergyTrail.KEY}", 0.07f, 4f);
        trail.Render(effect);
        SeedTrailHelper.SetEffectParams(effect, $"{Assets.Textures.VFX.EnergyTrail.KEY}", 0.07f, 6f);
        trail2.Render(effect);
    }

    public override bool PreDraw(ref Color lightColor) =>
        SeedTrailHelper.PreDrawDefault(this, lightColor);

    public override void OnKill(int timeLeft)
    {
        Collision.HitTiles(Projectile.position + Projectile.velocity,
            Projectile.velocity, Projectile.width, Projectile.height);
        SoundEngine.PlaySound(SoundID.Dig, Projectile.position);
    }
}

public class BlinkrootSeedProjectile : ModProjectile, IDrawPrimitive
{
    static readonly Color TrailColor = new(212, 151, 32);
    const int PointCount = 11;

    TrailPositionCache cache;
    Vector2[] positions;
    VertexTrail trail;
    VertexTrail trail2;

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

        cache ??= new TrailPositionCache(PointCount);
        positions ??= new Vector2[PointCount];
        trail ??= new VertexTrail(PointCount,
            f => f * 20f,
            f => TrailColor * 0.6f * MathF.Pow(f, 2f));
        trail2 ??= new VertexTrail(PointCount,
            f => f * 8f,
            f => Color.Lerp(TrailColor, Color.White, 0.6f) * 0.7f * MathF.Pow(f, 2f));

        cache.Update(Projectile.Center);
        cache.Fill(positions);
        trail.Positions = positions;
        trail2.Positions = positions;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        if (hit.Crit)
        {
            var owner = Main.player[Projectile.owner];
            owner.AddBuff(BuffID.Swiftness, 4 * 60);
            owner.AddBuff(BuffID.Hunter, 5 * 60);
        }
    }

    public void DrawPrimitives()
    {
        var effect = Assets.Shaders.pixelTrail.Asset.Value;
        if (effect == null) return;

        SeedTrailHelper.SetEffectParams(effect, $"{Assets.Textures.VFX.EnergyTrail.KEY}", 0.05f, 4f);
        trail.Render(effect);
        SeedTrailHelper.SetEffectParams(effect, $"{Assets.Textures.VFX.EnergyTrail.KEY}", 0.05f, 6f);
        trail2.Render(effect);
    }

    public override bool PreDraw(ref Color lightColor) =>
        SeedTrailHelper.PreDrawDefault(this, lightColor);

    public override void OnKill(int timeLeft)
    {
        Collision.HitTiles(Projectile.position + Projectile.velocity,
            Projectile.velocity, Projectile.width, Projectile.height);
        SoundEngine.PlaySound(SoundID.Dig, Projectile.position);
    }
}

public class FireblossomSeedProjectile : ModProjectile, IDrawPrimitive
{
    static readonly Color TrailColor = new(215, 68, 29);
    const int PointCount = 13;

    TrailPositionCache cache;
    Vector2[] positions;
    VertexTrail trail;
    VertexTrail trail2;

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

        cache ??= new TrailPositionCache(PointCount);
        positions ??= new Vector2[PointCount];
        trail ??= new VertexTrail(PointCount,
            f => f * 24f,
            f => TrailColor * 0.6f * MathF.Pow(f, 2f));
        trail2 ??= new VertexTrail(PointCount,
            f => f * 18f,
            f => Color.Lerp(TrailColor, Color.White, 0.6f) * 0.7f * MathF.Pow(f, 2f));

        cache.Update(Projectile.Center);
        cache.Fill(positions);
        trail.Positions = positions;
        trail2.Positions = positions;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        if (hit.Crit)
            target.AddBuff(BuffID.OnFire, 4 * 60);
    }

    public void DrawPrimitives()
    {
        var effect = Assets.Shaders.pixelTrail.Asset.Value;
        if (effect == null) return;

        SeedTrailHelper.SetEffectParams(effect, $"{Assets.Textures.VFX.FireTrail.KEY}", 0.07f, 4f);
        trail.Render(effect);
        SeedTrailHelper.SetEffectParams(effect, $"{Assets.Textures.VFX.FireTrail.KEY}", 0.07f, 6f);
        trail2.Render(effect);
    }

    public override bool PreDraw(ref Color lightColor) =>
        SeedTrailHelper.PreDrawDefault(this, lightColor);

    public override void OnKill(int timeLeft)
    {
        Collision.HitTiles(Projectile.position + Projectile.velocity,
            Projectile.velocity, Projectile.width, Projectile.height);
        SoundEngine.PlaySound(SoundID.Dig, Projectile.position);
    }
}

public class ShiverthornSeedProjectile : ModProjectile, IDrawPrimitive
{
    static readonly Color TrailColor = new(92, 183, 255);
    const int PointCount = 11;

    TrailPositionCache cache;
    Vector2[] positions;
    VertexTrail trail;
    VertexTrail trail2;

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

        cache ??= new TrailPositionCache(PointCount);
        positions ??= new Vector2[PointCount];
        trail ??= new VertexTrail(PointCount,
            f => f * 17f,
            f => TrailColor * 0.6f * MathF.Pow(f, 2f));
        trail2 ??= new VertexTrail(PointCount,
            f => f * 8f,
            f => Color.Lerp(TrailColor, Color.White, 0.6f) * 0.7f * MathF.Pow(f, 2f));

        cache.Update(Projectile.Center);
        cache.Fill(positions);
        trail.Positions = positions;
        trail2.Positions = positions;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        if (hit.Crit)
            target.AddBuff(BuffID.Frostburn, 4 * 60);
    }

    public void DrawPrimitives()
    {
        var effect = Assets.Shaders.pixelTrail.Asset.Value;
        if (effect == null) return;

        SeedTrailHelper.SetEffectParams(effect, $"{Assets.Textures.VFX.EnergyTrail.KEY}", 0.07f, 4f);
        trail.Render(effect);
        SeedTrailHelper.SetEffectParams(effect, $"{Assets.Textures.VFX.EnergyTrail.KEY}", 0.07f, 6f);
        trail2.Render(effect);
    }

    public override bool PreDraw(ref Color lightColor) =>
        SeedTrailHelper.PreDrawDefault(this, lightColor);

    public override void OnKill(int timeLeft)
    {
        Collision.HitTiles(Projectile.position + Projectile.velocity,
            Projectile.velocity, Projectile.width, Projectile.height);
        SoundEngine.PlaySound(SoundID.Dig, Projectile.position);
    }
}

 
public class MoonglowSeedProjectile : ModProjectile, IDrawPrimitive
{
    static readonly Color TrailColor = new(52, 196, 187);
    const int PointCount = 11;

    TrailPositionCache cache;
    Vector2[] positions;
    VertexTrail trail;
    VertexTrail trail2;

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

        cache ??= new TrailPositionCache(PointCount);
        positions ??= new Vector2[PointCount];
        trail ??= new VertexTrail(PointCount,
            f => f * 20f,
            f => TrailColor * 0.6f * MathF.Pow(f, 2f));
        trail2 ??= new VertexTrail(PointCount,
            f => f * 8f,
            f => Color.Lerp(TrailColor, Color.White, 0.6f) * 0.7f * MathF.Pow(f, 2f));

        cache.Update(Projectile.Center);
        cache.Fill(positions);
        trail.Positions = positions;
        trail2.Positions = positions;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        if (hit.Crit)
            Main.player[Projectile.owner].AddBuff(BuffID.Regeneration, 8 * 60);
    }

    public void DrawPrimitives()
    {
        var effect = Assets.Shaders.pixelTrail.Asset.Value;
        if (effect == null) return;

        SeedTrailHelper.SetEffectParams(effect, $"{Assets.Textures.VFX.BeamTrail.KEY}", 0.07f, 4f);
        trail.Render(effect);
        SeedTrailHelper.SetEffectParams(effect, $"{Assets.Textures.VFX.BeamTrail.KEY}", 0.07f, 6f);
        trail2.Render(effect);
    }

    public override bool PreDraw(ref Color lightColor) =>
        SeedTrailHelper.PreDrawDefault(this, lightColor);

    public override void OnKill(int timeLeft)
    {
        Collision.HitTiles(Projectile.position + Projectile.velocity,
            Projectile.velocity, Projectile.width, Projectile.height);
        SoundEngine.PlaySound(SoundID.Dig, Projectile.position);
    }
}

public class DeathweedSeedProjectile : ModProjectile, IDrawPrimitive
{
    static readonly Color TrailColor = new(177, 92, 182);
    const int PointCount = 11;

    TrailPositionCache cache;
    Vector2[] positions;
    VertexTrail trail;
    VertexTrail trail2;

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

        cache ??= new TrailPositionCache(PointCount);
        positions ??= new Vector2[PointCount];
        trail ??= new VertexTrail(PointCount,
            f => f * 20f,
            f => TrailColor * 0.6f * MathF.Pow(f, 2f));
        trail2 ??= new VertexTrail(PointCount,
            f => f * 8f,
            f => Color.Lerp(TrailColor, Color.White, 0.6f) * 0.7f * MathF.Pow(f, 2f));

        cache.Update(Projectile.Center);
        cache.Fill(positions);
        trail.Positions = positions;
        trail2.Positions = positions;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        if (hit.Crit)
            target.AddBuff(BuffID.Poisoned, 5 * 60);
    }

    public void DrawPrimitives()
    {
        var effect = Assets.Shaders.pixelTrail.Asset.Value;
        if (effect == null) return;

        SeedTrailHelper.SetEffectParams(effect, $"{Assets.Textures.VFX.BeamTrail.KEY}", 0.07f, 4f);
        trail.Render(effect);
        SeedTrailHelper.SetEffectParams(effect, $"{Assets.Textures.VFX.BeamTrail.KEY}", 0.07f, 6f);
        trail2.Render(effect);
    }

    public override bool PreDraw(ref Color lightColor) =>
        SeedTrailHelper.PreDrawDefault(this, lightColor);

    public override void OnKill(int timeLeft)
    {
        Collision.HitTiles(Projectile.position + Projectile.velocity,
            Projectile.velocity, Projectile.width, Projectile.height);
        SoundEngine.PlaySound(SoundID.Dig, Projectile.position);
    }
}

public class WaterleafSeedProjectile : ModProjectile, IDrawPrimitive
{
    static readonly Color TrailColor = new(100, 150, 8);
    const int PointCount = 11;

    TrailPositionCache cache;
    Vector2[] positions;
    VertexTrail trail;
    VertexTrail trail2;

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

        cache ??= new TrailPositionCache(PointCount);
        positions ??= new Vector2[PointCount];
        trail ??= new VertexTrail(PointCount,
            f => f * 12f,
            f => TrailColor * 0.6f * MathF.Pow(f, 2f));
        trail2 ??= new VertexTrail(PointCount,
            f => f * 5f,
            f => Color.Lerp(TrailColor, Color.White, 0.6f) * 0.7f * MathF.Pow(f, 2f));

        cache.Update(Projectile.Center);
        cache.Fill(positions);
        trail.Positions = positions;
        trail2.Positions = positions;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        if (hit.Crit)
            target.AddBuff(BuffID.Wet, 5 * 60);
    }

    public void DrawPrimitives()
    {
        var effect = Assets.Shaders.pixelTrail.Asset.Value;
        if (effect == null) return;

        SeedTrailHelper.SetEffectParams(effect, $"{Assets.Textures.VFX.RibbonTrail.KEY}", 0.07f, 4f);
        trail.Render(effect);
        SeedTrailHelper.SetEffectParams(effect, $"{Assets.Textures.VFX.RibbonTrail.KEY}", 0.07f, 6f);
        trail2.Render(effect);
    }

    public override bool PreDraw(ref Color lightColor) =>
        SeedTrailHelper.PreDrawDefault(this, lightColor);

    public override void OnKill(int timeLeft)
    {
        Collision.HitTiles(Projectile.position + Projectile.velocity,
            Projectile.velocity, Projectile.width, Projectile.height);
        SoundEngine.PlaySound(SoundID.Dig, Projectile.position);
    }
}