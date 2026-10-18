using System.Collections.Generic;
using System.Linq;
using TrelamiumIII.Content.Items.Quest;
using TrelamiumIII.Core.Dialogue;
using TrelamiumIII.Core.Jobs;
using TrelamiumIII.Core.NPCs;
using TrelamiumIII.Core.NPCs.Actors;
using TrelamiumIII.Core.NPCs.Behaviors;
using TrelamiumIII.Utilities.Extensions;

namespace TrelamiumIII.Content.NPCs.WorldNPCs.Argie;

/// <summary>
///     The mycologist in the glowing mushroom biome, John Substrate's apprentice. She lives in her lab
///     (<see cref="Common.Generation.ArgieLabPass" />), and Silvian's letter is how the player finds her. Meeting her plays
///     as a scene (<see cref="ArgieMeeting" />) that ends on her fungus hunt; her spore survey after it is how the player
///     gets the spore that wakes Fungore.
/// </summary>
public class ArgieNPC : TerrarianNPC, INarrativeSpeaker
{
    public const string GreetingKey = "Argie.Greeting";
    public const string LetterGreetingKey = "Argie.Greeting_Letter";

    public override bool CanDespawn => false;

    public override bool HiddenOnMapUntilMet => true;

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

    /// <summary>Paces her lab floor, or the ground around where she spawned without one.</summary>
    protected override IBehavior BuildScript()
    {
        Wander wander = Behavior.Wander(AnchorTile).WithRadius(12);

        if (Home is { } home)
            wander.Within(home.Area);

        return wander;
    }

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
        var argie = player.GetModPlayer<ArgiePlayer>();
        bool hasLetter = player.HasItem(ModContent.ItemType<SilviansLetter>());

        if (!argie.Met)
        {
            return ChatEncounter.Play(hasLetter ? LetterGreetingKey : GreetingKey)
                .Projected().AsScene().WithMusic(MusicID.Mushrooms);
        }

        // Met her before Silvian handed it over.
        if (hasLetter && !argie.LetterDelivered)
            return ChatEncounter.Play("Argie.LateLetter").Projected().AsScene();

        if (player.GetModPlayer<JobPlayer>().GetAllJobs().Any(j => j.ProviderNPC == Type && j.State == JobState.Active))
            return ChatEncounter.None; // let the jobs handle dialogue

        // The meeting was cut short before it could offer her first job.
        if (argie.JobStateOf(JobID.MycologyHunt) == JobState.Locked)
            return ChatEncounter.Play("Argie.JobOffer").Projected();

        if (argie.JobStateOf(JobID.MycologyHunt) == JobState.Completed && argie.JobStateOf(JobID.SporeSurvey) == JobState.Locked)
            return ChatEncounter.Play("Argie.SurveyOffer").Projected();

        if (ArgieMeeting.SporeDue(player))
            return ChatEncounter.Play("Argie.SporeAgain").Projected();

        return ChatEncounter.None;
    }

    public override string GetChat()
    {
        return Main.rand.Next(5) switch
        {
            0 => "Hello! Hello! Welcome to my laboratory!",
            1 => "Mycelia is my brand! Could you lend me a hand? Perhaps two...",
            2 => "The bioluminescence here increases at night... I haven't slept in four days.",
            3 => "Don't touch anything with your bare hands. I'm not saying something bad will happen. I'm just saying I don't know that something bad won't.",
            _ => "I talk to them sometimes. The mushrooms. Professionally, of course."
        };
    }
}
