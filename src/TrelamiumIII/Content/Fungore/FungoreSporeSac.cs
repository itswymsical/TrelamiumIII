using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Graphics.Fields.Layers;
using TrelamiumIII.Core.Graphics.Particles;
using TrelamiumIII.Utilities;
using static TrelamiumIII.Utilities.EaseFunction;

namespace TrelamiumIII.Content.Fungore;

/// <summary>
///     A sac pushed up by a slam that swells, then releases gas. An NPC so the player can kill it early, which releases
///     nothing. The gas deals no damage: contact is an overlap test that adds infection stacks without i-frames.
/// </summary>
public sealed class FungoreSporeSac : ModNPC
{
    /// <summary>Frames from a sprout to full size, and the window the player has to pop it.</summary>
    private const int GROW_TICKS = 165;

    /// <summary>Frames the cloud hangs around after it opens.</summary>
    private const int LINGER_TICKS = 330;

    /// <summary>Radius of the gas once it is out, in world pixels.</summary>
    private const float GAS_RADIUS = 96f;

    /// <summary>Frames between stacks for a player standing in it.</summary>
    private const int INFECT_INTERVAL = 30;

    private const float SPAWN_SCALE = 0.25f;

    private const float FULL_SCALE = 1.1f;

    /// <summary>Pulses per second, empty and full: the beat speeds up as a countdown to release.</summary>
    private const float PULSE_RATE_MIN = 1.6f;

    private const float PULSE_RATE_MAX = 8.5f;

    /// <summary>How far the pulse swells, as a fraction of the current size.</summary>
    private const float PULSE_DEPTH = 0.14f;

    private const int LIFE_NORMAL = 25;
    private const int LIFE_EXPERT = 40;
    private const int LIFE_MASTER = 65;

    /// <summary>How far it may be planted from where it was asked for, searching for a floor.</summary>
    private const int GROUND_SEARCH = 14;

    public override string Texture => AssetReferences.Content.Fungore.SporeclotSac.KEY;

    /// <summary>Frames elapsed. Rides <see cref="NPC.ai" /> so it survives a resync.</summary>
    private ref float Age => ref NPC.ai[0];

    private bool Open => Age >= GROW_TICKS;

    /// <summary>0 to 1 across the growth.</summary>
    private float Growth => Math.Clamp(Age / GROW_TICKS, 0f, 1f);

    /// <summary>1 while the cloud is thick, easing to 0 as it thins out.</summary>
    private float Presence => Open
        ? 1f - Math.Clamp((Age - GROW_TICKS) / LINGER_TICKS, 0f, 1f)
        : 0f;

    private static int GasSize => (int)(GAS_RADIUS * 2f);

    /// <summary>Cooldown on stacking. One value, because only the local player is ever infected here.</summary>
    private int infectCooldown;

    /// <summary>Accumulated pulse angle, integrated because the rate changes and sin(rate * time) would jump.</summary>
    private float pulsePhase;

    /// <summary>Plants one on the first floor under <paramref name="near" />, without modifying the world.</summary>
    /// <returns>Whether a floor was found.</returns>
    public static bool Plant(Vector2 near)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return false;

        if (!TryFindFloor(near, out Vector2 floor))
            return false;

        int type = ModContent.NPCType<FungoreSporeSac>();

        // Bottom-anchored: NewNPC takes the centre, and the sac sits on the tile it was planted on.
        int index = NPC.NewNPC(new EntitySource_SpawnNPC(), (int)floor.X, (int)floor.Y, type);

        if (index < 0 || index >= Main.maxNPCs)
            return false;

        if (Main.netMode == NetmodeID.Server)
            NetMessage.SendData(MessageID.SyncNPC, number: index);

