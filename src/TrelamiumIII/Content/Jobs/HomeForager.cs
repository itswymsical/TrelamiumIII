using System.Collections.Generic;
using Terraria.ModLoader.IO;
using TrelamiumIII.Core.Dialogue;
using TrelamiumIII.Core.Events;
using TrelamiumIII.Core.Jobs;

namespace TrelamiumIII.Content.Jobs;

public sealed class HomeForager : Job
{
    /// <summary>First and last set index that asks for a house. Sets in this range credit one house each.</summary>
    private const int FirstHousingSet = 1;
    private const int LastHousingSet = 3;

    // how often the guide will yap
    private const int CommentCooldown = 180;

    /// <summary>
    /// Rooms already credited to this Job. Each housing set needs a house the player hasn't shown before,
    /// so the same room can't be checked three times to clear all three sets.
    /// </summary>
    private readonly HashSet<Point> creditedRooms = [];

    private int lastCommentTick;

    public HomeForager() :
        base(JobID.HomeForager, "HomeForager.Name", "HomeForager.Description",
        taskList:
        [
            [
                ("HomeForager.Tasks.Set0.obj0", 10), // trees
                ("HomeForager.Tasks.Set0.obj1", 25) // stone
            ],

            [("HomeForager.Tasks.Set1.obj0", 1)], // create housing(s)
            [("HomeForager.Tasks.Set2.obj0", 1)], // create housing(s)
            [("HomeForager.Tasks.Set3.obj0", 1)], // create housing(s)
            [("HomeForager.Tasks.Set4.obj0", 1)], // check in
        ],
        rewards:
        [
            new JobReward(ItemID.SilverCoin, 75),
            new JobReward(ItemID.IronBar, 5)
        ],
        providerNPC: NPCID.Guide) { }

    public override void OnJobStart()
    {
        base.OnJobStart();
        DialogueManager.Instance.StartDialogue("Guide.HomeForager_Greeting", zoomIn: true, letterbox: true);
    }

    public override void Reset()
    {
        base.Reset();
        creditedRooms.Clear();
        lastCommentTick = 0;
    }

    public override void OnEvent(GameEvent evt)
    {
        if (evt.Type != GameEventType.HousingChecked || !evt.IsManualCheck)
            return;

        if (CurrentSet < FirstHousingSet || CurrentSet > LastHousingSet)
            return;

        if (evt.Issue != HousingIssue.None)
        {
            Speak(DialogueFor(evt.Issue));
            return;
        }

        // A house that passes but has already been counted: the player is showing the same room again.
        if (evt.RoomId is Point room && creditedRooms.Contains(room))
            Speak("Guide.HomeForager_Repeat");
    }

    public override void OnSetAdvanced(int previousSet, int newSet)
    {
        DialogueManager.Instance.StartDialogue($"Guide.HomeForager_Set{newSet}");
    }

    public override bool OnMatchedEvent(GameEvent evt, int taskIndex)
    {
        if (evt.Type == GameEventType.HousingChecked && evt.RoomId is Point room)
            creditedRooms.Add(room);

        return base.OnMatchedEvent(evt, taskIndex);
    }

    public override bool MatchesEvent(GameEvent evt, int currentSet, int taskIndex)
    {
        // Set 0, Task 0: chop trees
        if (currentSet == 0)
        {
            if (taskIndex == 0)
                return evt.Type == GameEventType.TileBreak
                    && evt.TileType.HasValue && evt.TileType == TileID.Trees;
            else if (taskIndex == 1)
                return evt.Type == GameEventType.ItemPickup
                    && evt.ItemType.HasValue && evt.ItemType == ItemID.StoneBlock;
        }

        if ((currentSet >= FirstHousingSet && currentSet <= LastHousingSet) && taskIndex == 0)
            return evt.Type == GameEventType.HousingChecked
                && evt.Amount == 1                     // passed the full housing check
                && !evt.IsOccupied
                && evt.RoomId is Point room
                && !creditedRooms.Contains(room);      // and isn't one of the houses already counted

        if (currentSet == 4 && taskIndex == 0)
            return evt.Type == GameEventType.NPCChat
                && evt.NPCType.HasValue && (evt.NPCType == this.ProviderNPC);

        return false;
    }

    public override void SaveCustomData(TagCompound tag)
    {
        // Flattened to x, y, x, y - TagCompound has no Point serializer.
        var rooms = new List<int>(creditedRooms.Count * 2);

        foreach (var room in creditedRooms)
        {
            rooms.Add(room.X);
            rooms.Add(room.Y);
        }

        tag["CreditedRooms"] = rooms;
    }

    public override void LoadCustomData(TagCompound tag)
    {
        creditedRooms.Clear();

        var rooms = tag.GetList<int>("CreditedRooms");

        for (int i = 0; i + 1 < rooms.Count; i += 2)
            creditedRooms.Add(new Point(rooms[i], rooms[i + 1]));
    }

    /// <summary>Guide line for each way a room can come back unusable.</summary>
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

    /// <summary>Says a line, if the Guide is alive to say it and he hasn't just said one.</summary>
    private void Speak(string dialogueKey)
    {
        int currentTick = (int)Main.GameUpdateCount;

        if (currentTick - lastCommentTick < CommentCooldown)
            return;

        if (!NPC.AnyNPCs(ProviderNPC))
            return;

        lastCommentTick = currentTick;
        DialogueManager.Instance.StartDialogue(dialogueKey);
    }
}
