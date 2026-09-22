using Terraria.Audio;
using Terraria.GameContent;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Graphics.Cinematics.Camera;
using TrelamiumIII.Core.Graphics.Interfaces;
using TrelamiumIII.Core.Graphics.Fields;
using TrelamiumIII.Core.Graphics.Fields.Layers;
using TrelamiumIII.Core.Graphics.Primitives;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Content.Items.Frostbark;

/// <summary>A slow flail that gains a gear each revolution, laying freezing fog, and detonates on impact at full wind-up.</summary>
public class FlailstormProj : ModProjectile, IDrawPrimitive
{
    #region Configuration & Constants

    private const string CHAIN_TEXTURE_PATH = AssetReferences.Content.Items.Frostbark.FlailstormChain.KEY;

    // One charge level per revolution. CHARGE_SCALE is the full-charge multiplier on damage and knockback, so the
    // item's numbers are the floor.
    private const int MAX_CHARGE_LEVEL = 4;
    private const float CHARGE_SCALE = 1.15f;
    private const float MAX_CRIT = 45f;

    // Radians per tick. Stepped by charge level. this is the gear change the weapon is named for.
    private const float BASE_SPIN_SPEED = 0.070f;
    private const float MAX_SPIN_SPEED = 0.250f;

    // A plain ellipse, unlike the vanilla flail's flattened lower arc, so the ring trail has no kink.
    private const float ORBIT_SQUASH = 0.86f;

    private const float BASE_SPIN_RADIUS = 30f;
    private const float SPIN_RADIUS_GROWTH = 24f;
    private const float BASE_HIT_RADIUS = 52f;
    private const float HIT_RADIUS_GROWTH = 28f;

    private const int LAUNCH_TIME_LIMIT = 18;
    private const float MAX_LAUNCH_LENGTH = 800f;
    private const float BASE_LAUNCH_SPEED = 9f;
    private const float MAX_LAUNCH_SPEED = 16f;
    private const float RETRACT_ACCEL = 3f;
    private const float MAX_RETRACT_SPEED = 20f;
    private const float FORCED_RETRACT_ACCEL = 6f;
    private const float MAX_FORCED_RETRACT_SPEED = 25f;

    private const int BASE_SPIN_HIT_COOLDOWN = 26;
    private const int MIN_SPIN_HIT_COOLDOWN = 12;
    private const int MOVING_HIT_COOLDOWN = 10;
    private const int DEFAULT_HIT_COOLDOWN = 30;
    private const float MAX_COLLISION_COUNT = 10f;

    // Harmless frostburn fields left on the ring per revolution, and the damaging clouds a fully
    // charged impact throws.
    private const int FIELDS_PER_REVOLUTION = 2;
    private const int BURST_CLOUDS = 8;
    private const float BURST_SPEED = 4.5f;

    // Spine points. Generated across the arc each frame rather than sampled from position history,
    // so this only has to resolve a circle -- 48 is a point every 7.5 degrees at full closure.
    private const int TRAIL_POINTS = 48;

    // Arc at zero charge. Grows to a full turn as the charge tops out.
    private const float MIN_TRAIL_ARC = 1.4f;

    private const float TRAIL_WIDTH = 15f;
    private const float TRAIL_REPEATS = 4f;

    private static readonly Color TrailEdgeColor = new(78, 150, 235);
    private static readonly Color TrailCoreColor = new(226, 245, 255);

    private static readonly SoundStyle RevolutionSound = SoundID.Item30 with { Volume = 0.55f, MaxInstances = 2 };
    private static readonly SoundStyle ChargedSound = SoundID.Item28 with { Volume = 0.8f, Pitch = 0.35f };
    private static readonly SoundStyle BurstSound = SoundID.Item27 with { Volume = 0.9f, Pitch = -0.5f };

    #endregion

    #region Properties & Fields

    private enum AIState { Spinning, LaunchingForward, Retracting, ForcedRetracting }

    private AIState CurrentAIState
    {
        get => (AIState)Projectile.ai[0];
        set => Projectile.ai[0] = (float)value;
    }

    public ref float StateTimer => ref Projectile.ai[1];

