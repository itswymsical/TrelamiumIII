using System.Collections.Generic;
using TrelamiumIII.Content.Misc;
using TrelamiumIII.Core.NPCs.Actors;
using TrelamiumIII.Core.NPCs.Controllers;
using TrelamiumIII.Core.NPCs.Controllers.Modules;

namespace TrelamiumIII.Content.NPCs.Testing;

/// <summary>
/// A world NPC that exists only to exercise the puppet and director in-game.
///
/// <para>
/// Every mode below maps to one thing that can break: whether an idle weapon
/// renders, whether a swing poses the arm, whether a projectile leaves the right
/// hand at the right angle, whether the combat loop closes distance and disengages,
/// whether the NPC finds its way home. Cycle modes with the chat button, or drive it
/// from <c>/wnpc</c>.
/// </para>
///
/// <para>
/// Never spawns naturally - <see cref="SpawnChance"/> is zero and it neither
/// despawns nor persists, so a test subject stays exactly where it was put and
/// leaves nothing behind in the world file.
/// </para>
/// </summary>
public class TestTerrarian : WorldNPC
{
    public enum TestMode
    {
        /// <summary>Stand at the anchor. Baseline: walk cycle, idle pose, facing.</summary>
        Idle,

        /// <summary>Stand holding a drawn sword. Checks the idle held-item render.</summary>
        Armed,

        /// <summary>Swing a sword at the nearest player on a loop. Checks use-style pose and arm compositing.</summary>
        Swing,

        /// <summary>Cast a staff at the nearest player. Checks projectile origin, aim and damage flags.</summary>
        Cast,

        /// <summary>Full combat loop against hostile NPCs. Checks approach, strike range and disengage.</summary>
        Fight,

        /// <summary>Turns on you with a sword. Checks that melee hitboxes actually land on a player.</summary>
        Hostile,

        /// <summary>Turns on you with a bow. Checks ammo pick, projectile ownership and hostile flags.</summary>
        HostileRanged,

        /// <summary>Trail the nearest player. Checks pathing, ledge jumps and gap jumps.</summary>
        Follow,

        /// <summary>Talking loop. Checks the dialogue pose and emote timing.</summary>
        Talk,

        /// <summary>Walk to the anchor from wherever it is. Checks travel and arrival.</summary>
        GoHome,
    }

    private const int MeleeWeapon = ItemID.CopperBroadsword;
    private const int RangedWeapon = ItemID.AmberStaff;
    private const int BowWeapon = ItemID.WoodenBow;

    /// <summary>True while a mode has flipped the NPC to an enemy, so it can be flipped back.</summary>
    private bool turnedHostile;

    /// <summary>Current test mode. The idle selector re-reads this every tick, so it can change live.</summary>
    public TestMode Mode { get; set; } = TestMode.Idle;

    public override bool CanDespawn => false;

    public override bool Persistent => false;

    protected override PuppetAllegiance Allegiance => PuppetAllegiance.Ally;

