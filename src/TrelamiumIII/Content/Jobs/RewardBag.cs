using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent.UI;
using Terraria.ModLoader.IO;
using TrelamiumIII.Core.Events;
using TrelamiumIII.Core.Jobs;

namespace TrelamiumIII.Content.Jobs;

/// <summary>
/// A finished <see cref="Job"/>'s payout, handed over as a single item the player opens when they
/// have room for it.
/// </summary>
/// <remarks>
///     <para>
///         Paying out five loose items at once is how rewards get lost: a full inventory sends them
///         to the ground, and a player walking away from the quest giver never notices. One bag
///         always fits somewhere, and whatever it holds is written on the tooltip, so nothing is
///         claimed blind.
///     </para>
///     <para>
///         The contents are a <em>snapshot</em> taken when the bag is made, not a live reference to
///         <see cref="Job.Rewards"/>. A bag sitting in a chest across a rebalance still pays what it
///         advertises, and cross-mod rewards keep the item <see cref="JobReward"/> resolved for
///         them. Vanilla's bag pipeline is deliberately not used: <see cref="ModItem.ModifyItemLoot"/>
///         runs once per item <em>type</em> during loading, so it cannot see per-instance contents.
///     </para>
/// </remarks>
public class RewardBag : ModItem
{
    /// <summary>Job this bag was paid out for, or <see cref="JobID.None"/> when it came from elsewhere.</summary>
    public int JobId = JobID.None;

    /// <summary>What opening the bag hands over. Owned instances, safe to hold and clone.</summary>
    public List<Item> Contents = [];

    /// <summary>Upper bound on contents accepted off the wire, so a malformed packet can't allocate freely.</summary>
    private const int MAX_NETWORKED_CONTENTS = 64;

    private static readonly Color JobLineColor = new(255, 210, 110);
    private static readonly Color HintColor = new(150, 150, 150);

    /// <summary>Job id to name key. See <see cref="JobNameKeyFor"/>.</summary>
    private static readonly Dictionary<int, string> NameKeyCache = [];

    public override void SetStaticDefaults()
        // Researchable would turn one job payout into infinite copies of its rewards.
        => Item.ResearchUnlockCount = 0;

    public override void SetDefaults()
    {
        Item.maxStack = Item.CommonMaxStack;
        Item.consumable = true;
        Item.width = Item.height = 24;
        Item.rare = ItemRarityID.Quest;
        Item.value = 0;
    }

    /// <summary>
    /// <see cref="ModItem.Clone(Item)"/> is a shallow copy, which would leave every bag sharing one
    /// contents list -- opening one would empty its neighbours.
    /// </summary>
    public override ModItem Clone(Item newEntity)
    {
        var clone = (RewardBag)base.Clone(newEntity);
        clone.Contents = Contents.Select(content => content.Clone()).ToList();

        return clone;
    }

    #region Creation
    /// <summary>
    /// Builds the bag a job pays out, or <see langword="null"/> when the job has nothing to give.
    /// </summary>
    /// <remarks>
    /// Rewards resolve here rather than at definition time, which is what lets a cross-mod
    /// <see cref="JobReward"/> land in the bag as a real item.
    /// </remarks>
    public static Item? ForJob(Job job)
    {
        var contents = new List<Item>();

        foreach (var reward in job.Rewards)
        {
            var item = reward.GetItem();

            // A copy: the JobReward keeps its resolved item as the definition of the reward, and the
            // bag must not be able to mutate it.
            if (item is { stack: > 0 } && item.type > ItemID.None)
                contents.Add(new Item(item.type, item.stack));
        }

        if (contents.Count == 0)
            return null;

        var bag = new Item(ModContent.ItemType<RewardBag>());

        if (bag.ModItem is RewardBag rewardBag)
        {
            rewardBag.JobId = job.ID;
            rewardBag.Contents = contents;
        }

        return bag;
    }

    /// <summary>Pays <paramref name="job"/> out to <paramref name="player"/> as a single bag.</summary>
    public static void Award(Player player, Job job)
    {
        if (ForJob(job) is { } bag)
            GiveOrDrop(player, bag, new EntitySource_Misc("Job_Reward"));
    }

    /// <summary>
    /// Puts <paramref name="item"/> straight into the player's inventory, dropping only the part
    /// that genuinely does not fit. Direct placement is the whole point of the bag: a world drop is
    /// exactly the outcome it exists to avoid.
    /// </summary>
    public static void GiveOrDrop(Player player, Item item, IEntitySource source)
    {
        // GetItem reaches into one specific player's slots, so it is only meaningful on the client
        // that owns them.
        if (player.whoAmI != Main.myPlayer)
        {
            player.QuickSpawnItem(source, item, item.stack);
            return;
        }

        var leftover = player.GetItem(player.whoAmI, item, GetItemSettings.NPCEntityToPlayerInventorySettings);

        if (!leftover.IsAir)
            player.QuickSpawnItem(source, leftover, leftover.stack);
    }
    #endregion

    #region Opening
    public override bool CanRightClick() => true;

