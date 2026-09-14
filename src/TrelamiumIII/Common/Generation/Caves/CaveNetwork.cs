using System.Collections.Generic;
using Terraria.Utilities;

namespace TrelamiumIII.Common.Generation.Caves;

/// <summary> Placement rules for <see cref="CaveNetwork.GrowBranch"/>. Distances are in tiles and angles in radians. </summary>
/// <param name="StepMin"> Shortest distance from a node's exit to the next node's entry. </param>
/// <param name="DirectionPull"> Fraction of the drift from the branch's starting heading undone per node. </param>
/// <param name="MaxForkDepth"> How many times forks can fork again; 0 disables forking. </param>
public readonly record struct CaveBranchSettings(
    float StepMin,
    float StepMax,
    float Jitter,
    float DirectionPull,
    float ForkChance,
    float ForkTurnMin,
    float ForkTurnMax,
    int MaxForkDepth);

/// <summary>
/// Cave nodes joined by tunnels, grown as branches out of existing nodes. A new node's bounds must lie inside the
/// placement area, miss every rectangle in <see cref="Reserved"/>, and stay <see cref="Spacing"/> tiles clear of
/// every node except its parent.
/// </summary>
public sealed class CaveNetwork(Rectangle area, UnifiedRandom random)
{
    private const int PlacementAttempts = 7;
    private const float AttemptTurn = 0.35f;

    public readonly List<CaveNode> Nodes = [];
    public readonly List<(CaveNode From, CaveNode To)> Tunnels = [];
    public readonly List<Rectangle> Reserved = [];

    public int Spacing { get; init; }

    /// <summary>
    /// Grows up to <paramref name="length"/> nodes out of <paramref name="parent"/>, heading in screen-space radians
    /// (0 right, pi/2 down). <paramref name="createNode"/> builds a node from its entry position and heading. The
    /// branch stops at the first node that can't be placed; forks get half the remaining length.
    /// </summary>
    public void GrowBranch(CaveNode parent, float heading, int length, CaveBranchSettings settings, Func<Vector2, float, CaveNode> createNode) =>
        GrowBranch(parent, heading, length, settings, createNode, forkDepth: 0);

    private void GrowBranch(CaveNode parent, float direction, int length, CaveBranchSettings settings, Func<Vector2, float, CaveNode> createNode, int forkDepth)
    {
        float heading = direction;

        for (int i = 0; i < length; i++)
        {
            heading += MathHelper.WrapAngle(direction - heading) * settings.DirectionPull + random.NextFloat(-settings.Jitter, settings.Jitter);

            if (Place(parent, ref heading, settings, createNode) is not { } node)
                return;

            Nodes.Add(node);
            Tunnels.Add((parent, node));
            parent = node;

            int remaining = length - i - 1;

            if (forkDepth < settings.MaxForkDepth && remaining > 0 && random.NextFloat() < settings.ForkChance)
            {
                float turn = random.NextFloat(settings.ForkTurnMin, settings.ForkTurnMax) * (random.NextBool(2) ? 1f : -1f);
                GrowBranch(node, heading + turn, (remaining + 1) / 2, settings, createNode, forkDepth + 1);
            }
        }
    }

    /// <summary> Tries <paramref name="heading"/> first, then turns further to alternating sides after each failure. </summary>
    private CaveNode? Place(CaveNode parent, ref float heading, CaveBranchSettings settings, Func<Vector2, float, CaveNode> createNode)
    {
        for (int attempt = 0; attempt < PlacementAttempts; attempt++)
        {
            float candidateHeading = heading + (attempt + 1) / 2 * AttemptTurn * (attempt % 2 == 0 ? -1f : 1f);
            Vector2 entry = parent.Exit + candidateHeading.ToRotationVector2() * random.NextFloat(settings.StepMin, settings.StepMax);
            CaveNode node = createNode(entry, candidateHeading);

            if (Fits(node, parent))
            {
                heading = candidateHeading;
                return node;
            }
        }

        return null;
    }

    private bool Fits(CaveNode node, CaveNode parent)
    {
        Rectangle bounds = node.Bounds;

        if (!area.Contains(bounds))
            return false;

        foreach (Rectangle reserved in Reserved)
        {
            if (reserved.Intersects(bounds))
                return false;
        }

        bounds.Inflate(Spacing, Spacing);

        foreach (CaveNode other in Nodes)
        {
            if (other != parent && other.Bounds.Intersects(bounds))
                return false;
        }

        return true;
    }
}
