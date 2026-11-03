using TrelamiumIII.Core.Jobs;

namespace TrelamiumIII.Core.Dialogue.Definitions;

 class MerchantDialogue : ModDialogue
{
    private const string Speaker = "Merchant";

    protected override void Register()
    {
        Dialogue.Define("Merchant.CopperStandard_Greeting")
            .Line(Speaker, emote: 0, speed: 1.2f, [new Pause(24), new Pause(12), new Pause(12), new Pause(24)])
            .Line(Speaker, emote: 0, speed: 1.2f, [new Pause(16), new Pause(24), new Pause(16), new Pause(16)])
            .Line(Speaker, emote: 0, speed: 1.2f, [new Pause(20), new Pause(24), new Pause(20)])
            .Line(Speaker, emote: 0, speed: 1.2f, new Pause(24))
            .Register();

        // Tin turned up while the job still wants ore or bars. See CopperStandard.OnEvent.
        Dialogue.Define("Merchant.CopperStandard_Tin")
            .Line(Speaker, emote: 2, speed: 1.2f, [new Pause(12), new Pause(20), new Pause(12), new Pause(20), new Pause(24)])
            .Register();

        // Set transitions, fired as each stage of the job is cleared. See CopperStandard.OnSetAdvanced.
        // The ore is in: go and smelt it.
        Dialogue.Define("Merchant.CopperStandard_Set1")
            .Line(Speaker, emote: 0, speed: 1.2f, [new Pause(20), new Pause(12), new Pause(20), new Pause(16)])
            .Line(Speaker, emote: 0, speed: 1.2f, new Pause(24))
            .Register();

        // The bars shown: put copper in things, starting with what people shoot.
        Dialogue.Define("Merchant.CopperStandard_Set2")
            .Line(Speaker, emote: 0, speed: 1.2f, [new Pause(24), new Pause(14), new Pause(24)])
            .Line(Speaker, emote: 0, speed: 1.2f, [new Pause(24), new Pause(20)])
            .Line(Speaker, emote: 0, speed: 1.2f, [new Pause(20), new Pause(20), new Pause(12), new Pause(12), new Pause(20)])
            .Line(Speaker, emote: 0, speed: 1.2f, [new Pause(20), new Pause(24), new Pause(12)])
            .Register();

        // The weapons reviewed: think bigger, think of the home.
        Dialogue.Define("Merchant.CopperStandard_Set3")
            .Line(Speaker, emote: 0, speed: 1.2f, [new Pause(20), new Pause(20), new Pause(24), new Pause(20), new Pause(14)])
            .Line(Speaker, emote: 1, speed: 1.2f, [new Pause(14), new Pause(24), new Pause(14)])
            .Line(Speaker, emote: 2, speed: 1.3f, [new Pause(16), new Pause(12), new Pause(20), new Pause(20), new Pause(10), new Pause(20), new Pause(14), new Pause(14)])
            .Line(Speaker, emote: 2, speed: 1.3f, [new Pause(14), new Pause(20), new Pause(10)])
            .Register();

        // The household goods reviewed: the gold bunny theory.
        Dialogue.Define("Merchant.CopperStandard_Set4")
            .Line(Speaker, emote: 0, speed: 1.2f, [new Pause(20), new Pause(16), new Pause(20)])
            .Line(Speaker, emote: 0, speed: 1.2f, [new Pause(20), new Pause(24)])
            .Line(Speaker, emote: 1, speed: 1.2f, new Pause(24))
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(30), new Pause(24), new Pause(16), new Pause(20)])
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(24), new Pause(30)])
            .Line(Speaker, emote: 2, speed: 1.2f, [new Pause(40), new Pause(24)])
            .Register();

        // Opened by the job's HandInTasks once everything is held. The next set's line plays after them.
        Dialogue.Define("Merchant.CopperStandard_Set1HandIn")
            .Line(Speaker, emote: 0, speed: 1.2f, [new Pause(20), new Pause(16), new Pause(24)])
                .HandIn(JobID.CopperStandard)
            .Register();

        Dialogue.Define("Merchant.CopperStandard_Set2HandIn")
            .Line(Speaker, emote: 0, speed: 1.2f, new Pause(20))
                .HandIn(JobID.CopperStandard)
            .Register();

        Dialogue.Define("Merchant.CopperStandard_Set3HandIn")
            .Line(Speaker, emote: 0, speed: 1.2f, new Pause(20))
                .HandIn(JobID.CopperStandard)
            .Register();

        // The last hand-in. There's no "not yet" here: whatever the player says, he's already taken the bunny.
        Dialogue.Define("Merchant.CopperStandard_Appraisal")
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(20), new Pause(24), new Pause(12)])
            .Line(Speaker, emote: 1, speed: 1.1f, [new Pause(16), new Pause(20), new Pause(12), new Pause(12), new Pause(16), new Pause(30)])
            .Line(Speaker, emote: 1, speed: 1f, [new Pause(40), new Pause(24)])
                .Choice(onSelected: HandInBunny)
                .Choice(onSelected: HandInBunny)
                .Choice(onSelected: HandInBunny)
            .Register();

        Dialogue.Define("Merchant.CopperStandard_Complete")
            .Line(Speaker, emote: 0, speed: 1.2f, [new Pause(10), new Pause(14), new Pause(20), new Pause(20)])
            .Line(Speaker, emote: 0, speed: 1.2f, [new Pause(20), new Pause(24), new Pause(20), new Pause(14)])
            .Line(Speaker, emote: 1, speed: 1f, [new Pause(30), new Pause(24), new Pause(12), new Pause(30)])
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(14), new Pause(24)])
            .Register();
    }

    private static void HandInBunny() => Main.LocalPlayer.GetModPlayer<JobPlayer>().GetOrCreateJob(JobID.CopperStandard)?.TryHandIn();
}
