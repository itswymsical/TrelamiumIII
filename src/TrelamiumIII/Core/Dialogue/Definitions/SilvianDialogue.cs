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
        Dialogue.Define("Silvian.Greeting")
            .Line(Speaker, emote: 0, speed: 1.4f, new Pause(16))
                .Choice("Silvian.Greeting_True")
                .Choice("Silvian.Greeting_Lie")
                .Choice("Silvian.Greeting_Silent")
            .Register();

        Dialogue.Define("Silvian.Greeting_True")
            .Line(Speaker, emote: 0, speed: 1.3f, [new Pause(16), new Pause(20), new Pause(16), new Pause(20)])
                .Choice("Silvian.Lost_Exploring")
                .Choice("Silvian.Lost_Trouble")
                .Choice("Silvian.Lost_Speedrunning")
                .Choice("Silvian.Lost_Silent")
            .Register();

        Dialogue.Define("Silvian.Greeting_Lie")
            .Line(Speaker, emote: 0, speed: 1.3f, [new Pause(16), new Pause(20)])
            .Line(Speaker, emote: 0, speed: 1.3f, [new Pause(20), new Pause(16)])
                .Choice("Silvian.Lost_Exploring")
                .Choice("Silvian.Lost_Trouble")
                .Choice("Silvian.Lost_Speedrunning")
                .Choice("Silvian.Lost_Silent")
            .Register();

        Dialogue.Define("Silvian.Greeting_Silent")
            .Line(Speaker, emote: 0, speed: 1.3f, [new Pause(12), new Pause(20), new Pause(20)])
                .Choice("Silvian.Lost_Exploring")
                .Choice("Silvian.Lost_Trouble")
                .Choice("Silvian.Lost_Speedrunning")
                .Choice("Silvian.Lost_Silent")
            .Register();

        Dialogue.Define("Silvian.Lost_Exploring")
            .Line(Speaker, emote: 0, speed: 1.3f, [new Pause(16)])
            .Line(Speaker, emote: 0, speed: 1.3f, [new Pause(20), new Pause(18), new Pause(18)])
            .Line(Speaker, emote: 0, speed: 1.3f, [new Pause(20)])
            .Register();

        Dialogue.Define("Silvian.Lost_Trouble")
            .Line(Speaker, emote: 0, speed: 1.3f, [new Pause(14), new Pause(14), new Pause(18)])
            .Line(Speaker, emote: 0, speed: 1.3f, [new Shake(0.7f)])
            .Line(Speaker, emote: 0, speed: 1.12f)
            .Line(Speaker, emote: 0, speed: 1.3f, [new Pause(20), new Pause(16), new Pause(18), new Pause(14)])
            .Register();

        Dialogue.Define("Silvian.Lost_Speedrunning")
            .Line(Speaker, emote: 0, speed: 1.3f)
            .Line(Speaker, emote: 0, speed: 1.3f, [new Pause(14), new Pause(16), new Pause(18)])
            .Line(Speaker, emote: 0, speed: 1.3f, [new Pause(20)])
            .Register();

        Dialogue.Define("Silvian.Lost_Silent")
            .Line(Speaker, emote: 0, speed: 1.3f, [new Pause(14), new Pause(14)])
            .Line(Speaker, emote: 0, speed: 1.3f, [new Pause(20), new Pause(18), new Pause(18)])
            .Line(Speaker, emote: 0, speed: 1.3f, [new Pause(20)])
            .Register();

        Dialogue.Define("Silvian.BotanyJob1_Greeting")
            .Line(Speaker, emote: 0, speed: 1.3f, [new Pause(20)])
            .Line(Speaker, emote: 0, speed: 1.3f, [new Pause(18), new Pause(18)])
            .Line(Speaker, emote: 0, speed: 1.3f, [new Pause(20)])
            .Line(Speaker, emote: 0, speed: 1.3f, [new Pause(16)])
            .Register();

        Dialogue.Define("Silvian.BotanyJob1_Complete")
            .Line(Speaker, emote: 0, speed: 1.3f, [new Pause(20), new Pause(16), new Pause(16)])
            .Line(Speaker, emote: 0, speed: 1.3f, [new Pause(20)])
            .Line(Speaker, emote: 0, speed: 1.3f, [new Pause(18)])
            .Register();
    }
}