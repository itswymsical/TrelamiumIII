namespace TrelamiumIII.Core.Dialogue.Definitions;

/// <summary>
///   The Guide explaining the Eye of Cthulhu's wake and calling the player back to town, then the Dryad's first
///   words after she arrives. Text is placeholder, in <c>Localization/DialogueLibrary/DryadArrival</c>.
/// </summary>
class DryadArrivalDialogue : ModDialogue
{
    private const string Guide = "Guide";

    private const string Dryad = "Dryad";

    protected override void Register()
    {
        // Guide portrait frames: 6 and 5 shadowed, 3 worried, 4 flat, 2 calm, 0 smiling.
        Dialogue.Define("DryadArrival.Briefing")
            .Line(Guide, emote: 6, speed: 1.3f, new Pause(24))
            .Line(Guide, emote: 5, speed: 1.3f, [new Pause(20), new Pause(30)])
            .Line(Guide, emote: 3, speed: 1.3f, [new Pause(18), new Pause(26)])
            .Line(Guide, emote: 4, speed: 1.3f, new Pause(24))
            .Line(Guide, emote: 2, speed: 1.3f, new Pause(20))
            .Line(Guide, emote: 0, speed: 1.3f, [new Pause(22), new Pause(16), new Pause(26), new Pause(18)])
            .Register();

        // By day while she is due, when the player is on the surface away from spawn and town. Repeats every few minutes.
        Dialogue.Define("DryadArrival.ReturnToTown")
            .Line(Guide, emote: 2, speed: 1.3f, [new Pause(20), new Pause(18)])
            .Register();

        Dialogue.Define("DryadArrival.Greeting")
            .Line(Dryad, emote: 0, speed: 1.2f, new Pause(20))
            .Line(Dryad, emote: 0, speed: 1.2f, new Pause(22))
            .Line(Dryad, emote: 0, speed: 1.2f, new Pause(24))
            .Line(Dryad, emote: 0, speed: 1.2f, [new Pause(24), new Pause(16)])
            .Line(Dryad, emote: 0, speed: 1.2f, [new Pause(20), new Pause(16)])
            .Register();
    }
}