    public override void RightClick(Player player)
    {
        var source = new EntitySource_ItemOpen(player, Type);

        foreach (var content in Contents)
        {
            if (content is null || content.IsAir)
                continue;

            GiveOrDrop(player, content.Clone(), source);
        }

        SoundEngine.PlaySound(SoundID.Grab, player.position);
    }

    /// <summary>
    /// Two bags are only interchangeable if they pay out the same thing; stacking them otherwise
    /// would quietly discard one payout's contents.
    /// </summary>
    public override bool CanStack(Item source)
    {
        if (source.ModItem is not RewardBag other || other.JobId != JobId)
            return false;

        if (other.Contents.Count != Contents.Count)
            return false;

        for (var i = 0; i < Contents.Count; i++)
        {
            if (Contents[i].type != other.Contents[i].type || Contents[i].stack != other.Contents[i].stack)
                return false;
        }

        return true;
    }

    public override bool CanStackInWorld(Item source) => CanStack(source);
    #endregion

    #region Tooltip
    public override void ModifyTooltips(List<TooltipLine> tooltips)
    {
        if (JobNameKeyFor(JobId) is { } nameKey)
        {
            var jobName = TrelamiumIII.Instance.GetLocalization($"Jobs.{nameKey}").Value;

            tooltips.Add(new TooltipLine(Mod, "RewardBagJob",
                this.GetLocalization("PaymentFor", () => "Payment for {0}").Format(jobName))
            {
                OverrideColor = JobLineColor
            });
        }

        if (Contents.Count == 0)
        {
            tooltips.Add(new TooltipLine(Mod, "RewardBagEmpty",
                this.GetLocalization("Empty", () => "Empty").Value)
            {
                OverrideColor = HintColor
            });

            return;
        }

        tooltips.Add(new TooltipLine(Mod, "RewardBagContains",
            this.GetLocalization("Contains", () => "Contains:").Value));

        for (var i = 0; i < Contents.Count; i++)
        {
            var content = Contents[i];

            if (content is null || content.IsAir)
                continue;

            tooltips.Add(new TooltipLine(Mod, $"RewardBagContent{i}", Describe(content))
            {
                OverrideColor = ItemRarity.GetColor(content.rare)
            });
        }

        tooltips.Add(new TooltipLine(Mod, "RewardBagOpen",
            this.GetLocalization("RightClickToOpen", () => "Right click to open").Value)
        {
            OverrideColor = HintColor
        });
    }

    /// <summary>
    /// One content line: vanilla's inline item tag for the icon, then the name. The tag draws its own
    /// stack overlay but clamps it at 999, so the count is spelled out beside it as well.
    /// </summary>
    private static string Describe(Item content)
    {
        if (content.stack <= 1)
            return $"[i:{content.type}] {content.Name}";

        return $"[i/s{content.stack}:{content.type}] {content.Name} ({content.stack})";
    }

    /// <summary>
    /// Localization key for a job's name, or <see langword="null"/> for a bag with no job behind it.
    /// </summary>
    /// <remarks>
    /// Cached because <see cref="EventSystem.CreateJob"/> builds an entire Job to answer, and a
    /// hovered tooltip asks every frame. The <em>key</em> is cached rather than the resolved name so
    /// a language switch still takes effect.
    /// </remarks>
    private static string? JobNameKeyFor(int jobId)
    {
        if (jobId == JobID.None)
            return null;

        if (NameKeyCache.TryGetValue(jobId, out var cached))
            return cached;

        var nameKey = EventSystem.CreateJob(jobId)?.NameKey;

        if (nameKey is not null)
            NameKeyCache[jobId] = nameKey;

        return nameKey;
    }
    #endregion

    #region Serialization & Network
    public override void SaveData(TagCompound tag)
    {
        if (JobId != JobID.None)
            tag["jobId"] = JobId;

        var contents = Contents.Where(content => content is { IsAir: false }).ToList();

        if (contents.Count > 0)
            tag["contents"] = contents.Select(ItemIO.Save).ToList();
    }

    public override void LoadData(TagCompound tag)
    {
        JobId = tag.TryGet("jobId", out int jobId) ? jobId : JobID.None;

        Contents = tag.TryGet("contents", out List<TagCompound> contents)
            ? contents.Select(ItemIO.Load).ToList()
            : [];
    }

    public override void NetSend(BinaryWriter writer)
    {
        var contents = Contents.Where(content => content is { IsAir: false }).ToList();

        writer.Write(JobId);
        writer.Write(contents.Count);

        foreach (var content in contents)
            ItemIO.Send(content, writer, true);
    }

    public override void NetReceive(BinaryReader reader)
    {
        JobId = reader.ReadInt32();

        var count = Math.Clamp(reader.ReadInt32(), 0, MAX_NETWORKED_CONTENTS);

        Contents = new List<Item>(count);

        for (var i = 0; i < count; i++)
        {
            var content = new Item();
            ItemIO.Receive(content, reader, true);

            Contents.Add(content);
        }
    }
    #endregion

    public override void Unload() => NameKeyCache.Clear();
}
