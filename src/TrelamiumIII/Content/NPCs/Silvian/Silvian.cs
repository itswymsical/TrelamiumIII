using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria.ModLoader.IO;
using TrelamiumIII.Core.Dialogue;
using TrelamiumIII.Core.Jobs;
using TrelamiumIII.Core.NPCs;
using TrelamiumIII.Core.NPCs.Actors;
using TrelamiumIII.Utilities.Extensions;

namespace TrelamiumIII.Content.NPCs.Silvian;

[LegacyName("Silva")]
public class Silvian : TerrarianNPC, INarrativeSpeaker
{
    public override bool CanDespawn => false;

    public override bool Persistent => false;
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
        return spawnInfo.Player.ZoneOverworldHeight && Main.dayTime && spawnInfo.Player.ZoneForest &&
            !spawnInfo.Player.ZoneCorrupt && !spawnInfo.Player.ZoneCrimson && !spawnInfo.Player.ZoneHallow ? 0.15f : 0f;
    }
    public ChatEncounter GetEncounter(Player player)
    {
        if (player.GetModPlayer<JobPlayer>().GetAllJobs().Any(j => j.ProviderNPC == this.Type && j.State == JobState.Active))
            return ChatEncounter.None; // let the jobs handle dialogue

        if (!SilvianSystem.firstEncounter)
        {
            SilvianSystem.firstEncounter = true;
            return ChatEncounter.Play("Silvian.Greeting").Projected().WithMusic(Assets.Music.HomeForagerMusic.Slot);
        }

        return ChatEncounter.None; // todo: batch of "banter" dialogue
    }

    private static ChatEncounter Banters()
    {
        int randomChoice = Main.rand.Next(3);

        switch (randomChoice)
        {
            case 0:
                return ChatEncounter.Play("Silvian.Banter1");
            case 1:
                return ChatEncounter.Play("Silvian.Banter2");
            case 2:
                return ChatEncounter.Play("Silvian.Banter3");
            default:
                return ChatEncounter.None;
        }
    }

    //protected override IBehavior BuildScript()
    //{
    //    return base.BuildScript();
    //}
}

public class SilvianSystem : ModSystem
{
    public static bool firstEncounter;

    public override void ClearWorld()
    {
        firstEncounter = false;
    }

    public override void SaveWorldData(TagCompound tag)
    {
        if (firstEncounter)
        {
            tag[nameof(firstEncounter)] = true;
        }
    }

    public override void LoadWorldData(TagCompound tag)
    {
        firstEncounter = tag.ContainsKey(nameof(firstEncounter));
    }
}