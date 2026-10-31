using System.Collections.Generic;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ObjectData;
using TrelamiumIII.Core.Structures;

namespace TrelamiumIII.Core.Jobs.Blueprints;

/// <summary>
///     Draws the local player's blueprints as ghosts over their sites: the current stage's layers clearly, later ones
///     faintly, nothing where the world already matches, and a red mark where the wrong tile is in the way. A blueprint
///     in hand (<see cref="BlueprintPlayer.Holding"/>) is drawn whole at the cursor instead.
/// </summary>
[Autoload(Side = ModSide.Client)]
public sealed class BlueprintRenderer : ModSystem
{
    private static readonly Color Ink = new(150, 200, 255);
    private static readonly Color WrongColor = new(255, 60, 50);
    private static readonly Color FitsColor = new(110, 255, 140);

    private const float StageOpacity = 0.5f;
    private const float LaterOpacity = 0.12f;
    private const float HeldOpacity = 0.4f;
    private const float WrongOpacity = 0.35f;

    /// <summary>The middle 16x16 of the fully surrounded wall frame, which is plain wall whatever the neighbours.</summary>
    private static readonly Rectangle WallFill = new(36 + 8, 36 + 8, 16, 16);

    /// <summary>Per frame, each blueprint underway and the layers its current stage asks for.</summary>
    private static readonly Dictionary<Blueprint, BlueprintLayer> Stages = [];

    public override void PostDrawTiles()
    {
        if (Main.gameMenu)
            return;

        var player = Main.LocalPlayer;
        var holding = player.GetModPlayer<BlueprintPlayer>().Holding;

        Stages.Clear();

        foreach (var task in BlueprintTask.Underway(player.GetModPlayer<JobPlayer>()))
            Stages[task.Blueprint] = Stages.GetValueOrDefault(task.Blueprint) | task.Layers;

        if (Stages.Count == 0)
            return;

        var batch = Main.spriteBatch;

        batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, Main.Rasterizer, null,
            Main.GameViewMatrix.TransformationMatrix);

