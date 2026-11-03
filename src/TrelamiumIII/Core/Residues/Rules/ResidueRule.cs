using System.Collections.Generic;
using System.Linq;

namespace TrelamiumIII.Core.Residues.Rules;

/// <summary>
///     Lays a residue on the world wherever, and whenever, it belongs: frost in a blizzard, stains
///     where liquid sits.
/// </summary>
/// <remarks>
///     <para>
///         The world's counterpart to a weapon or an NPC applying a residue. Rules are discovered
///         like residues are, so a new one is one file: say when it runs in <see cref="IsActive" />
///         and what it does to a tile in <see cref="Visit" />.
///     </para>
///     <para>
///         Every machine sweeps the tiles around its own view -- a server, around every player -- a
///         band of rows a tick, so each tile is visited once every <see cref="SweepTicks" />. Nothing
///         is synced, which means a rule has to decide from world state alone: tile types, liquid,
///         biome, weather, <see cref="Hash" />. Never <c>Main.rand</c>, or every machine lands on
///         different tiles.
///     </para>
///     <para>
///         Residues stay transient. A rule that should hold for as long as something lasts, like
///         frost through a blizzard, keeps what is in view topped up with <see cref="Maintain{T}" />;
///         what it leaves behind expires and is laid again on the way back. That is what lets it cover
///         the world without a byte of save data or a budget the size of the world.
///     </para>
/// </remarks>
public abstract class ResidueRule
{
    public virtual string Name => GetType().Name;

    /// <summary>Ticks to sweep the whole area once. Lower reacts faster and costs more.</summary>
    public virtual int SweepTicks => 30;

    /// <summary>Whether the rule runs around <paramref name="player" /> this tick: a biome, the weather, a time of day.</summary>
    public virtual bool IsActive(Player player) => true;

    /// <summary>Called for each tile of the sweep. Lay or refresh the residue here if the tile qualifies.</summary>
    public abstract void Visit(int i, int j, Tile tile);

    public virtual void Load() { }

    public virtual void Unload() { }

    /// <summary>
    ///     Lays <typeparamref name="T" /> on the tile, or tops it up once it is past half its life.
    /// </summary>
    /// <remarks>
    ///     Leaving a fresh mark alone is what keeps a rule cheap: a tile in view is reapplied every
    ///     half life rather than every sweep, and its fade-in is not restarted each time.
    /// </remarks>
    protected static bool Maintain<T>(int i, int j, float intensity = 1f, float duration = -1f, byte variant = 0, Color? tint = null) where T : class, IResidue
    {
        if (ResidueSystem.TryGet<T>(i, j, out ResidueInstance existing) && existing.Fade > 0.5f)
            return true;

        return ResidueSystem.Apply<T>(i, j, intensity, duration, origin: ResidueOrigin.Local, variant: variant, tint: tint);
    }

    /// <summary>
    ///     A stable 0..1 value per tile, the same on every machine and every visit. Use it wherever a
    ///     rule wants only some tiles, or uneven intensity.
    /// </summary>
    /// <param name="salt">Varies the pattern, so two rules hashing the same tile do not pick the same ones.</param>
    protected static float Hash(int i, int j, int salt = 0)
    {
        uint h = (uint)i * 374761393u + (uint)j * 668265263u + (uint)salt * 2246822519u;
        h = (h ^ (h >> 13)) * 1274126177u;
        h ^= h >> 16;

        return (h & 0xFFFFFF) / (float)0x1000000;
    }

    protected static bool IsSolid(int i, int j)
        => WorldGen.InWorld(i, j) && WorldGen.SolidTile(i, j);

    /// <summary>Whether the tile is solid with open air directly above it.</summary>
    protected static bool IsTopSurface(int i, int j)
        => IsSolid(i, j) && IsAir(i, j - 1);

    /// <summary>
    ///     Whether nothing solid stands over this tile for <paramref name="reach" /> tiles, so weather
    ///     falling from the sky would land on it. Walls do not count: vanilla rain falls past them.
    /// </summary>
    protected static bool IsSkyExposed(int i, int j, int reach = 48)
    {
        for (int y = j - 1; y >= j - reach; y--)
        {
            if (y < 0)
                return true;

            if (WorldGen.SolidTile(i, y) || Main.tile[i, y].LiquidAmount > 0)
                return false;
        }

        return true;
    }

