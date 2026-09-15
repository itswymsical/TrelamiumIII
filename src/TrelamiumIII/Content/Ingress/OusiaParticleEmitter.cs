namespace TrelamiumIII.Content.Ingress;

/// <summary>
/// Spawns <see cref="OusiaParticle"/>s on the client by sampling random on-screen tiles each tick: Ousia vines emit
/// now and then, and so does open air while the local player is in <see cref="IngressGardenBiome"/>. Doesn't run while
/// paused, so it can't flood the particle budget.
/// </summary>
public sealed class OusiaParticleEmitter : ModSystem
{
    private const int WorldEdge = 10;

    private const int VineSamplesPerTick = 48;
    private const float VineEmitChance = 0.05f;

    private const int AirSamplesPerTick = 1;
    private const float AirEmitChance = 0.4f;

    public override void PostUpdateEverything()
    {
        if (Main.dedServ || Main.gameMenu || VisibleTiles() is not { Width: > 0, Height: > 0 } view)
            return;

        int vineType = ModContent.TileType<OusiaVineTile>();

        for (int i = 0; i < VineSamplesPerTick; i++)
        {
            int x = Main.rand.Next(view.Left, view.Right);
            int y = Main.rand.Next(view.Top, view.Bottom);
            Tile tile = Main.tile[x, y];

            if (tile.HasTile && tile.TileType == vineType && Main.rand.NextFloat() < VineEmitChance)
                OusiaParticle.Spawn(new Vector2(x * 16 + Main.rand.NextFloat(2f, 14f), y * 16 + Main.rand.NextFloat(2f, 14f)));
        }

        if (!Main.LocalPlayer.InModBiome<IngressGardenBiome>())
            return;

        for (int i = 0; i < AirSamplesPerTick; i++)
        {
            int x = Main.rand.Next(view.Left, view.Right);
            int y = Main.rand.Next(view.Top, view.Bottom);
            Tile tile = Main.tile[x, y];

            if ((!tile.HasTile || !Main.tileSolid[tile.TileType]) && tile.LiquidAmount == 0 && Main.rand.NextFloat() < AirEmitChance)
                OusiaParticle.Spawn(new Vector2(x * 16 + Main.rand.NextFloat(16f), y * 16 + Main.rand.NextFloat(16f)));
        }
    }

    /// <summary> Tiles under the zoomed game view, clamped inside the world. </summary>
    private static Rectangle VisibleTiles()
    {
        Vector2 screen = new(Main.screenWidth, Main.screenHeight);
        Vector2 view = screen / Main.GameViewMatrix.Zoom;
        Vector2 topLeft = Main.screenPosition + (screen - view) * 0.5f;

        int left = Math.Max((int)(topLeft.X / 16f), WorldEdge);
        int top = Math.Max((int)(topLeft.Y / 16f), WorldEdge);
        int right = Math.Min((int)((topLeft.X + view.X) / 16f) + 1, Main.maxTilesX - WorldEdge);
        int bottom = Math.Min((int)((topLeft.Y + view.Y) / 16f) + 1, Main.maxTilesY - WorldEdge);

        return new Rectangle(left, top, right - left, bottom - top);
    }
}
