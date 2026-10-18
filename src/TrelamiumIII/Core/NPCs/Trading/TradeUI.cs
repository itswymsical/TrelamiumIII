using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.UI;
using Terraria.UI.Chat;

namespace TrelamiumIII.Core.NPCs.Trading;

/// <summary>
/// Draws a panel listing the current trader's offers as clickable rows. Patterned after <c>ChestUI</c>.
/// <see cref="ItemSlot.Draw"/> for item rendering.
/// </summary>
public static class TradeUI
{
    public static bool IsOpen { get; private set; }
    public static Villager CurrentTrader { get; private set; }

    // Layout
    private const int PanelWidth = 440;
    private const int HeaderHeight = 56;
    private const int RowHeight = 56;
    private const int FooterHeight = 40;
    private const int MaxVisibleRows = 8;
    private const float SlotScale = 0.755f;

    private static Item[][] _displayInputs;
    private static Item[] _displayOutputs;
    private static TradeOffer _conditionTooltipOffer;

    private static float[] _rowScale;
    private static bool[] _rowHovered;

    private static float _closeScale = 0.75f;
    private static bool _closeHovered;

    private const int InitialHoldDelay = 20;
    private const float DelayDecay = 0.7f;
    private const int MinDelay = 2;

    private const int BuyAllButtonWidth = 70;

    private static int _heldRow = -1;
    private static int _holdCountdown;
    private static int _currentDelay;
    private static int _holdFireCount;
    public static void Open(Villager trader)
    {
        if (trader == null || trader.Trades == null || trader.Trades.Count == 0)
            return;

        CurrentTrader = trader;
        IsOpen = true;
        BuildDisplayCache();
        SoundEngine.PlaySound(SoundID.MenuOpen);

        Main.npcChatText = string.Empty;
    }

    public static void Close()
    {
        if (!IsOpen) return;

        IsOpen = false;
        CurrentTrader = null;
        _displayInputs = null;
        _displayOutputs = null;
        _rowScale = null;
        _rowHovered = null;
        Main.LocalPlayer.SetTalkNPC(-1);
        SoundEngine.PlaySound(SoundID.MenuClose);
    }

    public static void Update()
    {
        if (!IsOpen) return;

        if (_heldRow != -1 && !Main.mouseLeft)
        {
            _heldRow = -1;
            _holdCountdown = 0;
            _currentDelay = InitialHoldDelay;
            _holdFireCount = 0;
        }

        if (Main.keyState.IsKeyDown(Keys.Escape) && !Main.oldKeyState.IsKeyDown(Keys.Escape))
        {
            Close();
            return;
        }

        // A sync can reuse the slot for another NPC, which replaces the ModNPC instance.
        if (CurrentTrader == null || !CurrentTrader.NPC.active || CurrentTrader.NPC.ModNPC != CurrentTrader)
        {
            Close();
            return;
        }

        if (Vector2.Distance(Main.LocalPlayer.Center, CurrentTrader.NPC.Center) > 180f)
            Close();
    }

    public static void Draw(SpriteBatch sb)
    {
        if (!IsOpen || CurrentTrader == null) return;

        _conditionTooltipOffer = null;

        var offers = CurrentTrader.Trades;
        int visible = Math.Min(offers.Count, MaxVisibleRows);
        int panelHeight = HeaderHeight + RowHeight * visible + FooterHeight;

        int panelX = (Main.screenWidth - PanelWidth) / 2;
        int panelY = (Main.screenHeight - panelHeight) / 2;

        if (MouseInRect(panelX, panelY, PanelWidth, panelHeight))
            Main.LocalPlayer.mouseInterface = true;

        DrawBackground(sb, panelX, panelY, PanelWidth, panelHeight);
        DrawHeader(sb, panelX, panelY);

        Main.inventoryScale = SlotScale;

        for (int i = 0; i < visible; i++)
        {
            int rowY = panelY + HeaderHeight + i * RowHeight;
            DrawOfferRow(sb, i, panelX, rowY);
        }

        DrawCloseButton(sb, panelX + PanelWidth - 60, panelY + panelHeight - FooterHeight + 12);

        if (_conditionTooltipOffer != null)
            DrawConditionTooltip(sb, _conditionTooltipOffer);
    }

