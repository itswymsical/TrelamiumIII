/*
 * Copyright (C) 2025 Spirit Reforged
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
namespace TrelamiumIII.Utilities.Extensions;

public static class TileExtensions
{
	public static Vector2 TileOffset => Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);

	public static void DrawSloped(this ModTile _, int i, int j, Texture2D texture, Color color, Vector2 positionOffset, bool overrideFrame = false)
		=> DrawSloped(i, j, texture, color, positionOffset, overrideFrame);

	public static void DrawSloped(int i, int j, Texture2D texture, Color color, Vector2 positionOffset, bool overrideFrame = false)
	{
		var tile = Main.tile[i, j];
		int frameX = tile.TileFrameX;
		int frameY = tile.TileFrameY;

		if (overrideFrame)
		{
			frameX = 0;
			frameY = 0;
		}

		var width = 16;
		var height = 16;
		var location = new Vector2(i * 16, j * 16);
		var offsets = -Main.screenPosition + TileOffset + positionOffset;
		var drawLoc = location + offsets;

		if (tile.Slope == 0 && !tile.IsHalfBlock || Main.tileSolid[tile.TileType] && Main.tileSolidTop[tile.TileType]) //second one should be for platforms
			Main.spriteBatch.Draw(texture, drawLoc, new Rectangle(frameX, frameY, width, height), color, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
		else if (tile.IsHalfBlock)
			Main.spriteBatch.Draw(texture, new Vector2(drawLoc.X, drawLoc.Y + 8), new Rectangle(frameX, frameY, width, 8), color, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
		else
		{
			var b = tile.Slope;
			Rectangle frame;
			Vector2 drawPos;

			if (b is SlopeType.SlopeDownLeft or SlopeType.SlopeDownRight)
			{
				int length;
				int height2;

				for (var a = 0; a < 8; ++a)
				{
					if (b == SlopeType.SlopeDownRight)
					{
						length = 16 - a * 2 - 2;
						height2 = 14 - a * 2;
					}
					else
					{
						length = a * 2;
						height2 = 14 - length;
					}

					frame = new Rectangle(frameX + length, frameY, 2, height2);
					drawPos = new Vector2(i * 16 + length, j * 16 + a * 2) + offsets;
					Main.spriteBatch.Draw(texture, drawPos, frame, color, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
				}

				frame = new Rectangle(frameX, frameY + 14, 16, 2);
				drawPos = new Vector2(i * 16, j * 16 + 14) + offsets;
				Main.spriteBatch.Draw(texture, drawPos, frame, color, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
			}
			else
			{
				int length;
				int height2;

				for (var a = 0; a < 8; ++a)
				{
					if (b == SlopeType.SlopeUpLeft)
					{
						length = a * 2;
						height2 = 16 - length;
					}
					else
					{
						length = 16 - a * 2 - 2;
						height2 = 16 - a * 2;
					}

					frame = new Rectangle(frameX + length, frameY + 16 - height2, 2, height2);
					drawPos = new Vector2(i * 16 + length, j * 16) + offsets;
					Main.spriteBatch.Draw(texture, drawPos, frame, color, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
				}

				drawPos = new Vector2(i * 16, j * 16) + offsets;
				frame = new Rectangle(frameX, frameY, 16, 2);
				Main.spriteBatch.Draw(texture, drawPos, frame, color, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
			}
		}
	}

	/// <summary>
	/// Mutually merges the given tile with all of the ids in <paramref name="otherIds"/>.
	/// </summary>
	/// <param name="tile">The tile to merge with.</param>
	/// <param name="otherIds">All other tiles to merge with.</param>
	public static void Merge(this ModTile tile, params int[] otherIds)
	{
		foreach (var id in otherIds)
		{
			Main.tileMerge[tile.Type][id] = true;
			Main.tileMerge[id][tile.Type] = true;
		}
	}
}
