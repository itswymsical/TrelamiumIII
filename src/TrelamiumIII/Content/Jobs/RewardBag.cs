using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent.UI;
using Terraria.ModLoader.IO;
using TrelamiumIII.Core.Events;
using TrelamiumIII.Core.Jobs;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Content.Jobs;

/// <summary>
/// A finished <see cref="Job"/>'s payout as one openable item, so a full inventory can't scatter rewards. Contents are a
/// snapshot taken at creation, not <see cref="Job.Rewards"/>, since <see cref="ModItem.ModifyItemLoot"/> can't see instances.
/// </summary>
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

    /// <summary>Deep-copies contents; the base shallow copy would share one list between bags.</summary>
    public override ModItem Clone(Item newEntity)
    {
        var clone = (RewardBag)base.Clone(newEntity);
        clone.Contents = Contents.Select(content => content.Clone()).ToList();

        return clone;
    }

    #region Creation
    /// <summary>
    /// The bag a job pays out, or <see langword="null"/> if it has nothing to give. Rewards resolve here, so cross-mod ones are real items.
    /// </summary>
    public static Item? ForJob(Job job)
    {
        var contents = new List<Item>();

        foreach (var reward in job.Rewards)
        {
            var item = reward.GetItem();

            // A clone rather than a fresh Item of the same type: a cross-mod reward may carry
            // per-instance data, and rebuilding it from type and stack alone would drop it.
            if (item is { stack: > 0 } && item.type > ItemID.None)
                contents.Add(item.Clone());
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
    /// Puts <paramref name="item"/> straight into the inventory, dropping only what doesn't fit. Raises its own pickup popup,
    /// since direct placement bypasses the path that would announce it.
    /// </summary>
    public static void GiveOrDrop(Player player, Item item, IEntitySource source)
    {
        // GetItem reaches into one specific player's slots, so it is only meaningful on the client
        // that owns them.
        if (player.whoAmI != Main.myPlayer)
        {
            Item.RequestNewItem(source, player.Center, item);
            return;
        }

        // GetItem consumes what it takes out of the instance handed to it, so both the display copy
        // and the requested count have to be read first.
        var announced = item.Clone();
        var requested = item.stack;

        var leftover = player.GetItem(item, GetItemSettings.GiftRecieved);
        var taken = requested - (leftover.IsAir ? 0 : leftover.stack);

        if (taken > 0)
            PopupText.NewText(new AdvancedPopupRequest
            {
                Text = requested > 1 ? $"{announced.Name} ({requested})" : announced.Name,
                DurationInFrames = 90,
                Velocity = new Vector2(0f, -4f),
                Color = RarityColorUtil.GetPopupRarityColor(announced.rare)
            }, player.Center);

        // Only the part that fit is announced here; the overflow drops and announces itself through
        // the normal pickup path, so nothing is counted twice.
        if (!leftover.IsAir)
            Item.RequestNewItem(source, player.Center, leftover);
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

    /// <summary>Bags only stack if they pay out the same thing, or one payout would be lost.</summary>
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

    public override bool CanStackInWorld(WorldItem destination, WorldItem source) => CanStack(source.inner);
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
                Color = JobLineColor
            });
        }

        if (Contents.Count == 0)
        {
            tooltips.Add(new TooltipLine(Mod, "RewardBagEmpty",
                this.GetLocalization("Empty", () => "Empty").Value)
            {
                Color = HintColor
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
                Color = ItemRarity.GetColor(content.rare)
            });
        }

        tooltips.Add(new TooltipLine(Mod, "RewardBagOpen",
            this.GetLocalization("RightClickToOpen", () => "Right click to open").Value)
        {
            Color = HintColor
        });
    }

    /// <summary>One content line: the item tag icon and name, with the stack spelled out since the tag caps at 999.</summary>
    private static string Describe(Item content)
    {
        if (content.stack <= 1)
            return $"[i:{content.type}] {content.Name}";

        return $"[i/s{content.stack}:{content.type}] {content.Name} ({content.stack})";
    }

    /// <summary>Localization key for a job's name, or <see langword="null"/> for a bag with no job.</summary>
    private static string? JobNameKeyFor(int jobId)
        => jobId != JobID.None && EventSystem.TryGetJobInfo(jobId, out var info) ? info.NameKey : null;
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

}
