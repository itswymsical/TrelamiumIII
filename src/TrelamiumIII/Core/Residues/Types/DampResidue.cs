using TrelamiumIII.Core.Residues.Reactions;

namespace TrelamiumIII.Core.Residues.Types;

/// <summary>
///     Ground dampened by rain: every drop that splashes on a tile darkens it a little more, up to a
///     limit, and it dries off once the drops stop landing. Looks and nothing more, until it's soaked through.
/// </summary>
/// <remarks>
///     <para>
///         On a client, laid by vanilla's rain particles as they land, which are different on each. A server has no particles, so
///         <see cref="Rules.RainRule" /> works out the same thing from the weather. None of this is shared, which is fine because
///         until a tile is soaked through it's only a look.
///     </para>
///     <para>
///         Soaked ground puts fires out, and only a server runs reactions, so its own wet ground is the one that counts. What
///         comes of that reaches clients like any other.
///     </para>
/// </remarks>
public sealed class DampResidue : FilmResidue
{
    /// <summary>The first drop on a dry tile: just past <see cref="FilmLook.Threshold" />, so it reads as a small dab.</summary>
    private const float FIRST_DROP = 0.14f;

    /// <summary>Small next to how fast the tile dries, so a drizzle levels off well short of <see cref="SOAKED" />.</summary>
    private const float PER_DROP = 0.02f;

    /// <summary>Drops each surface tile catches per tick at full rain, as measured from vanilla's particles. They scale evenly with <see cref="Main.cloudAlpha" />.</summary>
    private const float DROPS_PER_TICK = 0.09f;

    private const float SOAKED = 0.5f;

    /// <summary>The same coat a pool leaves on its banks, in water's colour. Only its cut is lower, so a single drop still shows as a dab, and its erosion with it, so the noise can't draw anything on a dry tile.</summary>
    protected override FilmLook Look { get; } = LiquidResidue.WetLook with
    {
        Threshold = 0.1f,
        NoiseStrength = 0.16f,
    };

    public override float Duration => 30f * 60f;

    public override int MaxTiles => 2500;

    public override int FadeInTicks => 20;

    public override int FadeOutTicks => 60;

    /// <summary>Soaked through, which is short of <see cref="SOAKED" /> because the tile is always drying a little between drops.</summary>
    private const float SOAKED_THROUGH = 0.4f;

    public override ResidueElement ElementsOf(in ResidueInstance instance)
        => instance.Strength >= SOAKED_THROUGH ? ResidueElement.Water : ResidueElement.None;

    public override float PotencyOf(in ResidueInstance instance) => MathF.Min(instance.Strength / SOAKED, 1f);

    public override void Load()
    {
        base.Load();

        if (!Main.dedServ)
            On_Rain.Update += Splash;
    }

    public override void OnLiquidContact(ref ResidueInstance instance, int liquidType, int amount)
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

        Wet(hit.X, hit.Y, PER_DROP);
    }

    /// <summary>What falls on a tile over <paramref name="ticks" /> of the current rain, for a machine with no particles to do it.</summary>
    public static void RainOn(int i, int j, int ticks) => Wet(i, j, PER_DROP * DROPS_PER_TICK * Main.cloudAlpha * ticks);

    private static void Wet(int i, int j, float added)
    {
        float wetness = ResidueSystem.TryGet<DampResidue>(i, j, out ResidueInstance existing) ? existing.Strength : 0f;

        ResidueSystem.Apply<DampResidue>(i, j, MathHelper.Clamp(wetness + added, FIRST_DROP, SOAKED), origin: ResidueOrigin.Local, tint: LiquidResidue.TintOf(i, j, LiquidID.Water));
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
