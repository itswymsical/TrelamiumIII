using ReLogic.Content;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.UI;
using TrelamiumIII.Common.Systems.Alchemy;
using TrelamiumIII.Core.UI.AlchemyStation;
using static TrelamiumIII.Core.AssetReferences;

namespace TrelamiumIII.Core.UI;

/// <summary>
/// The station's socket art. Each socket has two frames: an empty one carrying a glyph that hints
/// what belongs there, and a plain <c>_InUse</c> one that gives an item icon a clean background.
/// The frame itself is identical between the two.
/// </summary>
internal static class AlchemySlotArt
{
    private static Asset<Texture2D>? _bottle;
    private static Asset<Texture2D>? _bottleInUse;
    private static Asset<Texture2D>? _ingredient;
    private static Asset<Texture2D>? _ingredientInUse;

    /// <summary>Solvent and solution sockets.</summary>
    public static Asset<Texture2D> Bottle(bool inUse) => inUse
        ? _bottleInUse ??= Request(AssetReferences.Assets.Textures.UI.AlchemyStation.BottleSlot_InUse.KEY)
        : _bottle ??= Request(AssetReferences.Assets.Textures.UI.AlchemyStation.BottleSlot.KEY);

    /// <summary>Solute / reagent sockets.</summary>
    public static Asset<Texture2D> Ingredient(bool inUse) => inUse
        ? _ingredientInUse ??= Request(AssetReferences.Assets.Textures.UI.AlchemyStation.IngredientSlot_InUse.KEY)
        : _ingredient ??= Request(AssetReferences.Assets.Textures.UI.AlchemyStation.IngredientSlot.KEY);

    private static Asset<Texture2D> Request(string key)
        => ModContent.Request<Texture2D>(key, AssetRequestMode.ImmediateLoad);
}

/// <summary>
/// Accepts any item type referenced as a component by at least one registered alchemy recipe.
/// Three of these sit on the alchemy station UI. Locked while a brew is in progress.
/// </summary>
internal class AlchemyComponentSlot : BasicItemSlot
{
    private readonly IAlchemyStationUI _ui;

    // InventoryItem rather than EquipDye: a dye slot paints vanilla's own faint dye glyph when
    // empty, which would fight the frame's star hint now that the socket brings its own art.
    public AlchemyComponentSlot(Item[] items, int index, IAlchemyStationUI ui)
        : base(items, index, ItemSlot.Context.InventoryItem, 0.85f)
    {
        _ui = ui;
    }
    protected override Vector2 DrawOffset => Vector2.Zero;

    protected override Asset<Texture2D>? SlotFrame => AlchemySlotArt.Ingredient(!items[index].IsAir);

    public override bool ValidItemForSlot(Item item)
        => item.IsAir || AlchemyIngredients.IsValidSubstance(item);

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        base.DrawSelf(spriteBatch);
        DrawPotencyDraw(spriteBatch);
    }

    /// <summary>
    /// The potency the previewed recipe is drawing from this slot, as a star meter across the
    /// slot's foot. Amber while the stack covers it, red while it doesn't.
    /// </summary>
    private void DrawPotencyDraw(SpriteBatch sb)
    {
        int cost = _ui.SoluteCost(index);
        if (cost <= 0) return;

        var item = items[index];
        bool covered = item is { IsAir: false } && item.stack >= cost;

        var bounds = GetDimensions().ToRectangle();
        PotencyMeter.Draw(sb, cost,
            centerX: bounds.X + bounds.Width * 0.5f,
            y: bounds.Bottom - 9f * Scale,
            pipHeight: 8f * Scale,
            gap: 1f * Scale,
            tint: covered ? new Color(255, 220, 150) : new Color(255, 110, 110));
    }

    protected override void SlotLogic(ref Item item)
    {
        if (_ui.IsBrewing)
        {
            Main.LocalPlayer.mouseInterface = true;
            return;
        }

        base.SlotLogic(ref item);
    }
}

/// <summary>
/// accepts any registered vial.
/// The contents of this slot decide the active <see cref="AlchemyMode"/>.
/// Locked while a brew is in progress or while a finished result is waiting to be taken.
/// </summary>
internal class AlchemyVialSlot : BasicItemSlot
{
    private readonly IAlchemyStationUI _ui;

    public AlchemyVialSlot(Item[] items, IAlchemyStationUI ui)
        : base(items, 0, ItemSlot.Context.CraftingMaterial, 0.85f)
    {
        _ui = ui;
    }

    protected override Vector2 DrawOffset => Vector2.Zero;

    protected override Asset<Texture2D>? SlotFrame => AlchemySlotArt.Bottle(!items[index].IsAir);

    public override bool ValidItemForSlot(Item item)
        => item.IsAir || AlchemyIngredients.IsValidSolvent(item);

    protected override void SlotLogic(ref Item item)
    {
        if (_ui.IsBrewing)
        {
            Main.LocalPlayer.mouseInterface = true;
            return;
        }

        base.SlotLogic(ref item);
    }
}