    /// <summary>Which way round the orbit runs. Fixed at spawn so turning around cannot flip the ring.</summary>
    public ref float SpinDirection => ref Projectile.ai[2];

    public ref float CollisionCounter => ref Projectile.localAI[0];

    /// <summary>Total radians swung. Drives the charge, and survives the throw so the head remembers it.</summary>
    public ref float SpinAngle => ref Projectile.localAI[1];

    private int ChargeLevel => Math.Min(MAX_CHARGE_LEVEL, (int)(SpinAngle / MathHelper.TwoPi));
    private float ChargeProgress => Math.Min(1f, SpinAngle / (MathHelper.TwoPi * MAX_CHARGE_LEVEL));
    private bool FullyCharged => ChargeLevel >= MAX_CHARGE_LEVEL;
    private float ChargeMultiplier => MathHelper.Lerp(1f, CHARGE_SCALE, ChargeProgress);

    // Only swing rate steps per level; everything else grows continuously off ChargeProgress, so the gear change is
    // the one visible step.
    private float SpinSpeed => MathHelper.Lerp(BASE_SPIN_SPEED, MAX_SPIN_SPEED, ChargeLevel / (float)MAX_CHARGE_LEVEL);
    private float SpinRadius => BASE_SPIN_RADIUS + ChargeProgress * SPIN_RADIUS_GROWTH;
    private float HitRadius => BASE_HIT_RADIUS + ChargeProgress * HIT_RADIUS_GROWTH;
    private float TrailArc => MathHelper.Lerp(MIN_TRAIL_ARC, MathHelper.TwoPi, ChargeProgress);
    private float LaunchSpeed => MathHelper.Lerp(BASE_LAUNCH_SPEED, MAX_LAUNCH_SPEED, ChargeProgress);
    private int Facing => SpinDirection < 0f ? -1 : 1;

    private Vector2[]? trailPositions;
    private VertexTrail? trail;

    #endregion

