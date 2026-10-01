using TrelamiumIII.Content.NPCs.WorldNPCs.Silvian;
using TrelamiumIII.Core.Jobs;

namespace TrelamiumIII.Core.Dialogue.Definitions;

/// <summary>
///   Template showing the shape of a dialogue definition: flow lives here, text and
///   anchors live in <c>Localization/DialogueLibrary</c>.
///   <para>
///   Test in game with <c>/dialogue play Example.Greeting</c>.
///   </para>
/// </summary>
class SilvianDialogue : ModDialogue
{
    private const string Speaker = "Silvian";

    protected override void Register()
    {
        // Opened by the encounter scene (SilvianEncounter). Every answer ends on one of the Lost_ branches, which is where
        // the job unlocks: the silent answer is dead, and would discard an unlock attached up here.
        Dialogue.Define("Silvian.Greeting")
            .Line(Speaker, emote: 0, speed: 1f, new Pause(16))
                .Choice("Silvian.Greeting_True")
                .Choice("Silvian.Greeting_Lie")
                .Choice("Silvian.Greeting_Silent", dead: true)
            .Register();

        Dialogue.Define("Silvian.Greeting_True")
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(16), new Pause(20), new Pause(16), new Pause(20)])
                .Choice("Silvian.Lost_Exploring")
                .Choice("Silvian.Lost_Trouble")
                .Choice("Silvian.Lost_Speedrunning")
                .Choice("Silvian.Lost_Silent")
            .Register();

        Dialogue.Define("Silvian.Greeting_Lie")
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(16), new Pause(20)])
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(20), new Pause(16)])
                .Choice("Silvian.Lost_Exploring")
                .Choice("Silvian.Lost_Trouble")
                .Choice("Silvian.Lost_Speedrunning")
                .Choice("Silvian.Lost_Silent")
            .Register();

        Dialogue.Define("Silvian.Greeting_Silent")
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(12), new Pause(20), new Pause(20)])
                .Choice("Silvian.Lost_Exploring")
                .Choice("Silvian.Lost_Trouble")
                .Choice("Silvian.Lost_Speedrunning")
                .Choice("Silvian.Lost_Silent")
            .Register();

        Dialogue.Define("Silvian.Lost_Exploring")
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(16)])
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(20), new Pause(18), new Pause(18)])
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(20)])
            .UnlockJob(JobID.HerbalForager)
            .Register();

        Dialogue.Define("Silvian.Lost_Trouble")
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(14), new Pause(14), new Pause(18)])
            .Line(Speaker, emote: 0, speed: 1.2f, [new Shake(0.7f)])
            .Line(Speaker, emote: 0, speed: 1.12f)
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(20), new Pause(16), new Pause(18), new Pause(14)])
            .UnlockJob(JobID.HerbalForager)
            .Register();

        Dialogue.Define("Silvian.Lost_Speedrunning")
            .Line(Speaker, emote: 0, speed: 1.1f)
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(14), new Pause(16), new Pause(18)])
            .Line(Speaker, emote: 0, speed: 1.2f, [new Pause(20)])
            .UnlockJob(JobID.HerbalForager)
            .Register();

        Dialogue.Define("Silvian.Lost_Silent")
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(14), new Pause(14)])
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(20), new Pause(18), new Pause(18)])
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(20)])
            .UnlockJob(JobID.HerbalForager)
            .Register();

        // For a player who cut the greeting short before it offered the job. See Silvian.GetEncounter.
        Dialogue.Define("Silvian.JobOffer")
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(14), new Pause(18)])
            .UnlockJob(JobID.HerbalForager)
            .Register();

        // His uncle's almanac changes hands once the conversation plays through. See SilvianAlmanac.
        Dialogue.Define(SilvianAlmanac.HandoffKey)
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(16), new Pause(14)])
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(20), new Pause(18)])
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(20), new Pause(14)])
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(16)])
            .Line(Speaker, emote: 0, speed: 1.2f, [new Pause(18), new Pause(20), new Pause(24), new Pause(14)])
            .OnComplete(() => SilvianAlmanac.Give(Main.LocalPlayer))
            .Register();

        Dialogue.Define("Silvian.BotanyJob1_Greeting")
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(20)])
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(18), new Pause(18)])
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(20)])
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(16)])
            .Register();

        Dialogue.Define("Silvian.BotanyJob1_Complete")
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(20), new Pause(16), new Pause(16)])
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(20)])
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(18)])
            .Register();
    }
}