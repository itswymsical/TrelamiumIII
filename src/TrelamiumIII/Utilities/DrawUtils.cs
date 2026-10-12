/*
 * Copyright (C) 2024 Project Starlight River
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */
using Terraria.GameContent;

namespace TrelamiumIII.Utilities;

internal static class DrawUtils
{
    public static Vector3 ToVector3(this Vector2 vector) => new Vector3(vector.X, vector.Y, 0f);

    /// <summary>Ends and restarts the batch with vanilla's default world parameters.</summary>
    public static void RestartToDefault(this SpriteBatch batch)
    {
        batch.End();
        batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None,
            Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
    }

    public static void DrawLine(this SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color? color = null, int lineWidth = 1)
    {
        var dist = end - start;

        var rotation = dist.ToRotation();

        var lineColor = color ?? Color.White;

        var destRect = new Rectangle((int)start.X, (int)start.Y, (int)dist.Length(), lineWidth);

        spriteBatch.Draw(TextureAssets.MagicPixel.Value, destRect, null, lineColor, rotation, default, SpriteEffects.None, 0);
    }

    /// <summary>9-slice panel: a fill tinted <paramref name="c" /> under a white outline, both scaled by <paramref name="opacity" />.</summary>
    public static void DrawPanel(SpriteBatch sb, int x, int y, int w, int h, Color c = default, Texture texture = null, float opacity = 1f)
    {
        if (c == default)
            c = new Color(63, 65, 151, 255) * 0.785f;

        c *= opacity;
        var outline = Color.White * opacity;

        var value = (Texture2D)Assets.Textures.UI.Dialogue.PortraitBox.Asset;
        var value2 = (Texture2D)Assets.Textures.UI.Dialogue.PortraitBox_Inner.Asset;

        texture = texture ?? value;

        // Border sizes of the 480x106 PortraitBox sprites.
        int borderLeft = 20;
        int borderRight = 20;
        int borderTop = 30;
        int borderBottom = 30;

        int minWidth = borderLeft + borderRight;
        int minHeight = borderTop + borderBottom;

        if (w < minWidth) w = minWidth;
        if (h < minHeight) h = minHeight;

        int srcWidth = value.Width;
        int srcHeight = value.Height;
        int srcCenterWidth = srcWidth - borderLeft - borderRight;
        int srcCenterHeight = srcHeight - borderTop - borderBottom;

        // Draw the 9 sections of the panel
        #region Inner
        // Top-left corner
        sb.Draw(value2, new Rectangle(x, y, borderLeft, borderTop),
                new Rectangle(0, 0, borderLeft, borderTop), c);

        // Top edge (stretch horizontally)
        sb.Draw(value2, new Rectangle(x + borderLeft, y, w - borderLeft - borderRight, borderTop),
                new Rectangle(borderLeft, 0, srcCenterWidth, borderTop), c);

        // Top-right corner
        sb.Draw(value2, new Rectangle(x + w - borderRight, y, borderRight, borderTop),
                new Rectangle(srcWidth - borderRight, 0, borderRight, borderTop), c);

        // Left edge (stretch vertically)
        sb.Draw(value2, new Rectangle(x, y + borderTop, borderLeft, h - borderTop - borderBottom),
                new Rectangle(0, borderTop, borderLeft, srcCenterHeight), c);

        // Center (stretch both ways)
        sb.Draw(value2, new Rectangle(x + borderLeft, y + borderTop, w - borderLeft - borderRight, h - borderTop - borderBottom),
                new Rectangle(borderLeft, borderTop, srcCenterWidth, srcCenterHeight), c);

        // Right edge (stretch vertically)
        sb.Draw(value2, new Rectangle(x + w - borderRight, y + borderTop, borderRight, h - borderTop - borderBottom),
                new Rectangle(srcWidth - borderRight, borderTop, borderRight, srcCenterHeight), c);

        // Bottom-left corner
        sb.Draw(value2, new Rectangle(x, y + h - borderBottom, borderLeft, borderBottom),
                new Rectangle(0, srcHeight - borderBottom, borderLeft, borderBottom), c);

        // Bottom edge (stretch horizontally)
        sb.Draw(value2, new Rectangle(x + borderLeft, y + h - borderBottom, w - borderLeft - borderRight, borderBottom),
                new Rectangle(borderLeft, srcHeight - borderBottom, srcCenterWidth, borderBottom), c);

        // Bottom-right corner
        sb.Draw(value2, new Rectangle(x + w - borderRight, y + h - borderBottom, borderRight, borderBottom),
                new Rectangle(srcWidth - borderRight, srcHeight - borderBottom, borderRight, borderBottom), c);
        #endregion

        #region Outline
        // Top-left corner
        sb.Draw(value, new Rectangle(x, y, borderLeft, borderTop),
                new Rectangle(0, 0, borderLeft, borderTop), outline);

        // Top edge (stretch horizontally)
        sb.Draw(value, new Rectangle(x + borderLeft, y, w - borderLeft - borderRight, borderTop),
                new Rectangle(borderLeft, 0, srcCenterWidth, borderTop), outline);

        // Top-right corner
        sb.Draw(value, new Rectangle(x + w - borderRight, y, borderRight, borderTop),
                new Rectangle(srcWidth - borderRight, 0, borderRight, borderTop), outline);

        // Left edge (stretch vertically)
        sb.Draw(value, new Rectangle(x, y + borderTop, borderLeft, h - borderTop - borderBottom),
                new Rectangle(0, borderTop, borderLeft, srcCenterHeight), outline);

        // Center (stretch both ways)
        sb.Draw(value, new Rectangle(x + borderLeft, y + borderTop, w - borderLeft - borderRight, h - borderTop - borderBottom),
                new Rectangle(borderLeft, borderTop, srcCenterWidth, srcCenterHeight), outline);

        // Right edge (stretch vertically)
        sb.Draw(value, new Rectangle(x + w - borderRight, y + borderTop, borderRight, h - borderTop - borderBottom),
                new Rectangle(srcWidth - borderRight, borderTop, borderRight, srcCenterHeight), outline);

        // Bottom-left corner
        sb.Draw(value, new Rectangle(x, y + h - borderBottom, borderLeft, borderBottom),
                new Rectangle(0, srcHeight - borderBottom, borderLeft, borderBottom), outline);

        // Bottom edge (stretch horizontally)
        sb.Draw(value, new Rectangle(x + borderLeft, y + h - borderBottom, w - borderLeft - borderRight, borderBottom),
                new Rectangle(borderLeft, srcHeight - borderBottom, srcCenterWidth, borderBottom), outline);

        // Bottom-right corner
        sb.Draw(value, new Rectangle(x + w - borderRight, y + h - borderBottom, borderRight, borderBottom),
                new Rectangle(srcWidth - borderRight, srcHeight - borderBottom, borderRight, borderBottom), outline);
        #endregion
    }

    public static void DrawPanel(SpriteBatch sb, Rectangle R, Color c = default, Texture texture = null, float opacity = 1f)
        => DrawPanel(sb, R.X, R.Y, R.Width, R.Height, c, texture, opacity);

}
