// Modified from Spirit Mod's Backpack Item class
// Credits: Spirit Mod, GabeHasWon
// https://github.com/GabeHasWon/SpiritReforged/blob/master/Common/UI/BackpackInterface/BackbackUISlot.cs#L1

using Microsoft.Xna.Framework.Input;
using ReLogic.Graphics;
using System.Collections.Generic;
using System.IO;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ModLoader.IO;
using TrelamiumIII.Core;

namespace TrelamiumIII.Content.FarmingOverhaul.SatchelContent;

/// <summary>
/// Holds a few flowers and turns them into passive bonuses while it sits anywhere in the inventory.
/// What each flower does, and which ones fit at all. It is declared in
/// <see cref="FlowerCatalogSystem"/>; this item only owns the storage.
/// </summary>
public class FlowerSatchelItem : ModItem
{
    public override string Texture => AssetReferences.Content.FarmingOverhaul.SatchelContent.FlowerSatchel.KEY;
    public const int SLOT_COUNT = 3;

    /// <summary>The held flowers. Always <see cref="SLOT_COUNT"/> long and never null-elemented.</summary>
    public Item[] Flowers;

    public override void SetStaticDefaults()
        => ItemID.Sets.OpenableBag[Type] = true;

    public override void SetDefaults()
    {
        Item.consumable = false;
        Item.rare = ItemRarityID.Green;
        Item.width = Item.height = 26;
        Item.maxStack = 1;
        Item.value = Item.sellPrice(gold: 1);

        EnsureSlots();
    }

    /// <summary>
    /// Guarantees the flower array exists and every entry is a valid Item.
    /// Safe to call from any entry point (SetDefaults, LoadData, NetReceive).
    /// </summary>
    private void EnsureSlots()
    {
        if (Flowers == null || Flowers.Length != SLOT_COUNT)
            Flowers = new Item[SLOT_COUNT];

        for (int i = 0; i < SLOT_COUNT; i++)
            Flowers[i] ??= new Item();
    }

    public override bool PreDrawTooltipLine(DrawableTooltipLine line, ref int yOffset)
    {
        if (line.Name == "ItemName")
        {
            FlowerSatchelTooltipRenderer.DrawAnimatedTitle(line);
            return false;
        }

        return base.PreDrawTooltipLine(line, ref yOffset);
    }

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient(ItemID.Silk, 6)
            .AddIngredient(ItemID.Vine, 3)
            .AddTile(TileID.WorkBenches)
            .Register();
    }

    public override bool ConsumeItem(Player player) => false;

    public override void RightClick(Player player)
    {
        // Right-click only toggles UI visibility now.
        // Effects apply automatically while the satchel is anywhere in the inventory.
        var modPlayer = player.GetModPlayer<SatchelPlayer>();
        modPlayer.PanelVisible = !modPlayer.PanelVisible;

        SoundEngine.PlaySound(SoundID.MenuOpen);
    }

    public override bool CanPickup(Player player)
        => !player.HasItemInAnyInventory(ModContent.ItemType<FlowerSatchelItem>());

    #region Serialization & Network
    public override void SaveData(TagCompound tag)
    {
        for (int i = 0; i < SLOT_COUNT; i++)
        {
            if (Flowers[i] is { IsAir: false })
                tag[$"flower{i}"] = ItemIO.Save(Flowers[i]);
        }
    }

    public override void LoadData(TagCompound tag)
    {
        EnsureSlots();

        for (int i = 0; i < SLOT_COUNT; i++)
        {
            Flowers[i] = tag.TryGet($"flower{i}", out TagCompound itemTag)
                ? ItemIO.Load(itemTag)
                : new Item();
        }
    }

    public override void NetSend(BinaryWriter writer)
    {
        EnsureSlots();
        for (int i = 0; i < SLOT_COUNT; i++)
            ItemIO.Send(Flowers[i], writer, true);
    }

    public override void NetReceive(BinaryReader reader)
    {
        EnsureSlots();
        for (int i = 0; i < SLOT_COUNT; i++)
        {
            Flowers[i] = new Item();
            ItemIO.Receive(Flowers[i], reader, true);
        }
    }
    #endregion
}

/// <summary>
/// Turns the satchel's contents into player bonuses. The arithmetic is entirely
/// table-driven: bouquets claim their member flowers first, leftovers pay out individually, and every
/// contribution lands through <see cref="FlowerStats"/>.
/// <para>The only hand-written behaviour left is the <see cref="FlowerPerk"/> block.</para>
/// </summary>
public class SatchelPlayer : ModPlayer
{
    /// <summary>Per-slot stack cap on stat contribution. Extra flowers still stack, they just stop counting.</summary>
    public const int MAX_STACK_PER_SLOT = 30;

    /// <summary>True when the player has chosen to display the panel (right-click toggle).</summary>
    public bool PanelVisible;

