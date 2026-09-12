using System.Collections.Generic;
using TrelamiumIII.Core.NPCs.Actors;
using TrelamiumIII.Core.NPCs.Behaviors;
using TrelamiumIII.Core.NPCs.Combat;

namespace TrelamiumIII.Content.NPCs.Testing;

/// <summary>
/// A world NPC for exercising the actor and behaviour systems; each <see cref="TestMode"/> checks one thing that can break.
/// Cycle modes by talking to it or with <c>/wnpc</c>. Never spawns naturally, despawns or persists.
/// </summary>
public class TestTerrarian : TerrarianNPC
{
    public enum TestMode
    {
        /// <summary>Stand at the anchor. Baseline: walk cycle, idle pose, facing.</summary>
        Idle,

        /// <summary>Stand holding a drawn sword. Checks the idle held-item render.</summary>
        Armed,

        /// <summary>Swing a sword on a loop. Checks use-style pose and arm compositing.</summary>
        Swing,

        /// <summary>Cast a staff at you. Checks projectile origin, aim rotation and flags.</summary>
        Cast,

        /// <summary>Fight hostile NPCs. Checks approach, strike range and disengage.</summary>
        Fight,

        /// <summary>Turns on you with a sword. Checks melee hitboxes landing on a player.</summary>
        Hostile,

        /// <summary>Turns on you with a bow. Checks ammo pick, ownership and retreat.</summary>
        HostileRanged,

        /// <summary>Trail you. Checks pathing, ledge jumps and gap jumps.</summary>
        Follow,

        /// <summary>Talking loop. Checks the dialogue pose and emote timing.</summary>
        Talk,

        /// <summary>Walk home from wherever it is. Checks travel and arrival.</summary>
        GoHome,
    }

    private const int MeleeWeapon = ItemID.CopperBroadsword;
    private const int StaffWeapon = ItemID.AmberStaff;
    private const int BowWeapon = ItemID.WoodenBow;

    /// <summary>True while a mode has flipped the subject to an enemy.</summary>
    private bool turnedHostile;

    /// <summary>The script re-reads this every tick, so modes can change live.</summary>
    public TestMode Mode { get; set; } = TestMode.Idle;

    public override bool CanDespawn => false;

    public override bool Persistent => false;

    public override List<string> SetNPCNameList() => ["Test Subject"];

    /// <summary>Never spawns on its own; use <c>/wnpc spawn</c>.</summary>
    public override float SpawnChance(NPCSpawnInfo spawnInfo) => 0f;

    protected override void SetWorldNPCDefaults()
    {
        NPC.lifeMax = 5000;
        NPC.knockBackResist = 0f;
    }

    protected override void SetAppearanceDefaults()
    {
        Male = true;
        ClothingStyle = 0;
        HairType = 1;
        HairColor = new Color(220, 180, 90);
        SkinColor = new Color(255, 125, 90);
        EyeColor = new Color(80, 162, 224);
        ShirtColor = new Color(175, 90, 90);
        PantsColor = new Color(60, 70, 110);

        SetHeadSlot(ItemID.CopperHelmet);

        NPC.HitSound = SoundID.PlayerHit;
        NPC.DeathSound = SoundID.PlayerKilled;

        // Combat is opt-in: a world NPC has no attacks until something arms it.
        Puppet.ArmWith(PuppetAllegiance.Ally);
    }

    public override string GetChat()
        => $"Mode: {Mode}. Anchor: {AnchorTile.X}, {AnchorTile.Y}. Holding: {HeldItemName()}.";

    private string HeldItemName()
    {
        Item held = Puppet.HeldItem;
        return held.IsAir ? "nothing" : held.Name;
    }

    public override void SetChatButtons(ref string button, ref string button2)
    {
        button = "Next mode";
        button2 = "Reset anchor";
    }

    public override void OnChatButtonClicked(bool firstButton, ref string shop)
    {
        if (firstButton)
        {
            Mode = (TestMode)(((int)Mode + 1) % Enum.GetValues<TestMode>().Length);
            Main.NewText($"{NPC.GivenName}: {Mode}", Color.LightGreen);
            return;
        }

        SetAnchorTile(NPC.Center);
        Main.NewText($"{NPC.GivenName} anchored at {AnchorTile.X}, {AnchorTile.Y}.", Color.LightGreen);
    }

