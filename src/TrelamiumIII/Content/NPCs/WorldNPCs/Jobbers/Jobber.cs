using System.IO;
using SubworldLibrary;
using Terraria.ModLoader.IO;
using TrelamiumIII.Core.NPCs;

namespace TrelamiumIII.Content.NPCs.WorldNPCs.Jobbers;

/// <summary>
/// Wandering villager with randomized looks and name that offers one repeatable gathering job at a time. Persists only
/// while its job is claimed; at most <see cref="MaxAlive"/> exist, claimed or not.
/// </summary>
public class Jobber : Villager
{
    public const int MaxAlive = 2;

    /// <summary>Ticks between a hand-in and the next request.</summary>
    private const int RequestCooldown = 60 * 30;

    public override bool UsesTradingSystem { get; set; } = false;

    /// <summary>Identifies this jobber across saves and the network. 0 until the server assigns one.</summary>
    public int JobberId { get; private set; }

    /// <summary>What this jobber wants; null between requests. Unchanged while claimed.</summary>
    public JobberRequest? Request { get; private set; }

    private int requestCooldown;

    #region Appearance
    protected override Color[] HairPalette =>
    [
        new(60, 50, 40),
        new(25, 25, 25),
        new(102, 57, 49),
        new(220, 180, 90),
        new(255, 88, 52),
        new(150, 90, 60),
        new(160, 160, 165),
    ];

    protected override Color[] EyePalette =>
    [
        new(80, 162, 224),
        new(153, 229, 80),
        new(120, 80, 60),
        new(60, 50, 40),
        new(110, 110, 120),
    ];

    protected override Color[] SkinPallete =>
    [
        new(255, 125, 90),
        new(241, 196, 169),
        new(228, 186, 161),
        new(198, 123, 71),
        new(188, 105, 82),
        new(171, 115, 99),
        new(120, 75, 55),
    ];

    protected override int[] MaleHairPool => [1, 2, 3, 7, 13, 15, 16, 19, 24, 54, 57, 76, 114, 115, 156];

    protected override int[] FemaleHairPool => [5, 6, 9, 18, 26, 32, 40, 51, 78, 89, 90, 101, 110];

    protected override string[] MaleNames =>
    [
        "Wade", "Hollis", "Cooper", "Dex", "Garrett", "Tobias", "Jonah", "Silas",
        "Otis", "Reuben", "Milo", "Abe", "Lyle", "Bram", "Cal", "Rowan",
    ];

    protected override string[] FemaleNames =>
    [
        "Maude", "Ivy", "Nell", "Rosalind", "Tess", "Greta", "Hazel", "Opal",
        "Winnie", "Dot", "Mabel", "Bea", "Rhea", "Lou", "Juniper", "Rowan",
    ];

    private static readonly Color[] ShirtPalette =
    [
        new(175, 90, 90),
        new(90, 110, 150),
        new(110, 130, 80),
        new(160, 130, 90),
        new(200, 190, 170),
        new(80, 80, 90),
        new(150, 100, 60),
        new(60, 100, 90),
    ];

    private static readonly Color[] PantsPalette =
    [
        new(60, 70, 110),
        new(90, 70, 50),
        new(70, 70, 70),
        new(120, 100, 70),
        new(40, 45, 55),
    ];

    private static readonly Color[] ShoePalette =
    [
        new(80, 60, 40),
        new(50, 40, 35),
        new(100, 80, 60),
        new(60, 60, 60),
    ];

    private static readonly int[] Hats =
    [
        ItemID.MiningHelmet,
        ItemID.SummerHat,
        ItemID.ArchaeologistsHat,
        ItemID.EngineeringHelmet,
        ItemID.Fedora,
    ];

    protected override void SetAppearanceDefaults()
    {
        base.SetAppearanceDefaults();

        ShirtColor = Main.rand.Next(ShirtPalette);
        UnderShirtColor = Main.rand.Next(ShirtPalette);
        PantsColor = Main.rand.Next(PantsPalette);
        ShoeColor = Main.rand.Next(ShoePalette);

        if (Main.rand.NextBool(3))
            SetHeadSlot(Main.rand.Next(Hats));
    }
    #endregion

    #region Chat
    // {0} = amount, {1} = item name.
    private static readonly string[] PitchChat =
    [
        "Say, you wouldn't have {0} {1} on you? I'd pay.",
        "I'm {0} {1} short of a good week. Looking for work?",
        "Looking for {0} {1}. Coin's waiting if you find it.",
    ];

    private static readonly string[] ProgressChat =
    [
        "Still need {0} more {1}. No rush. Well, some rush.",
        "{0} more {1} and we're square.",
    ];

    private static readonly string[] ShortChat =
    [
        "You're {0} {1} short, friend.",
        "Count again. I need {0} more {1}.",
    ];

    private static readonly string[] ReadyChat =
    [
        "That's everything? Hand it over and I'll pay up.",
        "Looks like you've got it all. Let's settle up.",
    ];

    private static readonly string[] ThanksChat =
    [
        "Pleasure doing business.",
        "That's the lot. Here's your pay.",
        "Much obliged. I might have more work in a bit.",
    ];

    private static readonly string[] BusyChat =
    [
        "Already got someone on the job, sorry.",
        "Someone's already working for me. Try me later.",
    ];

    private static readonly string[] IdleChat =
    [
        "Nothing for you right now. Check back in a bit.",
        "Work comes and goes. Give it a minute.",
    ];

