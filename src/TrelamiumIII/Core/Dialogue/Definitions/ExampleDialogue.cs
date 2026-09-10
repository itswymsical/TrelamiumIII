namespace TrelamiumIII.Core.Dialogue.Definitions;

/// <summary>
///   Template showing the shape of a dialogue definition: flow lives here, text and
///   anchors live in <c>Localization/DialogueLibrary</c>.
///   <para>
///   Test in game with <c>/dialogue play Example.Greeting</c>.
///   </para>
/// </summary>
class ExampleDialogue : ModDialogue
{
    private const string Speaker = "WanderingEmployer";

    protected override void Register()
    {
        Dialogue.Define("Example.Greeting")
            .Line(Speaker, emote: 0, speed: 1f, new Pause(20))
            .Line(Speaker, emote: 0, speed: 1f, new Wave())
            .Register();

        Dialogue.Define("Example.Banter")
            .Line(Speaker, emote: 0, speed: 1.3f, [new Pause(8), new Pause(8)])
            .Line(Speaker, emote: 0, speed: 1f)
            .Register();
    }
}