    /// <summary>One selector re-evaluated every tick, so mode switches take effect immediately.</summary>
    protected override IBehavior BuildScript() => Behavior.Select()
        .When(_ => Mode == TestMode.Armed, Armed)
        .When(_ => Mode == TestMode.Swing, Swing)
        .When(_ => Mode == TestMode.Cast, Cast)
        .When(_ => Mode == TestMode.Fight, FightHostiles)
        .When(_ => Mode == TestMode.Hostile, HostileMelee)
        .When(_ => Mode == TestMode.HostileRanged, HostileRanged)
        .When(_ => Mode == TestMode.Follow, FollowPlayer)
        .When(_ => Mode == TestMode.Talk, TalkForever)
        .When(_ => Mode == TestMode.GoHome, GoHome)
        .Otherwise(Idle);

    private IBehavior Idle()
    {
        Stance(hostile: false, presenting: false);
        Puppet.Hold(ItemID.None);

        return Behavior.Stay(AnchorTile);
    }

    private IBehavior Armed()
    {
        Stance(hostile: false, presenting: true);
        Puppet.Hold(MeleeWeapon);

        return Behavior.Stay(AnchorTile).FacingNearestPlayer();
    }

    private IBehavior Swing()
    {
        Stance(hostile: false, presenting: false);

        // Ally allegiance can't hurt players, so this is pure animation even though it
        // swings straight at you.
        return Behavior.Loop(() => Behavior.Sequence(
            Fight.Use(MeleeWeapon, ctx => ctx.NearestPlayer(600f)?.Center),
            Behavior.Wait(24)));
    }

    private IBehavior Cast()
    {
        Stance(hostile: false, presenting: false);

        return Behavior.Loop(() => Behavior.Sequence(
            Fight.Use(StaffWeapon, ctx => ctx.NearestPlayer(800f)?.Center).Requiring(),
            Behavior.Wait(40)));
    }

    private IBehavior FightHostiles()
    {
        Stance(hostile: false, presenting: true);

        return Fight.With(MeleeWeapon, 500f)
            .WithRanges(preferred: 40f, strike: 64f)
            .WithCooldown(18)
            .Standing();
    }

    private IBehavior HostileMelee()
    {
        Stance(hostile: true, presenting: true);

        return Fight.WithAgainstPlayers(MeleeWeapon, 700f)
            .WithRanges(preferred: 36f, strike: 60f)
            .WithCooldown(14)
            .WithSpeed(2.75f)
            .Standing();
    }

    private IBehavior HostileRanged()
    {
        Stance(hostile: true, presenting: false);
        Puppet.Combat.SetAmmo(ItemID.WoodenArrow);

        // Preferred range is where it wants to stand, strike range is how far the
        // weapon reaches, retreat range is how close it lets you get.
        return Fight.WithAgainstPlayers(BowWeapon, 800f)
            .WithRanges(preferred: 220f, strike: 600f)
            .WithRetreat(140f)
            .WithCooldown(30)
            .Standing();
    }

    private IBehavior FollowPlayer()
    {
        Stance(hostile: false, presenting: false);
        Puppet.Hold(ItemID.None);

        return Behavior.FollowNearestPlayer().WithDistance(64f).WithSpeed(2.5f);
    }

    private IBehavior TalkForever()
    {
        Stance(hostile: false, presenting: false);
        Puppet.Hold(ItemID.None);

        return Behavior.Talk().WithEmotes(90);
    }

    private IBehavior GoHome()
    {
        Stance(hostile: false, presenting: false);

        return Behavior.Sequence(
            Behavior.WalkTo(AnchorTile).WithArrival(24f),
            Behavior.Do(_ => Mode = TestMode.Idle));
    }

    /// <summary>
    /// Sets up a mode. Hostile modes clear <c>friendly</c> and <c>immortal</c>, since world NPCs are invulnerable by default.
    /// </summary>
    private void Stance(bool hostile, bool presenting)
    {
        Puppet.PresentHeldItem = presenting;

        if (hostile == turnedHostile)
            return;

        turnedHostile = hostile;

        Puppet.Combat.Allegiance = hostile ? PuppetAllegiance.Enemy : PuppetAllegiance.Ally;
        NPC.friendly = !hostile;
        NPC.immortal = !hostile;
        NPC.dontTakeDamageFromHostiles = !hostile;
    }
}
