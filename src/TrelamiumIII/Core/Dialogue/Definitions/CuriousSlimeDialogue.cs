namespace TrelamiumIII.Core.Dialogue.Definitions;

class CuriousSlimeDialogue : ModDialogue
{
    private const string Speaker = "Player";

    protected override void Register()
    {
        Dialogue.Define("CuriousSlime.Greeting")
            .Line(Speaker, emote: 0, speed: 1f, new Wave(3))
            .Register();

        Dialogue.Define("CuriousSlime.Chat")
            .Line(Speaker, emote: 0, speed: 1f, [new Pause(20), new Pause(20)])
            .Register();

        Dialogue.Define("CuriousSlime.Job1End")
            .Line(Speaker, emote: 0, speed: 1f, new Wave(3))
            .Line(Speaker, emote: 0, speed: 1f, new Pause(20))
        .Register();
    }
}
