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
    private const string Speaker = "Player";

    protected override void Register()
    {
        // Affects bind to the bracketed anchors in the localized text, in order of appearance:
        // a [] point anchor takes a point affect (Pause), a [span] anchor takes a span affect
        // (Wave, Shake, Slow, Fast, Pitch).
        Dialogue.Define("Example.Greeting")
            .Line(Speaker, emote: 0, speed: 1f, new Pause(20))
            .Line(Speaker, emote: 0, speed: 1f, new Wave())
            .Register();
    }
}