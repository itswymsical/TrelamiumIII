using System.Collections.Generic;
using TrelamiumIII.Core.Components.NPCs;
using TrelamiumIII.Core.NPCs.Actors;
using TrelamiumIII.Core.NPCs.Controllers;
using TrelamiumIII.Utilities.Extensions;

namespace TrelamiumIII.Content.NPCs;

public class BodyguardNPC : WorldNPC
{
    public enum Phase
    {
        Idle = 0,
        Await,
        Follow,
        Attack,
        MagicMirror
    }

    public Phase State
    {
        get => (Phase)(int)NPC.ai[0];
        private set => NPC.ai[0] = (int)value;
    }

    public ref float StateTimer => ref NPC.ai[1];

    #region inherited stuff
    public override List<string> SetNPCNameList() => ["Sir Reginald", "Geoffrey", "Siegfried", "Sammelot"];

    public override bool CanDespawn => true;
    #endregion

    protected override void SetAppearanceDefaults()
    {
        NPC.HitSound = SoundID.FemaleHit;
        NPC.DeathSound = SoundID.PlayerKilled;

        Male = true;
        ClothingStyle = (int)PlayerClothingStyleID.MaleDefault;
        HairType = 116;
        HairColor = new Color(255, 81, 54);

        SkinColor = new Color(255, 175, 135);
        EyeColor = new Color(153, 229, 80);

        ShirtColor = new Color(203, 219, 252);
        UnderShirtColor = new Color(203, 219, 252);
        PantsColor = new Color(203, 219, 252);

        ShoeColor = new Color(60, 50, 40);

        SetAccessory(0, ItemID.SWATHelmet, vanity: true);
        SetBodySlot(ItemID.JimsBreastplate, vanity: true);
        SetLegSlot(ItemID.JimsLeggings, vanity: true);
    }

    public override bool PreAI()
    {
        return true; // disabled base for now so it doesnt actually despawn
    }

    public override string GetChat()
    {
        return Main.rand.Next(2) switch
        {
            0 => "Keep your eyes peeled... you never know what's lurking about.",
            _ => "I got your back, boss."
        };
    }

    public override void AI()
    {
        base.AI();
    }

    private void Attack()
    {
        // TODO: bodyguards will have a randomized class -- either they use melee weapons, ranged, magic, or controller.
        // Summoner would be kind of odd, so they use any of these weapon categories, anyways...
        // different classes call for different state machines so clench your ass.

        // fetch classType struct;
        // get/read definition data
        // load attack machine
        // attack machine reads struct data
        // attack alghorithm [x] is loaded and armed.

        // execute attacks --> loop attacks
    }
}
