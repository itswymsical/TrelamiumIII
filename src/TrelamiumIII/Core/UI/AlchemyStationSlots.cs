using ReLogic.Content;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.UI;
using Terraria.UI.Chat;
using TrelamiumIII.Core.Alchemy;
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

/// <summary>Potency meters and stack counts drawn by alchemy station slots.</summary>
internal static class AlchemySlotOverlay
{
    private static readonly Color MeterNeutral = new(200, 200, 215);
    private static readonly Color MeterMet = new(255, 220, 150);
    private static readonly Color MeterUnmet = new(255, 110, 110);

    /// <summary>
    /// Meter of <paramref name="item"/>'s stack potency below <paramref name="slot"/>. <paramref name="required"/> of 0 draws a
    /// neutral meter; otherwise the tint shows whether the requirement is met, and empty pips draw at level 0.
    /// </summary>
    public static void DrawPotency(SpriteBatch spriteBatch, Rectangle slot, float scale, Item item, int required)
    {
        if (item.IsAir)
            return;

        int level = SolutePotency.LevelOf(item);
        Color tint = required <= 0 ? MeterNeutral : level >= required ? MeterMet : MeterUnmet;

        PotencyMeter.Draw(spriteBatch, level, slot.Center.X, slot.Bottom + 2f * scale,
            pipHeight: 8f * scale, gap: scale, tint: tint, drawEmpty: required > 0);
    }

    /// <summary>Stack count at vanilla's slot position, for items not drawn through <see cref="ItemSlot.Draw" />.</summary>
    public static void DrawStack(SpriteBatch spriteBatch, Vector2 slotTopLeft, float scale, int stack)
    {
        if (stack <= 1)
            return;

        ChatManager.DrawColorCodedStringWithShadow(spriteBatch, FontAssets.ItemStack.Value, stack.ToString(),
            slotTopLeft + new Vector2(10f, 26f) * scale, Color.White, 0f, Vector2.Zero, new Vector2(scale), -1f, scale);
    }
}

/// <summary>Component slot. Acceptance follows <see cref="AlchemyStationRules.AcceptsComponent" />. Locked while brewing.</summary>
internal class AlchemyComponentSlot : BasicItemSlot
{
    private readonly IAlchemyStationUI _ui;

    public AlchemyComponentSlot(Item[] items, int index, IAlchemyStationUI ui, float scale)
        : base(items, index, ItemSlot.Context.InventoryItem, scale)
    {
        _ui = ui;
    }

    protected override Asset<Texture2D>? SlotFrame => ChemicalRoles.Of(items[index]) == ChemicalRole.Solvent
        ? AlchemySlotArt.Bottle(inUse: true)
        : AlchemySlotArt.Ingredient(!items[index].IsAir);

    public override bool ValidItemForSlot(Item item) => AlchemyStationRules.AcceptsComponent(_ui.Mode, items, index, item);

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        base.DrawSelf(spriteBatch);
        AlchemySlotOverlay.DrawPotency(spriteBatch, DrawnBounds(), Scale, items[index], _ui.RequiredPotency(index));
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

/// <summary>Input slot. Acceptance follows <see cref="AlchemyStationRules.AcceptsInput" />. Locked while brewing.</summary>
internal class AlchemyInputSlot : BasicItemSlot
{
    private readonly IAlchemyStationUI _ui;

    public AlchemyInputSlot(Item[] items, IAlchemyStationUI ui, float scale)
        : base(items, 0, ItemSlot.Context.InventoryItem, scale)
    {
        _ui = ui;
    }

    protected override Asset<Texture2D>? SlotFrame => _ui.Mode == AlchemyStationMode.SoluteMixture
        ? AlchemySlotArt.Ingredient(!items[index].IsAir)
        : AlchemySlotArt.Bottle(!items[index].IsAir);

    public override bool ValidItemForSlot(Item item) => AlchemyStationRules.AcceptsInput(_ui.Mode, item);

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        base.DrawSelf(spriteBatch);

        if (ChemicalRoles.Of(items[index]) == ChemicalRole.Solute)
            AlchemySlotOverlay.DrawPotency(spriteBatch, DrawnBounds(), Scale, items[index], required: 0);
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

/// <summary>Take-only slot holding bottles returned by completed brews.</summary>
internal class AlchemyBottleReturnSlot : BasicItemSlot
{
    public AlchemyBottleReturnSlot(Item[] items, float scale)
        : base(items, 0, ItemSlot.Context.InventoryItem, scale)
    {
    }

    protected override Asset<Texture2D>? SlotFrame => AlchemySlotArt.Bottle(!items[index].IsAir);

    public override bool ValidItemForSlot(Item item) => false;
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

    public AlchemyOutputSlot(Item[] items, IAlchemyStationUI ui, float scale)
        : base(items, 0, ItemSlot.Context.InventoryItem, scale)
    {
        _ui = ui;
    }

    protected override Vector2 DrawOffset => Vector2.Zero;

    /// <summary>A pending preview counts as in use, so the empty-state hint frame is replaced while one is shown.</summary>
    protected override Asset<Texture2D>? SlotFrame => AlchemySlotArt.Bottle(!items[index].IsAir);

    /// <summary>Block placing items into the output slot.</summary>
    public override bool ValidItemForSlot(Item item) => item.IsAir;

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        // Replaces BasicItemSlot.DrawSelf to choose between the real item and a silhouette.
        float oldScale = Main.inventoryScale;
        Main.inventoryScale = Scale;

        Vector2 position = DrawnBounds().TopLeft();

        if (_ui.IsResultReady || AlmanacDiscoverySystem.IsUnlocked(items[index].type))
        {
            SlotFrameOverride.Draw(spriteBatch, ref items[index], context, position, SlotFrame);
        }
        else
        {
            // Undiscovered preview: the frame around an empty item, then the silhouette and its stack.
            Item empty = new();
            SlotFrameOverride.Draw(spriteBatch, ref empty, context, position, SlotFrame);

            Item item = items[index];

            if (!item.IsAir)
            {
                DrawItemSilhouette(spriteBatch, item);
                AlchemySlotOverlay.DrawStack(spriteBatch, position, Scale, item.stack);
            }
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
