using ReLogic.Graphics;
using System.Collections.Generic;
using System.Linq;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.UI;
using TrelamiumIII.Common.Systems;

namespace TrelamiumIII.Content.Lodestone;

public class MicrolithItem : ModItem
{
    public override void SetDefaults()
    {
        Item.accessory = true;
        Item.rare = ItemRarityID.Blue;
        Item.width = Item.height = 32;
        Item.value = Item.sellPrice(gold: 1, silver: 8);
    }
    public override void UpdateAccessory(Player player, bool hideVisual)
    {
        player.GetModPlayer<MicrolithPlayer>().MicrolithEquipped = true;
    }
    public override void AddRecipes()
    {
        CreateRecipe()
        .AddIngredient(ItemID.SiltBlock, 45)
        .AddIngredient(ItemID.StoneBlock, 30)
        .AddIngredient(ItemID.AncientChisel)
        .AddTile(TileID.Furnaces)
        .Register();
    }
}

/// <summary>
///     Tracks whether the Microlith is equipped this tick. Reset every frame and set again from
///     <see cref="MicrolithItem.UpdateAccessory" />, so unequipping takes effect immediately.
/// </summary>
public class MicrolithPlayer : ModPlayer
{
    public bool MicrolithEquipped;

    public override void ResetEffects() => MicrolithEquipped = false;
}

/// <summary>
///     Drives <see cref="HarvestNotificationManager" />'s expiry sweep. Without this the manager
///     keeps handing already-expired notifications back to <see cref="HarvestNotification.AddToStack" />,
///     which resurrects a counter the vanilla tracker has already dropped.
/// </summary>
public class MicrolithNotificationSystem : ModSystem
{
    public override void PostUpdateWorld()
    {
        if (Main.dedServ)
            return;

        if (Main.LocalPlayer?.active == true && !Main.gameMenu)
            HarvestNotificationManager.UpdateNotifications();
    }

    /// <remarks>
    ///     The list is static, so without this a notification outlives the world that produced it and
    ///     the next world starts out able to stack onto an entry no tracker is drawing.
    /// </remarks>
    public override void ClearWorld() => HarvestNotificationManager.Clear();
}

public class MicrolithGlobalTile : GlobalTile
{
    private const float BONUS_CHANCE = 0.15f;

    public override void Drop(int i, int j, int type)
    {
        base.Drop(i, j, type);

        // Drop resolves on the server in multiplayer, where Main.LocalPlayer is the dummy player 255
        // and never has the accessory equipped. GetPlayerForTile is tModLoader's documented answer for
        // attributing a tile operation to whoever caused it, and returns the local player in single
        // player.
        Player player = WorldGen.GetPlayerForTile(i, j);
        if (player is null || !player.active)
            return;

        if (!player.GetModPlayer<MicrolithPlayer>().MicrolithEquipped)
            return;

        // Kept a subset of PlacedTileTracker.IsValuable, so nothing can be paid out on that the
        // tracker never had a chance to record.
        if (!Main.tileSolid[type] || !PlacedTileTracker.IsValuable(type))
            return;

        // The block has to have been found, not carried in. Without this any single ore block can be
        // placed and re-mined indefinitely for a net gain. Released either way -- the tile is on its
        // way out, so keeping the mark would only leave the set to grow.
        bool wasPlacedByPlayer = PlacedTileTracker.WasPlacedByPlayer(i, j);
        PlacedTileTracker.Forget(i, j);

        if (wasPlacedByPlayer)
            return;

        if (Main.rand.NextFloat() >= BONUS_CHANCE)
            return;

        int itemType = GetGemOrOreItem(i, j, type);

        // GetItemDropFromTypeAndStyle returns 0 for a tile that drops nothing. Spawning type 0 yields
        // a blank item and a notification titled after it, so there is nothing to award here.
        if (itemType <= 0)
            return;

        int extraItems = Main.rand.Next(1, 3);
        player.QuickSpawnItem(new EntitySource_TileBreak(i, j), itemType, extraItems);

        PlayHarvestFeedback(i, j, player, itemType, extraItems);
    }

    /// <remarks>
    ///     Runs from <see cref="Drop" /> rather than <see cref="KillTile" />. The KillTile hook fires
    ///     <i>before</i> drops resolve -- its <c>fail</c> and <c>noItem</c> parameters exist precisely
    ///     to influence them -- so a flag set during Drop and read in KillTile lands one tile late,
    ///     playing the previous break's effects on the next block mined.
    /// </remarks>
    private static void PlayHarvestFeedback(int i, int j, Player player, int itemType, int count)
    {
        // Dust, sound and the popup are all local presentation. On a server there is no one to show
        // them to, and in multiplayer they belong to the client that did the mining.
        if (Main.dedServ || player.whoAmI != Main.myPlayer)
            return;

        Vector2 tilePos = new Vector2(i * 16, j * 16);
        for (int dustCount = 0; dustCount < 30; dustCount++)
        {
            float velX = Main.rand.NextFloat(-2f, 2f);
            float velY = Main.rand.NextFloat(-4f, -1f);
            Dust.NewDust(tilePos, 16, 16, DustID.SpelunkerGlowstickSparkle, velX, velY, Scale: 1.2f);
        }

        SoundEngine.PlaySound(SoundID.CoinPickup, tilePos);
        HarvestNotificationManager.ShowHarvestNotification(itemType, count);
    }