    #region Setup & Initialization

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
        ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        ProjectileID.Sets.HeldProjDoesNotUsePlayerGfxOffY[Type] = true;
    }

    public override void SetDefaults()
    {
        Projectile.netImportant = true;
        Projectile.width = Projectile.height = 24;
        Projectile.friendly = true;
        Projectile.penetrate = -1;
        Projectile.DamageType = DamageClass.MeleeNoSpeed;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = DEFAULT_HIT_COOLDOWN;
    }

    public override void OnSpawn(Terraria.DataStructures.IEntitySource source)
        => SpinDirection = Main.player[Projectile.owner].direction;

    #endregion

    #region Core AI

    public override void AI()
    {
        var player = Main.player[Projectile.owner];

        if (ShouldKillProjectile(player))
        {
            Projectile.Kill();
            return;
        }

        var mountedCenter = player.MountedCenter;
        var meleeSpeed = player.GetTotalAttackSpeed(DamageClass.Melee);

        ExecuteCurrentState(player, mountedCenter, meleeSpeed);

        // Crit is a chance rather than a multiplier, so it cannot ride ChargeMultiplier with the
        // rest. Refreshed every tick because the ring damages while it is still winding up.
        Projectile.CritChance = (int)MathHelper.Lerp(player.GetWeaponCrit(player.HeldItem), MAX_CRIT, ChargeProgress);

        UpdateProjectileTransform(mountedCenter);
        UpdatePlayerAnimation(player, mountedCenter);
        UpdateTrail(mountedCenter, player.gfxOffY);
        TrailMist();
    }

    private bool ShouldKillProjectile(Player player) =>
        !player.active || player.dead || player.noItems || player.CCed ||
        Projectile.DistanceSQ(player.Center) > 900f * 900f ||
        (Main.myPlayer == Projectile.owner && Main.mapFullscreen);

    private void ExecuteCurrentState(Player player, Vector2 mountedCenter, float meleeSpeed)
    {
        switch (CurrentAIState)
        {
            case AIState.Spinning:
                HandleSpinning(player, mountedCenter);
                break;
            case AIState.LaunchingForward:
                HandleLaunching(player, mountedCenter);
                break;
            case AIState.Retracting:
                HandleRetracting(player, mountedCenter, meleeSpeed);
                break;
            case AIState.ForcedRetracting:
                HandleForcedRetracting(player, mountedCenter, meleeSpeed);
                break;
        }
    }

    #endregion

    #region State Handlers

    private void HandleSpinning(Player player, Vector2 mountedCenter)
    {
        if (Projectile.owner == Main.myPlayer)
        {
            var mouseDir = Vector2.Normalize(Main.MouseWorld - mountedCenter);
            if (mouseDir == Vector2.Zero) mouseDir = Vector2.UnitX * player.direction;

            player.ChangeDir(Math.Sign(mouseDir.X));

            if (!player.channel)
            {
                LaunchProjectile(mouseDir, mountedCenter, player);
                return;
            }
        }

        UpdateSpinning(player, mountedCenter);
    }

    private void LaunchProjectile(Vector2 direction, Vector2 mountedCenter, Player player)
    {
        CurrentAIState = AIState.LaunchingForward;
        StateTimer = 0f;
        Projectile.velocity = direction * LaunchSpeed + player.velocity;
        Projectile.Center = mountedCenter;
        Projectile.netUpdate = true;
        Projectile.ResetLocalNPCHitImmunity();
        Projectile.localNPCHitCooldown = MOVING_HIT_COOLDOWN;
    }

    private void UpdateSpinning(Player player, Vector2 mountedCenter)
    {
        int revolutionsBefore = (int)(SpinAngle / MathHelper.TwoPi);

        SpinAngle += SpinSpeed;

        if ((int)(SpinAngle / MathHelper.TwoPi) > revolutionsBefore)
            OnRevolutionComplete(mountedCenter);

        Projectile.Center = mountedCenter + OrbitOffset() + new Vector2(0f, player.gfxOffY);
        Projectile.velocity = Vector2.Zero;

        // Faster spin means faster hits; otherwise extra swing speed would be purely visual.
        Projectile.localNPCHitCooldown = (int)MathHelper.Lerp(BASE_SPIN_HIT_COOLDOWN, MIN_SPIN_HIT_COOLDOWN, ChargeProgress);

        RingMist();
    }

    private void HandleLaunching(Player player, Vector2 mountedCenter)
    {
        StateTimer++;
        bool shouldRetract = StateTimer >= LAUNCH_TIME_LIMIT ||
                           Projectile.DistanceSQ(mountedCenter) >= MAX_LAUNCH_LENGTH * MAX_LAUNCH_LENGTH;

        if (shouldRetract)
            TransitionTo(AIState.Retracting, 0.3f);

        player.ChangeDir(Math.Sign(Projectile.Center.X - player.Center.X));
        Projectile.localNPCHitCooldown = MOVING_HIT_COOLDOWN;
    }

    private void HandleRetracting(Player player, Vector2 mountedCenter, float meleeSpeed)
    {
        var toPlayer = mountedCenter - Projectile.Center;

        if (toPlayer.Length() <= MAX_RETRACT_SPEED * meleeSpeed)
        {
            Projectile.Kill();
            return;
        }

        var targetVel = Vector2.Normalize(toPlayer) * MAX_RETRACT_SPEED * meleeSpeed;
        Projectile.velocity = Vector2.Lerp(Projectile.velocity * 0.98f, targetVel, RETRACT_ACCEL * meleeSpeed / 60f);

        player.ChangeDir(Math.Sign(Projectile.Center.X - player.Center.X));
    }

    private void HandleForcedRetracting(Player player, Vector2 mountedCenter, float meleeSpeed)
    {
        Projectile.tileCollide = false;
        var toPlayer = mountedCenter - Projectile.Center;

        if (toPlayer.Length() <= MAX_FORCED_RETRACT_SPEED * meleeSpeed)
        {
            Projectile.Kill();
            return;
        }

        var targetVel = Vector2.Normalize(toPlayer) * MAX_FORCED_RETRACT_SPEED * meleeSpeed;
        Projectile.velocity = Vector2.Lerp(Projectile.velocity * 0.98f, targetVel, FORCED_RETRACT_ACCEL * meleeSpeed / 60f);

        var futurePos = Projectile.Center + Projectile.velocity;
        if (Vector2.Dot(Vector2.Normalize(toPlayer), Vector2.Normalize(mountedCenter - futurePos)) < 0f)
        {
            Projectile.Kill();
            return;
        }

        player.ChangeDir(Math.Sign(Projectile.Center.X - player.Center.X));
    }

    private void TransitionTo(AIState newState, float velocityMultiplier)
    {
        CurrentAIState = newState;
        StateTimer = 0f;
        Projectile.netUpdate = true;
        Projectile.velocity *= velocityMultiplier;
    }

    #endregion

    #region Frost

    // A revolution pays out a step of swing speed and a couple of harmless frostburn fields.
    private void OnRevolutionComplete(Vector2 mountedCenter)
    {
        // Past the top rung the ring keeps damaging but stops paying out, or holding would carpet the screen in fields.
        if ((int)(SpinAngle / MathHelper.TwoPi) > MAX_CHARGE_LEVEL)
            return;

        SoundEngine.PlaySound(FullyCharged ? ChargedSound : RevolutionSound with
        {
            Pitch = -0.4f + ChargeLevel * 0.22f,
        }, Projectile.Center);

        if (Main.myPlayer != Projectile.owner)
            return;

        // Spaced round the orbit, so the fields stand in the ring the player drew.
        for (int i = 0; i < FIELDS_PER_REVOLUTION; i++)
        {
            var position = mountedCenter + OrbitOffset(MathHelper.TwoPi * i / FIELDS_PER_REVOLUTION);

            Projectile.NewProjectile(Projectile.GetSource_FromThis(), position, Vector2.Zero,
                ModContent.ProjectileType<FrostCloudProj>(), 0, 0f, Projectile.owner);
        }
    }

    // The fully charged payoff, thrown where the head lands: damaging clouds, and a long freeze.
    private void FrostBurst(Vector2 position)
    {
        CameraSystem.shake = 8;
        SoundEngine.PlaySound(BurstSound, position);
        BurstMist(position);

        if (Main.myPlayer != Projectile.owner)
            return;

        // Full charged damage per cloud. The clouds share one static immunity, so overlapping them
        // cant multiply it
        int damage = (int)(Projectile.damage * CHARGE_SCALE);
        float knockback = Projectile.knockBack * CHARGE_SCALE;

        for (int i = 0; i < BURST_CLOUDS; i++)
        {
            var outward = (MathHelper.TwoPi * i / BURST_CLOUDS).ToRotationVector2();

            Projectile.NewProjectile(Projectile.GetSource_FromThis(), position + outward * 12f, outward * BURST_SPEED,
                ModContent.ProjectileType<FrostCloudProj>(), damage, knockback, Projectile.owner);
        }
    }

    /// <summary>Fog laid along the arc while spinning, so the ring reads as frost and not just a ribbon.</summary>
    private void RingMist()
    {
        if (FieldSystem.GetLayer<ArcticField>() is not { } mist)
            return;

        var mountedCenter = Main.player[Projectile.owner].MountedCenter;
        int count = 1 + (int)(ChargeProgress * 2f);

        for (int i = 0; i < count; i++)
        {
            // Behind the head only, so the ring reads as drawn by the weapon.
            var position = mountedCenter + OrbitOffset(Main.rand.NextFloat(0f, TrailArc));

            mist.SpawnNode(position + Main.rand.NextVector2Circular(4f, 4f), Vector2.Zero,
                radius: Main.rand.NextFloat(6f, 8f + ChargeProgress * 8f),
                life: Main.rand.NextFloat(16f, 26f));
        }
    }

    private void TrailMist()
    {
        if (CurrentAIState == AIState.Spinning || !Main.rand.NextBool(2))
            return;

        FieldSystem.GetLayer<ArcticField>()?.SpawnNode(
            Projectile.Center + Main.rand.NextVector2Circular(5f, 5f),
            Projectile.velocity * 0.06f + Main.rand.NextVector2Circular(0.3f, 0.3f),
            radius: Main.rand.NextFloat(10f, 16f + ChargeProgress * 8f),
            life: Main.rand.NextFloat(18f, 32f));
    }

    private static void BurstMist(Vector2 position)
    {
        if (FieldSystem.GetLayer<ArcticField>() is not { } mist)
            return;

        const int count = 20;

        for (int i = 0; i < count; i++)
        {
            var velocity = (MathHelper.TwoPi * i / count).ToRotationVector2() * Main.rand.NextFloat(2.2f, 5f);

            mist.SpawnNode(position + velocity * 5f, velocity,
                radius: Main.rand.NextFloat(13f, 26f),
                life: Main.rand.NextFloat(30f, 56f));
        }
    }

    #endregion

    #region Collision & Damage

    public override bool OnTileCollide(Vector2 oldVelocity)
    {
        var impactIntensity = 0;
        var bounceFactor = CurrentAIState switch
        {
            AIState.LaunchingForward => 0.5f,
            _ => 0.3f
        };

        if (MathF.Abs(oldVelocity.X - Projectile.velocity.X) > 0.1f)
        {
            if (MathF.Abs(oldVelocity.X) > 4f) impactIntensity = 1;
            Projectile.velocity.X = -oldVelocity.X * bounceFactor;
            CollisionCounter++;
        }

        if (MathF.Abs(oldVelocity.Y - Projectile.velocity.Y) > 0.1f)
        {
            if (MathF.Abs(oldVelocity.Y) > 4f) impactIntensity = 1;
            Projectile.velocity.Y = -oldVelocity.Y * bounceFactor;
            CollisionCounter++;
        }

        if (CurrentAIState == AIState.LaunchingForward)
            HandleLaunchCollision(oldVelocity, ref impactIntensity);

        if (impactIntensity > 0)
        {
            Projectile.netUpdate = true;
            for (int i = 0; i < impactIntensity; i++)
                Collision.HitTiles(Projectile.position, oldVelocity, Projectile.width, Projectile.height);

            SoundEngine.PlaySound(SoundID.Dig, Projectile.position);
        }

        if (CurrentAIState != AIState.Spinning && CollisionCounter >= MAX_COLLISION_COUNT)
        {
            CurrentAIState = AIState.ForcedRetracting;
            Projectile.netUpdate = true;
        }

        return false;
    }

    private void HandleLaunchCollision(Vector2 velocity, ref int impactIntensity)
    {
        Projectile.localNPCHitCooldown = MOVING_HIT_COOLDOWN;
        Projectile.netUpdate = true;

        var scanStart = Projectile.TopLeft.ToTileCoordinates();
        var scanEnd = Projectile.BottomRight.ToTileCoordinates();
        impactIntensity = 2;

        Projectile.CreateImpactExplosion(2, Projectile.Center, ref scanStart, ref scanEnd, Projectile.width, out var causedShockwaves);
        Projectile.CreateImpactExplosion2_FlailTileCollision(Projectile.Center, causedShockwaves, velocity);
        Projectile.position -= velocity;

        if (FullyCharged)
            FrostBurst(Projectile.Center);
    }

    public override bool? CanDamage() =>
        CurrentAIState == AIState.Spinning && SpinAngle <= MathHelper.PiOver2 ? false : null;

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        if (CurrentAIState == AIState.Spinning)
        {
            var mountedCenter = Main.player[Projectile.owner].MountedCenter;
            var toTarget = targetHitbox.ClosestPointInRect(mountedCenter) - mountedCenter;
            toTarget.Y /= ORBIT_SQUASH; // undo the orbit's squash, so reach matches the drawn ring
            return toTarget.LengthSquared() <= HitRadius * HitRadius;
        }
        return base.Colliding(projHitbox, targetHitbox);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        SoundEngine.PlaySound(Assets.SFX.HewerReturn.Asset with
        {
            MaxInstances = 1,
            Volume = MathHelper.Lerp(0.4f, 0.7f, ChargeProgress),
            Pitch = MathHelper.Lerp(-0.9f, -0.55f, ChargeProgress),
        }, Projectile.Center);

        target.AddBuff(BuffID.Frostburn, (int)MathHelper.Lerp(40f, 150f, ChargeProgress));

        // Only the thrown head detonates. A spinning ring sweeps the same target every dozen ticks,
        // and bursting on each of those would be a wall of fog rather than a payoff.
        if (FullyCharged && CurrentAIState != AIState.Spinning)
            FrostBurst(target.Center);
    }

    public override void OnHitPlayer(Player target, Player.HurtInfo info)
        => SoundEngine.PlaySound(Assets.SFX.HewerReturn.Asset with { MaxInstances = 1, Volume = 0.4f, Pitch = -0.9f }, Projectile.Center);

    public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
    {
        modifiers.SourceDamage *= ChargeMultiplier;
        modifiers.HitDirectionOverride = Math.Sign(target.Center.X - Main.player[Projectile.owner].Center.X);

        // The ring sweeps a target repeatedly, so it has to stay a wind-up rather than the payoff.
        modifiers.Knockback *= CurrentAIState == AIState.Spinning
            ? ChargeMultiplier * 0.3f
            : ChargeMultiplier;
    }

    #endregion

    #region Drawing & Rendering

    private void UpdateTrail(Vector2 mountedCenter, float gfxOffY)
    {
        if (CurrentAIState != AIState.Spinning)
            return;

        trailPositions ??= new Vector2[TRAIL_POINTS];
        trail ??= new VertexTrail(TRAIL_POINTS, TrailWidth, TrailColor);

        var anchor = mountedCenter + new Vector2(0f, gfxOffY);
        float arc = TrailArc;

        for (int i = 0; i < TRAIL_POINTS; i++)
        {
            // 0 = tail, 1 = head, matching VertexTrail's convention that the last point is newest.
            float factor = i / (float)(TRAIL_POINTS - 1);
            trailPositions[i] = anchor + OrbitOffset(arc * (1f - factor));
        }

        trail.Positions = trailPositions;
    }

    // Tapers to the tail, by less and less as the charge climbs: a low arc reads as a comet tail,
    // while a closed ring keeps most of its width so the circle reads as a circle.
    private float TrailWidth(float factor)
        => TRAIL_WIDTH * MathHelper.Lerp(ChargeProgress * 0.8f, 1f, factor);

    private Color TrailColor(float factor)
    {
        var color = Color.Lerp(TrailEdgeColor, TrailCoreColor, factor * factor);

        return color * MathHelper.Lerp(0.4f, 1f, ChargeProgress) * MathHelper.Lerp(0.2f, 1f, factor);
    }

    public void DrawPrimitives()
    {
        if (CurrentAIState != AIState.Spinning || trail == null)
            return;

        var effect = Assets.Shaders.LightningTrail.Asset.Value;
        if (effect == null) return;

        var transform = Matrix.CreateTranslation(-Main.screenPosition.ToVector3())
                      * Main.GameViewMatrix.TransformationMatrix
                      * Matrix.CreateOrthographicOffCenter(0, Main.screenWidth, Main.screenHeight, 0, -1, 1);

        effect.Parameters["time"]?.SetValue(Main.GameUpdateCount * 0.04f);
        effect.Parameters["repeats"]?.SetValue(TRAIL_REPEATS);
        effect.Parameters["transformMatrix"]?.SetValue(transform);
        effect.Parameters["sampleTexture"]?.SetValue(
            ModContent.Request<Texture2D>(Assets.Textures.VFX.EnergyTrail.KEY).Value);

        trail.Render(effect);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        var texture = TextureAssets.Projectile[Projectile.type].Value;
        var drawPos = Projectile.Center - Main.screenPosition + new Vector2(0f, Projectile.gfxOffY);
        var drawOrigin = texture.Size() * 0.5f;
        var spriteEffects = Projectile.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

        DrawChain();
        DrawAfterImages(lightColor);

        Main.EntitySpriteDraw(texture, drawPos, null, Projectile.GetAlpha(lightColor), Projectile.rotation,
            drawOrigin, Projectile.scale, spriteEffects, 0);

        // Fully charged pulse, so the ready state stays legible with the ring off the side of the screen.
        if (CurrentAIState == AIState.Spinning && FullyCharged)
        {
            float cycle = Main.GameUpdateCount % 30 / 30f;
            float opacity = cycle < 0.15f ? MathF.Sin(cycle * MathHelper.Pi * 6.67f) : 0f;

            if (opacity > 0f)
            {
                var flash = TrailCoreColor.ToVector4();
                flash.W = 0f; // additive

                Main.EntitySpriteDraw(texture, drawPos, null, new Color(flash) * opacity * 1.5f,
                    Projectile.rotation, drawOrigin, Projectile.scale, spriteEffects, 0);
            }
        }

        return false;
    }

    private void DrawChain()
    {
        var playerArmPos = Main.GetPlayerArmPosition(Projectile);
        playerArmPos.Y -= Main.player[Projectile.owner].gfxOffY;

        var chainTexture = ModContent.Request<Texture2D>(CHAIN_TEXTURE_PATH);
        var chainOrigin = chainTexture.Size() * 0.5f;
        var toPlayer = playerArmPos.MoveTowards(Projectile.Center, 4f) - Projectile.Center;
        var chainDir = Vector2.Normalize(toPlayer);
        var chainRotation = chainDir.ToRotation() + MathHelper.PiOver2;
        var segmentLength = chainTexture.Height();

        if (segmentLength == 0) segmentLength = 10;

        var chainPos = Projectile.Center;
        var remainingLength = toPlayer.Length() + segmentLength * 0.5f;
        int segmentCount = 0;

        while (remainingLength > 0f)
        {
            var chainColor = Lighting.GetColor((int)(chainPos.X / 16), (int)(chainPos.Y / 16));

            // Frost climbs the chain from the head, so the charge is readable off the chain alone.
            if (segmentCount < 4 && ChargeProgress > 0f)
            {
                float glow = ChargeProgress * (1f - segmentCount / 4f);
                chainColor = Color.Lerp(chainColor, TrailCoreColor, glow);
                chainColor = Color.Lerp(chainColor, TrailEdgeColor, glow * 0.4f);
            }

            Main.spriteBatch.Draw(chainTexture.Value, chainPos - Main.screenPosition, null,
                chainColor, chainRotation, chainOrigin, 1f, SpriteEffects.None, 0f);

            chainPos += chainDir * segmentLength;
            segmentCount++;
            remainingLength -= segmentLength;
        }
    }

    private void DrawAfterImages(Color lightColor)
    {
        if (CurrentAIState != AIState.LaunchingForward) return;

        var texture = TextureAssets.Projectile[Projectile.type].Value;
        var drawOrigin = texture.Size() * 0.5f;
        var spriteEffects = Projectile.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

        for (int k = 0; k < Math.Min(Projectile.oldPos.Length, StateTimer); k++)
        {
            var drawPos = Projectile.oldPos[k] - Main.screenPosition + drawOrigin + new Vector2(0f, Projectile.gfxOffY);
            float opacity = (float)(Projectile.oldPos.Length - k) / Projectile.oldPos.Length;
            var color = Color.Lerp(Projectile.GetAlpha(lightColor) * opacity, TrailEdgeColor * opacity, ChargeProgress * 0.6f);

            float scale = Projectile.scale - k / (float)Projectile.oldPos.Length / 3;
            Main.spriteBatch.Draw(texture, drawPos, null, color, Projectile.rotation, drawOrigin, scale, spriteEffects, 0f);
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    ///     Head position <paramref name="behind" /> radians back along the orbit, relative to the player. Shared by head and trail.
    /// </summary>
    private Vector2 OrbitOffset(float behind = 0f)
    {
        float angle = (SpinAngle - behind) * Facing;

        return new Vector2(MathF.Cos(angle), MathF.Sin(angle) * ORBIT_SQUASH) * SpinRadius;
    }

    private void UpdateProjectileTransform(Vector2 mountedCenter)
    {
        Projectile.direction = Math.Sign(Projectile.velocity.X);
        Projectile.spriteDirection = Projectile.direction;
        Projectile.rotation = (mountedCenter - Projectile.Center).ToRotation() + MathHelper.PiOver2;
    }

    private void UpdatePlayerAnimation(Player player, Vector2 mountedCenter)
    {
        Projectile.timeLeft = 2;
        player.heldProj = Projectile.whoAmI;
        player.SetDummyItemTime(2);
        player.itemRotation = (Projectile.Center - mountedCenter).ToRotation();

        if (Projectile.Center.X < mountedCenter.X)
            player.itemRotation += MathHelper.Pi;

        player.itemRotation = MathHelper.WrapAngle(player.itemRotation);
    }

    #endregion
}

public class FrostCloudProj : ModProjectile
{
    public override string Texture => Assets.Textures.VFX.RadialGradient.KEY;

    private const int LINGER_LIFETIME = 240;
    private const int BURST_LIFETIME = 100;

    private const float LINGER_RADIUS = 46f;
    private const float BURST_RADIUS = 74f;

    private const int LINGER_FROSTBURN = 90;
    private const int BURST_FROSTBURN = 5 * 60;

    private const int BITE_TICKS = 20;

    private const int REFREEZE_INTERVAL = 30;

    private const int SWELL_TICKS = 12;
    private const int FADE_TICKS = 40;

    private bool Damaging => Projectile.damage > 0;
    private float Radius => Damaging ? BURST_RADIUS : LINGER_RADIUS;
    private int Lifetime => Damaging ? BURST_LIFETIME : LINGER_LIFETIME;
    private int FrostburnTicks => Damaging ? BURST_FROSTBURN : LINGER_FROSTBURN;
    private int Age => Lifetime - Projectile.timeLeft;

    private float Envelope => MathHelper.Clamp(Age / (float)SWELL_TICKS, 0f, 1f)
                            * MathHelper.Clamp(Projectile.timeLeft / (float)FADE_TICKS, 0f, 1f);

    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 16;
        Projectile.friendly = true;
        Projectile.penetrate = -1;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.DamageType = DamageClass.MeleeNoSpeed;

        Projectile.usesIDStaticNPCImmunity = true;
        Projectile.idStaticNPCHitCooldown = BITE_TICKS;

        Projectile.timeLeft = BURST_LIFETIME;
    }

    public override void OnSpawn(Terraria.DataStructures.IEntitySource source)
        => Projectile.timeLeft = Lifetime;

    public override void AI()
    {
        Projectile.velocity *= 0.91f;

        // The field is the hitbox, so it grows with the envelope rather than biting at full width on
        // the frame it appears.
        int span = Math.Max((int)(Radius * 2f * Envelope), 2);
        Projectile.Resize(span, span);

        Freeze();
        Fog();

        Lighting.AddLight(Projectile.Center, 0.14f * Envelope, 0.24f * Envelope, 0.34f * Envelope);
    }

    private void Freeze()
    {
        if (Main.myPlayer != Projectile.owner || Age % REFREEZE_INTERVAL != 0)
            return;

        float radius = Radius * Envelope;

        for (int i = 0; i < Main.maxNPCs; i++)
        {
            var npc = Main.npc[i];

            if (!npc.active || npc.friendly || npc.dontTakeDamage || !npc.CanBeChasedBy(Projectile))
                continue;

            if (npc.Hitbox.ClosestPointInRect(Projectile.Center).DistanceSQ(Projectile.Center) <= radius * radius)
                npc.AddBuff(BuffID.Frostburn, FrostburnTicks);
        }
    }

    private void Fog()
    {
        if (FieldSystem.GetLayer<ArcticField>() is not { } mist)
            return;

        float envelope = Envelope;
        int count = Damaging ? 2 : 1;
        float nodeRadius = Damaging ? 22f : 15f;

        for (int i = 0; i < count; i++)
        {
            var offset = Main.rand.NextVector2Circular(1f, 1f) * Radius * 0.62f * envelope;

            mist.SpawnNode(Projectile.Center + offset,
                offset * 0.012f + Projectile.velocity * 0.15f,
                radius: Main.rand.NextFloat(nodeRadius * 0.6f, nodeRadius) * MathHelper.Lerp(0.5f, 1f, envelope),
                life: Main.rand.NextFloat(26f, 48f));
        }
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        float radius = Radius * Envelope;

        return targetHitbox.ClosestPointInRect(Projectile.Center).DistanceSQ(Projectile.Center) <= radius * radius;
    }

    public override bool? CanDamage() => Damaging && Age < BITE_TICKS;

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        => target.AddBuff(BuffID.Frostburn, FrostburnTicks);

    public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        => modifiers.HitDirectionOverride = Math.Sign(target.Center.X - Projectile.Center.X);

    public override bool PreDraw(ref Color lightColor) => false;
}
