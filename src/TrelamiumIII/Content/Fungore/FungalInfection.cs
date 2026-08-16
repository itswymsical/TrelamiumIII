using Terraria.Audio;
using Terraria.DataStructures;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Graphics.Cinematics.Camera;
using TrelamiumIII.Core.Graphics.Metaballs.Layers;

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
        BuffID.Sets.LongerExpertDebuff[Type] = true;
    }

    public override void Update(Player player, ref int buffIndex)
    {
        player.GetModPlayer<FungalInfectionPlayer>().Infect(FungalInfectionPlayer.AMBIENT);
        SporeLayer.Cling(player, 0.5f);
    }
}

/// <summary>
///     The heavier strain, thrown by <see cref="FungoreSpore" />.
/// </summary>
/// <remarks>
///     A separate buff rather than a stack count on the ambient one, because the two are applied by
///     completely different things and want completely different durations: the ambient strain hangs
///     around for as long as you stand in a cloud, while this one is a short, sharp four seconds that
///     a slam re-applies every time it catches you.
/// </remarks>
public sealed class FungalInfectionSpore : ModBuff
{
    public override string Texture => AssetReferences.Content.Fungore.FungalInfection.KEY;

    public override void SetStaticDefaults()
    {
        Main.debuff[Type] = true;
        Main.pvpBuff[Type] = false;
        Main.buffNoSave[Type] = true;

        // Deliberately not lengthened in expert. This one is already re-applied every few seconds
        // by an attack that is hard to avoid; doubling it as well would make it permanent.
        BuffID.Sets.LongerExpertDebuff[Type] = false;
    }

    public override void Update(Player player, ref int buffIndex)
    {
        player.GetModPlayer<FungalInfectionPlayer>().Infect(FungalInfectionPlayer.SPORE_BORNE);
        SporeLayer.Cling(player, 0.85f);
    }
}

public class FungalInfectionPlayer : ModPlayer
{
    /// <summary>Walked into a cloud. One tick of damage.</summary>
    public const int AMBIENT = 1;

    /// <summary>Caught by a slam. Two.</summary>
    public const int SPORE_BORNE = 2;

    /// <summary>
    ///     Added to <see cref="Player.lifeRegen" /> per point of severity.
    /// </summary>
    /// <remarks>
    ///     That field counts in half a health a second, so one point of severity is exactly one tick
    ///     of damage as the debuff readout counts them.
    /// </remarks>
    private const int REGEN_PER_SEVERITY = 2;

    /// <summary>Worst strain in the player this frame. Rebuilt from scratch every tick.</summary>
    public int Severity;

    public bool Infected => Severity > 0;

    /// <summary>
    ///     Raises the infection to <paramref name="severity" />.
    /// </summary>
    /// <remarks>
    ///     The worse strain wins rather than the two summing: being in a cloud while a slam lands is
    ///     already two sources of damage, and stacking the burn on top of that would make standing
    ///     anywhere near the fight a slow death independent of what you get hit by.
    /// </remarks>
    public void Infect(int severity) => Severity = Math.Max(Severity, severity);

    public override void ResetEffects() => Severity = 0;

    public override void UpdateDead() => Severity = 0;

    public override void UpdateBadLifeRegen()
    {
        if (Infected)
            Player.lifeRegen -= REGEN_PER_SEVERITY * Severity;
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

    public const int DAMAGE_INTERVAL = 30;


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

    /// <summary>
    ///     Player credited for infection damage, so kills still drop loot and count for the thrower.
    /// </summary>
    private int infectingPlayer = -1;

    /// <summary>Damage per tick at a given saturation.</summary>
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
            SporeLayer.Cling(npc, (float)Stacks / MAX_STACKS);
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

    /// <summary>
    ///     The host ruptures: a cloud, a shove to the camera, and a ring of spore blasts that carry
    ///     the infection outward.
    /// </summary>
    private void Bloom(NPC npc)
    {
        int owner = infectingPlayer;
        Vector2 center = npc.Center;

        Clear(npc);

        // Set before the blasts spawn, so the host is already immune when its own ring sweeps
        // back over it and cannot re-saturate itself into a loop.
        bloomCooldown = BLOOM_COOLDOWN;

        SoundEngine.PlaySound(SoundID.Item70 with { Volume = 1.05f, Pitch = -0.35f }, center);
        SoundEngine.PlaySound(Assets.SFX.Sporeclot_Boom.Asset with { Volume = 0.95f, Pitch = -0.35f }, center);

        if (!Main.dedServ)
        {
            SporeLayer.Burst(center, 160f);

            if (Main.LocalPlayer.WithinRange(center, SHAKE_RANGE))
                CameraSystem.shake = Math.Max(CameraSystem.shake, BLOOM_SHAKE);
        }

        SporeBlastProj.SpawnRing(npc, center, owner);
    }

    /// <summary>
    ///     Applies infection damage, crediting <paramref name="owner" /> so kills still drop loot.
    /// </summary>
    /// <remarks>
    ///     Struck through a prebuilt <see cref="NPC.HitInfo" /> rather than
    ///     <c>SimpleStrikeNPC</c>, because HitInfo.Damage is the final figure -- defense does not
    ///     get to shave a 1-damage tick down to nothing on anything armoured.
    ///     <para>
    ///         Server-authoritative: letting clients strike here would apply the tick once per
    ///         player in the world.
    ///     </para>
    /// </remarks>
    private void TickDamage(NPC npc)
    {
        int damage = TickDamageFor(Stacks);

        if (damage <= 0 || Main.netMode == NetmodeID.MultiplayerClient)
            return;

        if (npc.dontTakeDamage || npc.immortal || npc.life <= 0)
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

    /// <summary>
    ///     How far out along its own heading each arm starts. Spawning them all on the exact
    ///     centre would mean anything adjacent to the host overlaps all
    ///     <see cref="BLAST_COUNT" /> arms on the first frame and eats the entire blast at once;
    ///     starting them on a ring spreads the arms far enough apart that proximity decides how
    ///     many catch you.
    /// </summary>
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

        SporeLayer.Trail(Projectile.Center, Projectile.velocity * 0.18f, MathHelper.Lerp(8f, 20f, taper));
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
