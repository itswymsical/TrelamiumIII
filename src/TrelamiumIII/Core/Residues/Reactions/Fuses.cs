using System.Collections.Generic;
using Terraria.DataStructures;
using TrelamiumIII.Core.Residues.Types;

namespace TrelamiumIII.Core.Residues.Reactions;

/// <summary>
///     Tiles of something volatile that have caught and are about to go off. Each lights what's next to it after a moment and
///     blows a while later, so a flame runs along a trail with the blasts coming along behind it.
/// </summary>
/// <remarks>
///     Like the reactions that light them, these only exist on a server or in single player. What comes of them reaches clients
///     the usual way: marks as marks, broken tiles as tile squares, damage as a projectile, and the look as a
///     <see cref="ReactionEffect" />.
/// </remarks>
internal static class Fuses
{
    /// <summary>Ticks from a tile catching to lighting its neighbours. How fast the flame runs.</summary>
    private const int RUN_TICKS = 5;

    /// <summary>Ticks from a tile catching to going off. How far the blasts trail behind the flame.</summary>
    private const int BLOW_TICKS = 40;

    /// <summary>Tiles only go off on a beat, so that those close together can share a blast.</summary>
    private const int BEAT_TICKS = 6;

    /// <summary>Tiles going off in the same square of this many tiles a side share a blast, or a long shoreline would be hundreds of them.</summary>
    private const int BLAST_CELL = 5;

    private const float BLAST_RADIUS = 44f;

    private const float RADIUS_PER_TILE = 6f;

    private const float MAX_BLAST_RADIUS = 100f;

    /// <summary>What an NPC takes. As with any hostile projectile, vanilla makes it at least double that on a player.</summary>
    private const int BLAST_DAMAGE = 30;

    private const float BLAST_KNOCKBACK = 7f;

    private const float SCORCH_INTENSITY = 0.6f;

    /// <summary>A fire with no more heat than this has been put out, and what it was going to set off won't go.</summary>
    private const float DUD_HEAT = 0.05f;

    /// <summary>A cell holding at least this much liquid doesn't count as air beside a tile.</summary>
    private const int FLOODED = 128;

    private struct Fuse
    {
        public Point Tile;

        public int Age;

        public float Strength;

        public int Owner;

        public bool BreaksTile;
    }

    private struct Gathered
    {
        public Vector2 Sum;

        public int Count;

        public float Strength;

        public int Owner;
    }

    private static readonly List<Fuse> lit = [];

    private static readonly List<Fuse> blowing = [];

    private static readonly Dictionary<Point, Gathered> blasts = [];

    private static uint tick;

    /// <summary>Starts a tile off. Its fire is the caller's to light, as it's what shows.</summary>
    /// <param name="strength">How much was there, 0..1.</param>
    /// <param name="breaksTile">Whether the tile goes too when this does.</param>
    public static void Light(Point tile, float strength, int owner, bool breaksTile)
        => lit.Add(new Fuse { Tile = tile, Strength = strength, Owner = owner, BreaksTile = breaksTile });

    internal static void Clear()
    {
        lit.Clear();
        blowing.Clear();
    }

    /// <summary>Called by <see cref="ResidueSystem" /> once reactions are done.</summary>
    internal static void Update()
    {
        if (lit.Count == 0)
            return;

        bool beat = ++tick % BEAT_TICKS == 0;

        for (int i = lit.Count - 1; i >= 0; i--)
        {
            Fuse fuse = lit[i];

            fuse.Age++;

            if (fuse.Age == RUN_TICKS)
                LightAround(in fuse);

            if (beat && fuse.Age >= BLOW_TICKS)
            {
                blowing.Add(fuse);

                lit[i] = lit[^1];
                lit.RemoveAt(lit.Count - 1);

                continue;
            }

            lit[i] = fuse;
        }

        if (blowing.Count > 0)
            BlowAll();
    }

