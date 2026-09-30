namespace TrelamiumIII.Core.Dialogue.Definitions;

/// <summary>
///   The Dryad once she lives in town. Text is placeholder, in <c>Localization/DialogueLibrary/Dryad</c>.
/// </summary>
class DryadDialogue : ModDialogue
{
    private const string Speaker = "Dryad";

    protected override void Register()
    {
        // On taking CrossingOver. The rift opens, camera and all, once this ends.
        Dialogue.Define("Dryad.CrossingOver_Greeting")
            .Line(Speaker, emote: 0, speed: 1.2f, [new Pause(20), new Pause(18)])
            .Line(Speaker, emote: 0, speed: 1.2f, [new Pause(18), new Pause(22)])
            .Line(Speaker, emote: 0, speed: 1.2f, new Pause(16))
            .Register();
    }
}
