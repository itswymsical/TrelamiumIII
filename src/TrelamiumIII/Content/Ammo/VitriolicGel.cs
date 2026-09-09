using ReLogic.Content;
using Terraria.Audio;
using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core.Alchemy;
using TrelamiumIII.Core.Components.Items;
using TrelamiumIII.Core.Gas;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Content.Ammo;

public class VitriolicGel : ModItem, IChemicalRoleDefined, IAlmanacDefined
{
    public override string Texture => "Terraria/Images/Item_" + ItemID.Gel;

    public ChemicalRole ChemicalRole => ChemicalRole.Solute;

    AlmanacEntry IAlmanacDefined.AlmanacEntry => new()
    {
        Potency = 2,
        Chemical = ChemicalQuality.Acidic,
        Principle = PrincipleType.Martial,
    };

    public override void SetDefaults()
    {
        Item.ammo = ItemID.Gel;
        Item.DamageType = ModContent.GetInstance<ChemicalDamageClass>();
        Item.width = Item.height = 30;

        Item.damage = 3;
        Item.knockBack = 0.75f;
        Item.noMelee = true;
        Item.crit = 4;
        Item.shootSpeed = 7f;
        Item.consumable = true;
        Item.maxStack = Item.CommonMaxStack;
        Item.shoot = ModContent.ProjectileType<VitriolicGelProj>();
        Item.color = Color.YellowGreen;

        if (!Item.TryEnable(out ChemicalSourceComponent component))
            return;

        component.ChemicalType = ChemicalDamageType.Caustic;
        component.ChemicalDamage = 3f;
        component.AfflictionRate = 50;
        component.AfflictionDuration = TimeConversion.SetTime(secs: 5);
    }
}

public class VitriolicGelProj : ModProjectile
{
    private const int BURST_NODES = 3;
    public override string Texture => "Terraria/Images/Item_" + ItemID.Gel;

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 5;
        ProjectileID.Sets.TrailingMode[Type] = 2;
    }

    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 12;
        Projectile.friendly = true;
        Projectile.DamageType = ModContent.GetInstance<ChemicalDamageClass>();
        Projectile.penetrate = 1;
        Projectile.timeLeft = 240;
        Projectile.tileCollide = true;
        Projectile.aiStyle = -1;
        Projectile.scale = 1f;
    }

    public override void AI()
    {
        Projectile.velocity.Y += 0.16f;
        Projectile.velocity.X *= 0.997f;

        Projectile.rotation += Projectile.velocity.X * 0.04f;

        Trail();
    }

    private void Trail()
    {
        if (GasSystem.GetLayer<VitriolicGelLayer>() is not { } layer)
            return;

        Vector2 behind = Projectile.Center - Projectile.velocity * 0.4f;
        Vector2 drift = Projectile.velocity * 0.12f + Main.rand.NextVector2Circular(0.6f, 0.6f);

        layer.SpawnNode(
            behind,
            drift,
            radius: Main.rand.NextFloat(16f, 26f),
            life: Main.rand.NextFloat(16f, 26f),
            owner: Projectile.owner
        );
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        target.AddBuff(BuffID.Poisoned, 120);

        Burst(Projectile.Center, -Projectile.velocity.SafeNormalize(-Vector2.UnitY), Projectile.owner);
    }

    public override bool OnTileCollide(Vector2 oldVelocity)
    {
        var normal = new Vector2(
            Projectile.velocity.X != oldVelocity.X ? -MathF.Sign(oldVelocity.X) : 0f,
            Projectile.velocity.Y != oldVelocity.Y ? -MathF.Sign(oldVelocity.Y) : 0f
        );

        Burst(Projectile.Center, normal.SafeNormalize(-Vector2.UnitY), Projectile.owner);

        Projectile.Kill();

        return false;
    }

    public override void OnKill(int timeLeft)
        => SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.5f, Pitch = 0.5f, MaxInstances = 4 }, Projectile.Center);

    private static void Burst(Vector2 position, Vector2 outward, int owner)
    {

        if (GasSystem.GetLayer<VitriolicGelLayer>() is not { } layer)
            return;

        for (int i = 0; i < BURST_NODES; i++)
        {
            Vector2 direction = outward.RotatedByRandom(MathHelper.PiOver2 * 0.9f);

            layer.SpawnNode(
                position + direction * 4f,
                direction * Main.rand.NextFloat(1.5f, 4.5f),
                radius: Main.rand.NextFloat(7f, 12f),
                life: Main.rand.NextFloat(52f, 66f),
                owner: owner
            );
        }
    }

    public override bool PreDraw(ref Color lightColor) => false;
}

