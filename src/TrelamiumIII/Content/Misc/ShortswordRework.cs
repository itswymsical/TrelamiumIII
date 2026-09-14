using System.Collections.Generic;
using Terraria.DataStructures;
using Terraria.GameContent;
using TrelamiumIII.Core.Projectiles;

namespace TrelamiumIII.Content.Misc;

/// <summary>Runs vanilla shortsword stabs (<see cref="ProjAIStyleID.ShortSword" />) as a <see cref="HeldSword" /> combo.</summary>
/// <remarks>
///     The AI_161 detour replaces only vanilla's AI: the projectile keeps its type, owner and <c>ai[]</c> slots, and other
///     mods' PreAI/AI/PostAI still run. Stab types without an entry in <see cref="Profiles" /> fall through to vanilla.
/// </remarks>
public sealed class ShortswordRework : GlobalProjectile
{
    private static readonly Dictionary<int, SwordProfile> Profiles = BuildProfiles();

    /// <remarks>Created on first use, never in SetDefaults, so the cloned global template cant share one.</remarks>
    private Stab? stab;

    public override bool InstancePerEntity => true;

    public static bool Reworks(int projectileType) => Profiles.ContainsKey(projectileType);

    public override bool AppliesToEntity(Projectile entity, bool lateInstantiation)
        => lateInstantiation && entity.aiStyle == ProjAIStyleID.ShortSword && Reworks(entity.type);

    public override void Load() => On_Projectile.AI_161_RapierStabs += UpdateStab;

    public override void Unload() => On_Projectile.AI_161_RapierStabs -= UpdateStab;

    private static void UpdateStab(On_Projectile.orig_AI_161_RapierStabs orig, Projectile self)
    {
        if (self.TryGetGlobalProjectile(out ShortswordRework rework))
            rework.Sword(self).Update();
        else
            orig(self);
    }

    public override void SetDefaults(Projectile entity) => HeldSword.ApplyDefaults(entity, Profiles[entity.type]);

    public override void OnSpawn(Projectile projectile, IEntitySource source) => Sword(projectile).OnSpawn();

    // velocity carries the aim
    public override bool ShouldUpdatePosition(Projectile projectile) => false;

    public override bool? CanDamage(Projectile projectile) => Sword(projectile).CanDamage();

    public override bool? Colliding(Projectile projectile, Rectangle projHitbox, Rectangle targetHitbox)
        => Sword(projectile).Colliding(targetHitbox);

    public override void ModifyHitNPC(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers)
        => Sword(projectile).ModifyHitNPC(target, ref modifiers);

    public override bool PreDraw(Projectile projectile, ref Color lightColor)
    {
        Sword(projectile).Draw(lightColor);

        return false;
    }

    private Stab Sword(Projectile projectile) => stab ??= new Stab(projectile);

    /// <summary>Geometry measured off the vanilla stab sprites, in pixels at scale 1.</summary>
    private static Dictionary<int, SwordProfile> BuildProfiles()
    {
        // 32x32 up-right sprites: grip at (4.5, 27), tip at (30, 1.5), crossguard a quarter of the way up.
        SwordProfile metal = SwordProfile.Shortsword with
        {
            BladeLength = 36f,
            HiltFraction = 0.25f,
            Origin = new Vector2(0.14f, 0.84f),
        };

        var profiles = new Dictionary<int, SwordProfile>
        {
            // 12x28 sprite with no guard: grip at row 24.
            [ProjectileID.RulerStab] = SwordProfile.Shortsword with
            {
                BladeLength = 23f,
                HiltFraction = 0.15f,
                Origin = new Vector2(0.5f, 0.85f),
                SpriteAngle = MathHelper.PiOver2,
            },
            [ProjectileID.CopperShortswordStab] = SwordProfile.Shortsword with
            {
                BladeLength = 36f,
                HiltFraction = 0.25f,
                Origin = new Vector2(0.14f, 0.84f),
                RecoverTicks = 8,
            },
        };

        int[] metals =
        [
            ProjectileID.TinShortswordStab,
            ProjectileID.IronShortswordStab, ProjectileID.LeadShortswordStab,
            ProjectileID.SilverShortswordStab, ProjectileID.TungstenShortswordStab,
            ProjectileID.GoldShortswordStab, ProjectileID.PlatinumShortswordStab,
        ];

        foreach (int type in metals)
            profiles[type] = metal;

        return profiles;
    }

    private sealed class Stab(Projectile projectile) : HeldSword(projectile)
    {
        protected override SwordProfile Profile { get; } = Profiles[projectile.type];

        protected override Texture2D Texture
        {
            get
            {
                Main.instance.LoadProjectile(Projectile.type);

                return TextureAssets.Projectile[Projectile.type].Value;
            }
        }
    }
}

/// <summary>Clears the use sound of items that shoot a reworked stab; the combo plays <see cref="SwordProfile.SwingSound" /> per sweep.</summary>
public sealed class ShortswordReworkItem : GlobalItem
{
    public override bool AppliesToEntity(Item entity, bool lateInstantiation)
        => lateInstantiation && ShortswordRework.Reworks(entity.shoot);

    public override void SetDefaults(Item entity) { 
        entity.UseSound = null;

        if (entity.type == ItemID.CopperShortsword)
            entity.useTime = entity.useAnimation = 16;   
    }
}
