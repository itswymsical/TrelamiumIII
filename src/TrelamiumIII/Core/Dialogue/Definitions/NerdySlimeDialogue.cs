namespace TrelamiumIII.Core.Dialogue.Definitions;

class NerdySlimeDialogue : ModDialogue
{
    private const string Speaker = "NerdySlime";

    protected override void Register()
    {
        Dialogue.Define("NerdySlime.Greeting")
            .Line(Speaker, emote: 0, speed: 1f, new Pause(18))
            .Register();
    }
}
