using System.Collections.Generic;
using Terraria.ModLoader.IO;
using TrelamiumIII.Content.Items.Quest;
using TrelamiumIII.Core.Audio;
using TrelamiumIII.Core.Dialogue;
using TrelamiumIII.Core.Events;
using TrelamiumIII.Core.Jobs;
using TrelamiumIII.Core.Jobs.Blueprints;

namespace TrelamiumIII.Content.Jobs;

/// <summary>
///     The Guide's opening job: gather what a house takes, raise his house and then the Merchant's from his plans near
///     spawn, build one of the player's own design, then check in.
/// </summary>
/// <remarks>
///     <para>
///         Each house is a stage that stakes it, with <see cref="GuidesPlans"/>, and a stage that builds it: frame,
///         walls and furniture together. A finished house gets its tenant (<see cref="BlueprintHousing"/>): the Guide
///         moves into his, and the Merchant arrives for his.
///     </para>
///     <para>
///         The materials are only held, not handed in, since they're what the houses are built from. The houses are
///         <c>Assets/Structures/guidehome</c> and <c>MerchantHouse</c>; a structure's name here has to match its file's
///         capitalisation exactly.
///     </para>
/// </remarks>
public sealed class HomeForager : Job
{
    private const int GuideSiteSet = 1;
    private const int GuideHouseSet = 2;
    private const int MerchantSiteSet = 3;
    private const int MerchantHouseSet = 4;

    /// <summary>A house of the player's own: the only stage that runs the Guide's housing commentary.</summary>
    private const int OwnHouseSet = 5;

    /// <summary>The last set: go back to the Guide and hand the job in.</summary>
    private const int CheckInSet = 6;

    /// <summary>The Guide is a talker, so he gets a shorter leash than the default.</summary>
    protected override int SpeechCooldown => 180;

    private readonly Blueprint housing1;
    private readonly Blueprint housing2;

    /// <summary>Kept so <see cref="OnEvent"/> can ask which rooms have already counted.</summary>
    private readonly ValidHousingTask housingTask;

    public HomeForager() : this(Plans("HomeForagerHousing"), Plans("HomeForagerHousing")) { }


    private HomeForager(Blueprint housing1, Blueprint housing2) : base(
        JobID.HomeForager, "HomeForager.Name", "HomeForager.Description",
        taskList:
        [
            [
                new PickupItemTask("HomeForager.Tasks.Set0.obj0", ItemID.Wood, 100),
                new PickupItemTask("HomeForager.Tasks.Set0.obj1", ItemID.StoneBlock, 30),
                new CraftItemTask("HomeForager.Tasks.Set0.obj2", ItemID.Furnace),
                new CraftItemTask("HomeForager.Tasks.Set0.obj3", ItemID.WoodenHammer)
            ],
            [new StakeBlueprintTask("HomeForager.Tasks.Set1.obj0", housing1)],
            Build(housing1, "Set2"),
            [new StakeBlueprintTask("HomeForager.Tasks.Set3.obj0", housing2)],
            Build(housing2, "Set4"),
            [new ValidHousingTask("HomeForager.Tasks.Set5.obj0")],
            [new TalkToNPCTask("HomeForager.Tasks.Set6.obj0", NPCID.Guide)]
        ],
        rewards:
        [
            new JobReward(ItemID.GoldCoin, 5),
            new JobReward(ItemID.IronBar, 5)
        ],
        providerNPC: NPCID.Guide)
    {
        this.housing1 = housing1;
        this.housing2 = housing2;

        // The planned houses pass a housing check too; a house of their own means one that isn't.
        housingTask = ((ValidHousingTask)TaskList[OwnHouseSet].Task[0]).Excluding(IsPlanned);
    }

    private static Blueprint Plans(string structure) => new($"Assets/Structures/{structure}") { Zone = BlueprintZone.NearSpawn() };

    /// <summary>One stage that builds all of <paramref name="house"/> at once, each layer its own line on the tracker.</summary>
    private static List<JobTask> Build(Blueprint house, string set) =>
    [
        new BlueprintTask($"HomeForager.Tasks.{set}.obj0", house, BlueprintLayer.Blocks),
        new BlueprintTask($"HomeForager.Tasks.{set}.obj1", house, BlueprintLayer.Walls),
        new BlueprintTask($"HomeForager.Tasks.{set}.obj2", house, BlueprintLayer.Furniture)
    ];

    /// <summary>Whether <paramref name="room"/> is inside either house built from the Guide's plans.</summary>
    private bool IsPlanned(Point room) => Covers(housing1, room) || Covers(housing2, room);

    private static bool Covers(Blueprint house, Point room) => house.Site is { } site && house.AreaAt(site).Contains(room);

    /// <summary>The opening job, so it is offered from the world's first day.</summary>
    public override JobCondition UnlockCondition => JobCondition.WorldDay(0);

    public override void OnUnlocked()
    {
        if (Context.IsLocal)
            DialogueManager.Instance.StartDialogue("Guide.HomeForager");
    }

    public override void OnJobStart()
    {
        Context.Play("Guide.HomeForager_Greeting", zoomIn: true);

        //if (Context.IsLocal)
        //    MusicDirector.Play(HomeForagerMusic.Create(), fadeIn: 0.35f);
    }

    public override void OnJobComplete(bool giveRewards = true)
    {
        base.OnJobComplete(giveRewards);

        if (Context.IsLocal)
            Context.Revoke(ModContent.ItemType<GuidesPlans>());

        //if (Context.IsLocal)
        //    MusicDirector.Stop(1.5f);
    }