    /// <summary>The satchel currently driving effects. Auto-detected each frame from inventory.</summary>
    public Item ActiveSatchel;

    /// <summary>This frame's accumulated bonuses, keyed by stat.</summary>
    public readonly FlowerStatSet Stats = new();

    private readonly HashSet<FlowerPerk> _perks = new();

    // Per-frame display state, rebuilt in ResetEffects alongside the stats it describes.
    private readonly List<string> _bouquetLines = new();
    private readonly List<string> _flowerLines = new();
    private readonly List<string> _activeBouquets = new();

    // Working set for the calculation: how many of each flower are held, and which of those a
    // bouquet has already paid for.
    private readonly Dictionary<int, int> _counts = new();
    private readonly HashSet<int> _claimed = new();

    public bool HasPerk(FlowerPerk perk) => _perks.Contains(perk);

    public override void Initialize()
    {
        PanelVisible = false;
        ActiveSatchel = null;
        ClearEffects();
    }

    public override void ResetEffects()
    {
        // Auto-detect satchel from inventory. Effects no longer depend on UI state.
        ActiveSatchel = FindSatchelInInventory();

        // If the satchel left the inventory, hide the panel automatically.
        if (ActiveSatchel == null)
            PanelVisible = false;

        ClearEffects();

        if (ActiveSatchel?.ModItem is FlowerSatchelItem satchel)
            Recalculate(satchel);
    }

    /// <summary>
    /// Scans the player's main inventory for a Flower Satchel.
    /// CanPickup prevents owning more than one, so the first match is authoritative.
    /// </summary>
    private Item FindSatchelInInventory()
    {
        var inventory = Player.inventory;
        for (int i = 0; i < inventory.Length; i++)
        {
            if (inventory[i].ModItem is FlowerSatchelItem)
                return inventory[i];
        }
        return null;
    }

    #region Update Hooks
    public override void PostUpdateEquips()
    {
        Stats.ApplyAll(Player);

        if (HasPerk(FlowerPerk.NightVision))
            Player.nightVision = true;

        if (HasPerk(FlowerPerk.DaytimeSpeed) && Main.dayTime)
            Player.moveSpeed += 0.04f;
    }

    public override void OnHitByNPC(NPC npc, Player.HurtInfo hurtInfo) => TryDangersense();

    public override void OnHitByProjectile(Projectile proj, Player.HurtInfo hurtInfo) => TryDangersense();

    private void TryDangersense()
    {
        if (HasPerk(FlowerPerk.DangersenseOnHit))
            Player.AddBuff(BuffID.Dangersense, 90);
    }

    public override void PostUpdate()
    {
        if (HasPerk(FlowerPerk.HealthySight) && Player.statLife > Player.statLifeMax2 * 0.9f)
        {
            Player.dangerSense = true;
            Player.detectCreature = true;
        }
    }
    #endregion

    #region Effect Calculation
    private void ClearEffects()
    {
        Stats.Clear();
        _perks.Clear();
        _bouquetLines.Clear();
        _flowerLines.Clear();
        _activeBouquets.Clear();
        _counts.Clear();
        _claimed.Clear();
    }

    private void Recalculate(FlowerSatchelItem satchel)
    {
        if (satchel.Flowers == null) return;

        foreach (var item in satchel.Flowers)
        {
            if (item.IsAir) continue;

            int counted = Math.Min(item.stack, MAX_STACK_PER_SLOT);
            _counts[item.type] = _counts.GetValueOrDefault(item.type) + counted;
        }

        // Bouquets run first and claim their members, so a claimed flower doesn't also pay out
        // standalone. Several bouquets may claim the same flower and all pay.
        foreach (var bouquet in FlowerRegistry.Bouquets)
            TryApplyBouquet(bouquet);

        foreach (var (flowerType, count) in _counts)
        {
            if (_claimed.Contains(flowerType)) continue;

            if (FlowerRegistry.TryGet(flowerType, out var profile))
                ApplyFlower(profile, count);
        }
    }

    private void ApplyFlower(FlowerProfile profile, int count)
    {
        foreach (var (stat, perUnit) in profile.Contributions)
        {
            float value = perUnit * count;
            Stats.Add(stat, value);

            if (!FlowerStats.IsNegligible(stat, value))
                _flowerLines.Add($"[i:{profile.ItemType}]{FlowerStats.Describe(stat, value)}");
        }
    }

