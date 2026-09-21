using Terraria.GameContent;

namespace TrelamiumIII.Core.Structures;

/// <summary>
///     Marks a cell's tile channel as outside the structure, so placing it keeps the world's block. Solid, so
///     furniture can stand on the terrain it stands in for; any placed block replaces it.
/// </summary>
public sealed class StructureVoidTile : ModTile
{
    private const float Opacity = 0.55f;

    public override void SetStaticDefaults()
    {
        Main.tileSolid[Type] = true;

        TileID.Sets.DrawsWalls[Type] = true;
        TileID.Sets.BreakableWhenPlacing[Type] = true;

        MinPick = 0;
        MineResist = 0.1f;
    }

    public override bool CanDrop(int i, int j) => false;

    public override bool CreateDust(int i, int j, ref int type) => false;

    public override bool Slope(int i, int j) => false;

    public override bool PreDraw(int i, int j, SpriteBatch spriteBatch)
    {
        StructureVoidDrawing.Draw(spriteBatch, TextureAssets.Tile[Type].Value, i, j, Opacity);
        return false;
    }
}

/// <summary>Marks a cell's wall channel as outside the structure, so placing it keeps the world's wall.</summary>
public sealed class StructureVoidWall : ModWall
{
    private const float Opacity = 0.45f;

    public override bool Drop(int i, int j, ref int type) => false;

    public override bool CreateDust(int i, int j, ref int type) => false;

    public override bool PreDraw(int i, int j, SpriteBatch spriteBatch)
    {
        StructureVoidDrawing.Draw(spriteBatch, TextureAssets.Wall[Type].Value, i, j, Opacity);
        return false;
    }
}

internal static class StructureVoidDrawing
{
    /// <summary>Draws the texture's top-left 16x16 over tile (<paramref name="i"/>, <paramref name="j"/>) in the tile/wall batch.</summary>
    public static void Draw(SpriteBatch spriteBatch, Texture2D texture, int i, int j, float opacity)
    {
        Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
        Vector2 position = new Vector2(i * 16, j * 16) - Main.screenPosition + offset;

        spriteBatch.Draw(texture, position, new Rectangle(0, 0, 16, 16), Color.White * opacity);
    }
}