    public override void OnSetAdvanced(int previousSet, int newSet)
    {
        if (Context.IsLocal)
        {
            // A finished house gets its tenant before the Guide says anything about it.
            if (previousSet == GuideHouseSet)
                BlueprintHousing.MoveIn(NPCID.Guide, housing1);
            else if (previousSet == MerchantHouseSet)
                BlueprintHousing.MoveIn(NPCID.Merchant, housing2);

            if (newSet is GuideSiteSet or MerchantSiteSet)
                HandOverPlans();
        }

        Context.Play($"Guide.HomeForager_Set{newSet}");
    }

    /// <summary>Gives the player the Guide's plans for a staking stage, unless they're still carrying a copy.</summary>
    private void HandOverPlans()
    {
        int plans = ModContent.ItemType<GuidesPlans>();

        if (!Context.Player.HasItem(plans))
            Context.Grant(plans);

        if (!Context.Player.HasItem(ItemID.BuilderPotion))
            Context.Grant(ItemID.BuilderPotion);
    }

    /// <summary>
    /// The Guide's building commentary, while the player builds their own house. Runs before the task sees the event, so
    /// "already credited" means a re-shown room.
    /// </summary>
    public override void OnEvent(GameEvent evt)
    {
        // Manual checks only: vanilla re-scans housing constantly on its own.
        if (evt.Type != GameEventType.HousingChecked || !evt.IsManualCheck || CurrentTaskSet != OwnHouseSet)
            return;

        if (evt.RoomId is Point planned && IsPlanned(planned))
        {
            Speak("Guide.HomeForager_Planned");
            return;
        }

        if (evt.Issue != HousingIssue.None)
        {
            Speak(DialogueFor(evt.Issue));
            return;
        }

        if (evt.RoomId is Point room && housingTask.HasCredited(room))
            Speak("Guide.HomeForager_Repeat");
    }

    public override ChatEncounter GetChatEncounter(NPC npc, Player player) => CurrentTaskSet switch
    {
        CheckInSet => ChatEncounter.Play("Guide.HomeForager_CheckIn"),

        // Nothing registered for the other sets yet, so the Guide keeps his ordinary greeting.
        _ => ChatEncounter.None
    };

    /// <summary>The Guide's line for each way a room can come back unusable.</summary>
    private static string DialogueFor(HousingIssue issue) => issue switch
    {
        HousingIssue.Occupied => "Guide.HomeForager_Occupied",
        HousingIssue.SolidTile => "Guide.HomeForager_SolidTile",
        HousingIssue.TooSmall => "Guide.HomeForager_TooSmall",
        HousingIssue.NotEnclosed => "Guide.HomeForager_NotEnclosed",
        HousingIssue.NeedsDoor => "Guide.HomeForager_NeedsDoor",
        HousingIssue.NeedsLight => "Guide.HomeForager_NeedsLight",
        HousingIssue.NeedsTable => "Guide.HomeForager_NeedsTable",
        HousingIssue.NeedsChair => "Guide.HomeForager_NeedsChair",
        HousingIssue.Corrupt => "Guide.HomeForager_Corrupt",
        _ => "Guide.HomeForager_NoValidSpot"
    };
}

/// <summary>
/// Build a room that passes a housing check and hasn't been shown before. The credited-rooms ledger can be shared
/// across sibling tasks, so re-checking one house can't clear several sets.
/// </summary>
public sealed class ValidHousingTask(string key) : JobTask(key, 1)
{
    private HashSet<Point> creditedRooms = [];

    /// <summary>Rooms that never count, by their <see cref="GameEvent.RoomId"/>. Null counts every room.</summary>
    private Func<Point, bool>? excluded;

    /// <summary>Points this task at a ledger shared with its siblings.</summary>
    public void ShareLedger(HashSet<Point> ledger) => creditedRooms = ledger;

    /// <summary>Leaves out the rooms <paramref name="rule"/> picks, such as ones the job had built to its own plans.</summary>
    public ValidHousingTask Excluding(Func<Point, bool> rule)
    {
        excluded = rule;
        return this;
    }

    public bool HasCredited(Point room) => creditedRooms.Contains(room);

    public override bool Matches(GameEvent evt)
        => evt.Type == GameEventType.HousingChecked
        && evt.Amount == 1                              // passed the full check
        && !evt.IsOccupied
        && evt.RoomId is Point room
        && !creditedRooms.Contains(room)                // and isn't one already counted
        && excluded?.Invoke(room) != true;

    public override int CreditFor(GameEvent evt)
    {
        if (evt.RoomId is Point room)
            creditedRooms.Add(room);

        return 1;
    }

    protected override void OnReset() => creditedRooms.Clear();

    public override void SaveData(TagCompound tag)
    {
        base.SaveData(tag);

        // Flattened to x, y, x, y -- TagCompound has no Point serializer.
        var rooms = new List<int>(creditedRooms.Count * 2);

        foreach (var room in creditedRooms)
        {
            rooms.Add(room.X);
            rooms.Add(room.Y);
        }

        tag["rooms"] = rooms;
    }

    public override void LoadData(TagCompound tag)
    {
        base.LoadData(tag);

        var rooms = tag.GetList<int>("rooms");

        for (var i = 0; i + 1 < rooms.Count; i += 2)
            creditedRooms.Add(new Point(rooms[i], rooms[i + 1]));
    }
}