namespace TrelamiumIII.Core.Dialogue.Definitions;

 class MerchantDialogue : ModDialogue
{
    private const string Speaker = "Merchant";

    protected override void Register()
    {
        Dialogue.Define("Merchant.GoldMine_Greeting")
            .Line(Speaker, emote: 0, speed: 1.3f, new Pause(20))
            .Line(Speaker, emote: 0, speed: 1.15f)
            .Line(Speaker, emote: 0, speed: 1.3f, new Pause(20))
            .Register();

        Dialogue.Define("Merchant.GoldMine_SurplusOre")
            .Line(Speaker, emote: 0, speed: 1.3f, [new Pause(16), new Pause(30)])
            .Line(Speaker, emote: 0, speed: 1.3f, [new Pause(20), new Pause(20)])
            .Line(Speaker, emote: 0, speed: 1.3f)
            .Register();

        Dialogue.Define("Merchant.GoldMine_SurplusBars")
            .Line(Speaker, emote: 0, speed: 1.3f, [new Pause(20), new Pause(20)])
            .Register();

        Dialogue.Define("Merchant.GoldMine_SurplusFurniture")
            .Line(Speaker, emote: 0, speed: 1.3f)
            .Line(Speaker, emote: 0, speed: 1.3f)
            .Line(Speaker, emote: 0, speed: 1.3f)
            .Register();

        Dialogue.Define("Merchant.GoldMine_SurplusTools")
            .Line(Speaker, emote: 0, speed: 1.3f)
            .Line(Speaker, emote: 0, speed: 1.3f)
            .Line(Speaker, emote: 0, speed: 1.3f)
            .Register();
    }
}
