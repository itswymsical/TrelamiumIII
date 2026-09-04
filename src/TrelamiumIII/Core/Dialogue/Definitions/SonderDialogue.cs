namespace TrelamiumIII.Core.Dialogue.Definitions;

/// <summary>
///   Template showing the shape of a dialogue definition: flow lives here, text and
///   anchors live in <c>Localization/DialogueLibrary</c>.
/// </summary>
class SonderDialogue : ModDialogue
{
    private const string Speaker = "Sonder";

    protected override void Register()
    {
        Dialogue.Define("Sonder.SonderGreeting")
            .Line(Speaker, emote: 0, speed: 1.4f, [new Pause(20), new Pause(16), new Pause(30)]) // 1
            .Line(Speaker, emote: 0, speed: 1.3f) // 2
            .Line(Speaker, emote: 0, speed: 1.4f) // 3
            .Line(Speaker, emote: 0, speed: 1.4f, new Pause(30)) // 4
                .Choice("Sonder.SonderChoice_1")
                .Choice("Sonder.SonderChoice_2")
                .Choice("Sonder.SonderChoice_3")
                .Choice("Sonder.SonderChoice_4")
            .Register();

        Dialogue.Define("Sonder.SonderChoice_1")
            .Line(Speaker, emote: 0, speed: 1.4f)
            .Line(Speaker, emote: 0, speed: 1.4f)
            .Line(Speaker, emote: 0, speed: 1.4f, new Pause(30))
            .Register();

        Dialogue.Define("Sonder.SonderChoice_2")
            .Line(Speaker, emote: 0, speed: 1.4f)
            .Register();

        Dialogue.Define("Sonder.SonderChoice_3")
            .Line(Speaker, emote: 0, speed: 1.4f, new Pause(30))
            .Register();

        Dialogue.Define("Sonder.SonderChoice_4")
            .Line(Speaker, emote: 0, speed: 1.4f, new Pause(30))
            .Register();
    }
}