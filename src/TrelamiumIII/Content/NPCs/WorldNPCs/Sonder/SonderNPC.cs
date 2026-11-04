using System.Collections.Generic;
using Terraria.ModLoader.IO;
using TrelamiumIII.Common.Systems;
using TrelamiumIII.Core.Dialogue;
using TrelamiumIII.Core.NPCs;
using TrelamiumIII.Core.NPCs.Actors;
using TrelamiumIII.Utilities;
using TrelamiumIII.Utilities.Extensions;

namespace TrelamiumIII.Content.NPCs.WorldNPCs.Sonder;

//public class SonderNPC : TerrarianNPC, INarrativeSpeaker
//{
//    public override bool CanDespawn => false;
//    public override bool HiddenOnMapUntilMet => true;

//    public override List<string> SetNPCNameList() => ["Sonder"];

//    protected override void SetAppearanceDefaults()
//    {
//        Male = false;
//        ClothingStyle = PlayerClothingStyleID.MaleDefault;
//        HairType = HairstyleID.Bald;
//        HairColor = new Color(0, 0, 0);

//        SkinColor = new Color(128, 128, 128);
//        EyeColor = new Color(69, 0, 138);

//        ShirtColor = new Color(128, 128, 128);
//        UnderShirtColor = new Color(128, 128, 128);
//        PantsColor = new Color(128, 128, 128);

//        ShoeColor = new Color(0, 0, 0);

//        SetHeadSlot(ItemID.ReaperHood);
//        SetBodySlot(ItemID.TimelessTravelerRobe);
//        SetLegSlot(ItemID.TimelessTravelerBottom);

//        NPC.HitSound = SoundID.NPCHit1;
//        NPC.DeathSound = SoundID.PlayerKilled;
//    }

//    public ChatEncounter GetEncounter(Player player)
//        => ChatEncounter.Play("Sonder.SonderGreeting");

//    public override float SpawnChance(NPCSpawnInfo spawnInfo)
//    {
//        if (NPC.AnyNPCs(Type))
//            return 0f;
//        var Appearance1 = spawnInfo.Player.ZoneOverworldHeight && !Main.dayTime && spawnInfo.Player.ZoneForest &&
//            WorldTimeSystem.TotalDaysElapsed == 0 /*&& !SonderPlayer.HasMet*/;

//        var Appearance2 = spawnInfo.Player.ZoneOverworldHeight && Main.dayTime && spawnInfo.Player.ZoneForest &&
//            WorldTimeSystem.TotalDaysElapsed == 1;

//        return Appearance1 ? 0.45f : 0f;
//    }

//    // Todo: despawn after dialogue, set Met condition to true on dialogue activation.
//    // Needs to be handled with other systems; DialogueSystem, WorldTimeSystem, and SonderPlayer.

//    public override void PostAI()
//    {
//        base.PostAI();

//        if (Main.netMode != NetmodeID.MultiplayerClient && HasLowerSlotDuplicate())
//            Despawn();
//    }

//    private bool HasLowerSlotDuplicate()
//    {
//        foreach (NPC other in Main.ActiveNPCs)
//        {
//            if (other.whoAmI >= NPC.whoAmI)
//                return false;

//            if (other.type == Type)
//                return true;
//        }

//        return false;
//    }
//}

//public class SonderPlayer : ModPlayer
//{
//    public static bool HasMet;

//    public override void SaveData(TagCompound tag)
//    {
//        tag["HasMetSonder"] = HasMet;
//    }
//    public override void LoadData(TagCompound tag)
//    {
//        HasMet = tag.GetBool("HasMetSonder");
//    }
//}