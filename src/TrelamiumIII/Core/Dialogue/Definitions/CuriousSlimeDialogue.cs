namespace TrelamiumIII.Core.Dialogue.Definitions;

class CuriousSlimeDialogue : DialogueEntry
{
    private const string Speaker = "Player";

    protected override void Register()
    {
        Dialogue.Define("CuriousSlime.Greeting")
            .Line(Speaker, emote: 0, speed: 1f, [new Pause(20), new Pause(20), new Pause(20)])
            .Line(Speaker, emote: 0, speed: 1f, new Wave(3))
            .Register();
    }
}
