using System.Collections.Generic;
using System.IO;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader.IO;
using Terraria.UI;
using TrelamiumIII.Core.Events;

namespace TrelamiumIII.Core.Jobs;

/// <summary>
/// Reserves an item for a job: it stays in the player's hands, but it cannot be spent, worn, fired,
/// sold, researched, trashed, or stored anywhere until the job releases it.
/// </summary>
/// <remarks>
///     <para>
///         The previous version asked nicely. It set <see cref="Item.favorited"/>, which stops a
///         quick-stack and a shift-trash and nothing else -- the player could still drop the item in a
///         chest, fire it as ammo, or wear it, and any of those could strand a job with no way to
///         finish. Worse, it also cleared favourites the <em>player</em> had set, and zeroed
///         <see cref="Item.value"/> and <see cref="Item.rare"/> without recording what they had been,
///         so unmarking left the item permanently worthless.
///     </para>
///     <para>
///         What replaces it is enforcement at every exit an item has. The <c>Can*</c> hooks cover use,
///         ammo and accessories; <see cref="ModPlayer.CanSellItem"/> covers the shop; and the one
///         remaining class of exit -- chest slots, armour slots, the trash, the piggy bank, the void
///         vault -- all funnel through <see cref="ItemSlot.PickItemMovementAction"/>, so a single
///         detour closes them together.
///     </para>
/// </remarks>
public class MarkedItem : GlobalItem
{
    public override bool InstancePerEntity => true;

    /// <summary>
    /// Which job reserved this item, or <see cref="JobID.None"/>. An id rather than a bare flag: two
    /// jobs wanting copper at the same time have to be able to tell their stacks apart, and a
    /// hand-in has to take back only its own.
    /// </summary>
    private int jobId = JobID.None;

    /// <summary>
    /// What the item looked like before it was reserved, so unmarking restores it rather than leaving
    /// a permanently valueless, permanently Quest-rarity item behind.
    /// </summary>
    private int originalValue;
    private int originalRare;

    public bool IsMarked => jobId != JobID.None;

    /// <summary>
    /// Attached to everything, because anything can in principle be asked for by a job. The cost that
    /// carries is kept to the wire format -- see <see cref="NetSend"/>, which spends one byte on an
    /// unmarked item rather than four.
    /// </summary>
    public override bool AppliesToEntity(Item item, bool lateInstantiation) => true;

    #region Marking
    /// <summary>Reserves <paramref name="item"/> for <paramref name="jobId"/>.</summary>
    public static void MarkFor(Item item, int jobId)
    {
        if (item is not { IsAir: false } || jobId == JobID.None || !item.TryGetGlobalItem(out MarkedItem marked))
            return;

        if (!marked.IsMarked)
        {
            marked.originalValue = item.value;
            marked.originalRare = item.rare;
        }

        marked.jobId = jobId;

        item.value = 0;
        item.rare = ItemRarityID.Quest;
    }

    /// <summary>Releases <paramref name="item"/>, restoring what marking overwrote.</summary>
    public static void Unmark(Item item)
    {
        if (item is not { IsAir: false } || !item.TryGetGlobalItem(out MarkedItem marked) || !marked.IsMarked)
            return;

        item.value = marked.originalValue;
        item.rare = marked.originalRare;

        marked.jobId = JobID.None;
        marked.originalValue = 0;
        marked.originalRare = 0;
    }

    public static bool IsMarkedForJob(Item item) => MarkedJobId(item) != JobID.None;

    /// <summary>True only for the job that reserved it.</summary>
    public static bool IsMarkedFor(Item item, int jobId)
        => jobId != JobID.None && MarkedJobId(item) == jobId;

    /// <summary>
    /// Null-tolerant on purpose: this is reached from a detour on vanilla's slot-movement code, where
    /// throwing would take the whole inventory down with it.
    /// </summary>
    public static int MarkedJobId(Item item)
        => item is { IsAir: false } && item.TryGetGlobalItem(out MarkedItem marked)
            ? marked.jobId
            : JobID.None;
    #endregion

    #region Prevention
    // Splitting a marked stack has to carry the mark, or half of a delivery quietly becomes spendable.
    public override GlobalItem Clone(Item from, Item to)
    {
        var clone = (MarkedItem)base.Clone(from, to);

        clone.jobId = jobId;
        clone.originalValue = originalValue;
        clone.originalRare = originalRare;

        return clone;
    }

    /// <summary>Reserved stacks only merge with stacks reserved by the same job.</summary>
    public override bool CanStack(Item destination, Item source)
        => MarkedJobId(destination) == MarkedJobId(source);

    public override bool CanStackInWorld(Item destination, Item source) => CanStack(destination, source);

    public override bool CanUseItem(Item item, Player player) => !IsMarked;

    // Both ammo hooks run against the globals on the weapon *and* on the ammo, so they have to test
    // the ammo parameter rather than this instance's own flag -- which may belong to the weapon.
    // Null on the first leaves undecided ammo to everyone else.
    public override bool? CanBeChosenAsAmmo(Item ammo, Item weapon, Player player)
        => IsMarkedForJob(ammo) ? false : null;

    public override bool CanBeConsumedAsAmmo(Item ammo, Item weapon, Player player)
        => !IsMarkedForJob(ammo);

    public override bool CanEquipAccessory(Item item, Player player, int slot, bool modded) => !IsMarked;

    public override bool CanResearch(Item item) => !IsMarked;

    public override bool CanReforge(Item item) => !IsMarked;