        var pulse = 0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 3f);

        foreach ((var blueprint, var stage) in Stages)
        {
            if (blueprint != holding && blueprint.Site is { } site)
                DrawSite(batch, blueprint, site, stage, pulse);
        }

        if (holding is not null)
            DrawHeld(batch, holding);

        batch.End();
    }

    private static void DrawSite(SpriteBatch batch, Blueprint blueprint, Point site, BlueprintLayer stage, float pulse)
    {
        var visible = Rectangle.Intersect(blueprint.AreaAt(site), VisibleTiles());

        if (visible.IsEmpty)
            return;

        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var piece in blueprint.Pieces)
            {
                if (!InPass(piece, pass))
                    continue;

                var opacity = (piece.Layer & stage) != 0 ? StageOpacity * pulse : LaterOpacity;

                foreach (var cell in piece.Cells)
                {
                    int i = site.X + cell.X, j = site.Y + cell.Y;

                    if (!visible.Contains(i, j))
                        continue;

                    switch (blueprint.StateOf(piece, cell, site))
                    {
                        case BlueprintCellState.Built:
                            break;

                        case BlueprintCellState.Wrong when piece.Layer != BlueprintLayer.Walls:
                            Fill(batch, i, j, WrongColor * (WrongOpacity * opacity / StageOpacity));
                            break;

                        default:
                            DrawCell(batch, blueprint, piece, cell, i, j, Ink * opacity);
                            break;
                    }
                }
            }
        }

        Outline(batch, blueprint.AreaAt(site), Ink * (0.35f * pulse));
    }

    private static void DrawHeld(SpriteBatch batch, Blueprint blueprint)
    {
        var topLeft = BlueprintPlayer.HeldOrigin(blueprint);
        var area = blueprint.AreaAt(topLeft);
        var visible = Rectangle.Intersect(area, VisibleTiles());

        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var piece in blueprint.Pieces)
            {
                if (!InPass(piece, pass))
                    continue;

                foreach (var cell in piece.Cells)
                {
                    int i = topLeft.X + cell.X, j = topLeft.Y + cell.Y;

                    if (visible.Contains(i, j))
                        DrawCell(batch, blueprint, piece, cell, i, j, Ink * HeldOpacity);
                }
            }
        }

        var fits = blueprint.FitsAt(topLeft);
        var color = fits ? FitsColor : WrongColor;
        var hint = TrelamiumIII.Instance.GetLocalization(fits ? "Jobs.Blueprint.HoldHint" : "Jobs.Blueprint.OffWorld").Value;

        Outline(batch, area, color * 0.8f);
        Utils.DrawBorderString(batch, hint, new Vector2(area.Left * 16, area.Top * 16 - 28) - Main.screenPosition, color);
    }

    /// <summary>Walls go in the first pass, so the ghost tiles in front of them draw over them.</summary>
    private static bool InPass(BlueprintPiece piece, int pass) => piece.Layer == BlueprintLayer.Walls == (pass == 0);

    /// <summary>The blueprint's own tile or wall at (<paramref name="i"/>, <paramref name="j"/>), framed as it was captured.</summary>
    private static void DrawCell(SpriteBatch batch, Blueprint blueprint, BlueprintPiece piece, Point16 cell, int i, int j, Color color)
    {
        var position = new Vector2(i * 16, j * 16) - Main.screenPosition;

        if (piece.Layer == BlueprintLayer.Walls)
        {
            // Hidden behind a solid block, as the real wall would be.
            var tile = Framing.GetTileSafely(i, j);

            if (tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType] && !tile.IsActuated)
                return;

            Main.instance.LoadWall(piece.Type);
            batch.Draw(TextureAssets.Wall[piece.Type].Value, position, WallFill, color);

            return;
        }

        DrawTile(batch, blueprint.Data[cell.X, cell.Y], position, color);
    }

    private static void DrawTile(SpriteBatch batch, in StructureCell cell, Vector2 position, Color color)
    {
        Main.instance.LoadTiles(cell.TileType);

        var texture = TextureAssets.Tile[cell.TileType].Value;

        if (Main.tileFrameImportant[cell.TileType] && TileObjectData.GetTileData(cell.TileType, 0) is { } objectData)
        {
            var source = new Rectangle(cell.TileFrameX, cell.TileFrameY, objectData.CoordinateWidth, RowHeight(objectData, cell.TileFrameY));
            batch.Draw(texture, position + new Vector2(objectData.DrawXOffset, objectData.DrawYOffset), source, color);

            return;
        }

        switch (cell.BlockType)
        {
            case BlockType.Solid:
                batch.Draw(texture, position, new Rectangle(cell.TileFrameX, cell.TileFrameY, 16, 16), color);
                break;

            case BlockType.HalfBlock:
                batch.Draw(texture, position + new Vector2(0, 8), new Rectangle(cell.TileFrameX, cell.TileFrameY, 16, 8), color);
                break;

            default:
                // Slopes as eight 2px columns, each cut to the solid part of the triangle.
                for (var x = 0; x < 16; x += 2)
                {
                    (int top, int height) = cell.BlockType switch
                    {
                        BlockType.SlopeDownLeft => (x, 16 - x),
                        BlockType.SlopeDownRight => (14 - x, x + 2),
                        BlockType.SlopeUpLeft => (0, 16 - x),
                        _ => (0, x + 2)
                    };

                    batch.Draw(texture, position + new Vector2(x, top), new Rectangle(cell.TileFrameX + x, cell.TileFrameY + top, 2, height), color);
                }

                break;
        }
    }

    /// <summary>Height of the object row that <paramref name="frameY"/> falls in; rows can differ, like a table's taller legs.</summary>
    private static int RowHeight(TileObjectData objectData, int frameY)
    {
        var y = frameY % objectData.CoordinateFullHeight;
        var top = 0;

        foreach (var height in objectData.CoordinateHeights)
        {
            top += height + objectData.CoordinatePadding;

            if (y < top)
                return height;
        }

        return 16;
    }

    private static void Fill(SpriteBatch batch, int i, int j, Color color)
        => batch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(i * 16 - (int)Main.screenPosition.X, j * 16 - (int)Main.screenPosition.Y, 16, 16), color);

    private static void Outline(SpriteBatch batch, Rectangle tiles, Color color)
    {
        const int thickness = 2;

        var left = tiles.Left * 16 - thickness - (int)Main.screenPosition.X;
        var top = tiles.Top * 16 - thickness - (int)Main.screenPosition.Y;
        var width = tiles.Width * 16 + thickness * 2;
        var height = tiles.Height * 16 + thickness * 2;
        var pixel = TextureAssets.MagicPixel.Value;

        batch.Draw(pixel, new Rectangle(left, top, width, thickness), color);
        batch.Draw(pixel, new Rectangle(left, top + height - thickness, width, thickness), color);
        batch.Draw(pixel, new Rectangle(left, top + thickness, thickness, height - thickness * 2), color);
        batch.Draw(pixel, new Rectangle(left + width - thickness, top + thickness, thickness, height - thickness * 2), color);
    }

    private static Rectangle VisibleTiles()
    {
        var left = (int)(Main.screenPosition.X / 16f) - 1;
        var top = (int)(Main.screenPosition.Y / 16f) - 1;

        return new Rectangle(left, top, Main.screenWidth / 16 + 3, Main.screenHeight / 16 + 3);
    }
}
