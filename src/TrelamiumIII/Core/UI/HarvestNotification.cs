using System.Collections.Generic;
using System.Linq;
using ReLogic.Graphics;
using Terraria.GameContent;
using Terraria.UI;

namespace TrelamiumIII.Core.UI;

/// <summary>Drives the notification expiry sweep, so expired entries can't be stacked onto and resurrected.</summary>
public class HarvestNotificationSystem : ModSystem
{
    public override void PostUpdateWorld()
    {
        if (Main.dedServ)
            return;

        if (Main.LocalPlayer?.active == true && !Main.gameMenu)
            HarvestNotificationManager.UpdateNotifications();
    }

    /// <remarks>The list is static, so notifications would otherwise outlive their world.</remarks>
    public override void ClearWorld() => HarvestNotificationManager.Clear();
}

/// <summary>A popup naming an item and how many of it a payout awarded.</summary>
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

        scale = 1.4f;
    }

    public void DrawInGame(SpriteBatch spriteBatch, Vector2 bottomAnchorPosition)
    {
        float alpha = 1f;
        if (timer > DURATION - FADE_TIME)
        {
            alpha = 1f - (timer - (DURATION - FADE_TIME)) / FADE_TIME;
        }

        Vector2 drawPos = bottomAnchorPosition + new Vector2(30, yOffset);

        if (TextureAssets.Item[ItemType]?.Value is Texture2D itemTexture)
        {
            var itemColor = color * alpha;
            var itemOrigin = itemTexture.Size() * 0.5f;

            spriteBatch.Draw(itemTexture, drawPos - new Vector2(40, 0), null, itemColor, 0f,
                itemOrigin, scale * 0.8f, SpriteEffects.None, 0f);
        }

        string countText = $"x{Count}";
        var font = FontAssets.MouseText.Value;
        var textSize = font.MeasureString(countText);
        var textPos = drawPos + new Vector2(-32, -textSize.Y * 0.5f);
        var textColor = new Color(204, 181, 72) * alpha;

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
        positionAnchorBottom.Y -= 30f; // Fixed height per notification
    }

    public void Update()
    {
        timer++;

        scale = MathHelper.Lerp(scale, 1f, 0.1f);

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
        // Prune first: vanilla's tracker may already have dropped an expired entry, and stacking onto it would show nothing.
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
