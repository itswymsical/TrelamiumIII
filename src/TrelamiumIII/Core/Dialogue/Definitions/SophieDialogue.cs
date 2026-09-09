namespace TrelamiumIII.Core.Dialogue.Definitions;

/// <summary>
///   Template showing the shape of a dialogue definition: flow lives here, text and
///   anchors live in <c>Localization/DialogueLibrary</c>.
///   <para>
///   Test in game with <c>/dialogue play Example.Greeting</c>.
///   </para>
/// </summary>
class SophieDialogue : ModDialogue
{
    private const string Speaker = "Sophie";

    protected override void Register()
    {
        Dialogue.Define("Sophie.Greeting")
            .Line(Speaker, emote: 0, speed: 1.2f, [new Pause(14), new Pause(20), new Pause(22)])
                .Choice("Sophie.GreetingAnswer1")
                .Choice("Sophie.GreetingAnswer2")
                .Choice("Sophie.GreetingAnswer3")
            .Register();

        Dialogue.Define("Sophie.GreetingAnswer1")
            .Line(Speaker, emote: 0, speed: 1.2f, [new Pause(20), new Pause(16)])
            .Register();

        Dialogue.Define("Sophie.GreetingAnswer2")
            .Line(Speaker, emote: 0, speed: 1.2f, new Pause(20))
            .Register();

        Dialogue.Define("Sophie.GreetingAnswer3")
            .Line(Speaker, emote: 0, speed: 1.2f, new Pause(16))
            .Register();
    }
}