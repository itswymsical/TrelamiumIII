using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Graphics.Metaballs.Layers;
using TrelamiumIII.Core.Graphics.Particles;
using TrelamiumIII.Utilities;
using static TrelamiumIII.Utilities.EaseFunction;

namespace TrelamiumIII.Content.Fungore;

/// <summary>
///     A sac pushed up out of the ground by a slam, which swells and then lets go.
/// </summary>
/// <remarks>
///     <para>
///         <b>An NPC rather than a projectile, so it can be killed.</b> That is the entire point of
///         the design: the sac is area denial with a long fuse, and a fuse the player can cut is a
///         far better mechanic than one they can only walk around. It has no defence and very little
///         life -- clearing one should cost a moment of attention, not a rotation.
///     </para>
///     <para>
///         <b>Destroying it early releases nothing.</b> Letting go is what the growth was building
///         toward, so a sac that never finished has nothing to give; the reward for the shot is that
///         the gas simply never happens.
///     </para>
///     <para>
///         <b>The gas deals no damage.</b> Contact is an overlap test with its own cooldown rather
///         than NPC contact damage. Going through that path would have meant inheriting immunity
///         frames, and immunity frames are exactly wrong for a saturating debuff -- standing in it
///         has to keep costing something, and the cost is the stack count.
///     </para>
/// </remarks>
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

    /// <summary>
    ///     Pulses per second, empty and full.
    /// </summary>
    /// <remarks>
    ///     The whole tell is in the rate. A sac that swells and then sits there gives the player one
    ///     piece of information -- where -- and they have to guess at when; one that beats faster the
    ///     closer it gets to going off is a countdown they can read from across the arena without
    ///     ever looking directly at it.
    /// </remarks>
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

    /// <summary>
    ///     Accumulated pulse angle.
    /// </summary>
    /// <remarks>
    ///     Carried forward rather than computed from the age, because the rate changes as it fills:
    ///     feeding a moving frequency into a sine of absolute time makes the phase jump every frame,
    ///     and the sac stutters instead of beating.
    /// </remarks>
    private float pulsePhase;

    /// <summary>
    ///     Plants one on the first floor under <paramref name="near" />.
    /// </summary>
    /// <remarks>
    ///     "Marks a tile" only in the sense that it picks one -- the world is never written to. A
    ///     slam that changed terrain would leave the arena a different shape after every fight.
    /// </remarks>
    /// <returns>Whether a floor was found to plant it on.</returns>
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

        // Planted. Nothing moves it, and it should not be pushed out of the tile it grew on.
        NPC.noGravity = true;
        NPC.noTileCollide = true;

        NPC.friendly = false;

        // Neither a threat to the spawn budget nor worth money -- it is a hazard the boss made, not
        // an enemy that wandered in.
        NPC.dontCountMe = true;
        NPC.npcSlots = 0f;
        NPC.value = 0f;

        NPC.HitSound = SoundID.NPCHit1;
        NPC.DeathSound = SoundID.NPCDeath1;

        NPC.timeLeft = GROW_TICKS + LINGER_TICKS + 60;
    }

    /// <summary>
    ///     Fixed figures per difficulty rather than a multiplier.
    /// </summary>
    /// <remarks>
    ///     This hook runs after vanilla has already adjusted life for the difficulty, so assigning
    ///     here is the last word. A sac is a timer the player is racing, and how many shots it takes
    ///     to beat that timer is the whole balance of the mechanic -- worth stating outright rather
    ///     than leaving to whatever the difficulty multiplier happens to be.
    /// </remarks>
    public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
    {
        NPC.lifeMax = Main.masterMode ? LIFE_MASTER : LIFE_EXPERT;
        NPC.life = NPC.lifeMax;
    }

    /// <summary>Never despawned by distance -- it takes itself off on its own clock.</summary>
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

        SporeLayer.Trail(
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

        SoundEngine.PlaySound(Assets.SFX.Sporeclot_Boom.Asset with { Volume = 0.75f, Pitch = 0.2f }, centre);

        // The sac is gone; what is left is gas. Untargetable so homing weapons and minions do not
        // spend the rest of the fight shooting at a cloud they cannot remove.
        NPC.dontTakeDamage = true;
        NPC.chaseable = false;

        if (Main.dedServ)
            return;

        SporeLayer.Burst(centre, GAS_RADIUS);

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

        // Kept topped up rather than spawned once. A single burst thins out in a couple of seconds
        // and the hitbox would outlive the thing the player is reading it from.
        SporeLayer.Trail(
            NPC.Center + Main.rand.NextVector2Circular(GAS_RADIUS * 0.75f, GAS_RADIUS * 0.5f),
            new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(-0.6f, 0.1f)),
            Main.rand.NextFloat(10f, 20f) * presence
        );
    }

    /// <summary>
    ///     Saturates anyone standing in the cloud, on a cooldown.
    /// </summary>
    /// <remarks>
    ///     Every client runs this for itself, and applies only to the player it owns. Buffs on the
    ///     local player sync themselves, and the stack count is a client-side quantity that drives a
    ///     shader -- there is nothing here the server needs to arbitrate.
    /// </remarks>
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
            SporeLayer.Trail(NPC.Center, new Vector2(hit.HitDirection * 1.2f, -0.8f), Main.rand.NextFloat(4f, 8f));

            return;
        }

        // Cut down before it could let go. It comes apart and leaves nothing behind, which is the
        // whole reward for having spent the shots.
        SoundEngine.PlaySound(Assets.SFX.Sporeclot_Splash.Asset with { Volume = 0.7f, Pitch = 0.5f }, NPC.Center);

        Debris(NPC.Center, count: 5, force: 3.2f, scale: Growth);

        for (int i = 0; i < 5; i++)
            SporeLayer.Trail(NPC.Center + Main.rand.NextVector2Circular(10f, 10f), Main.rand.NextVector2Circular(1.6f, 1.6f), Main.rand.NextFloat(5f, 9f) * Growth);
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
        // Once it opens, the cloud is the whole visual -- the sac has come apart.
        if (Open)
            return false;

        Texture2D texture = TextureAssets.Npc[Type].Value;

        // Overshoots and settles: it pushes up out of the ground rather than inflating evenly, which
        // is what makes it read as something growing instead of something being scaled.
        float growth = Growth;
        float scale = MathHelper.Lerp(SPAWN_SCALE, FULL_SCALE, BloomEase.Ease(growth));

        // Volume-preserving, roughly: wider as it flattens, taller as it draws in. A sac that only
        // changed size reads as a scale keyframe; one that changes shape reads as something with
        // pressure inside it.
        float beat = MathF.Sin(pulsePhase) * PULSE_DEPTH * growth;

        var pulse = new Vector2(1f + beat, 1f - beat * 0.65f) * scale;

        // Anchored at the base, so it sits on the tile it was planted on however far the pulse
        // stretches it.
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