public class VitriolicGelLayer : ChemicalGasField
{
    private const int BURN_TICKS = 45;

    public override ChemicalDamageType ChemicalType => ChemicalDamageType.Caustic;

    public override float ChemicalDamage => 0.25f;

    public override int AfflictionDuration => 90;

    public override float AfflictionRate => 30f;

    public override float ReactionChance => 0f;

    public override float ReactionDamage => 0f;

    protected override Asset<Effect> Shader => Assets.Shaders.GasFbm.Asset;

    protected override string TexturePath => Assets.Textures.VFX.FlameNode_02.KEY;

    protected override int GasType => GasTypeID.Liquid;

    /// <summary>Heavy and sluggish: gel arcs and lands, it does not billow.</summary>
    protected override float DefaultDensity => 0.95f;

    protected override float DefaultViscosity => 0.72f;

    protected override float GravityScale => 0.18f;

    protected override float BuoyancyForce => 0.02f;

    protected override float Drag => 0.986f;

    protected override float LifeDecay => 0.9f;

    protected override float EnclosedLifePenalty => 0.6f;

    protected override bool UsesTileField => true;

    protected override float TileFieldThreshold => 0.45f;

    protected override int TileFieldInterval => 3;

    private Asset<Texture2D>? noiseTexture;

    public override void Load()
    {
        base.Load();

        if (!Main.dedServ)
            noiseTexture = Assets.Textures.Noise.LiquidNoise.Asset;
    }

    public override void Unload()
    {
        base.Unload();

        noiseTexture = null;
    }

    protected override void ApplyShaderParameters(Effect effect)
    {
        effect.Parameters["Time"]?.SetValue((float)Main.timeForVisualEffects / 60f);

        if (noiseTexture is { Value: { } noise })
            effect.Parameters["uNoiseTex"]?.SetValue(noise);

        effect.Parameters["WorldLight"]?.SetValue(new Vector3(0.25f));

        effect.Parameters["FieldTexel"]?.SetValue(new Vector2(2f / Main.screenWidth, 2f / Main.screenHeight));
        effect.Parameters["WorldOffset"]?.SetValue(Main.screenPosition / Main.screenHeight);
        effect.Parameters["Aspect"]?.SetValue(Main.screenWidth / (float)Main.screenHeight);

        effect.Parameters["Threshold"]?.SetValue(0.13f);
        effect.Parameters["NoiseScale"]?.SetValue(1.0f);
        effect.Parameters["NoiseStrength"]?.SetValue(0.14f);
        effect.Parameters["NoiseDrift"]?.SetValue(new Vector2(0.016f, 0.092f));

        effect.Parameters["Bands"]?.SetValue(8f);
        effect.Parameters["BodyAlpha"]?.SetValue(0.90f);

        effect.Parameters["ShadowColor"]?.SetValue(new Vector3(0.035f, 0.09f, 0.008f));
        effect.Parameters["BodyColor"]?.SetValue(new Vector3(0.25f, 0.48f, 0.018f));
        effect.Parameters["CoreColor"]?.SetValue(new Vector3(0.65f, 0.88f, 0.035f));
        effect.Parameters["OutlineColor"]?.SetValue(new Vector3(0.88f, 1.00f, 0.20f));
        effect.Parameters["OutlineAlpha"]?.SetValue(0.94f);
    }

    protected override void ApplyLighting(ref GasNode node)
        => Lighting.AddLight(node.Position, 0.03f * node.Alpha, 0.19f * node.Alpha, 0.09f * node.Alpha);

    protected override void OnNPCContact(ref GasNode node, NPC npc)
    {
        base.OnNPCContact(ref node, npc);

        npc.AddBuff(BuffID.Poisoned, BURN_TICKS);

        npc.velocity.X *= 0.97f;
    }

    protected override void OnPlayerContact(ref GasNode node, Player player) { }
}