    private static IEnumerable<Item> BuildCoinItems(int copperValue)
    {
        if (copperValue <= 0) yield break;

        int platinum = copperValue / 1_000_000;
        int gold = copperValue / 10_000 % 100;
        int silver = copperValue / 100 % 100;
        int copper = copperValue % 100;

        if (platinum > 0) yield return MakeItem(ItemID.PlatinumCoin, platinum);
        if (gold > 0) yield return MakeItem(ItemID.GoldCoin, gold);
        if (silver > 0) yield return MakeItem(ItemID.SilverCoin, silver);
        if (copper > 0) yield return MakeItem(ItemID.CopperCoin, copper);

        static Item MakeItem(int type, int stack)
        {
            var item = new Item();
            item.SetDefaults(type);
            item.stack = stack;
            return item;
        }
    }
    private static void DrawOfferRow(SpriteBatch sb, int index, int panelX, int rowY)
    {
        var offer = CurrentTrader.Trades[index];
        var inputs = _displayInputs[index];
        var output = _displayOutputs[index];

        bool canAfford = offer.PlayerCanAfford(Main.LocalPlayer);
        bool inStock = offer.InStock(CurrentTrader);
        bool unlocked = offer.IsUnlocked;
        bool executable = unlocked && inStock && canAfford;

        // Split the row into a main zone
        int buyAllX = panelX + PanelWidth - BuyAllButtonWidth - 18;
        var mainRect = new Rectangle(panelX + 12, rowY + 4, buyAllX - panelX - 16, RowHeight - 8);
        var buyAllRect = new Rectangle(buyAllX, rowY + 10, BuyAllButtonWidth, RowHeight - 20);

        bool mainHovered = MouseInRect(mainRect);
        bool buyAllHovered = MouseInRect(buyAllRect);
        UpdateRowHover(index, mainHovered || buyAllHovered);

        if (mainHovered && !unlocked && offer.Conditions.Count > 0)
            _conditionTooltipOffer = offer;

        UpdateRowHover(index, mainHovered || buyAllHovered);

        // Row backdrop
        Texture2D pixel = TextureAssets.MagicPixel.Value;
        Color rowColor = new(40, 40, 55, 120);
        if (!unlocked || !inStock) rowColor = new Color(80, 30, 30, 100);
        else if (mainHovered && executable) rowColor = new Color(60, 100, 60, 140);
        else if (mainHovered && !canAfford) rowColor = new Color(100, 80, 40, 120);
        sb.Draw(pixel, mainRect, rowColor);

        // Input slots
        int slotSize = (int)(52 * SlotScale);
        int slotY = rowY + (RowHeight - slotSize) / 2;
        int x = panelX + 24;
        for (int i = 0; i < inputs.Length; i++)
        {
            var slotRect = new Rectangle(x, slotY, slotSize, slotSize);
            ItemSlot.Draw(sb, ref inputs[i], ItemSlot.Context.ChatItem, new Vector2(x, slotY));
            TryHoverItem(inputs[i], slotRect);
            x += slotSize + 4;
        }

        // Arrow
        ChatManager.DrawColorCodedStringWithShadow(
            sb, FontAssets.MouseText.Value, ">>",
            new Vector2(panelX + 160, rowY + RowHeight / 2f - 10),
            executable ? Color.White : Color.Gray * 0.8f,
            0f, Vector2.Zero, Vector2.One);

        // Output slot
        var outputRect = new Rectangle(panelX + 196, slotY, slotSize, slotSize);
        ItemSlot.Draw(sb, ref output, ItemSlot.Context.ChatItem, new Vector2(panelX + 196, slotY));
        TryHoverItem(output, outputRect);

        string rightText = !unlocked
            ? "out of stock"
            : offer.MaxUses > 0
                ? $"{offer.UsesRemaining}/{offer.MaxUses}"
                : $"x{CurrentTrader.GetCarriedItemCount(output.type)}";
        ChatManager.DrawColorCodedStringWithShadow(
            sb, FontAssets.MouseText.Value, rightText,
            new Vector2(panelX + 250, rowY + RowHeight / 2f - 10),
            executable ? Color.White : Color.Gray, 0f, Vector2.Zero, Vector2.One);

        DrawBuyAllButton(sb, buyAllRect, buyAllHovered, executable);
        HandleRowInput(index, offer, mainHovered, buyAllHovered, executable);
    }

    private static void HandleRowInput(int index, TradeOffer offer, bool mainHovered, bool buyAllHovered, bool executable)
    {
        // Buy-all click
        if (buyAllHovered && Main.mouseLeft && Main.mouseLeftRelease)
        {
            if (executable) ExecuteBuyAll(offer);
            else SoundEngine.PlaySound(SoundID.MenuTick);
            return;
        }

        if (mainHovered && Main.mouseLeft && Main.mouseLeftRelease)
        {
            if (!executable)
            {
                SoundEngine.PlaySound(SoundID.MenuTick);
                return;
            }

            TryFire(offer);
            _heldRow = index;
            _holdCountdown = InitialHoldDelay;
            _currentDelay = InitialHoldDelay;
            _holdFireCount = 0;
            return;
        }

        if (_heldRow == index && Main.mouseLeft && mainHovered && executable)
        {
            _holdCountdown--;
            if (_holdCountdown <= 0)
            {
                TryFire(offer);
                _currentDelay = Math.Max(MinDelay, (int)(_currentDelay * DelayDecay));
                _holdCountdown = _currentDelay;
            }
        }
    }