    private static void LightAround(in Fuse fuse)
    {
        for (int x = fuse.Tile.X - 1; x <= fuse.Tile.X + 1; x++)
        {
            for (int y = fuse.Tile.Y - 1; y <= fuse.Tile.Y + 1; y++)
            {
                if (ResidueSystem.HasElement(x, y, ResidueElement.Volatile) && IsExposed(x, y))
                    BurnedResidue.Ignite(x, y, BurnProfile.Fuse, fuse.Strength, fuse.Owner);
            }
        }
    }

    private static void BlowAll()
    {
        blasts.Clear();

        foreach (Fuse fuse in blowing)
        {
            if (!ResidueSystem.TryGet<BurnedResidue>(fuse.Tile.X, fuse.Tile.Y, out ResidueInstance fire) || fire.Level <= DUD_HEAT)
                continue;

            var cell = new Point(fuse.Tile.X / BLAST_CELL, fuse.Tile.Y / BLAST_CELL);

            blasts.TryGetValue(cell, out Gathered blast);

            blast.Sum += fuse.Tile.ToWorldCoordinates();
            blast.Count++;
            blast.Strength = MathF.Max(blast.Strength, fuse.Strength);
            blast.Owner = fuse.Owner;

            blasts[cell] = blast;

            if (fuse.BreaksTile)
                Residue.TryBreakTile(fuse.Tile.X, fuse.Tile.Y);
        }

        foreach (Gathered blast in blasts.Values)
            Blast(in blast);

        blowing.Clear();
    }

    private static void Blast(in Gathered blast)
    {
        Vector2 center = blast.Sum / blast.Count;
        float radius = MathF.Min(BLAST_RADIUS + blast.Count * RADIUS_PER_TILE, MAX_BLAST_RADIUS);
        int damage = (int)(BLAST_DAMAGE * MathHelper.Lerp(0.6f, 1f, blast.Strength));

        ReactionEffects.Blast(center.ToTileCoordinates());

        Projectile.NewProjectile(new EntitySource_Misc("Residue Blast"), center, Vector2.Zero, ModContent.ProjectileType<ResidueBlast>(), damage, BLAST_KNOCKBACK, Main.myPlayer, radius);

        ScorchAround(center, radius, in blast);
    }

    /// <summary>Lights any more of it that's in reach, so a blast can cross a gap, and blackens the rest.</summary>
    private static void ScorchAround(Vector2 center, float radius, in Gathered blast)
    {
        int reach = (int)(radius / 16f);
        Point middle = center.ToTileCoordinates();

        for (int x = middle.X - reach; x <= middle.X + reach; x++)
        {
            for (int y = middle.Y - reach; y <= middle.Y + reach; y++)
            {
                if (Vector2.Distance(new Vector2(x * 16f + 8f, y * 16f + 8f), center) > radius || !IsExposed(x, y))
                    continue;

                if (ResidueSystem.HasElement(x, y, ResidueElement.Volatile))
                    BurnedResidue.Ignite(x, y, BurnProfile.Fuse, blast.Strength, blast.Owner);
                else if (!ResidueSystem.Has<BurnedResidue>(x, y))
                    BurnedResidue.Ignite(x, y, BurnProfile.Scuff, SCORCH_INTENSITY, blast.Owner);
            }
        }
    }

    /// <summary>Whether a tile has air beside it. Fire needs that, so oil in the rock or under a lake only goes once something opens it up.</summary>
    private static bool IsExposed(int x, int y)
    {
        foreach (Point next in (ReadOnlySpan<Point>)[new(x, y - 1), new(x - 1, y), new(x + 1, y), new(x, y + 1)])
        {
            if (WorldGen.InWorld(next.X, next.Y, 1) && !WorldGen.SolidTile(next.X, next.Y) && Main.tile[next.X, next.Y].LiquidAmount < FLOODED)
                return true;
        }

        return false;
    }
}
