using TrelamiumIII.Content.NPCs.WorldNPCs.Silvian;
using TrelamiumIII.Core.Jobs;

namespace TrelamiumIII.Core.Dialogue.Definitions;

/// <summary>
///   Silvian's conversations. Flow lives here; text and pause anchors live in <c>Localization/DialogueLibrary/Silvian</c>.
///   Test in game with <c>/dialogue play Silvian.Greeting</c>.
/// </summary>
class SilvianDialogue : ModDialogue
{
    private const string Speaker = "Silvian";

    protected override void Register()
    {
        RegisterGreeting();
        RegisterFirstJob();
        RegisterBotany();

        // His uncle's almanac changes hands once the conversation plays through. See SilvianAlmanac.
        Dialogue.Define(SilvianAlmanac.HandoffKey)
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(16), new Pause(18)])
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(20)])
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(20)])
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(14), new Pause(20)])
            .Line(Speaker, emote: 0, speed: 1.2f, [new Pause(18), new Pause(24), new Pause(14)])
            .OnComplete(() => SilvianAlmanac.Give(Main.LocalPlayer))
            .Register();
    }

    /// <summary>
    ///   The meeting: his name, what brings the player here, then the trampled garden, which is the question that tells
    ///   him where the player's head is at. Every path ends on a Garden_ branch, which is where the first job unlocks.
    /// </summary>
    private static void RegisterGreeting()
    {
        // The silent answer is dead, and would discard an unlock attached up here.
        Dialogue.Define("Silvian.Greeting")
            .Line(Speaker, emote: 0, speed: 1f, [new Pause(16), new Pause(16), new Pause(14)])
                .Choice("Silvian.Greeting_True")
                .Choice("Silvian.Greeting_Lie")
                .Choice("Silvian.Greeting_Silent", dead: true)
            .Register();

        Dialogue.Define("Silvian.Greeting_True")
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(14), new Pause(20), new Pause(18)])
                .Choice("Silvian.Lost_Exploring")
                .Choice("Silvian.Lost_Trouble")
                .Choice("Silvian.Lost_Speedrunning")
                .Choice("Silvian.Lost_Silent")
            .Register();

        Dialogue.Define("Silvian.Greeting_Lie")
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(20), new Pause(18)])
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(20)])
                .Choice("Silvian.Lost_Exploring")
                .Choice("Silvian.Lost_Trouble")
                .Choice("Silvian.Lost_Speedrunning")
                .Choice("Silvian.Lost_Silent")
            .Register();

        Dialogue.Define("Silvian.Greeting_Silent")
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(16), new Pause(16)])
            .Line(Speaker, emote: 0, speed: 1.1f)
                .Choice("Silvian.Lost_Exploring")
                .Choice("Silvian.Lost_Trouble")
                .Choice("Silvian.Lost_Speedrunning")
                .Choice("Silvian.Lost_Silent")
            .Register();

        // Each Lost_ branch ends on the garden question.
        DefineLost("Silvian.Lost_Exploring", [new Pause(16)], [new Pause(18)]);
        DefineLost("Silvian.Lost_Trouble", [new Pause(20)], [new Pause(20), new Shake(0.6f)]);
        DefineLost("Silvian.Lost_Speedrunning", [new Pause(16)], [new Pause(18)]);
        DefineLost("Silvian.Lost_Silent", [new Pause(24), new Pause(16)], [new Pause(18)]);

        Dialogue.Define("Silvian.Garden_Defend")
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(14), new Pause(16)])
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(18), new Pause(16)])
            .UnlockJob(JobID.HerbalForager)
            .Register();

        Dialogue.Define("Silvian.Garden_Help")
            .Line(Speaker, emote: 0, speed: 1f, [new Pause(24)])
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(18), new Pause(20)])
            .UnlockJob(JobID.HerbalForager)
            .Register();

        // No unlock here: both answers to "you're joking, right?" carry it.
        Dialogue.Define("Silvian.Garden_Join")
            .Line(Speaker, emote: 0, speed: 0.8f, [new Pause(40)])
            .Line(Speaker, emote: 0, speed: 0.9f, [new Pause(24), new Pause(16)])
                .Choice("Silvian.Garden_Joking")
                .Choice("Silvian.Garden_Serious")
            .Register();

        Dialogue.Define("Silvian.Garden_Joking")
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(14), new Pause(16)])
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(18), new Pause(16)])
            .UnlockJob(JobID.HerbalForager)
            .Register();

        Dialogue.Define("Silvian.Garden_Serious")
            .Line(Speaker, emote: 0, speed: 0.8f, [new Pause(36)])
            .Line(Speaker, emote: 0, speed: 0.9f)
            .Line(Speaker, emote: 0, speed: 1f, [new Pause(18), new Pause(16), new Pause(18)])
            .UnlockJob(JobID.HerbalForager)
            .Register();

        Dialogue.Define("Silvian.Garden_Silent")
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(16)])
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(14), new Pause(18), new Pause(16)])
            .UnlockJob(JobID.HerbalForager)
            .Register();

        // For a player who cut the meeting short before it offered the job. See Silvian.GetEncounter.
        Dialogue.Define("Silvian.JobOffer")
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(14), new Pause(18)])
            .UnlockJob(JobID.HerbalForager)
            .Register();
    }

    /// <summary>Two lines of reaction, then the garden question with its four answers.</summary>
    private static void DefineLost(string key, DialogueAffect[] first, DialogueAffect[] second)
    {
        Dialogue.Define(key)
            .Line(Speaker, emote: 0, speed: 1.1f, first)
            .Line(Speaker, emote: 0, speed: 1.1f, second)
            .Line(Speaker, emote: 0, speed: 1.05f, [new Pause(18), new Pause(20), new Pause(24)])
                .Choice("Silvian.Garden_Defend")
                .Choice("Silvian.Garden_Help")
                .Choice("Silvian.Garden_Join")
                .Choice("Silvian.Garden_Silent")
            .Register();
    }

    private static void RegisterFirstJob()
    {
        Dialogue.Define("Silvian.HerbalForager_Greeting")
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(16)])
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(16), new Pause(18), new Pause(16)])
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(16), new Pause(14), new Pause(18)])
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(14), new Pause(16), new Pause(16), new Pause(18)])
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(16), new Pause(14), new Pause(18)])
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(18)])
            .Register();

        // Opened by HerbalForager's HandInTask once everything is held. The complete line plays after it.
        Dialogue.Define("Silvian.HerbalForager_HandIn")
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(16)])
                .HandIn(JobID.HerbalForager)
            .Register();

        // Ends on the botany offer. Declining or cutting it short leaves Silvian.BotanyOffer to ask again.
        Dialogue.Define("Silvian.HerbalForager_Complete")
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(14), new Pause(14), new Pause(20)])
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(18)])
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(14), new Pause(16), new Pause(18), new Pause(16)])
            .Line(Speaker, emote: 0, speed: 1.05f, [new Pause(24), new Pause(18), new Pause(12), new Pause(12), new Pause(18)])
                .Choice("Silvian.BotanyOffer_Yes")
                .Choice("Silvian.BotanyOffer_No", dead: true)
            .Register();
    }

    private static void RegisterBotany()
    {
        Dialogue.Define("Silvian.BotanyOffer")
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(18)])
                .Choice("Silvian.BotanyOffer_Yes")
                .Choice("Silvian.BotanyOffer_No", dead: true)
            .Register();

        Dialogue.Define("Silvian.BotanyOffer_Yes")
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(14), new Pause(18)])
            .UnlockJob(JobID.BotanyBasics)
            .Register();

        Dialogue.Define("Silvian.BotanyOffer_No")
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(16)])
            .Register();

        Dialogue.Define("Silvian.BotanyBasics_Greeting")
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(20)])
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(18), new Pause(16)])
            .Register();

        // BotanyBasics' soil stage: the lesson, and the first time he brings up his uncle's alchemy.
        Dialogue.Define("Silvian.BotanyBasics_Soil")
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(18)])
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(16), new Pause(16), new Pause(16), new Pause(18), new Pause(14), new Pause(16)])
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(18)])
            .Line(Speaker, emote: 0, speed: 0.95f, [new Pause(30), new Pause(18)])
            .Line(Speaker, emote: 0, speed: 1f, [new Pause(18), new Pause(16), new Pause(12), new Pause(12), new Pause(18), new Pause(12), new Pause(18)])
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(16), new Pause(16), new Pause(18)])
            .Register();

        // BotanyBasics' last stage. The almanac handoff is queued behind it when the job completes.
        Dialogue.Define("Silvian.BotanyBasics_WrapUp")
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(18)])
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(18)])
            .Line(Speaker, emote: 0, speed: 1f, [new Pause(24), new Pause(18)])
            .Register();
    }
}
