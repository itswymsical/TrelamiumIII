namespace TrelamiumIII.Core.Dialogue.Definitions;

class PermafrostDialogue : ModDialogue
{
    private const string Speaker = "Permafrost";

    protected override void Register()
    {
        Dialogue.Define("Permafrost.Greeting")
            .Line(Speaker, emote: 0, speed: 1f, new Pause(20))
            .Line(Speaker, emote: 0, speed: 1f, [new Pause(16), new Pause(20)])
            .Line(Speaker, emote: 0, speed: 1.2f, [new Pause(20), new Pause(48), new Pause(20), new Pause(20), new Pause(20)])
            .Register();
    }
}