/// <summary>
/// Output slot. Three render modes:
/// <list type="bullet">
///   <item>Preview: silhouette of the resolved item; left-click starts a brew.</item>
///   <item>Brewing: silhouette of the locked snapshot + progress bar.</item>
///   <item>Result ready: the real item.</item>
/// </list>
/// </summary>
internal class AlchemyOutputSlot : BasicItemSlot
{
    private readonly IAlchemyStationUI _ui;

    public AlchemyOutputSlot(Item[] items, IAlchemyStationUI ui)
        : base(items, 0, ItemSlot.Context.CraftingMaterial, 0.85f)
    {
        _ui = ui;
    }

    protected override Vector2 DrawOffset => Vector2.Zero;

    /// <summary>
    /// A pending preview counts as in use: the socket is showing something, even if it's only the
    /// silhouette of what a brew would yield, so the flask hint has done its job and steps aside.
    /// </summary>
    protected override Asset<Texture2D>? SlotFrame => AlchemySlotArt.Bottle(!items[index].IsAir);

    /// <summary>Block placing items into the output slot.</summary>
    public override bool ValidItemForSlot(Item item) => item.IsAir;

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        // Skip BasicItemSlot.DrawSelf so we control whether the icon or a silhouette is drawn.
        float oldScale = Main.inventoryScale;
        Main.inventoryScale = Scale;

        var pos = GetDimensions().ToRectangle().TopLeft();

        // Both paths go through SlotFrameOverride so the socket keeps its own frame; SlotFrame
        // reads the real contents, so the empty-state hint isn't fooled by the dummy below.
        if (_ui.IsResultReady || AlmanacDiscoverySystem.IsUnlocked(items[index].type))
        {
            SlotFrameOverride.Draw(spriteBatch, ref items[index], context, pos + DrawOffset, SlotFrame);
        }
        else
        {
            // Preview/brewing
            Item dummy = new Item();
            SlotFrameOverride.Draw(spriteBatch, ref dummy, context, pos + DrawOffset, SlotFrame);

            var item = items[index];
            if (!item.IsAir)
                DrawItemSilhouette(spriteBatch, item);
        }

        if (_ui.IsBrewing && _ui.ShowInlineBrewBar)
            DrawProgressBar(spriteBatch);

        Main.inventoryScale = oldScale;

        if (IsMouseHovering)
            SlotLogic(ref items[index]);
    }

    private void DrawItemSilhouette(SpriteBatch sb, Item item)
    {
        Main.instance.LoadItem(item.type);
        var tex = TextureAssets.Item[item.type]?.Value;
        if (tex == null) return;

        var anim = Main.itemAnimations[item.type];
        var frame = anim != null ? anim.GetFrame(tex) : tex.Frame();
        var origin = frame.Size() / 2f;

        // Match ItemSlot.Draw's scaling
        float drawScale = Scale;
        if (frame.Width > 32 || frame.Height > 32)
        {
            float fit = frame.Width > frame.Height ? 32f / frame.Width : 32f / frame.Height;
            drawScale *= fit;
        }

        var slotRect = GetDimensions().ToRectangle();
        var center = new Vector2(slotRect.X + slotRect.Width * 0.5f,
                                 slotRect.Y + slotRect.Height * 0.5f) + DrawOffset;

        sb.Draw(tex, center, frame, Color.Black, 0f, origin, drawScale, SpriteEffects.None, 0f);
    }

    private void DrawProgressBar(SpriteBatch sb)
    {
        var bounds = GetDimensions().ToRectangle();
        const int BAR_HEIGHT = 4;
        const int BAR_GAP = 4;

        int barX = bounds.X;
        int barY = bounds.Bottom + BAR_GAP;
        int barW = bounds.Width;

        var pixel = TextureAssets.MagicPixel.Value;
        sb.Draw(pixel, new Rectangle(barX, barY, barW, BAR_HEIGHT), new Color(20, 20, 20, 200));

        int filledW = (int)(barW * _ui.BrewProgress);
        sb.Draw(pixel, new Rectangle(barX, barY, filledW, BAR_HEIGHT), new Color(170, 230, 120));
    }

    protected override void SlotLogic(ref Item item)
    {
        Main.LocalPlayer.mouseInterface = true;

        if (_ui.IsBrewing) return;

        if (_ui.IsResultReady)
        {
            // Hover tooltip and cursor pickup.
            if (!item.IsAir)
                ItemSlot.MouseHover(ref item, context);

            if (Main.mouseLeft && Main.mouseLeftRelease && Main.mouseItem.IsAir && !item.IsAir)
            {
                Main.mouseItem = item.Clone();
                item.TurnToAir();
                SoundEngine.PlaySound(SoundID.Grab);
                Main.mouseLeftRelease = false;
            }
            return;
        }

        if (item.IsAir)
        {
            if (AlmanacDiscoverySystem.IsUnlocked(items[index].type))
                ItemSlot.MouseHover(ref item, context);

            return;
        }


        if (Main.mouseLeft && Main.mouseLeftRelease)
        {
            _ui.TryStartBrew();
            Main.mouseLeftRelease = false;
        }
    }
}