    private static int GetGemOrOreItem(int i, int j, int tileType)
    {
        // gem blocks drop actual gems instead of blocks
        switch (tileType)
        {
            case TileID.Sapphire: return ItemID.Sapphire;
            case TileID.Ruby: return ItemID.Ruby;
            case TileID.Emerald: return ItemID.Emerald;
            case TileID.Topaz: return ItemID.Topaz;
            case TileID.Amethyst: return ItemID.Amethyst;
            case TileID.Diamond: return ItemID.Diamond;
        }

        // Style has to come from the tile, not default to 0. A multi-style ore -- modded ores sharing
        // one tile type, most obviously -- otherwise always pays out as whichever variant sits at
        // style 0.
        Tile tile = Framing.GetTileSafely(i, j);
        int style = tile.TileFrameX / 18;

        return TileLoader.GetItemDropFromTypeAndStyle(tileType, style);
    }
}

public class HarvestNotification : IInGameNotification
{
    private const int DURATION = 180;
    private const float FADE_TIME = 30f;

    public int ItemType { get; private set; }
    public int Count { get; private set; }
    public string ItemName { get; private set; }

    private int timer;
    private float yOffset;
    private float scale = 1f;
    private Color color = Color.White;

    public bool ShouldBeRemoved => timer >= DURATION;

    public HarvestNotification(int itemType, int count)
    {
        ItemType = itemType;
        Count = count;

        Item item = new();
        item.SetDefaults(itemType);
        ItemName = item.Name;

        timer = 0;
        yOffset = -325f; // Start at anchor position
    }

    public void AddToStack(int additionalCount)
    {
        Count += additionalCount;
        timer = 0; // Reset timer

        // Small bounce effect when adding
        scale = 1.4f;
    }

    public void DrawInGame(SpriteBatch spriteBatch, Vector2 bottomAnchorPosition)
    {
        // Calculate fade
        float alpha = 1f;
        if (timer > DURATION - FADE_TIME)
        {
            alpha = 1f - (timer - (DURATION - FADE_TIME)) / FADE_TIME;
        }

        Vector2 drawPos = bottomAnchorPosition + new Vector2(30, yOffset);

        // Draw item texture
        if (TextureAssets.Item[ItemType]?.Value is Texture2D itemTexture)
        {
            var itemColor = color * alpha;
            var itemOrigin = itemTexture.Size() * 0.5f;

            spriteBatch.Draw(itemTexture, drawPos - new Vector2(40, 0), null, itemColor, 0f,
                itemOrigin, scale * 0.8f, SpriteEffects.None, 0f);
        }

        // Draw count text
        string countText = $"x{Count}";
        var font = FontAssets.MouseText.Value;
        var textSize = font.MeasureString(countText);
        var textPos = drawPos + new Vector2(-32, -textSize.Y * 0.5f);
        var textColor = new Color(204, 181, 72) * alpha;

        // Text outline
        for (int i = -1; i <= 1; i += 2)
        {
            for (int j = -1; j <= 1; j += 2)
            {
                spriteBatch.DrawString(font, countText, textPos + new Vector2(i, j),
                    Color.Black * alpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
        }

        spriteBatch.DrawString(font, countText, textPos, textColor, 0f, Vector2.Zero,
            scale, SpriteEffects.None, 0f);
    }

    public void PushAnchor(ref Vector2 positionAnchorBottom)
    {
        // Push anchor up by consistent notification height for better stacking
        positionAnchorBottom.Y -= 30f; // Fixed height per notification
    }

    public void Update()
    {
        timer++;

        // Smooth scale back to normal
        scale = MathHelper.Lerp(scale, 1f, 0.1f);

        // Float upward only during fade period
        if (timer > DURATION - FADE_TIME)
        {
            yOffset -= 0.5f;
        }
    }
}

public static class HarvestNotificationManager
{
    private static readonly List<HarvestNotification> ActiveNotifications = [];

    public static void ShowHarvestNotification(int itemType, int count)
    {
        // ShouldBeRemoved is the same predicate InGameNotificationsTracker prunes on, and its sweep
        // can run before ours. Testing it here rather than trusting the list closes the window where
        // an expired entry is still present: stacking onto one would reset a timer on a notification
        // the vanilla tracker has already dropped, and the pickup would show nothing at all.
        UpdateNotifications();

        var existing = ActiveNotifications.FirstOrDefault(n => n.ItemType == itemType);

        if (existing != null)
        {
            existing.AddToStack(count);
        }
        else
        {
            var notification = new HarvestNotification(itemType, count);
            ActiveNotifications.Add(notification);
            InGameNotificationsTracker.AddNotification(notification);
        }
    }

    public static void UpdateNotifications()
    {
        for (int i = ActiveNotifications.Count - 1; i >= 0; i--)
        {
            if (ActiveNotifications[i].ShouldBeRemoved)
            {
                ActiveNotifications.RemoveAt(i);
            }
        }
    }

    /// <summary>Drops every tracked notification when leaving a world.</summary>
    public static void Clear() => ActiveNotifications.Clear();
}