using System.Collections.Generic;
using System.Linq;
using TrelamiumIII.Core.Dialogue;
using TrelamiumIII.Core.Jobs;
using TrelamiumIII.Core.NPCs.Actors;
using TrelamiumIII.Core.NPCs.Combat;
using TrelamiumIII.Utilities.Extensions;

namespace TrelamiumIII.Content.NPCs.WorldNPCs.Silvian;

/// <summary>
///     John Substrate's nephew, and the way into alchemy. The player meets him in a short scene
///     (<see cref="SilvianEncounter" />), works his jobs, and in the end inherits his uncle's almanac
///     (<see cref="SilvianAlmanac" />). His chat window's Help button answers questions about anything alchemical the
///     player holds up (<see cref="SilvianCounsel" />).
/// </summary>
[LegacyName("Silva")]
public class Silvian : TerrarianNPC, INarrativeSpeaker
{
    public override bool CanDespawn => false;

    public override bool HiddenOnMapUntilMet => true;

    public override List<string> SetNPCNameList() => ["Silvian"];

    protected override void SetAppearanceDefaults()
    {
        Male = true;
        ClothingStyle = PlayerClothingStyleID.MaleDefault;
        SkinColor = new Color(210, 140, 98);
        EyeColor = new Color(37, 38, 38);
        PantsColor = new Color(236, 247, 248);
        ShoeColor = new Color(77, 78, 78);
        HairType = 72;
        HairColor = new Color(236, 247, 248);

        SetBodySlot(ItemID.CowboyJacket);

        NPC.HitSound = SoundID.NPCHit1;
        NPC.DeathSound = SoundID.PlayerKilled;
    }

    /// <summary>Paces his house and garden, or 12 tiles around his anchor without one.</summary>
    protected override int WanderRadius => Home?.Area.Width ?? 12;

    /// <summary>Stays behind closed doors at night and in the rain.</summary>
    protected override bool CanUseDoors => Main.dayTime && !Main.raining;

    protected override int ChatChance => 300;

    /// <summary>Cuts down anything that comes close with his sickle.</summary>
    public override int CombatWeapon => ItemID.Sickle;

    protected override CombatStyle GetCombatStyle() => CombatStyle.Melee with { DetectRange = 160f, LeashRange = 200f };

    /// <summary>Paces his house and garden, or 12 tiles around his anchor without one.</summary>
    protected override int WanderRadius => Home?.Area.Width ?? 12;

    /// <summary>Stays behind closed doors at night and in the rain.</summary>
    protected override bool CanUseDoors => Main.dayTime && !Main.raining;

    protected override int ChatChance => 300;

    /// <summary>Cuts down anything that comes close with his sickle.</summary>
    public override int CombatWeapon => ItemID.Sickle;

    protected override CombatStyle GetCombatStyle() => CombatStyle.Melee with { DetectRange = 160f, LeashRange = 200f };

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
<<<<<<< HEAD
            return ChatEncounter.Play(SilvianEncounter.GreetingKey).AsScene().WithMusic(Assets.Music.Silvian.SilviansTheme.Slot);
=======
            return ChatEncounter.Play(SilvianEncounter.GreetingKey).AsScene().WithMusic(Assets.Music.HomeForagerMusic.Slot);
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb

        if (SilvianAlmanac.IsDue(player))
            return ChatEncounter.Play(SilvianAlmanac.HandoffKey);

        // The greeting was cut short before it could offer his first job.
        if (silvian.JobStateOf(JobID.HerbalForager) == JobState.Locked)
            return ChatEncounter.Play(SilvianEncounter.JobOfferKey);

        // The first job's closing line asks about botany lessons; this asks again if that was declined or cut short.
        if (silvian.JobStateOf(JobID.HerbalForager) == JobState.Completed && silvian.JobStateOf(JobID.BotanyBasics) == JobState.Locked)
            return ChatEncounter.Play("Silvian.BotanyOffer");

        if (silvian.AlmanacOpened && !silvian.AskedAboutArgie)
            return ChatEncounter.Play(SilvianEncounter.ArgieKey);

        return ChatEncounter.None;
    }

    public override string GetChat() => SilvianCounsel.Chat(Main.LocalPlayer);

    public override void SetChatButtons(ref string button, ref string button2) => button = SilvianCounsel.HelpButton;

    /// <remarks>
    ///     Like the Guide's crafting button, a press with nothing to talk about opens the inventory, so there is something to
    ///     pick up and ask about next.
    /// </remarks>
    public override void OnChatButtonClicked(bool firstButton, ref string shop)
    {
        if (!firstButton)
            return;

        Player player = Main.LocalPlayer;

        Main.npcChatText = SilvianCounsel.Answer(player, Main.mouseItem, player.HeldItem, out int subject);
        Main.npcChatCornerItem = subject;

        if (subject == 0)
            Main.playerInventory = true;
    }
}
