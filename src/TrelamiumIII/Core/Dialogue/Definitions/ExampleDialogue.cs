namespace TrelamiumIII.Core.Dialogue.Definitions;

class ExampleDialogue : ModDialogue
{
    private const string Speaker = "WanderingEmployer";

    protected override void Register()
    {
        Dialogue.Define("Example.Greeting")
            .Line(Speaker, emote: 0, speed: 1f, new Pause(20))
            .Line(Speaker, emote: 0, speed: 1f, new Wave())
            .Register();

        Dialogue.Define("Example.Banter")
            .Line(Speaker, emote: 0, speed: 1.3f, [new Pause(8), new Pause(8)])
            .Line(Speaker, emote: 0, speed: 1f)
            .Register();

        Dialogue.Define("Example.Inflection")
            .Line(Speaker, emote: 0, speed: 1f, [Inflection.Emphasis(0.25f, lift: 2f), new Pause(18), Inflection.Question()])
            .Line(Speaker, emote: 0, speed: 1f, [new Inflection(0.75f, shape: InflectionShape.Settle), new Pause(16), Inflection.Waver()])
            .Line(Speaker, emote: 0, speed: 1f, [new Inflection(1.4f, steps: 4, lift: 3f), new Inflection(0.7f, speed: 0.6f, lift: 3f)])
            .Register();
    }
}