    private static bool IsAir(int i, int j)
        => WorldGen.InWorld(i, j) && !WorldGen.SolidTile(i, j) && Main.tile[i, j].LiquidAmount == 0;
}

/// <summary>Discovers every <see cref="ResidueRule" /> and sweeps them around the players each tick.</summary>
public sealed class ResidueRuleSystem : ModSystem
{
    /// <summary>Tiles past the unzoomed screen still swept, so a rule's work is in place before it scrolls into view.</summary>
    private const int VIEW_PADDING = 16;

    /// <summary>Half the area swept around a player with no screen to measure: a server's view of a client.</summary>
    private static readonly Point HeadlessHalfExtent = new(1920 / 32 + VIEW_PADDING, 1080 / 32 + VIEW_PADDING);

    private static readonly List<ResidueRule> rules = [];

    private static uint tick;

    /// <summary>Every registered rule, for tooling that enumerates them.</summary>
    public static IReadOnlyList<ResidueRule> Rules => rules;

    public override void OnModLoad()
    {
        IEnumerable<Type> discovered = Mod.Code.GetTypes()
            .Where(static type => !type.IsAbstract && type.IsSubclassOf(typeof(ResidueRule)))
            .OrderBy(static type => type.FullName, StringComparer.Ordinal);

        foreach (Type type in discovered)
        {
            var rule = (ResidueRule)Activator.CreateInstance(type)!;
            rule.Load();
            rules.Add(rule);
        }
    }

    public override void OnModUnload()
    {
        foreach (ResidueRule rule in rules)
            rule.Unload();

        rules.Clear();
    }

    public override void PostUpdateEverything()
    {
        if (Main.gameMenu || rules.Count == 0)
            return;

        tick++;

        if (Main.netMode != NetmodeID.Server)
        {
            Sweep(Main.LocalPlayer, ScreenArea());
            return;
        }

        foreach (Player player in Main.ActivePlayers)
            Sweep(player, AreaAround(player.Center, HeadlessHalfExtent));
    }

    private static void Sweep(Player player, Rectangle area)
    {
        if (!player.active || area.IsEmpty)
            return;

        foreach (ResidueRule rule in rules)
        {
            if (!rule.IsActive(player))
                continue;

            // Rows are dealt out by their absolute index rather than their place in the area, so the
            // band lands on the same world rows however the area has moved since the last tick.
            int period = Math.Max(rule.SweepTicks, 1);
            int phase = (int)(tick % (uint)period);
            int first = area.Top + ((phase - area.Top % period) % period + period) % period;

            for (int j = first; j < area.Bottom; j += period)
            {
                for (int i = area.Left; i < area.Right; i++)
                    rule.Visit(i, j, Main.tile[i, j]);
            }
        }
    }

    /// <summary>The unzoomed screen in tiles, padded. A superset of the view at every zoom Terraria allows.</summary>
    private static Rectangle ScreenArea()
    {
        Vector2 center = Main.screenPosition + new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;

        return AreaAround(center, new Point(Main.screenWidth / 32 + VIEW_PADDING, Main.screenHeight / 32 + VIEW_PADDING));
    }

    private static Rectangle AreaAround(Vector2 center, Point halfExtent)
    {
        int centerX = (int)(center.X / 16f);
        int centerY = (int)(center.Y / 16f);

        // One tile in from the edge, so a rule may always look at its neighbours.
        int left = Math.Max(centerX - halfExtent.X, 1);
        int top = Math.Max(centerY - halfExtent.Y, 1);
        int right = Math.Min(centerX + halfExtent.X, Main.maxTilesX - 1);
        int bottom = Math.Min(centerY + halfExtent.Y, Main.maxTilesY - 1);

        return right <= left || bottom <= top ? Rectangle.Empty : new Rectangle(left, top, right - left, bottom - top);
    }
}
