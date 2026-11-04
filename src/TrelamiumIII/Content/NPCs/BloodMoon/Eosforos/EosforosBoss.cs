using Terraria.Localization;
using TrelamiumIII.Core.Graphics.Cinematics.BossIntros;
using TrelamiumIII.Core.Graphics.Cinematics.Camera;
using TrelamiumIII.Core.NPCs;

namespace TrelamiumIII.Content.NPCs.BloodMoon.Eosforos;

[AutoloadBossHead]
public class EosforosBoss : ModNPC
{
    public enum EosforosState
    {
        Intro,
        Idle,
        Walk,
        Fly,
        BloodHex,
        Necromancy,
        StaffIdle, // floats in the air with his staff and manifests blood magic
        StaffCast,
        Death
    }

    public EosforosState State
    {
        get => (EosforosState)(int)NPC.ai[0];
        private set => NPC.ai[0] = (int)value;
    }

    public ref float StateTimer => ref NPC.ai[1];

    private Player Target => Main.player[NPC.target];

    private float LifeFraction => NPC.lifeMax <= 0 ? 1f : NPC.life / (float)NPC.lifeMax;

    private const float PHASE_TWO_LIFE = 0.72f;
    private const float PHASE_THREE_LIFE = 0.5f;
    private const float PHASE_FOUR_LIFE = 0.15f;

    private int Phase => LifeFraction switch
    {
        > PHASE_TWO_LIFE => 1,
        > PHASE_THREE_LIFE => 2,
        > PHASE_FOUR_LIFE => 3,
        _ => 4,
    };
    private bool introStarted;
    private bool cameraHeld;
    private const int INTRO_TICKS = 120;

    public override void SetStaticDefaults()
    {
        base.SetStaticDefaults();
    }

    public override void SetDefaults()
    {
        NPC.boss = true;

        NPC.gfxOffY = -2f;
        NPC.width = 20;
        NPC.height = 42;

        NPC.damage = 20;
        NPC.defense = 28;
        NPC.lifeMax = 1550;

        NPC.knockBackResist = 0f;
        NPC.aiStyle = -1;

        Music = Assets.Music.Eosforos.EosforosTheme.Slot;
    }

    public override void AI()
    {
        Main.NewText($"State: {State}, tick: {StateTimer}");

        switch (State)
        {
            case EosforosState.Intro:
                StartIntro();
                break;
            case EosforosState.Idle:
                UpdateIdle();
                break;
        }
    }

    private void UpdateIdle()
    {
        if (StateTimer > 0)
            StateTimer++;
    }

    private void StartIntro()
    {
        if (Main.dedServ || introStarted)
            return;

        introStarted = true;

        if (State != EosforosState.Intro || StateTimer > INTRO_TICKS)
           return;

        cameraHeld = true;
        CameraSystem.MoveCameraOut(INTRO_TICKS, NPC.Center);
        BossIntroSystem.Play(CreateIntro());

        StateTimer++;
        if (StateTimer >= INTRO_TICKS)
        {
            State = EosforosState.Idle;
            StateTimer = 0;
        }
    }

    private void Despawn()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        NPC.active = false;

        if (Main.netMode != NetmodeID.Server)
            return;

        NPC.netSkip = -1;
        NPC.life = 0;

        NetMessage.SendData(MessageID.SyncNPC, number: NPC.whoAmI);
    }

    private BossIntro CreateIntro() => new()
    {
        Name = Lang.GetNPCNameValue(ModContent.NPCType<EosforosBoss>()),
        Epithet = Language.GetTextValue($"Mods.{nameof(TrelamiumIII)}.NPCs.{nameof(EosforosBoss)}.Epithet"),

        TrackTitle = "Effusive Eosforos",
        TrackArtist = "wymsical",

        Accent = new Color(255, 38, 44),

        RevealTick = INTRO_TICKS,

        AutoSettle = true,
        OutroTicks = 80,

        BossIndex = NPC.whoAmI,
        OnAbandon = ReleaseCamera,
    };

    private void ReleaseCamera()
    {
        if (!cameraHeld)
            return;

        cameraHeld = false;
        CameraSystem.ReturnCamera(30);
    }

    public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
    {
        base.ApplyDifficultyAndPlayerScaling(numPlayers, balance, bossAdjustment);
    }

    public override bool CheckActive()
    {
        return base.CheckActive();
        // despawn method.
    }
    public override bool CheckDead()
    {
        return base.CheckDead();
        // Todo: death scene, dialogue, world state.
    }
}
