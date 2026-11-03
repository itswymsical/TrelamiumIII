using System.Collections.Generic;
using System.Linq;

namespace TrelamiumIII.Core.Residues.Reactions;

/// <summary>One mark on a tile as a reaction sees it: a copy, so change it through <see cref="ResidueSystem" />.</summary>
/// <param name="Potency">How much of its element is here, 0..1. See <see cref="IResidue.PotencyOf" />.</param>
public readonly record struct ResidueReactant(IResidue Residue, ResidueInstance Instance, ResidueElement Elements, float Potency);

/// <summary>Two marks on one tile, sorted into the order the reaction declared its elements in.</summary>
/// <param name="First">Carries <see cref="ResidueReaction.First" />.</param>
/// <param name="Second">Carries <see cref="ResidueReaction.Second" />.</param>
/// <param name="FirstIsParent">Whether <paramref name="First" /> was on the tile before <paramref name="Second" /> arrived.</param>
public readonly record struct ReactionPair(Point Tile, ResidueReactant First, ResidueReactant Second, bool FirstIsParent);

/// <summary>
///     What two elements do when they meet on the same tile.
/// </summary>
/// <remarks>
///     <para>
///         Reactions are discovered like residues are, so a new one is one class: name its two elements and write
///         <see cref="React" />. A residue takes part by answering <see cref="IResidue.ElementsOf" />.
///     </para>
///     <para>
///         A tile's marks are kept in the order they arrived, and the first of them is its parent. Pairs are tried oldest
///         first, so what was there first decides what happens first, and <see cref="FirstWins" /> gives it the edge in a
///         contest: a soaked tile is harder to light than a burning one is to soak.
///     </para>
///     <para>
///         Only a server or a single player runs these, and whatever they lay, spend or weaken goes out to clients from
///         there. A reaction can change the world directly as long as it syncs that itself, as breaking tiles does.
///     </para>
/// </remarks>
public abstract class ResidueReaction
{
    /// <summary>How much harder a parent is to shift than a newcomer of the same strength.</summary>
    private const float PARENT_EDGE = 1.25f;

    public virtual string Name => GetType().Name;

    public abstract ResidueElement First { get; }

    public abstract ResidueElement Second { get; }

    /// <summary>Acts on a pair. Runs outside the batch walk, so changes land immediately.</summary>
    /// <returns>
    ///     Whether anything changed. False means not yet, usually a threshold not met, and the tile is looked at again
    ///     shortly for as long as both marks last.
    /// </returns>
    public abstract bool React(in ReactionPair pair);

    /// <summary>Whether <see cref="ReactionPair.First" /> beats <see cref="ReactionPair.Second" />, with the parent's edge counted. The parent holds a tie.</summary>
    protected static bool FirstWins(in ReactionPair pair)
    {
        float first = pair.First.Potency * (pair.FirstIsParent ? PARENT_EDGE : 1f);
        float second = pair.Second.Potency * (pair.FirstIsParent ? 1f : PARENT_EDGE);

        return first == second ? pair.FirstIsParent : first > second;
    }

    /// <summary>Takes a mark off the tile outright.</summary>
    protected static void Spend(in ReactionPair pair, in ResidueReactant mark)
        => ResidueSystem.Remove(mark.Residue, pair.Tile.X, pair.Tile.Y);

    /// <summary>Takes <paramref name="share" /> of what a mark has left. See <see cref="IResidue.Diminish" />.</summary>
    protected static void Weaken(in ReactionPair pair, in ResidueReactant mark, float share)
        => ResidueSystem.Diminish(mark.Residue, pair.Tile.X, pair.Tile.Y, share);

    /// <summary>Pairs two marks, oldest first, if between them they carry this reaction's elements.</summary>
    internal bool TryPair(Point tile, in ResidueReactant parent, in ResidueReactant child, out ReactionPair pair)
    {
        if ((parent.Elements & First) != 0 && (child.Elements & Second) != 0)
        {
            pair = new ReactionPair(tile, parent, child, true);
            return true;
        }

        if ((parent.Elements & Second) != 0 && (child.Elements & First) != 0)
        {
            pair = new ReactionPair(tile, child, parent, false);
            return true;
        }

        pair = default;
        return false;
    }
}

/// <summary>
///     Runs <see cref="ResidueReaction" />s on the tiles that could need them.
/// </summary>
/// <remarks>
///     Driven by arrivals, not by a sweep: <see cref="Touch" /> names the tile whenever a mark with an element lands, so
///     thousands of idle marks cost nothing here. A tile whose marks are waiting on a threshold stays on a short list
///     and is looked at every <see cref="SIMMER_TICKS" /> until they react or expire.
/// </remarks>
public sealed class ResidueReactionSystem : ModSystem
{
    private const int SIMMER_TICKS = 12;

    /// <summary>Most reactions one tile runs in a tick. Each can lay marks for the next, but a chain never needs many.</summary>
    private const int MAX_PASSES = 6;

    private static readonly List<ResidueReaction> reactions = [];

    private static readonly HashSet<Point> touched = [];

    private static readonly HashSet<Point> simmering = [];

    private static readonly List<Point> work = [];

    /// <summary>The marks on the tile being resolved, oldest first. Index 0 is the parent.</summary>
    private static readonly List<ResidueReactant> stack = [];

    private static uint tick;

    /// <summary>Every registered reaction, for tooling that enumerates them.</summary>
    public static IReadOnlyList<ResidueReaction> Reactions => reactions;

    public override void OnModLoad()
    {
        IEnumerable<Type> discovered = Mod.Code.GetTypes()
            .Where(static type => !type.IsAbstract && type.IsSubclassOf(typeof(ResidueReaction)))
            .OrderBy(static type => type.FullName, StringComparer.Ordinal);

        foreach (Type type in discovered)
            reactions.Add((ResidueReaction)Activator.CreateInstance(type)!);
    }

    public override void OnModUnload()
    {
        reactions.Clear();
        Clear();
    }

    internal static void Clear()
    {
        touched.Clear();
        simmering.Clear();
    }

    /// <summary>Says a mark with an element just landed on <paramref name="tile" />.</summary>
    internal static void Touch(Point tile) => touched.Add(tile);

    /// <summary>Resolves every tile named since the last tick. Called by <see cref="ResidueSystem" /> once the batches are done.</summary>
    internal static void Resolve()
    {
        if (++tick % SIMMER_TICKS == 0 && simmering.Count > 0)
        {
            touched.UnionWith(simmering);
            simmering.Clear();
        }

        if (touched.Count == 0)
            return;

        // Copied out: a reaction's own marks touch the tile again, which is for the next tick.
        work.Clear();
        work.AddRange(touched);
        touched.Clear();

        foreach (Point tile in work)
            ResolveTile(tile);
    }

    private static void ResolveTile(Point tile)
    {
        for (int pass = 0; pass < MAX_PASSES; pass++)
        {
            ResidueSystem.CollectReactants(tile, stack);

            if (stack.Count < 2 || !ReactOnce(tile))
                return;
        }
    }

    /// <summary>Runs the first reaction willing to go, oldest pair first.</summary>
    private static bool ReactOnce(Point tile)
    {
        for (int parent = 0; parent < stack.Count - 1; parent++)
        {
            for (int child = parent + 1; child < stack.Count; child++)
            {
                foreach (ResidueReaction reaction in reactions)
                {
                    if (!reaction.TryPair(tile, stack[parent], stack[child], out ReactionPair pair))
                        continue;

                    if (reaction.React(in pair))
                        return true;

                    simmering.Add(tile);
                }
            }
        }

        return false;
    }
}