    public override List<string> SetNPCNameList() => ["Test Subject"];

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
    }

    /// <summary>Never spawns on its own; spawn it with <c>/wnpc</c>.</summary>
    public override float SpawnChance(NPCSpawnInfo spawnInfo) => 0f;

    public override string GetChat()
        => $"Mode: {Mode}. Anchor: {AnchorTile.X}, {AnchorTile.Y}. Holding: {(HasValidWeapon ? ActiveWeapon.Name : "nothing")}.";

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

    /// <summary>
    /// One selector, re-evaluated every tick, so switching modes swaps the running
    /// module immediately instead of waiting for the current one to finish.
    /// </summary>
    protected override IBehaviorModule CreateAnchorIdleModule() => new Selector()
        .When(_ => Mode == TestMode.Armed, ArmedModule)
        .When(_ => Mode == TestMode.Swing, SwingModule)
        .When(_ => Mode == TestMode.Cast, CastModule)
        .When(_ => Mode == TestMode.Fight, FightModule)
        .When(_ => Mode == TestMode.Hostile, HostileMeleeModule)
        .When(_ => Mode == TestMode.HostileRanged, HostileRangedModule)
        .When(_ => Mode == TestMode.Follow, FollowModule)
        .When(_ => Mode == TestMode.Talk, TalkModule)
        .When(_ => Mode == TestMode.GoHome, GoHomeModule)
        .Otherwise(IdleModule);

    private IBehaviorModule IdleModule()
    {
        BecomeFriendly();
        Puppet.PresentHeldItem = false;
        Puppet.Hold(ItemID.None);

        return new Stay().At(AnchorTile).WithTolerance(2);
    }

    private IBehaviorModule HostileMeleeModule()
    {
        BecomeHostile();
        Puppet.PresentHeldItem = true;

        return AttackWith.Players(MeleeWeapon, 700f)
            .WithRanges(preferred: 36f, strike: 60f)
            .WithCooldown(14)
            .WithSpeed(2.75f)
            .Standing();
    }

    private IBehaviorModule HostileRangedModule()
    {
        BecomeHostile();
        Puppet.PresentHeldItem = false;
        Puppet.SetAmmo(ItemID.WoodenArrow);

        // Holds its distance and shoots, which is what separates a ranged stance from
        // a melee one: preferred range is where it wants to stand, strike range is
        // how far the weapon reaches.
        return AttackWith.Players(BowWeapon, 800f)
            .WithRanges(preferred: 220f, strike: 600f)
            .WithRetreat(140f)
            .WithCooldown(30)
            .Standing();
    }

    /// <summary>
    /// Flips the subject to an enemy: its attacks can hurt players, and players can
    /// hurt it back. The base class makes a non-despawning world NPC immortal, which
    /// would otherwise make a fight one-sided and untestable.
    /// </summary>
    private void BecomeHostile()
    {
        if (turnedHostile)
            return;

        turnedHostile = true;
        Puppet.Allegiance = PuppetAllegiance.Enemy;

        NPC.friendly = false;
        NPC.immortal = false;
        NPC.dontTakeDamageFromHostiles = false;
    }

    private void BecomeFriendly()
    {
        if (!turnedHostile)
            return;

        turnedHostile = false;
        Puppet.Allegiance = Allegiance;

        NPC.friendly = true;
        NPC.immortal = true;
        NPC.dontTakeDamageFromHostiles = true;
    }

    private IBehaviorModule ArmedModule()
    {
        BecomeFriendly();
        Puppet.PresentHeldItem = true;
        Puppet.Hold(MeleeWeapon);

        return new Parallel(
            new Stay().At(AnchorTile).WithTolerance(2),
            new FaceNearestPlayer().WithRange(400f))
        { RequireAll = false };
    }

    private IBehaviorModule SwingModule()
    {
        BecomeFriendly();
        Puppet.PresentHeldItem = false;

        // Ally allegiance can't hurt players, so this is a pure animation test even
        // though it swings straight at you.
        return Behavior.Forever(() => Behavior.Seq(
            new UseItem(MeleeWeapon, ctx => ctx.NearestPlayer(600f)?.Center),
            Behavior.Pause(24)));
    }

    private IBehaviorModule CastModule()
    {
        BecomeFriendly();
        Puppet.PresentHeldItem = false;

        return Behavior.Forever(() => Behavior.Seq(
            new UseItem(RangedWeapon, ctx => ctx.NearestPlayer(800f)?.Center).Requiring(),
            Behavior.Pause(40)));
    }

    private IBehaviorModule FightModule()
    {
        BecomeFriendly();
        Puppet.PresentHeldItem = true;

        return AttackWith.Hostiles(MeleeWeapon, 500f)
            .WithRanges(preferred: 40f, strike: 64f)
            .WithCooldown(18)
            .Standing();
    }

    private IBehaviorModule FollowModule()
    {
        BecomeFriendly();
        Puppet.PresentHeldItem = false;
        Puppet.Hold(ItemID.None);

        return Follow.NearestPlayer().WithDistance(64f).WithSpeed(2.5f);
    }

    private IBehaviorModule TalkModule()
    {
        BecomeFriendly();
        Puppet.PresentHeldItem = false;
        Puppet.Hold(ItemID.None);

        return new Talk().WithEmotes(90);
    }

    private IBehaviorModule GoHomeModule()
    {
        BecomeFriendly();
        Puppet.PresentHeldItem = false;

        return Behavior.Seq(
            new TravelTo(AnchorTile.ToWorldCoordinates()).WithArrival(24f),
            Behavior.Once(_ => Mode = TestMode.Idle));
    }
}
