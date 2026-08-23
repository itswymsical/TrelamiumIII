// Credits: Spirit Mod, GabeHasWon
// https://github.com/GabeHasWon/SpiritReforged/blob/master/Common/UI/Misc/BasicItemSlot.cs#L8

using ReLogic.Content;
using Terraria.Audio;
using Terraria.UI;

namespace TrelamiumIII.Core.UI;

/// <summary>
///     A vanilla item slot as a <see cref="UIElement" />: it draws one entry of an
///     <see cref="Item" /> array and hands clicks straight to vanilla, so stack splitting,
///     shift-pickup and hover tooltips all behave as players expect. Subclasses restrict what fits
///     (<see cref="ValidItemForSlot" />), swap the backdrop art (<see cref="SlotFrame" />), or gate
///     interaction (<see cref="SlotLogic" />).
/// </summary>
internal class BasicItemSlot : UIElement
{
    public float Scale { get; private set; }

    protected readonly Item[] items;
    protected readonly int index;
    protected readonly int context;

    /// <summary>
    ///     Nudges where the slot draws relative to its layout box. Hit-testing follows it (see
    ///     <see cref="ContainsPoint" />), so an offset slot is still clicked where it appears.
    /// </summary>
    protected virtual Vector2 DrawOffset => Vector2.Zero;

    /// <summary>
    ///     Custom art to stand in for the vanilla slot backdrop. Null (the default) keeps vanilla's,
    ///     so slots that do not care are unaffected. See <see cref="SlotFrameOverride" />.
    /// </summary>
    protected virtual Asset<Texture2D>? SlotFrame => null;

    public BasicItemSlot(Item[] items, int index, int context = ItemSlot.Context.ChestItem, float scale = 0.85f)
    {
        this.items = items;
        this.index = index;
        this.context = context;

        SetScale(scale);
    }

    /// <summary>
    ///     Updates the slot's draw scale and keeps its hit-box in sync. Used to track game zoom.
    /// </summary>
    public void SetScale(float scale)
    {
        Scale = scale;
        Width = Height = new StyleDimension(52f * scale, 0f);
    }

    /// <summary>The slot's on-screen box: its layout box shifted by <see cref="DrawOffset" />.</summary>
    protected Rectangle DrawnBounds()
    {
        Rectangle box = GetDimensions().ToRectangle();
        box.Offset((int)DrawOffset.X, (int)DrawOffset.Y);

        return box;
    }

    /// <summary>
    ///     Hit-tests the drawn box rather than the layout box. Without this a slot with a non-zero
    ///     <see cref="DrawOffset" /> only responds where its layout box happens to overlap the art,
    ///     which reads to the player as a slot that only accepts clicks along one edge.
    /// </summary>
    public override bool ContainsPoint(Vector2 point) => DrawnBounds().Contains((int)point.X, (int)point.Y);

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        base.DrawSelf(spriteBatch);

        float oldScale = Main.inventoryScale;
        Main.inventoryScale = Scale;

        SlotFrameOverride.Draw(spriteBatch, ref items[index], context, DrawnBounds().TopLeft(), SlotFrame);

        if (items[index].IsAir)
            DrawEmptyPlaceholder(spriteBatch);

        Main.inventoryScale = oldScale;

        if (IsMouseHovering)
            SlotLogic(ref items[index]);
    }

    /// <summary>Hook for subclasses to draw something inside an empty slot.</summary>
    protected virtual void DrawEmptyPlaceholder(SpriteBatch spriteBatch) { }

    protected virtual void SlotLogic(ref Item item)
    {
        Main.LocalPlayer.mouseInterface = true;

        if (Main.mouseItem.IsAir || ValidItemForSlot(Main.mouseItem))
        {
            // Handled as InventoryItem regardless of the context above: that context is the frame
            // borrowed for looks, while interaction should always be plain-inventory behaviour.
            ItemSlot.Handle(ref item, ItemSlot.Context.InventoryItem);
        }
        else if (Main.mouseLeft && Main.mouseLeftRelease)
        {
            // Rejected with a soft tick rather than a swap.
            SoundEngine.PlaySound(SoundID.MenuTick);
        }
    }

    /// <summary>Determines whether the given item can be placed in this slot.</summary>
    public virtual bool ValidItemForSlot(Item item) => true;
}