    // No CanRightClick override: tModLoader ORs that hook across globals, so a false here cannot veto
    // anything -- it would be dead code implying a guarantee it does not make. Right-clicking only
    // matters for openable containers, and a job has no business reserving one of those.
    #endregion

    #region Presentation
    public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
    {
        if (!IsMarked)
            return;

        var jobName = EventSystem.TryGetJobInfo(jobId, out var info)
            ? TrelamiumIII.Instance.GetLocalization($"Jobs.{info.NameKey}").Value
            : null;

        // A job that no longer exists -- an id left in a save after the job was removed -- still gets
        // an honest line rather than a blank one.
        var text = jobName is null
            ? Text("Generic").Value
            : Text("ForJob").Format(jobName);

        tooltips.Add(new TooltipLine(Mod, "JobMarked", text) { OverrideColor = MarkColor });

        tooltips.Add(new TooltipLine(Mod, "JobMarkedLock", Text("Locked").Value)
        {
            OverrideColor = new Color(150, 140, 110)
        });
    }

    private static LocalizedText Text(string key)
        => TrelamiumIII.Instance.GetLocalization($"Jobs.MarkedItem.{key}");

    /// <summary>Gold, pulsing slowly, so a reserved stack reads as reserved at a glance in a full inventory.</summary>
    private static Color MarkColor
    {
        get
        {
            var pulse = 0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2.5f);

            return Color.Lerp(new Color(226, 180, 70), new Color(255, 232, 160), pulse);
        }
    }

    /// <summary>
    /// Draws a soft seal behind the icon. Behind rather than over it, so the item itself stays as
    /// readable as every other slot.
    /// </summary>
    public override bool PreDrawInInventory(Item item, SpriteBatch spriteBatch, Vector2 position,
        Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
    {
        if (!IsMarked)
            return true;

        var texture = TextureAssets.MagicPixel.Value;
        var glow = MarkColor * 0.32f;
        var size = new Vector2(frame.Width, frame.Height) * scale;

        spriteBatch.Draw(texture, position - origin * scale - size * 0.08f, null, glow, 0f,
            Vector2.Zero, size * 1.16f / new Vector2(texture.Width, texture.Height), SpriteEffects.None, 0f);

        // True: the seal is drawn behind, the item still draws itself normally on top.
        return true;
    }
    #endregion

    #region Serialization
    public override void SaveData(Item item, TagCompound tag)
    {
        if (!IsMarked)
            return;

        tag["jobId"] = jobId;
        tag["value"] = originalValue;
        tag["rare"] = originalRare;
    }

    public override void LoadData(Item item, TagCompound tag)
    {
        jobId = tag.TryGet("jobId", out int id) ? id : JobID.None;
        originalValue = tag.TryGet("value", out int value) ? value : 0;
        originalRare = tag.TryGet("rare", out int rare) ? rare : 0;
    }

    /// <summary>
    /// A flag, then the id only when it is set. This runs for every synced item in the world, and the
    /// overwhelming majority of them are not reserved by anything.
    /// </summary>
    public override void NetSend(Item item, BinaryWriter writer)
    {
        writer.Write(IsMarked);

        if (IsMarked)
            writer.Write(jobId);
    }

    public override void NetReceive(Item item, BinaryReader reader)
        => jobId = reader.ReadBoolean() ? reader.ReadInt32() : JobID.None;
    #endregion
}

/// <summary>
/// Closes the exits a <c>Can*</c> hook cannot reach: chest and bank slots, armour and accessory slots,
/// and the trash.
/// </summary>
/// <remarks>
/// tModLoader has no "can this item be stored" hook, but every one of those moves is decided by
/// <see cref="ItemSlot.PickItemMovementAction"/>, whose <c>-1</c> return means "this slot refuses this
/// item". One detour therefore covers all of them at once, including shift-click and quick-stack,
/// which route through the same call.
/// </remarks>
public class MarkedItemSlotGuard : ModSystem
{
    public override void Load() => On_ItemSlot.PickItemMovementAction += BlockMarkedItems;

    public override void Unload() => On_ItemSlot.PickItemMovementAction -= BlockMarkedItems;

    private static int BlockMarkedItems(On_ItemSlot.orig_PickItemMovementAction orig,
        Item[] inv, int context, int slot, Item checkItem)
    {
        if (MarkedItem.IsMarkedForJob(checkItem) && IsStorageContext(context))
            return -1;

        return orig(inv, context, slot, checkItem);
    }

    /// <summary>
    /// Slot kinds a reserved item must never reach. The player's own inventory and hotbar are
    /// deliberately absent -- reserving an item restricts what can be done <em>with</em> it, not
    /// where in the backpack it sits.
    /// </summary>
    private static bool IsStorageContext(int context) => context
        is ItemSlot.Context.ChestItem
        or ItemSlot.Context.BankItem
        or ItemSlot.Context.VoidItem
        or ItemSlot.Context.TrashItem
        or ItemSlot.Context.EquipArmor
        or ItemSlot.Context.EquipArmorVanity
        or ItemSlot.Context.EquipAccessory
        or ItemSlot.Context.EquipAccessoryVanity
        or ItemSlot.Context.EquipDye
        or ItemSlot.Context.ShopItem;
}

/// <summary>Refuses to sell a reserved item, which no slot hook covers.</summary>
public class MarkedItemPlayer : ModPlayer
{
    public override bool CanSellItem(NPC vendor, Item[] shopInventory, Item item)
        => !MarkedItem.IsMarkedForJob(item) && base.CanSellItem(vendor, shopInventory, item);
}
