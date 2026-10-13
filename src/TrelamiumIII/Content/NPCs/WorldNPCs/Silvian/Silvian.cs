using System.Collections.Generic;
using System.Linq;
using TrelamiumIII.Core.Dialogue;
using TrelamiumIII.Core.Jobs;
using TrelamiumIII.Core.NPCs.Actors;
using TrelamiumIII.Core.NPCs.Behaviors;
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
        if (NPC.AnyNPCs(Type))
            return 0f;

        return spawnInfo.Player.ZoneOverworldHeight && Main.dayTime && spawnInfo.Player.ZoneForest &&
            !spawnInfo.Player.ZoneCorrupt && !spawnInfo.Player.ZoneCrimson && !spawnInfo.Player.ZoneHallow ? 0.15f : 0f;
    }

    /// <summary>
    ///     Paces his house and garden, or the ground around where he stands when he spawned without one, and chats with
    ///     any NPC that wanders by. Stays behind closed doors at night and in the rain.
    /// </summary>
    protected override IBehavior BuildScript()
    {
        Wander wander = Behavior.Wander(AnchorTile)
            .UsingDoors(() => Main.dayTime && !Main.raining)
            .Chatting();

        if (Home is { } home)
            wander.Within(home.Area).WithRadius(home.Area.Width);

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

        // The first job's closing line asks about botany lessons; this asks again if that was declined or cut short.
        if (silvian.JobStateOf(JobID.HerbalForager) == JobState.Completed && silvian.JobStateOf(JobID.BotanyBasics) == JobState.Locked)
            return ChatEncounter.Play("Silvian.BotanyOffer").Projected();

        if (silvian.AlmanacOpened && !silvian.AskedAboutArgie)
            return ChatEncounter.Play(SilvianEncounter.ArgieKey).Projected();

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
