namespace TrelamiumIII.Core.Dialogue.Definitions;

class TinkererDialogue : ModDialogue
{
    private const string Speaker = "Goblin Tinkerer";

    protected override void Register()
    {
        Dialogue.Define("Tinkerer.ExplosiveArchitecture_Greeting")
            .Line(Speaker, emote: 0, speed: 1.1f)
            .Line(Speaker, emote: 0, speed: 1.1f, new Pause(16))
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(25), new Pause(25), new Pause(25)])
            .Line(Speaker, emote: 0, speed: 1.1f, new Pause(20))
            .Line(Speaker, emote: 0, speed: 1.1f)
            .Line(Speaker, emote: 0, speed: 1.1f)
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(16), new Pause(16)])
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(20), new Pause(16)])
            .Register();

        Dialogue.Define("Tinkerer.ExplosiveArchitecture_TaskSet1Complete")
            .Line(Speaker, emote: 0, speed: 1.1f, new Pause(25))
            .Line(Speaker, emote: 0, speed: 1.1f)
            .Register();
    }
}