    /// <summary>
    /// Pays out a bouquet if every member is stocked. Each contribution scales with its own member's
    /// count; the perk thresholds gate on the <em>lowest</em> member, so a bouquet is only as strong
    /// as its thinnest flower.
    /// </summary>
    private void TryApplyBouquet(BouquetProfile bouquet)
    {
        int lowest = int.MaxValue;
        foreach (int member in bouquet.Members)
        {
            if (!_counts.TryGetValue(member, out int held) || held <= 0) return;
            lowest = Math.Min(lowest, held);
        }
        if (lowest is int.MaxValue or <= 0) return;

        foreach (var (member, stat, perUnit) in bouquet.Contributions)
        {
            int held = _counts[member];
            float value = perUnit * held;
            Stats.Add(stat, value);

            if (!FlowerStats.IsNegligible(stat, value))
                _bouquetLines.Add($"[i:{member}]{FlowerStats.Describe(stat, value)} ({held})");
        }

        int satchelIcon = ModContent.ItemType<FlowerSatchelItem>();
        foreach (var (atCount, perk) in bouquet.Perks)
        {
            if (lowest < atCount) continue;

            _perks.Add(perk);
            _bouquetLines.Add($"[i:{satchelIcon}][c/a0fea3:{FlowerPerks.Describe(perk)}]");
        }

        _activeBouquets.Add(bouquet.Name);
        foreach (int member in bouquet.Members)
            _claimed.Add(member);
    }
    #endregion

    #region Display
    /// <summary>Effects shown beneath the title. Holds Alt to expand individual flower effects.</summary>
    public List<string> GetDisplayEffects()
    {
        var lines = new List<string>(_bouquetLines);

        if (Main.keyState.IsKeyDown(Keys.LeftAlt))
            lines.AddRange(_flowerLines);
        else if (_flowerLines.Count > 0)
            lines.Add("Hold [Left Alt] to show flower effects");

        return lines;
    }

    /// <summary>One-line summary of active bouquets and standalone flowers.</summary>
    public string GetSummary()
    {
        if (ActiveSatchel?.ModItem is not FlowerSatchelItem)
            return "No active effects";

        var parts = new List<string>(_activeBouquets);

        var standalone = new List<string>();
        foreach (var (flowerType, count) in _counts)
        {
            if (_claimed.Contains(flowerType) || count <= 0) continue;
            if (!FlowerRegistry.TryGet(flowerType, out var profile) || profile.Contributions.Count == 0) continue;

            standalone.Add(Lang.GetItemNameValue(flowerType));
        }

        if (standalone.Count > 0)
            parts.Add($"{string.Join(", ", standalone)} effects");

        return parts.Count == 0 ? "No active effects" : string.Join(" | ", parts);
    }
    #endregion
}

/// <summary>
/// Draws the animated leaf-particle title for the Flower Satchel tooltip.
/// Extracted from FlowerSatchelItem to keep item logic separate from rendering math.
/// </summary>
internal static class FlowerSatchelTooltipRenderer
{
    private const int FRAME_WIDTH = 10;
    private const int FRAME_HEIGHT = 14;
    private const int LEAF_COUNT = 4;
    private const int FRAME_TOTAL = 8;
    private const float ROTATION_SPEED = 0.8f;
    private const float SWAY_AMOUNT = 15f;
    private const float VERTICAL_DRIFT_RANGE = 40f;

    public static void DrawAnimatedTitle(DrawableTooltipLine line)
    {
        var pos = new Vector2(line.X, line.Y);
        var text = line.Text;
        float textWidth = FontAssets.MouseText.Value.MeasureString(text).X;

        // Draw the title text itself
        Main.spriteBatch.DrawString(FontAssets.MouseText.Value, text, pos, Color.YellowGreen);

        // Switch to additive for glowing leaves, then restore
        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive,
            null, null, null, null, Main.UIScaleMatrix);

        DrawLeafParticles(pos, textWidth);

        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
            null, null, null, null, Main.UIScaleMatrix);
    }

    private static void DrawLeafParticles(Vector2 basePos, float textWidth)
    {
        var leafTexture = ModContent.Request<Texture2D>(Assets.Textures.MagnoliaLeaf.KEY).Value;
        var origin = new Vector2(FRAME_WIDTH / 2f, FRAME_HEIGHT / 2f);
        float time = Main.GlobalTimeWrappedHourly;

        for (int i = 0; i < LEAF_COUNT; i++)
        {
            float individualTime = time + i * 1.5f;

            float swayX = (float)Math.Sin(individualTime * 1.5f) * SWAY_AMOUNT;
            float driftX = (individualTime * 15f) % textWidth;
            float driftY = (individualTime * 8f) % VERTICAL_DRIFT_RANGE;

            var offset = new Vector2(swayX + driftX, driftY);
            float rotation = individualTime * ROTATION_SPEED + i * MathHelper.PiOver2;

            var color = Color.ForestGreen
                * (0.5f + (float)Math.Sin(time * 1.2f) * 0.3f)
                * 1.15f;

            int frame = ((int)(Main.GameUpdateCount / 6) + i) % FRAME_TOTAL;
            var sourceRect = new Rectangle(0, FRAME_HEIGHT * frame, FRAME_WIDTH, FRAME_HEIGHT);

            Main.spriteBatch.Draw(
                leafTexture,
                basePos + offset,
                sourceRect,
                color,
                rotation,
                origin,
                1f,
                SpriteEffects.None,
                0f
            );
        }
    }
}
