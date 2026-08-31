using TrelamiumIII.Core.NPCs;

namespace TrelamiumIII.Content.NPCs;

public class ShinobiNPC : Villager
{
    //public enum Phase
    //{
    //    Idle = 0,
    //    Await,
    //    Follow,
    //    Attack,
    //    MagicMirror
    //}

    //public Phase State
    //{
    //    get => (Phase)(int)NPC.ai[0];
    //    private set => NPC.ai[0] = (int)value;
    //}

    //public ref float StateTimer => ref NPC.ai[1];

    public override bool CanDespawn => true;

    public override bool UsesTradingSystem => false;

    protected override Color[] HairPalette => [Color.Black, Color.SaddleBrown, Color.LightGoldenrodYellow];

    protected override Color[] EyePalette => [Color.SandyBrown, Color.SaddleBrown, Color.MediumPurple];

    protected override Color[] SkinPallete => 
    [
        new Color(238, 195, 154), 
        new Color(248, 210, 175),
        new Color(252, 226, 201)
    ];

    protected override int[] MaleHairPool => [76, 94, 109, 137];

    protected override int[] FemaleHairPool => [47, 78, 79, 84];

    protected override string[] MaleNames => ["Hanzō", "Kotarō", "Sandayū", "Danzō", "Saizō", "Shin", "Kage"];

    protected override string[] FemaleNames => ["Chiyome", "Mochizuki", "Ukifune", "Kasumi", "Asuka", "Kohaku"];

    protected override void SetAppearanceDefaults()
    {
        base.SetAppearanceDefaults();

        SetBodySlot(ItemID.NinjaShirt);
        SetLegSlot(ItemID.NinjaPants);

        if (Main.rand.NextBool(8))
            SetHeadSlot(ItemID.NinjaHood);
    }

    public override string GetChat()
    {
        return Main.rand.Next(2) switch
        {
            0 => $"Greetings, {Main.LocalPlayer.name}. We have been watching you from afar.",
            1 => "Boo.",
        };
    }
}
