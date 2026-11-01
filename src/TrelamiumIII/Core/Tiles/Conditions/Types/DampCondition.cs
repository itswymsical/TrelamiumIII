namespace TrelamiumIII.Core.Tiles.Conditions.Types;

/// <summary>
///     Ground dampened by rain: every drop that splashes on a tile darkens it a little more, up to a
///     limit, and it dries off once the drops stop landing. Looks and nothing more.
/// </summary>
/// <remarks>
///     Laid by vanilla's rain particles rather than by a rule. Those exist only on a client and land
///     differently on each, which is fine for as long as this stays cosmetic: a contact effect would
///     need it to move to a <see cref="Rules.TileConditionRule" />.
/// </remarks>
public sealed class DampCondition : FilmCondition
{
    /// <summary>The first drop on a dry tile: just past <see cref="TileFilmLook.Threshold" />, so it reads as a small dab.</summary>
    private const float FIRST_DROP = 0.14f;

    /// <summary>Small next to how fast the tile dries, so a drizzle levels off well short of <see cref="SOAKED" />.</summary>
    private const float PER_DROP = 0.02f;

    private const float SOAKED = 0.5f;

    protected override TileFilmLook Look { get; } = new()
    {
        Threshold = 0.1f,
        NoiseScale = 0.2f,
        NoiseStrength = 0.16f,
        Bands = 3f,
        ShadowColor = new Vector3(0.16f, 0.2f, 0.28f),
        BodyColor = new Vector3(0.12f, 0.15f, 0.22f),
        CoreColor = new Vector3(0.09f, 0.11f, 0.18f),
        BodyAlpha = 0.42f,
        BodyAlphaFloor = 0.55f,
        // The thin band's own colour and alpha: a damp patch has no edge to outline.
        OutlineColor = new Vector3(0.16f, 0.2f, 0.28f),
        OutlineAlpha = 0.23f,
        HighlightColor = new Vector3(0.62f, 0.74f, 0.9f),
        HighlightAlpha = 0.22f,
    };

    public override float Duration => 30f * 60f;

    public override int MaxTiles => 2500;

    public override int FadeInTicks => 20;

    public override int FadeOutTicks => 60;

    public override void Load()
    {
        base.Load();

        if (!Main.dedServ)
            On_Rain.Update += Splash;
    }

    public override void OnLiquidContact(ref TileConditionInstance instance, int liquidType, int amount)
    {
        if (liquidType == LiquidID.Lava)
            instance.Life = 0f;
    }

    /// <summary>Dampens whatever a drop just landed on. Remix worlds rain straight through the terrain, so nothing lands there.</summary>
    private static void Splash(On_Rain.orig_Update orig, Rain self)
    {
        bool falling = self.active;

        orig(self);

        if (!falling || self.active || Main.gameMenu || Main.remixWorld || !LandedOn(self.position, out Point hit))
            return;

        float wetness = TileConditionSystem.TryGet<DampCondition>(hit.X, hit.Y, out TileConditionInstance existing) ? existing.Strength : 0f;

        TileConditionSystem.Apply<DampCondition>(hit.X, hit.Y, MathHelper.Clamp(wetness + PER_DROP, FIRST_DROP, SOAKED), sync: false);
    }

    /// <summary>The solid tile under a drop's 2x2 box, by the test vanilla ended it with. Drops ended by water or the screen's edge have none.</summary>
    private static bool LandedOn(Vector2 position, out Point hit)
    {
        Point from = position.ToTileCoordinates();
        Point to = (position + new Vector2(2f)).ToTileCoordinates();

        for (int x = from.X; x <= to.X; x++)
        {
            for (int y = from.Y; y <= to.Y; y++)
            {
                if (WorldGen.InWorld(x, y, 1) && Main.tile[x, y] is { HasUnactuatedTile: true } tile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType])
                {
                    hit = new Point(x, y);
                    return true;
                }
            }
        }

        hit = default;
        return false;
    }
}
