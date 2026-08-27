using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent.Bestiary;
using Terraria.Localization;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Graphics.Cinematics.BossIntros;
using TrelamiumIII.Core.Graphics.Cinematics.Camera;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Content.WoodenWarden;

[AutoloadBossHead]
public class WoodenWardenBoss : ModNPC
{
    public enum WardenState
    {
        Hovering,
        Slamming,
        Recovering
    }
    private WardenState State
    {
        get => (WardenState)NPC.ai[0];
        set => NPC.ai[0] = (int)value;
    }
    public float StateTimer
    {
        get => NPC.ai[1];
        set => NPC.ai[1] = value;
    }

    private float slamTimer;
    private float recoveryTimer;

    private bool introStarted;
    private const int INTRO_TICKS = 60;

    private int bossDefense = 10;
    public override void SetStaticDefaults()
    {
        NPCID.Sets.BossBestiaryPriority.Add(Type);
        var drawModifiers = new NPCID.Sets.NPCBestiaryDrawModifiers()
        {
            CustomTexturePath = AssetReferences.Content.WoodenWarden.WardenBestiary.KEY,
            PortraitScale = 0.6f,
            PortraitPositionYOverride = 0f,
        };

        NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, drawModifiers);

        NPCID.Sets.TrailCacheLength[NPC.type] = 8;
        NPCID.Sets.TrailingMode[NPC.type] = 2;
    }
    public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
    {
        bestiaryEntry.Info.AddRange([ new MoonLordPortraitBackgroundProviderBestiaryInfoElement(),
                new FlavorTextBestiaryInfoElement(Mod.GetLocalizationValue("WoodenWardenBoss.Bestiary"))
        ]);
    }

    public override void SetDefaults()
    {
        NPC.damage = 12;
        NPC.defense = bossDefense;
        NPC.lifeMax = 2020;

        NPC.noGravity = true;
        NPC.noTileCollide = true;
        NPC.width = 200;
        NPC.height = 184;
        NPC.aiStyle = -1;
        AIType = -1;

        NPC.value = Item.buyPrice(gold: 4);
        NPC.boss = true;
        NPC.lavaImmune = true;
        NPC.knockBackResist = 0f;
        if (!Main.dedServ)
            Music = Assets.Music.WoodenWarden.Slot;
        
        NPC.HitSound = Assets.SFX.NPC.WoodHit2.Asset with
        {
            Volume = 1.4f,
            PitchVariance = 0.2f,
            MaxInstances = 3,
        };

        NPC.DeathSound = new SoundStyle($"{Assets.SFX.NPC.WardenDeath.KEY}" + Main.rand.Next(1, 3))
        {
            Volume = 0.9f,
            PitchVariance = 0.2f,
            MaxInstances = 3,
        };
    }

    public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
    {
        float damage = NPC.damage;

        if (Main.expertMode)
        {
            NPC.damage += (int)(damage * .2f);
            bossAdjustment = NPC.life;
            NPC.life += (int)(bossAdjustment * .03f);
        }
        if (Main.masterMode)
        {
            NPC.damage += (int)(damage * .35f);

            NPC.life += (int)(bossAdjustment * .12f);
            bossDefense = 18;
        }
    }

    public override void AI()
    {
        StartIntro();

        var target = Main.player[NPC.target];
        NPC.TargetClosest(true);
        var inactiveTimer = 0;
        inactiveTimer++;
        switch (State)
        {
            case WardenState.Hovering:
                HoverAbovePlayer(target);
                break;
            case WardenState.Slamming:
                ChargeSlam(target);
                break;
            case WardenState.Recovering:
                Recover();
                break;
        }

        SpawnArms();

        StateTimer++;

        if (NPC.AnyNPCs(ModContent.NPCType<WardenArm>()))
        {
            if (StateTimer >= 600 % NPC.lifeMax * 0.70f)
            {
                if (State == WardenState.Hovering && Main.player[NPC.target].Top.Y > NPC.Bottom.Y)
                {
                    State = WardenState.Slamming;
                }
                else if (State == WardenState.Recovering)
                {
                    State = WardenState.Hovering;
                }
                StateTimer = 0;
            }
        }
        else
        {
            if (StateTimer >= 300 % NPC.lifeMax * 0.20f)
            {
                if (State == WardenState.Hovering)
                {
                    State = WardenState.Slamming;
                }
                else if (State == WardenState.Recovering)
                {
                    State = WardenState.Hovering;
                }
                StateTimer = 0;
            }
            if (Main.masterMode)
            {
                if (StateTimer >= 260 % NPC.lifeMax * 0.16f)
                {
                    if (State == WardenState.Hovering)
                    {
                        State = WardenState.Slamming;
                    }
                    else if (State == WardenState.Recovering)
                    {
                        State = WardenState.Hovering;
                    }
                    StateTimer = 0;
                }
            }
        }
        if (target.velocity == Vector2.Zero && inactiveTimer > 90) //if they stay still slam down
        {
            State = WardenState.Slamming;
            inactiveTimer = 0;
        }
    }

    private void StartIntro()
    {
        if (Main.dedServ || introStarted)
            return;

        introStarted = true;
        // CameraSystem.MoveCameraOut(INTRO_TICKS, NPC.Center);

        if (State == WardenState.Hovering && StateTimer <= INTRO_TICKS)
        {
            BossIntroSystem.Play(CreateIntro());
        }
    }

    private static BossIntro CreateIntro() => new()
    {
        Name = Lang.GetNPCNameValue(ModContent.NPCType<WoodenWardenBoss>()),
        Epithet = Language.GetTextValue($"Mods.{nameof(TrelamiumIII)}.NPCs.{nameof(WoodenWardenBoss)}.Epithet"),

        TrackTitle = "Wooden Warden",
        TrackArtist = "wymsical",

        Accent = Color.YellowGreen,

        RevealTick = INTRO_TICKS,
    };

    private void HoverAbovePlayer(Player target)
    {
        var point = new Vector2(target.Center.X, target.Center.Y - 272);
        var speed = Vector2.Distance(NPC.Center, point);
        speed = MathHelper.Clamp(speed, -6f, 6f);
        Move(point, speed);
        NPC.rotation = NPC.rotation.AngleLerp(NPC.velocity.X * 0.01f, 0.07f);
    }

    private void Move(Vector2 position, float speed)
    {
        var direction = NPC.DirectionTo(position);

        var velocity = direction * speed;

        NPC.velocity = Vector2.SmoothStep(NPC.velocity, velocity, 0.12f);
    }

    private void ChargeSlam(Player target)
    {
        var tile = Framing.GetTileSafely(new Point((int)NPC.position.X / 16, (int)(NPC.position.Y + NPC.height) / 16));
        slamTimer++;
        if (Main.player[NPC.target].Top.Y > NPC.Bottom.Y)
        {
            NPC.noTileCollide = true;
        }
        else
        {
            NPC.noTileCollide = false;
        }
        if (slamTimer < 40) // Shake and rise for the first 60 ticks
        {
            NPC.position += new Vector2(Main.rand.Next(-2, 2), Main.rand.Next(-2, 2)); // Shake effect
            NPC.velocity.Y = -0.5f; // Slowly rise
            NPC.velocity.X = 0;
            NPC.rotation = 0;
        }
        else if (slamTimer < 360) // Rapidly descend
        {
            NPC.damage = 60;
            NPC.velocity.Y = 22f;
            if (NPC.collideY) // Check if NPC hits the ground
            {
                NPC.noTileCollide = false;
                CameraSystem.shake = 33;

                SoundEngine.PlaySound(Assets.SFX.NPC.WardenDeath_2.Asset with
                {
                    Volume = 0.9f,
                    PitchVariance = 0.2f,
                    MaxInstances = 3,
                });
                SpawnShockwaveProjectiles();
                Collision.HitTiles(NPC.position, NPC.velocity, NPC.width, NPC.height);
                slamTimer = 0;
                State = WardenState.Recovering; // New state for recovery period
            }
        }
    }

    private void SpawnShockwaveProjectiles()
    {
        var position = NPC.Hitbox.Bottom();
        Vector2 velocityLeft = new(-10f, 0f); // Adjust speed as needed
        Vector2 velocityRight = new(10f, 0f);

        Projectile.NewProjectile(default, position, velocityLeft, ProjectileID.TentacleSpike, 20, 1f);
        Projectile.NewProjectile(default, position, velocityRight, ProjectileID.TentacleSpike, 20, 1f);
    }

    private void Recover()
    {
        NPC.damage = 1;
        var plr = Main.player[NPC.target];
        if (recoveryTimer < 60) // Recovery period
        {
            NPC.velocity = Vector2.Zero; // Stop movement
        }
        else if (recoveryTimer >= 60 && recoveryTimer < 120) // Slowly return to hovering position
        {
            var hoverPosition = plr.Center - new Vector2(0, 200); // Adjust hover height as needed
            NPC.velocity = (hoverPosition - NPC.Center) * 0.05f; // Smoothly move to hover position
        }
        else
        {
            NPC.noTileCollide = true;
            recoveryTimer = 0;
            State = WardenState.Hovering;
        }
        recoveryTimer++;
    }

    private void SpawnArms()
    {
        if (NPC.localAI[0] == 0f)
        {
            var leftArmIndex = NPC.NewNPC(default, (int)NPC.Center.X - 100, (int)NPC.Center.Y, ModContent.NPCType<WardenArm>());
            var leftArm = Main.npc[leftArmIndex];
            leftArm.ai[3] = NPC.whoAmI;
            leftArm.localAI[0] = 0; // Set arm type to left

            var rightArmIndex = NPC.NewNPC(default, (int)NPC.Center.X + 100, (int)NPC.Center.Y, ModContent.NPCType<WardenArm>());
            var rightArm = Main.npc[rightArmIndex];
            rightArm.ai[3] = NPC.whoAmI;
            rightArm.localAI[0] = 1; // Set arm type to right

            NPC.localAI[0] = 1f; // Mark arms as spawned
        }
        if (!NPC.AnyNPCs(ModContent.NPCType<WardenArm>()) && NPC.localAI[3] == 1f)
        {
            var armType = Main.rand.Next(2); // 0 for left, 1 for right
            var armIndex = NPC.NewNPC(default, (int)NPC.Center.X, (int)NPC.Center.Y, ModContent.NPCType<WardenArm>());
            var arm = Main.npc[armIndex];
            arm.ai[1] = NPC.whoAmI;
            arm.localAI[0] = armType; // Set arm type

            if (armType == 0)
            {
                arm.localAI[1] = 1f; // Flag to prevent looping
            }
            else
            {
                arm.localAI[1] = 2f; // Flag to prevent looping
            }

            NPC.localAI[3] = 0f; // Reset spawn flag
        }
    }

    public override bool PreKill()
    {
        //DownedBossSystem.downedWarden = true;
        return base.PreKill();
    }

    private void CheckPlatform(Player player) // Spirit Mod :kek: - naka
    {
        var onplatform = true;
        for (var i = (int)NPC.position.X; i < NPC.position.X + NPC.width; i += NPC.height / 2)
        {
            var tile = Framing.GetTileSafely(new Point((int)NPC.position.X / 16, (int)(NPC.position.Y + NPC.height + 8) / 16));
            if (!TileID.Sets.Platforms[tile.TileType])
                onplatform = false;
        }
        if (onplatform && NPC.Center.Y < player.position.Y - 20)
            NPC.noTileCollide = true;
        else
            NPC.noTileCollide = false;
    }

    public static string SlamQuotes()
    {
        string[] OverloadQuotes =
        [
            " was flattened into a pancake.",
            "'s body was contorted into a twig.",
            " was crushed by massive amounts of wood.",
            "--... really? that's how you died?"
        ];
        var randomQuote = Main.rand.Next(OverloadQuotes.Length);

        return OverloadQuotes[randomQuote];
    }

    public override void ModifyHitPlayer(Player target, ref Player.HurtModifiers modifiers)
    {
        if (State == WardenState.Slamming)
        {
            modifiers.Knockback += 3f;
            target.Hurt(PlayerDeathReason.ByCustomReason(target.name + SlamQuotes()), 70, 0, knockback: 1f);

        }
        else if (State == WardenState.Recovering)
        {
            modifiers.Knockback += 3f;
            target.Hurt(PlayerDeathReason.ByCustomReason(target.name + " was sent flying by the Warden."), 25, 0, knockback: 1f);
        }
        else
        {
            modifiers.Knockback -= 3f;
        }
    }
}