    public override string GetChat()
    {
        timeAlive = 0;

        if (JobberJob.ActiveFor(Main.LocalPlayer, this) is { } job)
        {
            return job.Remaining > 0
                ? string.Format(Main.rand.Next(ProgressChat), job.Remaining, job.Request.ItemName)
                : Main.rand.Next(ReadyChat);
        }

        if (HasActiveJob)
            return Main.rand.Next(BusyChat);

        return Request is { } request
            ? string.Format(Main.rand.Next(PitchChat), request.Count, request.ItemName)
            : Main.rand.Next(IdleChat);
    }

    public override void SetChatButtons(ref string button, ref string button2)
    {
        if (JobberJob.ActiveFor(Main.LocalPlayer, this) is not null)
            button = "Hand in";
    }

    public override void OnChatButtonClicked(bool firstButton, ref string shop)
    {
        if (!firstButton || JobberJob.ActiveFor(Main.LocalPlayer, this) is not { } job)
            return;

        Main.npcChatText = job.TryHandIn()
            ? Main.rand.Next(ThanksChat)
            : string.Format(Main.rand.Next(ShortChat), job.Remaining, job.Request.ItemName);
    }
    #endregion

    #region Spawning & upkeep
    public override float SpawnChance(NPCSpawnInfo spawnInfo)
    {
        Player player = spawnInfo.Player;

        if (NPC.CountNPCS(Type) >= MaxAlive || SubworldSystem.AnyActive())
            return 0f;

        if (!Main.dayTime || !player.ZoneOverworldHeight || Main.invasionType > 0 || Main.eclipse)
            return 0f;

        return player.ZoneCorrupt || player.ZoneCrimson || player.ZoneHallow ? 0f : 0.08f;
    }

    public override void PostAI()
    {
        base.PostAI();

        if (Main.netMode == NetmodeID.MultiplayerClient || !NPC.active)
            return;

        if (IsSurplus())
        {
            Despawn();
            return;
        }

        if (JobberId == 0)
        {
            JobberId = Main.rand.Next(1, int.MaxValue);
            NPC.netUpdate = true;
        }

        if (Request is not null || HasActiveJob)
            return;

        if (requestCooldown > 0)
        {
            requestCooldown--;
            return;
        }

        Request = JobberRequests.Roll();
        NPC.netUpdate = true;
    }

    /// <summary>True for an unclaimed jobber past the first <see cref="MaxAlive"/> by slot, e.g. from a same-tick spawn.</summary>
    private bool IsSurplus()
    {
        if (HasActiveJob)
            return false;

        int lower = 0;

        foreach (NPC other in Main.ActiveNPCs)
        {
            if (other.whoAmI >= NPC.whoAmI)
                break;

            if (other.type == Type)
                lower++;
        }

        return lower >= MaxAlive;
    }

    public static Jobber? Find(int jobberId)
    {
        if (jobberId == 0)
            return null;

        foreach (NPC npc in Main.ActiveNPCs)
        {
            if (npc.ModNPC is Jobber jobber && jobber.JobberId == jobberId)
                return jobber;
        }

        return null;
    }
    #endregion

    #region Claiming
    /// <summary>Marks the request taken: the jobber stops offering it and no longer despawns.</summary>
    public void Claim()
    {
        HasActiveJob = true;
        NPC.netUpdate = true;
        SendClaim(claimed: true);
    }

    /// <summary>Frees the jobber after a hand-in. The next request rolls after <see cref="RequestCooldown"/>.</summary>
    public void CompleteRequest()
    {
        ApplyCompletion();
        SendClaim(claimed: false);
    }

    private void ApplyCompletion()
    {
        HasActiveJob = false;
        Request = null;
        requestCooldown = RequestCooldown;
        timeAlive = 0;
        NPC.netUpdate = true;
    }

    /// <summary>Client to server. The server applies it and syncs back through <see cref="SendExtraAI"/>.</summary>
    private void SendClaim(bool claimed)
    {
        if (Main.netMode != NetmodeID.MultiplayerClient)
            return;

        ModPacket packet = Mod.GetPacket();

        packet.Write((byte)TrelamiumIII.MessageType.JobberClaim);
        packet.Write((byte)NPC.whoAmI);
        packet.Write(JobberId);
        packet.Write(claimed);

        packet.Send();
    }

    internal static void ReceiveClaim(BinaryReader reader)
    {
        int whoAmI = reader.ReadByte();
        int jobberId = reader.ReadInt32();
        bool claimed = reader.ReadBoolean();

        if (Main.netMode != NetmodeID.Server || whoAmI >= Main.maxNPCs)
            return;

        if (Main.npc[whoAmI] is not { active: true, ModNPC: Jobber jobber } || jobber.JobberId != jobberId)
            return;

        if (claimed)
        {
            jobber.HasActiveJob = true;
            jobber.NPC.netUpdate = true;
        }
        else
        {
            jobber.ApplyCompletion();
        }
    }
    #endregion

    #region Persistence & sync
    public override void SaveData(TagCompound tag)
    {
        base.SaveData(tag);

        tag["jobberId"] = JobberId;
        tag["requestCooldown"] = requestCooldown;
        Request?.Save(tag);
    }

    public override void LoadData(TagCompound tag)
    {
        base.LoadData(tag);

        JobberId = tag.GetInt("jobberId");
        requestCooldown = tag.GetInt("requestCooldown");
        Request = JobberRequest.Load(tag);
    }

    public override void SendExtraAI(BinaryWriter writer)
    {
        base.SendExtraAI(writer);
        writer.Write(JobberId);
        writer.Write(HasActiveJob);
        JobberRequest.Write(writer, Request);
    }

    public override void ReceiveExtraAI(BinaryReader reader)
    {
        base.ReceiveExtraAI(reader);
        JobberId = reader.ReadInt32();
        HasActiveJob = reader.ReadBoolean();
        Request = JobberRequest.Read(reader);
    }
    #endregion
}
