// Modified from Spirit Mod's Backpack slot
// Credits: Spirit Mod, GabeHasWon
// https://github.com/GabeHasWon/SpiritReforged/blob/master/Common/UI/BackpackInterface/BackbackUISlot.cs#L1

using System.Collections.Generic;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;
using TrelamiumIII.Content.FarmingOverhaul;

namespace TrelamiumIII.Core.UI.FlowerSatchel;

/// <summary>
/// One flower slot accepts whatever <see cref="FlowerRegistry"/> knows about, so a cross-mod herb
/// becomes placeable the moment it is registered.
/// </summary>
internal sealed class FlowerSlot : BasicItemSlot
{
    // GuideItem is borrowed purely for its violet backdrop; BasicItemSlot handles clicks as a plain
    // inventory slot, so nothing about vanilla's crafting-guide behaviour comes along with it.
    public FlowerSlot(Item[] flowers, int index)
        : base(flowers, index, ItemSlot.Context.GuideItem, 0.85f) { }

    public override bool ValidItemForSlot(Item item) => item.IsAir || FlowerRegistry.IsFlower(item.type);

    /// <summary>A faint centre dot, so an empty slot still reads as a slot against the world behind it.</summary>
    protected override void DrawEmptyPlaceholder(SpriteBatch spriteBatch)
    {
        var texture = TextureAssets.MagicPixel.Value;
        var source = texture.Frame();
        var bounds = DrawnBounds();

        spriteBatch.Draw(texture,
            new Vector2(bounds.X + bounds.Width * 0.5f, bounds.Y + bounds.Height * 0.5f),
            source, Color.White * 0.35f, 0f, source.Size() / 2f, Scale, SpriteEffects.None, 0f);
    }

    protected override void SlotLogic(ref Item item)
    {
        if (VanillaInventoryLayout.ContainerGrid() is { } grid && grid.Contains(DrawnBounds().Center))
            return;

        base.SlotLogic(ref item);

        if (item.IsAir)
        {
            Main.hoverItemName = "Flower Slot";
            Main.mouseText = true;
        }
    }
}

/// <summary>
/// The satchel panel: a row of flower slots with the active effects listed under it.
/// <para>The row docks to the trash slot.</para>
/// </summary>
internal sealed class FlowerSatchelUIState : UIState
{
    /// <summary>Gap between the last flower slot and the trash slot it docks against, before scaling.</summary>
    private const float DOCK_GAP = VanillaInventoryLayout.SLOT_GAP;

    /// <summary>Text offsets below the slot row.</summary>
    private const float SUMMARY_GAP = 8f;
    private const float EFFECTS_GAP = 32f;

    private const float PANEL_WIDTH = 260f;
    private const float PANEL_HEIGHT = 220f;

    private UIElement _root;
    private UIText _summary;
    private UIText _effects;

    private readonly List<FlowerSlot> _slots = new();

    /// <summary>The satchel the slots are bound to. Slots hold its array, so a change means a rebuild.</summary>
    private Item _boundSatchel;

    public override void OnInitialize()
    {
        Width = Height = StyleDimension.Fill;

        _root = new UIElement
        {
            Width = StyleDimension.FromPixels(PANEL_WIDTH),
            Height = StyleDimension.FromPixels(PANEL_HEIGHT),
        };
        _root.SetPadding(0f);
        Append(_root);

        _summary = new UIText("No active effects", 0.925f, large: false)
        {
            TextColor = Color.LightGreen * 0.95f,
            ShadowColor = Color.Black,
        };
        _root.Append(_summary);

        _effects = new UIText(string.Empty, 0.8f, large: false)
        {
            TextColor = Color.White * 0.8f,
            ShadowColor = Color.Black,
        };
        _root.Append(_effects);
    }

    public override void Update(GameTime gameTime)
    {
        var modPlayer = Main.LocalPlayer.GetModPlayer<SatchelPlayer>();

        if (modPlayer.ActiveSatchel?.ModItem is not FlowerSatchelItem satchel)
        {
            Unbind();
            return;
        }

        if (!ReferenceEquals(_boundSatchel, satchel.Item))
            Bind(satchel);

        Anchor();

        _summary.SetText(modPlayer.GetSummary());
        _effects.SetText(string.Join("\n", modPlayer.GetDisplayEffects()));

        base.Update(gameTime);
    }

    /// <summary>
    /// Re-docks the row against the trash slot and re-lays the slots at its scale. Runs every frame,
    /// so opening a chest carries the whole panel along with the slot it is attached to.
    /// </summary>
    private void Anchor()
    {
        var (trash, scale) = VanillaInventoryLayout.TrashSlot();

        float box = VanillaInventoryLayout.SLOT_BOX * scale;
        float gap = VanillaInventoryLayout.SLOT_GAP * scale;
        float stride = box + gap;

        int count = _slots.Count;
        float rowWidth = count > 0 ? count * box + (count - 1) * gap : 0f;

        // Grow leftward from the trash slot: that edge is open in every vanilla layout, whereas the
        // bestiary and emote buttons sit immediately to its right.
        _root.Left.Set(trash.X - DOCK_GAP * scale - rowWidth, 0f);
        _root.Top.Set(trash.Y, 0f);

        for (int i = 0; i < count; i++)
        {
            _slots[i].SetScale(scale);
            _slots[i].Left.Set(i * stride, 0f);
            _slots[i].Top.Set(0f, 0f);
        }

        _summary.Top.Set(box + SUMMARY_GAP, 0f);
        _effects.Top.Set(box + EFFECTS_GAP, 0f);

        _root.Recalculate();
    }

    /// <summary>Builds one slot per flower slot on the given satchel, bound to its array.</summary>
    private void Bind(FlowerSatchelItem satchel)
    {
        RemoveSlots();

        for (int i = 0; i < satchel.Flowers.Length; i++)
        {
            var slot = new FlowerSlot(satchel.Flowers, i);
            _slots.Add(slot);
            _root.Append(slot);
        }

        _boundSatchel = satchel.Item;
    }

    private void Unbind()
    {
        if (_boundSatchel == null) return;

        RemoveSlots();
        _summary.SetText("No active effects");
        _effects.SetText(string.Empty);
        _boundSatchel = null;
    }

    private void RemoveSlots()
    {
        // Only the slots are transient; the two text elements live for the panel's lifetime.
        foreach (var slot in _slots)
            _root.RemoveChild(slot);

        _slots.Clear();
    }
}