    private static void TryFire(TradeOffer offer) => TradeNet.Trade(CurrentTrader, offer, 1, bulk: false);

    private static void ExecuteBuyAll(TradeOffer offer) => TradeNet.Trade(CurrentTrader, offer, int.MaxValue, bulk: true);

    /// <summary>Called once trades went through: at once in singleplayer, on the server's grant in multiplayer. 0 means refused.</summary>
    internal static void OnTraded(int count, bool bulk)
    {
        if (count <= 0)
        {
            SoundEngine.PlaySound(SoundID.MenuTick);
            return;
        }

        if (bulk)
        {
            SoundEngine.PlaySound(SoundID.Coins);
            return;
        }

        if (_holdFireCount % 3 == 0)
            SoundEngine.PlaySound(SoundID.Grab);

        _holdFireCount++;
    }

    /// <summary>Rebuilds the rows after a server sync replaced <paramref name="trader"/>'s offers.</summary>
    internal static void Refresh(Villager trader)
    {
        if (IsOpen && CurrentTrader == trader)
            BuildDisplayCache();
    }

    private static void DrawBuyAllButton(SpriteBatch sb, Rectangle rect, bool hovered, bool enabled)
    {
        Texture2D pixel = TextureAssets.MagicPixel.Value;

        Color bg = !enabled
            ? new Color(40, 25, 25, 140)
            : hovered
                ? new Color(80, 140, 90, 220)
                : new Color(50, 90, 60, 180);
        sb.Draw(pixel, rect, bg);

        Color border = !enabled
            ? new Color(80, 70, 70)
            : hovered ? new Color(140, 200, 140) : new Color(90, 140, 90);
        sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), border);
        sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), border);
        sb.Draw(pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), border);
        sb.Draw(pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), border);

        const string label = "All";
        Vector2 size = FontAssets.MouseText.Value.MeasureString(label) * 0.85f;
        Vector2 pos = new(
            rect.X + (rect.Width - size.X) / 2f,
            rect.Y + (rect.Height - size.Y) / 2f);
        Color textColor = enabled ? Color.White : Color.Gray * 0.7f;
        ChatManager.DrawColorCodedStringWithShadow(
            sb, FontAssets.MouseText.Value, label, pos, textColor,
            0f, Vector2.Zero, new Vector2(0.85f));

        if (hovered) Main.LocalPlayer.mouseInterface = true;
    }
    private static void DrawHeader(SpriteBatch sb, int panelX, int panelY)
    {
        string name = CurrentTrader.NPC.GivenOrTypeName;
        ChatManager.DrawColorCodedStringWithShadow(
            sb, FontAssets.DeathText.Value, name,
            new Vector2(panelX + 20, panelY + 14),
            Color.White, 0f, Vector2.Zero, new Vector2(0.7f));
    }

    private static void DrawCloseButton(SpriteBatch sb, int x, int y)
    {
        const string text = "Close";
        Vector2 size = FontAssets.MouseText.Value.MeasureString(text);

        var rect = new Rectangle(
            (int)(x - size.X * _closeScale / 2f) - 6,
            y - 4,
            (int)(size.X * _closeScale) + 12,
            (int)size.Y + 4);

        bool hovered = MouseInRect(rect);

        if (hovered)
        {
            if (!_closeHovered) SoundEngine.PlaySound(SoundID.MenuTick);
            _closeHovered = true;
            _closeScale = Math.Min(_closeScale + 0.05f, 1f);
            Main.LocalPlayer.mouseInterface = true;

            if (Main.mouseLeft && Main.mouseLeftRelease)
                Close();
        }
        else
        {
            _closeHovered = false;
            _closeScale = Math.Max(_closeScale - 0.05f, 0.75f);
        }

        Color color = hovered ? Main.OurFavoriteColor : Color.White * 0.9f;
        ChatManager.DrawColorCodedStringWithShadow(
            sb, FontAssets.MouseText.Value, text,
            new Vector2(x, y), color, 0f, size / 2f, new Vector2(_closeScale));
    }

    private static void DrawBackground(SpriteBatch sb, int x, int y, int w, int h)
    {
        Texture2D pixel = TextureAssets.MagicPixel.Value;

        sb.Draw(pixel, new Rectangle(x, y, w, h), new Color(20, 22, 38, 220));

        var border = new Color(90, 100, 140);
        sb.Draw(pixel, new Rectangle(x, y, w, 2), border);
        sb.Draw(pixel, new Rectangle(x, y + h - 2, w, 2), border);
        sb.Draw(pixel, new Rectangle(x, y, 2, h), border);
        sb.Draw(pixel, new Rectangle(x + w - 2, y, 2, h), border);

        sb.Draw(pixel,
            new Rectangle(x + 8, y + HeaderHeight - 2, w - 16, 1),
            new Color(70, 80, 120, 180));
    }


    private static bool MouseInRect(int x, int y, int w, int h)
        => Main.mouseX >= x && Main.mouseX <= x + w
        && Main.mouseY >= y && Main.mouseY <= y + h;

    private static bool MouseInRect(Rectangle r)
        => MouseInRect(r.X, r.Y, r.Width, r.Height);

    private static void UpdateRowHover(int index, bool hovering)
    {
        if (_rowHovered == null) return;

        if (hovering && !_rowHovered[index])
            SoundEngine.PlaySound(SoundID.MenuTick);

        _rowHovered[index] = hovering;
        _rowScale[index] = hovering
            ? Math.Min(_rowScale[index] + 0.05f, 1f)
            : Math.Max(_rowScale[index] - 0.05f, 0.75f);
    }

    private static void BuildDisplayCache()
    {
        var trades = CurrentTrader.Trades;
        _displayInputs = new Item[trades.Count][];
        _displayOutputs = new Item[trades.Count];
        _rowScale = new float[trades.Count];
        _rowHovered = new bool[trades.Count];

        for (int i = 0; i < trades.Count; i++)
        {
            var offer = trades[i];
            _rowScale[i] = 0.75f;

            var slots = new List<Item>(offer.Inputs.Count + 4);
            foreach (var req in offer.Inputs)
            {
                var item = new Item();
                item.SetDefaults(req.ItemId);
                item.stack = req.Stack;
                slots.Add(item);
            }
            slots.AddRange(BuildCoinItems(offer.CoinCost));
            _displayInputs[i] = slots.ToArray();

            var output = new Item();
            output.SetDefaults(offer.Output.ItemId);
            output.stack = offer.Output.Stack;
            _displayOutputs[i] = output;
        }
    }

    private static void TryHoverItem(Item item, Rectangle rect)
    {
        if (item.IsAir || !MouseInRect(rect)) return;

        Main.HoverItem = item.Clone();
        Main.hoverItemName = item.Name;
        Main.LocalPlayer.mouseInterface = true;
    }

    private static void DrawConditionTooltip(SpriteBatch sb, TradeOffer offer)
    {
        var font = FontAssets.MouseText.Value;
        var lines = new List<(string Text, Color Color)>
        {
            ("Requires:", new Color(255, 220, 150))
        };

        foreach (var cond in offer.Conditions)
        {
            bool met = cond.IsMet();
            Color color = met ? new Color(140, 230, 140) : new Color(230, 120, 120);
            lines.Add((cond.Description.Value, color));
        }

        const int padding = 10;
        const int lineSpacing = 22;

        float maxWidth = 0;
        foreach (var (text, _) in lines)
        {
            float w = font.MeasureString(text).X;
            if (w > maxWidth) maxWidth = w;
        }

        int panelW = (int)maxWidth + padding * 2;
        int panelH = lines.Count * lineSpacing + padding * 2 - 4;

        // Anchor near cursor, flip to stay on-screen.
        int px = Main.mouseX + 18;
        int py = Main.mouseY + 18;
        if (px + panelW > Main.screenWidth) px = Main.mouseX - panelW - 8;
        if (py + panelH > Main.screenHeight) py = Main.screenHeight - panelH - 4;

        Texture2D pixel = TextureAssets.MagicPixel.Value;
        sb.Draw(pixel, new Rectangle(px, py, panelW, panelH), new Color(25, 28, 48, 240));

        var border = new Color(110, 120, 160);
        sb.Draw(pixel, new Rectangle(px, py, panelW, 1), border);
        sb.Draw(pixel, new Rectangle(px, py + panelH - 1, panelW, 1), border);
        sb.Draw(pixel, new Rectangle(px, py, 1, panelH), border);
        sb.Draw(pixel, new Rectangle(px + panelW - 1, py, 1, panelH), border);

        for (int i = 0; i < lines.Count; i++)
        {
            ChatManager.DrawColorCodedStringWithShadow(
                sb, font, lines[i].Text,
                new Vector2(px + padding, py + padding + i * lineSpacing),
                lines[i].Color, 0f, Vector2.Zero, Vector2.One);
        }
    }
}