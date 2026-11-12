using Terraria.Audio;
using Terraria.DataStructures;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Graphics.Cinematics.Camera;
using TrelamiumIII.Core.Graphics.Fields.Layers;
using TrelamiumIII.Core.Graphics.ScreenEffects;

namespace TrelamiumIII.Content.Fungore;

public sealed class FungalInfection : ModBuff
{
    public override void SetStaticDefaults()
    {
        Main.debuff[Type] = true;
        Main.pvpBuff[Type] = false;
        Main.buffNoSave[Type] = true;

        BuffID.Sets.LongerExpertDebuff[Type] = false;
    }
}

public sealed class FungalInfectionHostile : ModBuff
{
    public override string Texture => AssetReferences.Content.Fungore.FungalInfection.KEY;

    public override void SetStaticDefaults()
    {
        Main.debuff[Type] = true;
        Main.pvpBuff[Type] = false;
        Main.buffNoSave[Type] = true;

        // Off: InfectionTicks scales duration by difficulty, where vanilla's flag would blindly double it.
        BuffID.Sets.LongerExpertDebuff[Type] = false;
    }

    public override void Update(Player player, ref int buffIndex)
    {
        player.GetModPlayer<FungalInfectionPlayer>().Infect(FungalInfectionPlayer.AMBIENT);
        SporeField.Cling(player, 0.5f);
    }
}

/// <summary>The heavier strain, thrown by <see cref="FungoreSpore" />.</summary>
public sealed class FungalInfectionSpore : ModBuff
{
    public override string Texture => AssetReferences.Content.Fungore.FungalInfection.KEY;

    public override void SetStaticDefaults()
    {
        Main.debuff[Type] = true;
        Main.pvpBuff[Type] = false;
        Main.buffNoSave[Type] = true;

        // Not lengthened in expert: it's reapplied every few seconds, so doubling would make it permanent.
        BuffID.Sets.LongerExpertDebuff[Type] = false;
    }

    public override void Update(Player player, ref int buffIndex)
    {
        player.GetModPlayer<FungalInfectionPlayer>().Infect(FungalInfectionPlayer.SPORE_BORNE);
        SporeField.Cling(player, 0.85f);
    }
}

public class FungalInfectionPlayer : ModPlayer
{
    /// <summary>Walked into a cloud. One tick of damage.</summary>
    public const int AMBIENT = 1;

    /// <summary>Caught by a slam. Two.</summary>
    public const int SPORE_BORNE = 2;

    /// <summary>Saturation ceiling.</summary>
    public const int MAX_STACKS = 10;

    /// <summary>Peak warp at a full stack.</summary>
    private const float WOBBLE_STRENGTH = 0.4f;

    /// <summary>Warp frequency at zero and full stacks. Climbing both is what sells it.</summary>
    private const float WOBBLE_SCALE_MIN = 2.2f;

    /// <summary>Capped low for the same reason: fine noise is what actually destroys legibility.</summary>
    private const float WOBBLE_SCALE_MAX = 4.5f;

    public static int InfectionTicks => Main.masterMode ? 20 * 60
        : Main.expertMode ? 13 * 60
        : 9 * 60;

    /// <summary>Applies a strain and a stack of saturation in one go.</summary>
    public static void Inflict(Player player, int buffType, int ticks, int stacks = 1)
    {
        if (player is not { active: true } || player.dead)
            return;

        player.AddBuff(buffType, ticks);

        player.GetModPlayer<FungalInfectionPlayer>().AddStacks(stacks);
    }

    /// <summary>Added to <see cref="Player.lifeRegen" /> per point of severity.</summary>
    private const int REGEN_PER_SEVERITY = 2;

    /// <summary>Worst strain in the player this frame. Rebuilt from scratch every tick.</summary>
    public int Severity;

    /// <summary>Saturation, 0 to <see cref="MAX_STACKS" />.</summary>
    public int Stacks;

    public bool Infected => Severity > 0;

    public void AddStacks(int count) => Stacks = Math.Clamp(Stacks + count, 0, MAX_STACKS);

    /// <summary>Raises the infection to <paramref name="severity" />; never lowers it.</summary>
    public void Infect(int severity) => Severity = Math.Max(Severity, severity);

    public override void ResetEffects() => Severity = 0;

    public override void UpdateDead()
    {
        Severity = 0;
        Stacks = 0;
    }

    public override void UpdateBadLifeRegen()
    {
        if (Infected)
            Player.lifeRegen -= REGEN_PER_SEVERITY * Severity;
    }

