using System.Collections.Generic;

public class OverlayedTileManager : ModSystem
{
    private struct OverlayData
    {
        public OverlayedTile Overlay;
        public float TimeLeft;
        public float MaxDuration;
    }

    private static Dictionary<Point, OverlayData> overlayedTiles = new Dictionary<Point, OverlayData>();

    public override void PostUpdateWorld()
    {
        UpdateOverlayedTiles();
    }

    private void UpdateOverlayedTiles()
    {
        var tilesToRemove = new List<Point>();
        var tilesToUpdate = new List<Point>(overlayedTiles.Keys);

        foreach (Point tilePos in tilesToUpdate)
        {
            OverlayData data = overlayedTiles[tilePos];
            data.TimeLeft -= 1f;

            if (data.TimeLeft <= 0f)
                tilesToRemove.Add(tilePos);
            else
                overlayedTiles[tilePos] = data;
        }

        foreach (Point tilePos in tilesToRemove)
            overlayedTiles.Remove(tilePos);
    }

    /// <summary>
    /// Applies overlay <typeparamref name="T"/> to the tile at (i, j), using that
    /// overlay's own Duration. Replaces whatever overlay was previously there, if any.
    /// </summary>
    public static void AddOverlayedTile<T>(int i, int j) where T : OverlayedTile
    {
        T overlay = ModContent.GetInstance<T>();
        overlayedTiles[new Point(i, j)] = new OverlayData
        {
            Overlay = overlay,
            TimeLeft = overlay.Duration,
            MaxDuration = overlay.Duration
        };
    }

    /// <summary>True if any overlay is active at (i, j).</summary>
    public static bool OverlayActive(int i, int j)
    {
        return overlayedTiles.ContainsKey(new Point(i, j));
    }

    /// <summary>True if <paramref name="overlay"/> specifically is the active one at (i, j).</summary>
    public static bool OverlayActive(OverlayedTile overlay, int i, int j)
    {
        return overlayedTiles.TryGetValue(new Point(i, j), out OverlayData data) && data.Overlay == overlay;
    }

    public static float GetIntensity(int i, int j)
    {
        if (overlayedTiles.TryGetValue(new Point(i, j), out OverlayData data) && data.MaxDuration > 0f)
            return MathHelper.Clamp(data.TimeLeft / data.MaxDuration, 0.2f, 1f);
        return 0f;
    }

    public override void ClearWorld()
    {
        overlayedTiles.Clear();
    }
}
