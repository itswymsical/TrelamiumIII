using System.Collections.Generic;
using Terraria.ModLoader.IO;
using TrelamiumIII.Core.Audio;
using TrelamiumIII.Core.Dialogue;
using TrelamiumIII.Core.Events;
using TrelamiumIII.Core.Jobs;

namespace TrelamiumIII.Content.Jobs;

/// <summary>
/// The Guide walks the player through their first houses: gather the materials, then build three
/// rooms he'll actually approve of, then come back.
/// </summary>
public sealed class HomeForager : Job
{
    /// <summary>First and last set that asks for a house. Each credits one.</summary>
    private const int FirstHousingSet = 1;
    private const int LastHousingSet = 3;

    /// <summary>The last set: go back to the Guide and hand the job in.</summary>
    private const int CheckInSet = 4;

    /// <summary>The Guide is a talker, so he gets a shorter leash than the default.</summary>
    protected override int SpeechCooldown => 180;

    /// <summary>Kept so <see cref="OnEvent"/> can ask which rooms have already counted.</summary>
    private readonly ValidHousingTask housingTask;

    public HomeForager() : base(
        JobID.HomeForager, "HomeForager.Name", "HomeForager.Description",
        taskList:
        [
            [
                new BreakTileTask("HomeForager.Tasks.Set0.obj0", TileID.Trees, 40),
                new PickupItemTask("HomeForager.Tasks.Set0.obj1", ItemID.StoneBlock, 25)
            ],
            [new ValidHousingTask("HomeForager.Tasks.Set1.obj0")],
            [new ValidHousingTask("HomeForager.Tasks.Set2.obj0")],
            [new ValidHousingTask("HomeForager.Tasks.Set3.obj0")],
            [new TalkToNPCTask("HomeForager.Tasks.Set4.obj0", NPCID.Guide)]
        ],
        rewards:
        [
            new JobReward(ItemID.SilverCoin, 75),
            new JobReward(ItemID.IronBar, 5)
        ],
        providerNPC: NPCID.Guide)
    {
        // The three housing sets share one ledger of credited rooms, so showing the same house three
        // times clears one set, not all of them.
        var ledger = new HashSet<Point>();

        for (var set = FirstHousingSet; set <= LastHousingSet; set++)
            ((ValidHousingTask)TaskList[set].Task[0]).ShareLedger(ledger);

        housingTask = (ValidHousingTask)TaskList[FirstHousingSet].Task[0];
    }

    public override void OnJobStart()
    {
        Context.Play("Guide.HomeForager_Greeting", zoomIn: true, style: DialogueStyle.Projected);

        //if (Context.IsLocal)
        //    MusicDirector.Play(HomeForagerMusic.Create(), fadeIn: 0.35f);
    }

    public override void OnJobComplete(bool giveRewards = true)
    {
        base.OnJobComplete(giveRewards);

        //if (Context.IsLocal)
        //    MusicDirector.Stop(1.5f);
    }

    public override void OnSetAdvanced(int previousSet, int newSet)
        => Context.Play($"Guide.HomeForager_Set{newSet}");

    /// <summary>
    /// The Guide's running commentary on the player's building. Runs before the task sees the event,
    /// so a room that reads as already-credited here is genuinely one the player is re-showing.
    /// </summary>
    public override void OnEvent(GameEvent evt)
    {
        // Only ever speak to a check the player asked for. Vanilla re-scans housing constantly on its
        // own -- NPCs revisiting their homes, the nightly spawn pass -- and none of that is the player
        // asking a question.
        if (evt.Type != GameEventType.HousingChecked || !evt.IsManualCheck)
            return;

        if (CurrentTaskSet < FirstHousingSet || CurrentTaskSet > LastHousingSet)
            return;

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
        CheckInSet => ChatEncounter.Play("Guide.HomeForager_CheckIn").Projected(),

        // Nothing registered for the other sets yet, so the Guide keeps his ordinary greeting. This
        // used to name "Guide.HomeForager_HomeForager_CheckIn", a key that has never existed --
        // silently falling through to vanilla chat rather than erroring.
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
/// Build a room that passes a housing check, and hasn't been shown before.
/// </summary>
/// <remarks>
/// The ledger of credited rooms is the task's own state, and can be shared between several instances
/// so a run of "build another one" sets can't all be cleared by re-checking a single house. It used
/// to live on the job, which meant the job had to intercept its own task's credit to maintain it.
/// </remarks>
public sealed class ValidHousingTask(string key) : JobTask(key, 1)
{
    private HashSet<Point> creditedRooms = [];

    /// <summary>Points this task at a ledger shared with its siblings.</summary>
    public void ShareLedger(HashSet<Point> ledger) => creditedRooms = ledger;

    public bool HasCredited(Point room) => creditedRooms.Contains(room);

    public override bool Matches(GameEvent evt)
        => evt.Type == GameEventType.HousingChecked
        && evt.Amount == 1                              // passed the full check
        && !evt.IsOccupied
        && evt.RoomId is Point room
        && !creditedRooms.Contains(room);               // and isn't one already counted

    public override int CreditFor(GameEvent evt)
    {
        // Recorded here rather than from a job-side hook: the task decided this room counts, so the
        // task is what remembers it.
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
