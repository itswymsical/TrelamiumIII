namespace TrelamiumIII.Content.Jobs;

// An unfinished job, kept as a worked example of the current shape: tasks carry their own triggers,
// so there is no MatchesEvent ladder to write, and the job itself only handles the narrative.
//
// To bring it back: add NerdySlimeI to JobID, add its strings under Jobs in the localization file,
// and uncomment.
//
// public sealed class NerdySlimeI : Job
// {
//     public NerdySlimeI() : base(
//         JobID.NerdySlimeI, "NerdySlimeI.Name", "NerdySlimeI.Description",
//         taskList:
//         [
//             [new TalkToNPCTask("NerdySlimeI.Tasks.Set0.obj0", NPCID.TownSlimeBlue)],
//
//             // Iron or lead, whichever this world rolled -- the one case here that isn't a plain
//             // "pick up N of item X", so it uses the predicate escape hatch rather than its own type.
//             [new PredicateTask("NerdySlimeI.Tasks.Set1.obj0",
//                 evt => evt.Type == GameEventType.ItemPickup
//                     && evt.ItemType is ItemID.IronOre or ItemID.LeadOre, 25)],
//
//             [new TalkToNPCTask("NerdySlimeI.Tasks.Set2.obj0", NPCID.TownSlimeBlue)]
//         ],
//         rewards:
//         [
//             new JobReward(ItemID.SilverCoin, 75),
//             new JobReward(ItemID.IronBar, 5)
//         ],
//         providerNPC: NPCID.TownSlimeBlue)
//     { }
//
//     public override void OnJobStart() => Context.Play("NerdySlime.Greeting", zoomIn: true, letterbox: true);
//
//     // What the slime says when talked to mid-job, resolved ahead of its ordinary greeting. The old
//     // draft opened this dialogue from inside MatchesEvent, which fired it once per task per chat.
//     public override ChatEncounter GetChatEncounter(NPC npc, Player player) => CurrentTaskSet switch
//     {
//         1 => ChatEncounter.Play("NerdySlime.Reminder"),
//         _ => ChatEncounter.None
//     };
// }