    /// <summary>Pushes the saturation into the warp.</summary>
    public override void PostUpdate()
    {
        if (!Infected)
            Stacks = 0;

        if (Player.whoAmI != Main.myPlayer || Main.dedServ)
            return;

        float saturation = Stacks / (float)MAX_STACKS;

        // Pushed every frame, zero included. The system ramps toward whatever it was last told, so
        // a stack landing or the debuff falling off is a slide rather than a step.
        ScreenWobbleSystem.Set(
            saturation * WOBBLE_STRENGTH,
            MathHelper.Lerp(WOBBLE_SCALE_MIN, WOBBLE_SCALE_MAX, saturation)
        );
    }
}

public sealed class FungalInfectionNPC : GlobalNPC
{
    public const int MAX_STACKS = 5;

    public const int INFECTION_DURATION = 8 * 60;

    public const int MINOR_DAMAGE_STACKS = 2;

    public const int MAJOR_DAMAGE_STACKS = 4;

    public const int MINOR_TICK_DAMAGE = 1;

    public const int MAJOR_TICK_DAMAGE = 2;

    public const int DAMAGE_INTERVAL = 18;


    private const int BLOOM_COOLDOWN = 90;

    private const int CLING_INTERVAL = 5;

    private const int BLOOM_SHAKE = 12;

    /// <summary>Beyond this the bloom is off-screen and has no business shaking the camera.</summary>
    private const float SHAKE_RANGE = 1400f;

    public override bool InstancePerEntity => true;

    public int Stacks { get; private set; }

    public bool IsInfected => Stacks > 0;

    private int infectionTimer;
    private int damageTimer;
    private int bloomCooldown;
    private int pendingBloom;

    /// <summary>Player credited for infection damage, so kills still drop loot and count for the thrower.</summary>
    private int infectingPlayer = -1;

    public static int TickDamageFor(int stacks) => stacks switch
    {
        >= MAJOR_DAMAGE_STACKS => MAJOR_TICK_DAMAGE,
        >= MINOR_DAMAGE_STACKS => MINOR_TICK_DAMAGE,
        _ => 0,
    };

    public static bool AddBuffStack(NPC npc, int count, int owner)
    {
        if (npc is not { active: true } || npc.friendly || !npc.CanBeChasedBy())
            return false;

        int buffType = ModContent.BuffType<FungalInfection>();

        if (npc.buffImmune[buffType])
            return false;

        var infection = npc.GetGlobalNPC<FungalInfectionNPC>();

        // Spores are spent right after a bloom; nothing takes hold until the host recovers.
        if (infection.bloomCooldown > 0)
            return false;

        infection.Stacks = Math.Min(infection.Stacks + count, MAX_STACKS);
        infection.infectionTimer = INFECTION_DURATION;

        if (owner >= 0 && owner < Main.maxPlayers)
            infection.infectingPlayer = owner;

        npc.AddBuff(buffType, INFECTION_DURATION);

        // Staggered so a blast that saturates several hosts at once pops them in sequence.
        if (infection.Stacks >= MAX_STACKS && infection.pendingBloom <= 0)
            infection.pendingBloom = Main.rand.Next(3, 8);

        return true;
    }

    public override void PostAI(NPC npc)
    {
        if (bloomCooldown > 0)
            bloomCooldown--;

        if (!IsInfected)
            return;

        if (--infectionTimer <= 0)
        {
            Clear(npc);
            return;
        }

        if (pendingBloom > 0 && --pendingBloom == 0)
        {
            Bloom(npc);
            return;
        }

        if (++damageTimer >= DAMAGE_INTERVAL)
        {
            damageTimer = 0;
            TickDamage(npc);
        }

        if (!Main.dedServ && infectionTimer % CLING_INTERVAL == 0)
            SporeField.Cling(npc, (float)Stacks / MAX_STACKS);
    }

    private void Clear(NPC npc)
    {
        Stacks = 0;
        infectionTimer = 0;
        damageTimer = 0;
        pendingBloom = 0;
        infectingPlayer = -1;

        // NPCs have no RemoveBuff(type); the index has to be looked up first.
        int index = npc.FindBuffIndex(ModContent.BuffType<FungalInfection>());

        if (index >= 0)
            npc.DelBuff(index);
    }

