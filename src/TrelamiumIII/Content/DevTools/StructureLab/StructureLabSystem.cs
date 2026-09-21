using Terraria.GameContent;

namespace TrelamiumIII.Content.DevTools.StructureLab;

/// <summary>Draws the canvas frame and grid over the lab's tiles, and lets blocks be placed in midair there.</summary>
public sealed class StructureLabSystem : ModSystem
{
    /// <summary>Every this many tiles, counted from the canvas top-left, a grid line is drawn darker.</summary>
    private const int MajorGridStep = 10;

    private static readonly Color FrameColor = new(255, 190, 60);
    private static readonly Color MinorGridColor = Color.Black * 0.1f;
    private static readonly Color MajorGridColor = Color.Black * 0.25f;

    public override void Load() => On_Player.PlaceThing_Tiles_BlockPlacementForAssortedThings += PlaceInMidair;

    /// <summary>Allows any empty target in the lab: a new canvas has nothing for its first block to attach to.</summary>
    private static bool PlaceInMidair(On_Player.orig_PlaceThing_Tiles_BlockPlacementForAssortedThings orig, Player self, bool canPlace)
        => orig(self, canPlace) || (StructureEditor.InLab && !Main.tile[Player.tileTargetX, Player.tileTargetY].HasTile);

    public override void PostDrawTiles()
    {
        Rectangle canvas = StructureEditor.Canvas;

        if (!StructureEditor.InLab || canvas.IsEmpty)
            return;

        SpriteBatch batch = Main.spriteBatch;

        batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, Main.Rasterizer, null,
            Main.GameViewMatrix.TransformationMatrix);

        DrawGrid(batch, canvas);
        DrawFrame(batch, canvas);

        string label = $"{StructureEditor.DocumentName}  {canvas.Width}x{canvas.Height}";
        Utils.DrawBorderString(batch, label, new Vector2(canvas.Left * 16, canvas.Top * 16 - 28) - Main.screenPosition, FrameColor);

        batch.End();
    }

    private static void DrawGrid(SpriteBatch batch, Rectangle canvas)
    {
        Rectangle visible = Rectangle.Intersect(canvas, VisibleTiles());

        if (visible.IsEmpty)
            return;

        for (int x = visible.Left; x <= visible.Right; x++)
        {
            Color color = (x - canvas.Left) % MajorGridStep == 0 ? MajorGridColor : MinorGridColor;
            Line(batch, new Rectangle(x * 16, visible.Top * 16, 1, visible.Height * 16), color);
        }

        for (int y = visible.Top; y <= visible.Bottom; y++)
        {
            Color color = (y - canvas.Top) % MajorGridStep == 0 ? MajorGridColor : MinorGridColor;
            Line(batch, new Rectangle(visible.Left * 16, y * 16, visible.Width * 16, 1), color);
        }
    }

    private static void DrawFrame(SpriteBatch batch, Rectangle canvas)
    {
        const int thickness = 2;

        int left = canvas.Left * 16 - thickness;
        int top = canvas.Top * 16 - thickness;
        int width = canvas.Width * 16 + thickness * 2;
        int height = canvas.Height * 16 + thickness * 2;

        Line(batch, new Rectangle(left, top, width, thickness), FrameColor);
        Line(batch, new Rectangle(left, top + height - thickness, width, thickness), FrameColor);
        Line(batch, new Rectangle(left, top, thickness, height), FrameColor);
        Line(batch, new Rectangle(left + width - thickness, top, thickness, height), FrameColor);
    }

    /// <summary>Draws a world-space rectangle.</summary>
    private static void Line(SpriteBatch batch, Rectangle world, Color color)
    {
        world.Offset(-(int)Main.screenPosition.X, -(int)Main.screenPosition.Y);
        batch.Draw(TextureAssets.MagicPixel.Value, world, color);
    }

    private static Rectangle VisibleTiles()
    {
        int left = (int)(Main.screenPosition.X / 16f) - 1;
        int top = (int)(Main.screenPosition.Y / 16f) - 1;

        return new Rectangle(left, top, Main.screenWidth / 16 + 3, Main.screenHeight / 16 + 3);
    }
}
