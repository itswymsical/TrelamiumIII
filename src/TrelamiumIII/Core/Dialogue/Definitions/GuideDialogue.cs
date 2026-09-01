namespace TrelamiumIII.Core.Dialogue.Definitions;

class GuideDialogue : ModDialogue
{
    private const string Speaker = "Guide";

    protected override void Register()
    {

        Dialogue.Define("Guide.HomeForager")
            .Line(Speaker, emote: 0, speed: 1.4f, new Pause(20))
            .Register();

        Dialogue.Define("Guide.HomeForager_Greeting")
            .Line(Speaker, emote: 0, speed: 1.4f, new Pause(20))
            .Line(Speaker, emote: 0, speed: 1.4f, [new Pause(20), new Pause(16)])
            .Line(Speaker, emote: 0, speed: 1.4f, [new Pause(14), new Pause(48), new Pause(36), new Pause(20)])
            .Register();
    }
}