    /// <summary>The host ruptures: a cloud, camera shake, and a ring of spore blasts that spread the infection.</summary>
    private void Bloom(NPC npc)
    {
        int owner = infectingPlayer;
        Vector2 center = npc.Center;

        Clear(npc);

        // Set before the blasts spawn, so the host is already immune when its own ring sweeps
        // back over it and cannot re-saturate itself into a loop.
        bloomCooldown = BLOOM_COOLDOWN;

        SoundEngine.PlaySound(SoundID.Item70 with { Volume = 1.05f, Pitch = -0.35f }, center);
        SoundEngine.PlaySound(Assets.SFX.Items.Sporeclot_Boom.Asset with { Volume = 0.95f, Pitch = -0.35f }, center);

        if (!Main.dedServ)
        {
            SporeField.Burst(center, 160f);

            if (Main.LocalPlayer.WithinRange(center, SHAKE_RANGE))
                CameraSystem.shake = Math.Max(CameraSystem.shake, BLOOM_SHAKE);
        }

        SporeBlastProj.SpawnRing(npc, center, owner);
    }

    private void TickDamage(NPC npc)
    {
        int damage = TickDamageFor(Stacks);

        if (damage <= 0 || Main.netMode == NetmodeID.MultiplayerClient)
            return;

        if (npc.dontTakeDamage || npc.life <= 0)
            return;

        if (infectingPlayer >= 0 && infectingPlayer < Main.maxPlayers && Main.player[infectingPlayer].active)
            npc.playerInteraction[infectingPlayer] = true;

        npc.StrikeNPC(
            new NPC.HitInfo
            {
                Damage = damage,
                Knockback = 0f,
                HitDirection = 0,
                Crit = false,
                DamageType = DamageClass.Default,
            },
            fromNet: false,
            noPlayerInteraction: true
        );
    }
}

public sealed class SporeBlastProj : ModProjectile
{
    public const int BLAST_COUNT = 14;

    public const int BLAST_DAMAGE = 6;

    /// <summary>How far out each arm starts, so proximity decides how many arms catch an adjacent target.</summary>
    private const float SPAWN_RADIUS = 20f;

    private const float MIN_SPEED = 7f;
    private const float MAX_SPEED = 11f;

    private const int MIN_LIFETIME = 22;
    private const int MAX_LIFETIME = 30;

    private const int SPORE_INTERVAL = 4;

    /// <summary>Index of the NPC this blast came out of, which it cannot hit.</summary>
    private int SourceNPC => (int)Projectile.ai[0];

    public override string Texture => AssetReferences.Content.Fungore.SporeclotSac.KEY;

    public static void SpawnRing(NPC source, Vector2 center, int owner)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        var entitySource = new EntitySource_Parent(source);
        float baseAngle = Main.rand.NextFloat(MathHelper.TwoPi);

        for (int i = 0; i < BLAST_COUNT; i++)
        {
            // Evenly spaced with jitter: a clean ring reads as a mechanism, a ragged one as a burst.
            float angle = baseAngle + MathHelper.TwoPi * i / BLAST_COUNT + Main.rand.NextFloat(-0.12f, 0.12f);
            Vector2 heading = angle.ToRotationVector2();
            Vector2 velocity = heading * Main.rand.NextFloat(MIN_SPEED, MAX_SPEED);

            Projectile.NewProjectile(
                entitySource,
                center + heading * SPAWN_RADIUS,
                velocity,
                ModContent.ProjectileType<SporeBlastProj>(),
                BLAST_DAMAGE,
                0f,
                owner >= 0 && owner < Main.maxPlayers ? owner : Main.myPlayer,
                ai0: source.whoAmI
            );
        }
    }

    public override void SetDefaults()
    {
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.width = Projectile.height = 22;

        Projectile.friendly = true;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;

        // Each arm gets one bite of a given target; several arms overlapping is what stacks it.
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;

        Projectile.penetrate = -1;
        Projectile.aiStyle = -1;
        Projectile.timeLeft = MAX_LIFETIME;
    }

    public override void OnSpawn(IEntitySource source)
    {
        Projectile.timeLeft = Main.rand.Next(MIN_LIFETIME, MAX_LIFETIME + 1);
    }

    public override void AI()
    {
        Projectile.velocity *= 0.93f;

        if (Main.dedServ || Projectile.timeLeft % SPORE_INTERVAL != 0)
            return;

        float taper = Projectile.timeLeft / (float)MAX_LIFETIME;

        SporeField.Trail(Projectile.Center, Projectile.velocity * 0.18f, MathHelper.Lerp(8f, 20f, taper));
    }

    public override bool? CanHitNPC(NPC target)
    {
        if (target.whoAmI == SourceNPC)
            return false;

        return null;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        FungalInfectionNPC.AddBuffStack(target, 1, Projectile.owner);
    }

    public override bool PreDraw(ref Color lightColor) => false;
}
