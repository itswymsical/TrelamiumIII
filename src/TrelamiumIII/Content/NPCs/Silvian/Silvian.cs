using System.Collections.Generic;
using System.Linq;
using Terraria.ModLoader.IO;
using TrelamiumIII.Core.Dialogue;
using TrelamiumIII.Core.Jobs;
using TrelamiumIII.Core.NPCs.Actors;
using TrelamiumIII.Utilities.Extensions;

namespace TrelamiumIII.Content.NPCs.Silvian;

[LegacyName("Silva")]
public class Silvian : TerrarianNPC, INarrativeSpeaker
{
    public override bool CanDespawn => false;

    public override List<string> SetNPCNameList() => ["Silvian"];

    protected override void SetAppearanceDefaults()
    {
        Male = true;
        ClothingStyle = PlayerClothingStyleID.MaleDefault;
        SkinColor = new Color(205, 135, 126);
        EyeColor = new Color(37, 38, 38);
        PantsColor = new Color(236, 247, 248);
        ShoeColor = new Color(77, 78, 78);
        HairType = 72;
        HairColor = new Color(236, 247, 248);

        SetBodySlot(ItemID.CowboyJacket);

        NPC.HitSound = SoundID.NPCHit1;
        NPC.DeathSound = SoundID.PlayerKilled;
    }

    public override float SpawnChance(NPCSpawnInfo spawnInfo)
    {
        // He never despawns and persists with the world, so every extra spawn would be permanent.
        if (NPC.AnyNPCs(Type))
            return 0f;

        return spawnInfo.Player.ZoneOverworldHeight && Main.dayTime && spawnInfo.Player.ZoneForest &&
            !spawnInfo.Player.ZoneCorrupt && !spawnInfo.Player.ZoneCrimson && !spawnInfo.Player.ZoneHallow ? 0.15f : 0f;
    }

    public override void PostAI()
    {
        base.PostAI();

        // Removes duplicates already saved into a world: only the lowest slot stays.
        if (Main.netMode != NetmodeID.MultiplayerClient && HasLowerSlotDuplicate())
            Despawn();
    }

    private bool HasLowerSlotDuplicate()
    {
        foreach (NPC other in Main.ActiveNPCs)
        {
            if (other.whoAmI >= NPC.whoAmI)
                return false;

            if (other.type == Type)
                return true;
        }

        return false;
    }

    public ChatEncounter GetEncounter(Player player)
    {
        if (player.GetModPlayer<JobPlayer>().GetAllJobs().Any(j => j.ProviderNPC == this.Type && j.State == JobState.Active))
            return ChatEncounter.None; // let the jobs handle dialogue

        if (!SilvianSystem.firstChatHappened)
        {
            SilvianSystem.firstChatHappened = true;
            return ChatEncounter.Play("Silvian.Greeting").Projected().WithMusic(Assets.Music.HomeForagerMusic.Slot);
        }

        return ChatEncounter.None; // todo: batch of "banter" dialogue
    }

    private static ChatEncounter Banters()
    {
        int randomChoice = Main.rand.Next(3);

        return randomChoice switch
        {
            0 => ChatEncounter.Play("Silvian.Banter1"),
            1 => ChatEncounter.Play("Silvian.Banter2"),
            2 => ChatEncounter.Play("Silvian.Banter3"),
            _ => ChatEncounter.None,
        };
    }

    //protected override IBehavior BuildScript()
    //{
    //    return base.BuildScript();
    //}
}

public class SilvianSystem : ModSystem
{
    public static bool firstChatHappened;

    public override void ClearWorld()
    {
        firstChatHappened = false;
    }

    public override void SaveWorldData(TagCompound tag)
    {
        if (firstChatHappened)
        {
            tag[nameof(firstChatHappened)] = true;
        }
    }

    public override void LoadWorldData(TagCompound tag)
    {
        firstChatHappened = tag.ContainsKey(nameof(firstChatHappened));
    }
}