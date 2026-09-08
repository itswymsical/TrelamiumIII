namespace TrelamiumIII.Core.Dialogue.Definitions;

class KrakenDialogue : ModDialogue
{
    private const string Speaker = "Player";

    protected override void Register()
    {
        Dialogue.Define("Kraken.Greeting")
            .Line(Speaker, emote: 0, speed: 1.3f, new Pause(20))
            .Line(Speaker, emote: 0, speed: 1.3f, new Pause(20))
            .Register();
    }
}