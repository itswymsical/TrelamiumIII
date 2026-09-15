using Terraria.DataStructures;
using Terraria.GameContent.Bestiary;

namespace TrelamiumIII.Content.Ingress.NPCs;

public class Bushwhack : ModNPC
{
    private const int FrameCount = 4;
    private const int AirFrame = 2;
    private const float IdleSpeed = 0.1f;

    private const float StrideLength = 5f;

    private const float WalkSpeed = 0.8f;
    private const float Traction = 0.1f;
    private const float AirTraction = 0.02f;
    private const float HopSpeed = 5f;

    private const int HopHeightTiles = 2;

    private const int DropTilesMax = 3;

    private const int WalkTicksMin = 120;
    private const int WalkTicksMax = 420;
    private const int IdleTicksMin = 60;
    private const int IdleTicksMax = 240;
    private const float TurnOnWalkChance = 0.5f;
    private const int TurnCooldownTicks = 40;
    private const int StuckTicksMax = 45;

    private const float SpawnWeight = 0.3f;
    private const int AliveMax = 8;

    private bool Walking
    {
        get => NPC.ai[0] == 1f;
        set => NPC.ai[0] = value ? 1f : 0f;
    }

    private ref float StateTimer => ref NPC.ai[1];

    private ref float StuckTimer => ref NPC.ai[2];

    private ref float TurnCooldown => ref NPC.ai[3];

    public override void SetStaticDefaults()
    {
        Main.npcFrameCount[Type] = FrameCount;

        NPCID.Sets.CountsAsCritter[Type] = true;
        NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, new NPCID.Sets.NPCBestiaryDrawModifiers { Velocity = 1f });
    }

    public override void SetDefaults()
    {
        NPC.width = 22;
        NPC.height = 24;
        NPC.aiStyle = -1;
        NPC.damage = 0;
        NPC.defense = 2;
        NPC.lifeMax = 30;
        NPC.knockBackResist = 0.8f;
        NPC.chaseable = false;
        NPC.HitSound = SoundID.Grass;
        NPC.DeathSound = SoundID.Grass;

        DrawOffsetY = -4f;

        SpawnModBiomes = [ModContent.GetInstance<IngressGardenBiome>().Type];
    }

    public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
    {
        bestiaryEntry.Info.Add(new FlavorTextBestiaryInfoElement(this.GetLocalizationKey("Bestiary")));
    }

    public override float SpawnChance(NPCSpawnInfo spawnInfo)
    {
        if (!spawnInfo.Player.InModBiome<IngressGardenBiome>() || spawnInfo.Water || spawnInfo.SpawnTileY <= Main.worldSurface)
            return 0f;

        if (spawnInfo.SpawnTileType != ModContent.TileType<IngressGrass>() || NPC.CountNPCS(Type) >= AliveMax)
            return 0f;

        return SpawnWeight;
    }

    public override void OnSpawn(IEntitySource source)
    {
        NPC.direction = Main.rand.NextBool() ? 1 : -1;
        StateTimer = Main.rand.Next(IdleTicksMin, IdleTicksMax + 1);
        NPC.netUpdate = true;
    }

    public override void AI()
    {
        bool grounded = NPC.velocity.Y == 0f;

        if (Main.netMode != NetmodeID.MultiplayerClient)
            UpdateState(grounded);

        float targetSpeed = Walking ? NPC.direction * WalkSpeed : 0f;
        NPC.velocity.X = MathHelper.Lerp(NPC.velocity.X, targetSpeed, grounded ? Traction : AirTraction);

        if (!Walking || !grounded)
            return;

        Collision.StepUp(ref NPC.position, ref NPC.velocity, NPC.width, NPC.height, ref NPC.stepSpeed, ref NPC.gfxOffY);

        if (NPC.collideX && !TooTallToHop(FrontTile()))
            NPC.velocity.Y = -HopSpeed;
    }

    private void UpdateState(bool grounded)
    {
        if (TurnCooldown > 0f)
            TurnCooldown--;

        if (--StateTimer <= 0f)
        {
            if (Walking)
                Stand();
            else
                Walk();

            return;
        }

        if (!Walking || !grounded)
        {
            StuckTimer = 0f;
            return;
        }

        StuckTimer = NPC.collideX ? StuckTimer + 1f : 0f;

        Point front = FrontTile();
        bool blocked = WaterAhead(front) || DropAhead(front) || (NPC.collideX && TooTallToHop(front)) || StuckTimer > StuckTicksMax;

        if (!blocked)
            return;

        if (TurnCooldown > 0f)
            Stand();
        else
            TurnAround();
    }

    private void Walk()
    {
        Walking = true;
        StateTimer = Main.rand.Next(WalkTicksMin, WalkTicksMax + 1);

        if (Main.rand.NextFloat() < TurnOnWalkChance)
            NPC.direction = -NPC.direction;

        NPC.netUpdate = true;
    }

    private void Stand()
    {
        Walking = false;
        StateTimer = Main.rand.Next(IdleTicksMin, IdleTicksMax + 1);
        StuckTimer = 0f;
        NPC.netUpdate = true;
    }

    private void TurnAround()
    {
        NPC.direction = -NPC.direction;
        TurnCooldown = TurnCooldownTicks;
        StuckTimer = 0f;
        NPC.netUpdate = true;
    }

    private Point FrontTile()
    {
        float x = NPC.direction > 0 ? NPC.Right.X + 1f : NPC.Left.X - 1f;
        return new Point((int)(x / 16f), (int)((NPC.Bottom.Y - 1f) / 16f));
    }

    private static bool TooTallToHop(Point front) => WorldGen.SolidOrSlopedTile(Framing.GetTileSafely(front.X, front.Y - HopHeightTiles));

    private static bool WaterAhead(Point front) =>
        Framing.GetTileSafely(front.X, front.Y).LiquidAmount > 0 || Framing.GetTileSafely(front.X, front.Y + 1).LiquidAmount > 0;

    private static bool DropAhead(Point front)
    {
        for (int y = front.Y; y <= front.Y + 1 + DropTilesMax; y++)
        {
            Tile tile = Framing.GetTileSafely(front.X, y);

            if (tile.HasUnactuatedTile && (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType]))
                return false;
        }

        return true;
    }

    public override void FindFrame(int frameHeight)
    {
        NPC.spriteDirection = -NPC.direction;

        if (NPC.velocity.Y != 0f)
        {
            NPC.frame.Y = AirFrame * frameHeight;
            return;
        }

        if (Math.Abs(NPC.velocity.X) < IdleSpeed)
        {
            NPC.frame.Y = 0;
            NPC.frameCounter = 0;
            return;
        }

        NPC.frameCounter += Math.Abs(NPC.velocity.X);

        if (NPC.frameCounter >= StrideLength)
        {
            NPC.frameCounter -= StrideLength;
            NPC.frame.Y = (NPC.frame.Y / frameHeight + 1) % FrameCount * frameHeight;
        }
    }

    public override void HitEffect(NPC.HitInfo hit)
    {
        int count = NPC.life > 0 ? 3 : 14;

        for (int i = 0; i < count; i++)
            Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.GrassBlades, hit.HitDirection * 1.5f, -1f);
    }
}