        return true;
    }

    public override void SetStaticDefaults()
    {
        Main.npcFrameCount[Type] = 1;

        NPCID.Sets.CantTakeLunchMoney[Type] = true;

        NPCID.Sets.NPCBestiaryDrawModifiers modifiers = new() { Hide = true };

        NPCID.Sets.NPCBestiaryDrawOffset[Type] = modifiers;
    }

    public override void SetDefaults()
    {
        NPC.width = 38;
        NPC.height = 36;

        NPC.lifeMax = LIFE_NORMAL;
        NPC.defense = 0;
        NPC.damage = 0;

        NPC.knockBackResist = 0f;
        NPC.aiStyle = -1;

        NPC.noGravity = true;
        NPC.noTileCollide = true;

        NPC.friendly = false;

        // A boss-made hazard: doesn't count toward spawns or drop money.
        NPC.dontCountMe = true;
        NPC.npcSlots = 0f;
        NPC.value = 0f;

        NPC.HitSound = SoundID.NPCHit1;
        NPC.DeathSound = SoundID.NPCDeath1;

        NPC.timeLeft = GROW_TICKS + LINGER_TICKS + 60;
    }

    /// <summary>Fixed life per difficulty, assigned after vanilla scaling: shots-to-kill is the mechanic's whole balance.</summary>
    public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
    {
        NPC.lifeMax = Main.masterMode ? LIFE_MASTER : LIFE_EXPERT;
        NPC.life = NPC.lifeMax;
    }

    public override bool CheckActive() => false;

    public override void AI()
    {
        NPC.velocity = Vector2.Zero;

        if (!Open)
        {
            Grow();

            Age++;

            return;
        }

        if (Age == GROW_TICKS)
            Release();
        else if (NPC.width < GasSize)
        {
            // A client that received this after it had already opened never saw the tick the hitbox
            // grew on, and would otherwise carry a sprout-sized cloud for the rest of its life.
            Widen();
        }

        Age++;

        Spread();
        Infect();

        if (Age >= GROW_TICKS + LINGER_TICKS)
            NPC.active = false;
    }

    /// <summary>Swelling. Quiet, but it leaks the whole time so it cannot be mistaken for scenery.</summary>
    private void Grow()
    {
        float growth = Growth;

        pulsePhase += MathHelper.Lerp(PULSE_RATE_MIN, PULSE_RATE_MAX, growth) * MathHelper.TwoPi / 60f;

        if (Main.dedServ)
            return;

        Lighting.AddLight(NPC.Center, 0.3f * growth, 0.16f * growth, 0.04f * growth);

        // Thickens as it fills out, so the last second before it opens is visibly the last second.
        if (Age % Math.Max((int)MathHelper.Lerp(14f, 4f, growth), 1) != 0)
            return;

        SporeField.Trail(
            NPC.Center - new Vector2(0f, 6f * growth),
            new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(-0.9f, -0.2f)),
            Main.rand.NextFloat(4f, 7f) * (0.5f + growth)
        );
    }

    /// <summary>Grows the hitbox out of a sprout into a cloud, so the reach matches what is on screen.</summary>
    private void Widen()
    {
        Vector2 centre = NPC.Center;

        NPC.width = NPC.height = GasSize;
        NPC.Center = centre;
    }

    /// <summary>It lets go. From here it is a cloud, and there is nothing left to shoot.</summary>
    private void Release()
    {
        Widen();

        Vector2 centre = NPC.Center;

        SoundEngine.PlaySound(Assets.SFX.Items.Sporeclot_Boom.Asset with { Volume = 0.75f, Pitch = 0.2f }, centre);

        // Untargetable once it's gas, so homing weapons and minions don't waste the fight on it.
        NPC.dontTakeDamage = true;
        NPC.chaseable = false;

        if (Main.dedServ)
            return;

        SporeField.Burst(centre, GAS_RADIUS);

        Debris(centre, count: 7, force: 4.5f);
    }

    private void Spread()
    {
        if (Main.dedServ)
            return;

        float presence = Presence;

        Lighting.AddLight(NPC.Center, 0.22f * presence, 0.12f * presence, 0.03f * presence);

        if (Age % 4 != 0)
            return;

        // Topped up rather than spawned once, or the visible cloud would thin out before the hitbox ends.
        SporeField.Trail(
            NPC.Center + Main.rand.NextVector2Circular(GAS_RADIUS * 0.75f, GAS_RADIUS * 0.5f),
            new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(-0.6f, 0.1f)),
            Main.rand.NextFloat(10f, 20f) * presence
        );
    }

    /// <summary>Stacks infection on the local player while in the cloud. Runs per client; nothing needs server arbitration.</summary>
    private void Infect()
    {
        if (Main.dedServ)
            return;

        Player player = Main.LocalPlayer;

        if (player is not { active: true } || player.dead)
            return;

        if (infectCooldown > 0)
        {
            infectCooldown--;

            return;
        }

        if (!player.Hitbox.Intersects(NPC.Hitbox))
            return;

        infectCooldown = INFECT_INTERVAL;

        FungalInfectionPlayer.Inflict(player, ModContent.BuffType<FungalInfectionHostile>(), FungalInfectionPlayer.InfectionTicks);
    }

    public override void HitEffect(NPC.HitInfo hit)
    {
        if (Main.dedServ)
            return;

        // The cloud phase cannot be hurt, so anything arriving here is a hit on the sac itself.
        if (NPC.life > 0)
        {
            SporeField.Trail(NPC.Center, new Vector2(hit.HitDirection * 1.2f, -0.8f), Main.rand.NextFloat(4f, 8f));

            return;
        }

        SoundEngine.PlaySound(Assets.SFX.Items.Sporeclot_TileImpact.Asset with { Volume = 0.7f, Pitch = 0.5f }, NPC.Center);

        Debris(NPC.Center, count: 5, force: 3.2f, scale: Growth);

        for (int i = 0; i < 5; i++)
            SporeField.Trail(NPC.Center + Main.rand.NextVector2Circular(10f, 10f), Main.rand.NextVector2Circular(1.6f, 1.6f), Main.rand.NextFloat(5f, 9f) * Growth);
    }

    /// <summary>Husk and shards, thrown outward and left to settle on the ground.</summary>
    private static void Debris(Vector2 centre, int count, float force, float scale = 1f)
    {
        for (int i = 0; i < count; i++)
        {
            // Biased upward so the pieces arc rather than skidding along the floor they grew on.
            Vector2 velocity = Main.rand.NextVector2Circular(force, force);
            velocity.Y -= Main.rand.NextFloat(1f, 3f);

            ParticleSystem.Spawn<SporeclotParticle>(
                centre + Main.rand.NextVector2Circular(8f, 8f),
                velocity,
                timeLeft: Main.rand.Next(180, 300),
                scale: Main.rand.NextFloat(0.7f, 1.05f) * Math.Max(scale, 0.35f),
                rotationVelocity: Main.rand.NextFloat(-0.3f, 0.3f)
            );
        }
    }

    public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        if (Open)
            return false;

        Texture2D texture = TextureAssets.Npc[Type].Value;

        float growth = Growth;
        float scale = MathHelper.Lerp(SPAWN_SCALE, FULL_SCALE, BloomEase.Ease(growth));

        // Roughly volume-preserving squash and stretch, so it reads as pressurised rather than just scaled.
        float beat = MathF.Sin(pulsePhase) * PULSE_DEPTH * growth;

        var pulse = new Vector2(1f + beat, 1f - beat * 0.65f) * scale;

        // Base-anchored, so it stays on its tile however the pulse stretches it.
        var origin = new Vector2(texture.Width * 0.5f, texture.Height);

        spriteBatch.Draw(
            texture,
            new Vector2(NPC.Center.X, NPC.Bottom.Y) - screenPos,
            null,
            NPC.GetAlpha(drawColor),
            0f,
            origin,
            pulse,
            SpriteEffects.None,
            0f
        );

        return false;
    }

    private static readonly EaseFunction BloomEase = BuildBloomEase();

    private static EaseFunction BuildBloomEase()
    {
        var ease = new EaseBuilder();

        // Barely moves for the first third, then swells past full size and settles back.
        ease.AddPoint(0.35f, 0.12f, EaseQuadIn);
        ease.AddPoint(0.85f, 1.12f, EaseCubicOut);
        ease.AddPoint(1f, 1f, EaseQuadInOut);

        return ease;
    }

    /// <summary>First floor at or below <paramref name="from" />, as a world position to stand on.</summary>
    private static bool TryFindFloor(Vector2 from, out Vector2 floor)
    {
        int tileX = (int)(from.X / 16f);
        int tileY = (int)(from.Y / 16f);

        for (int i = 0; i <= GROUND_SEARCH; i++)
        {
            int y = tileY + i;

            if (!WorldGen.InWorld(tileX, y, 2))
                break;

            if (!WorldGen.SolidTile(tileX, y))
                continue;

            floor = new Vector2(tileX * 16f + 8f, y * 16f);

            return true;
        }

        floor = from;

        return false;
    }
}
