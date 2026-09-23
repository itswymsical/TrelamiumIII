using ReLogic.Content;
using System.IO;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using TrelamiumIII.Content.Fungore;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Graphics.Cinematics.Camera;
using TrelamiumIII.Core.Graphics.Particles;

namespace TrelamiumIII.Content.NPCs.Forest;

public class SporadicSlime : ModNPC
{
    private static readonly int[] VariantPool =
    {
        NPCID.BlueSlime,
        NPCID.GreenSlime,
        NPCID.PurpleSlime,
    };

    private const int MUSHROOM_CHUNK_COUNT = 5;
    private const int GROUND_POUND_COOLDOWN = 360;
    private const float GROUND_POUND_RANGE = 360f;
    private const float GRAVITY = 0.3f;
    private const float MAX_FALL_SPEED = 10f;

    // Synced state
    private int variantType = NPCID.BlueSlime;

    // Local state
    private Vector2 squishScale = Vector2.One;
    private int groundPoundCooldown;
    private int groundPoundPhase;
    private float groundPoundTimer;

    public override string Texture => $"Terraria/Images/NPC_{NPCID.BlueSlime}";

    public override void SetStaticDefaults()
    {
        Main.npcFrameCount[Type] = 2;
        NPCID.Sets.NPCBestiaryDrawModifiers drawModifiers = new()
        {
            Velocity = 0f,
            Direction = 1,
        };

        NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, drawModifiers);
    }

    public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
    {
        bestiaryEntry.Info.AddRange([BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Surface,
            new FlavorTextBestiaryInfoElement("Mods.TrelamiumIII.NPCs.SporadicSlime.Bestiary")
        ]);
    }
    public override void SetDefaults()
    {
        NPC.aiStyle = -1;
        NPC.width = 24;
        NPC.height = 18;
        NPC.lifeMax = 25;
        NPC.damage = 7;
        NPC.defense = 2;
        NPC.knockBackResist = 0.92f;
        NPC.HitSound = SoundID.NPCHit1;
        NPC.DeathSound = SoundID.NPCDeath1;
        NPC.value = Item.buyPrice(copper: 50);
        AnimationType = NPCID.BlueSlime;
    }

    public override void OnSpawn(IEntitySource source)
    {
        variantType = VariantPool[Main.rand.Next(VariantPool.Length)];
        CopyVariantStats();
        NPC.netUpdate = true;
    }

    private void CopyVariantStats()
    {
        NPC reference = new NPC();
        reference.SetDefaults(variantType);

        NPC.lifeMax = (int)(reference.lifeMax * 1.15f);
        NPC.life = (int)(reference.lifeMax * 1.15f);
        NPC.damage = reference.damage;
        NPC.defense = reference.defense + 2;
        NPC.knockBackResist = reference.knockBackResist;
        NPC.value = reference.value;
        NPC.alpha = reference.alpha;
        NPC.color = reference.color;
    }

    public override void SendExtraAI(BinaryWriter writer)
    {
        writer.Write(variantType);
        writer.Write((byte)groundPoundPhase);
    }

    public override void ReceiveExtraAI(BinaryReader reader)
    {
        variantType = reader.ReadInt32();
        groundPoundPhase = reader.ReadByte();
        CopyVariantStats();
    }

    public override void FindFrame(int frameHeight)
    {
        base.FindFrame(frameHeight);
    }

    public override float SpawnChance(NPCSpawnInfo spawnInfo)
    {
        if (Main.dayTime)
            return spawnInfo.Player.ZoneOverworldHeight && spawnInfo.Player.ZoneForest ? 0.11f : 0f;
        
        else return 0f;
    }

    public override void ModifyNPCLoot(NPCLoot npcLoot)
    {
        npcLoot.Add(ItemDropRule.Common(ItemID.Mushroom, 1, 1, 3));
        npcLoot.Add(ItemDropRule.Common(ItemID.Gel, 2, 1, 4));
    }

    public override void AI()
    {
        if (groundPoundPhase > 0)
        {
            DoGroundPound();
        }
        else
        {
            DoSlimeAI();
            TryStartGroundPound();
        }

        // Phase 3 hard-locks Y velocity for the slam, so skip gravity there
        if (groundPoundPhase != 3)
            ApplyGravity();

        HandleSquishScale();
    }

    private void ApplyGravity()
    {
        if (NPC.noGravity)
            return;
        NPC.velocity.Y += GRAVITY;
        if (NPC.velocity.Y > MAX_FALL_SPEED)
            NPC.velocity.Y = MAX_FALL_SPEED;
    }

    // slime AI adapted from vanilla AI_001_Slimes.
    // ai[0] = jump countdown timer (negative wait, 0 or above triggers jump)
    // ai[2] = init flag plus direction-change cooldown
    // ai[3] = previous X position for stuck detection
    private void DoSlimeAI()
    {
        if (NPC.ai[2] == 0f)
        {
            NPC.ai[0] = -100f;
            NPC.ai[2] = 1f;
            NPC.TargetClosest();
        }

        if (NPC.ai[2] > 1f)
            NPC.ai[2] -= 1f;

        if (NPC.velocity.Y == 0f)
        {
            if (NPC.collideY && NPC.oldVelocity.Y != 0f && Collision.SolidCollision(NPC.position, NPC.width, NPC.height))
                NPC.position.X -= NPC.velocity.X + NPC.direction;

            if (NPC.ai[3] == NPC.position.X)
            {
                NPC.direction *= -1;
                NPC.ai[2] = 200f;
            }
            NPC.ai[3] = 0f;

            NPC.velocity.X *= 0.8f;
            if (NPC.velocity.X > -0.1f && NPC.velocity.X < 0.1f)
                NPC.velocity.X = 0f;

            NPC.ai[0] += 1f;

            // Sporadic timer fast-forwards
            if (Main.rand.NextBool(20))
                NPC.ai[0] += Main.rand.NextFloat(2f, 6f);

            if (NPC.ai[0] >= 0f)
            {
                NPC.netUpdate = true;
                if (NPC.ai[2] == 1f)
                    NPC.TargetClosest();

                bool bigJump = Main.rand.NextBool(4);
                float yPower = (bigJump ? 8f : 6f) * Main.rand.NextFloat(0.85f, 1.15f);
                float xPower = bigJump ? 3f : 2f;

                NPC.velocity.Y = -yPower;
                NPC.velocity.X += xPower * NPC.direction;

                NPC.ai[0] = Main.rand.Next(-200, -30);
                NPC.ai[3] = NPC.position.X;
            }
        }
        else if (NPC.target < 255 && ((NPC.direction == 1 && NPC.velocity.X < 3f) || (NPC.direction == -1 && NPC.velocity.X > -3f)))
        {
            if (NPC.collideX && Math.Abs(NPC.velocity.X) == 0.2f)
                NPC.position.X -= 1.4f * NPC.direction;
            if (NPC.collideY && NPC.oldVelocity.Y != 0f && Collision.SolidCollision(NPC.position, NPC.width, NPC.height))
                NPC.position.X -= NPC.velocity.X + NPC.direction;

            if ((NPC.direction == -1 && NPC.velocity.X < 0.01f) || (NPC.direction == 1 && NPC.velocity.X > -0.01f))
                NPC.velocity.X += 0.2f * NPC.direction;
            else
                NPC.velocity.X *= 0.93f;
        }
    }

    private void TryStartGroundPound()
    {
        if (groundPoundCooldown > 0)
        {
            groundPoundCooldown--;
            return;
        }

        if (NPC.velocity.Y != 0f)
            return;

        Player target = Main.player[NPC.target];
        if (!target.active || target.dead)
            return;

        if (Vector2.Distance(NPC.Center, target.Center) > GROUND_POUND_RANGE)
            return;

        if (Main.rand.NextBool(150))
        {
            groundPoundPhase = 1;
            groundPoundTimer = 0f;
            groundPoundCooldown = GROUND_POUND_COOLDOWN;
            NPC.netUpdate = true;
        }
    }

    private void DoGroundPound()
    {
        groundPoundTimer++;

        switch (groundPoundPhase)
        {
            case 1: // Charge
                NPC.velocity.X *= 0.6f;
                if (groundPoundTimer >= 28f)
                {
                    SoundEngine.PlaySound(SoundID.Item95 with { PitchVariance = 0.2f }, NPC.Center);
                    NPC.velocity.Y = -10f;
                    NPC.velocity.X = 0f;
                    groundPoundPhase = 2;
                    groundPoundTimer = 0f;
                }
                break;

            case 2: // Apex pivot
                NPC.velocity.X *= 0.95f;
                if (NPC.velocity.Y > 0f)
                {
                    NPC.velocity = new Vector2(0f, 16f);
                    groundPoundPhase = 3;
                    groundPoundTimer = 0f;
                }
                break;

            case 3: // Slam
                if (GroundBeneath() || NPC.velocity.Y == 0f)
                {
                    SoundEngine.PlaySound(SoundID.Item14 with { PitchVariance = 0.2f }, NPC.Center);
                    CameraSystem.shake = 6;
                    Projectile.NewProjectile(new EntitySource_Misc("Sporadic Slime"), 
                        NPC.Bottom, Vector2.Zero, ModContent.ProjectileType<SporadicSlamProjectile>(), NPC.damage, 0.75f);
                    SpawnSlamDust();
                    groundPoundPhase = 4;
                    groundPoundTimer = 0f;
                }
                else
                {
                    NPC.velocity.Y = 16f;
                }
                break;

            case 4: // Recovery
                NPC.velocity.X *= 0.85f;
                if (groundPoundTimer >= 22f)
                {
                    groundPoundPhase = 0;
                }
                break;
        }
    }

    private bool GroundBeneath()
    {
        int tileX = (int)(NPC.position.X / 16f);
        int tileEndX = (int)((NPC.position.X + NPC.width) / 16f);
        int tileY = (int)((NPC.position.Y + NPC.height) / 16f);

        for (int i = tileX; i <= tileEndX; i++)
        {
            Tile tile = Framing.GetTileSafely(i, tileY);
            if (tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType])
                return true;
        }
        return false;
    }

    private void SpawnSlamDust()
    {
        for (int i = 0; i < 18; i++)
        {
            Vector2 dustVel = Vector2.UnitX.RotatedBy(MathHelper.ToRadians(i * 20f)) * 6f;
            Dust dust = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.t_Slime,
                dustVel.X, dustVel.Y);
            dust.noGravity = true;
            dust.scale = 1.6f;
        }
    }

    private void HandleSquishScale()
    {
        const float MAX_STRETCH = 1.5f;
        const float MIN_SQUISH = 0.7f;

        float yVelFactor = MathHelper.Clamp(NPC.velocity.Y / 16f, -1f, 1f);

        if (groundPoundPhase == 0 && NPC.velocity.Y == 0f && NPC.ai[0] >= -30f)
        {
            float prep = Math.Min(1f, (NPC.ai[0] + 30f) / 30f);
            squishScale.X = MathHelper.Lerp(1f, 1.3f, prep);
            squishScale.Y = MathHelper.Lerp(1f, 0.92f, prep);
            return;
        }

        if (groundPoundPhase == 1)
        {
            float prep = Math.Min(1f, groundPoundTimer / 28f);
            squishScale.X = MathHelper.Lerp(1f, 1.45f, prep);
            squishScale.Y = MathHelper.Lerp(1f, 0.6f, prep);
            return;
        }

        if (NPC.velocity.Y < 0f)
        {
            squishScale.Y = MathHelper.Lerp(1f, MAX_STRETCH, -yVelFactor);
            squishScale.X = MathHelper.Lerp(1f, MIN_SQUISH, -yVelFactor);
        }
        else if (NPC.velocity.Y > 0f)
        {
            squishScale.Y = MathHelper.Lerp(1f, MIN_SQUISH, yVelFactor);
            squishScale.X = MathHelper.Lerp(1f, MAX_STRETCH, yVelFactor);
        }
        else
        {
            squishScale = Vector2.Lerp(squishScale, Vector2.One, 0.15f);
        }
    }

    public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        Vector2 origin = NPC.frame.Size() / 2f;
        Vector2 drawPos = NPC.Center - screenPos + new Vector2(0f, NPC.gfxOffY);

        Color color = drawColor * 0.85f;

        DrawMushroomChunks(drawPos, color);

        return false;
    }

    public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        Texture2D texture = TextureAssets.Npc[NPCID.BlueSlime].Value;
        Vector2 origin = NPC.frame.Size() / 2f;
        Vector2 drawPos = NPC.Center - screenPos + new Vector2(0f, NPC.gfxOffY);

        SpriteEffects effects = NPC.spriteDirection == 1
            ? SpriteEffects.None
            : SpriteEffects.FlipHorizontally;

        Color finalColor = NPC.color;
        finalColor.R = (byte)((finalColor.R * drawColor.R) / 255);
        finalColor.G = (byte)((finalColor.G * drawColor.G) / 255);
        finalColor.B = (byte)((finalColor.B * drawColor.B) / 255);
        finalColor.A = (byte)175;

        Main.EntitySpriteDraw(texture, drawPos, NPC.frame, finalColor, NPC.rotation,
            origin, NPC.scale * squishScale, effects);
    }

    private void DrawMushroomChunks(Vector2 center, Color drawColor)
    {
        Asset<Texture2D>[] chunkAssets = new[]
        {
            AssetReferences.Content.Fungore.Sporeclot_Gore_1.Asset,
            AssetReferences.Content.Fungore.Sporeclot_Gore_2.Asset,
            Main.Assets.Request<Texture2D>($"Images/Item_{ItemID.Mushroom}"),
        };

        foreach (var asset in chunkAssets)
        {
            if (!asset.IsLoaded)
                return;
        }

        Vector2 origin = chunkAssets[0].Value.Size() / 2f;
        float radius = NPC.height * 0.62f;

        for (int i = 0; i < MUSHROOM_CHUNK_COUNT; i++)
        {
            int textureIndex = (int)((i + NPC.frameCounter / 10) % chunkAssets.Length);
            Texture2D texture = chunkAssets[textureIndex].Value;

            float angle = MathHelper.TwoPi * i / MUSHROOM_CHUNK_COUNT;
            Vector2 dir = angle.ToRotationVector2();
            Vector2 offset = new Vector2(dir.X * squishScale.X, dir.Y * squishScale.Y) * radius;
            float rotation = angle + (squishScale.Y - squishScale.X) * 0.4f;

            Main.EntitySpriteDraw(texture, center + offset, null,
                drawColor, rotation, origin, NPC.scale * 0.75f, SpriteEffects.None);
        }
    }
    public override void OnKill()
    {
        for (int i = 0; i < 12; i++)
        {
            Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.t_Slime, Scale: 1f);
        }

        for (int j = 0; j < 2; j++)
        {
            ParticleSystem.Spawn<SporeclotParticle>(NPC.position, NPC.oldVelocity, 60);
        }
    }
}

public class SporadicSlamProjectile : ModProjectile
{
    public override string Texture => Assets.Textures.NullTex.KEY;

    public override void SetDefaults()
    {
        base.SetDefaults();
        Projectile.hostile = true;
        Projectile.damage = 5;
        Projectile.DamageType = DamageClass.Melee;

        Projectile.tileCollide = false;
        Projectile.aiStyle = -1;
        Projectile.width = 86;
        Projectile.height = 32;
        Projectile.timeLeft = 28;
    }
    public override void OnKill(int timeLeft)
    {
        base.OnKill(timeLeft);
        Dust.QuickDustSmall(Projectile.Center, Color.White);
    }
}