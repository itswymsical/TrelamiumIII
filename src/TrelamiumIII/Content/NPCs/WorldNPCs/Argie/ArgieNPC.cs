using System.Collections.Generic;
using System.Linq;
using TrelamiumIII.Content.NPCs.WorldNPCs.Silvian;
using TrelamiumIII.Core.Dialogue;
using TrelamiumIII.Core.Jobs;
using TrelamiumIII.Core.NPCs;
using TrelamiumIII.Core.NPCs.Actors;
using TrelamiumIII.Utilities.Extensions;

namespace TrelamiumIII.Content.NPCs.WorldNPCs.Argie;

public class ArgieNPC : TerrarianNPC, INarrativeSpeaker
{
    public override bool CanDespawn => false;

    public override List<string> SetNPCNameList() => ["Argie"];

    protected override void SetAppearanceDefaults()
    {
        Male = false;
        ClothingStyle = PlayerClothingStyleID.FemaleLabcoat;
        HairType = HairstyleID.LongHairBabyHairStickingOut;
        HairColor = new Color(255, 81, 54);

        SkinColor = new Color(255, 175, 135);
        EyeColor = new Color(153, 229, 80);

        ShirtColor = new Color(203, 219, 252);
        UnderShirtColor = new Color(203, 219, 252);
        PantsColor = new Color(203, 219, 252);

        ShoeColor = new Color(60, 50, 40);

        SetAccessory(0, ItemID.SpectreGoggles);
        SetBodySlot(ItemID.FamiliarShirt);
        SetLegSlot(ItemID.ClothierPants);

        NPC.HitSound = SoundID.NPCHit1;
        NPC.DeathSound = SoundID.PlayerKilled;
    }

    //public override float SpawnChance(NPCSpawnInfo spawnInfo)
    //{
    //    if (NPC.AnyNPCs(Type))
    //        return 0f;

    //    return spawnInfo.Player.ZoneOverworldHeight && Main.dayTime && spawnInfo.Player.ZoneForest &&
    //        !spawnInfo.Player.ZoneCorrupt && !spawnInfo.Player.ZoneCrimson && !spawnInfo.Player.ZoneHallow ? 0.15f : 0f;
    //}

    public override void PostAI()
    {
        base.PostAI();

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
        if (player.GetModPlayer<JobPlayer>().GetAllJobs().Any(j => j.ProviderNPC == Type && j.State == JobState.Active))
            return ChatEncounter.None; // let the jobs handle dialogue

        var silvian = player.GetModPlayer<SilvianPlayer>();

        // Talked to before the encounter could trigger, so the greeting plays as a small scene of its own.
        if (!silvian.Met)
            return ChatEncounter.Play(SilvianEncounter.GreetingKey).Projected().AsScene().WithMusic(Assets.Music.HomeForagerMusic.Slot);

        if (SilvianAlmanac.IsDue(player))
            return ChatEncounter.Play(SilvianAlmanac.HandoffKey).Projected();

        // The greeting was cut short before it could offer his first job.
        if (silvian.JobStateOf(JobID.HerbalForager) == JobState.Locked)
            return ChatEncounter.Play(SilvianEncounter.JobOfferKey).Projected();

        return ChatEncounter.None;
    }

    public override string GetChat()
    {
        return Main.rand.Next(4) switch
        {
            0 => "Hello! Hello! Welcome to my Labratory!",
            1 => "Mycelia is my brand! Could you lend me a hand? Perhaps two...",
            2 => "The bioluminescence here increases at night... I haven't slept in four days.",
            3 => "Don't touch anything with your bare hands. I'm not saying something bad will happen. I'm just I don't know that something bad won't.",
            _ => "I talk to them sometimes. The mushrooms. Professionally, of course."
        };
    }

    //public override void SetChatButtons(ref string button, ref string button2) => button = SilvianCounsel.HelpButton;
}
