using System.Collections.Generic;
using TrelamiumIII.Core.ExperientialSystem.Skills;

namespace TrelamiumIII.Core.UI.SkillTree;

/// <summary>A skill and where it sits in tree space.</summary>
internal readonly record struct PlacedSkill(Skill Skill, Vector2 Space);

/// <summary>An edge between two placed skills, indices into the layout's node list.</summary>
internal readonly record struct SkillLink(int From, int To);

/// <summary>
/// Resolves every registered skill to a point in tree space and derives the edges between them.
/// Deterministic: the registry's order is stable, so the same set of skills always lays out the same way.
/// </summary>
internal sealed class SkillTreeLayout
{
    /// <summary>Tree-space radius of the innermost Threshold ring.</summary>
    private const float RING_START = 1.6f;

    /// <summary>Radius added per Threshold band outward.</summary>
    private const float RING_STEP = 1.75f;

    /// <summary>Radius wobble applied across a band, so a ring does not read as a perfect circle.</summary>
    private const float RING_JITTER = 0.45f;

    /// <summary>Golden angle. Successive nodes land as far from their predecessors as the circle allows.</summary>
    private const float SPREAD = 2.39996323f;

    /// <summary>Tree-space distance from a parent to its children.</summary>
    private const float CHILD_RADIUS = 1.3f;

    /// <summary>Radians a parent's children are spread across, first to last.</summary>
    private const float CHILD_ARC = 2.4f;

    private readonly List<PlacedSkill> _nodes = [];
    private readonly List<SkillLink> _links = [];

    public IReadOnlyList<PlacedSkill> Nodes => _nodes;

    public IReadOnlyList<SkillLink> Links => _links;

    /// <summary>Half-extent of the laid-out content in tree space, for clamping the pan.</summary>
    public Vector2 Extent { get; private set; }

    public void Build()
    {
        _nodes.Clear();
        _links.Clear();
        Extent = Vector2.Zero;

        Place();
        Connect();
        Measure();
    }

    /// <summary>
    /// Honours an authored <see cref="Skill.Space"/>, otherwise puts the skill on its Threshold's ring at
    /// the next golden angle. The counter runs across the whole registry, so no two rings share a spoke.
    /// Children are placed after every top-level skill, see <see cref="PlaceChildren"/>.
    /// </summary>
    private void Place()
    {
        ReadOnlySpan<int> bands = SkillRegistry.Bands;
        int placed = 0;

        foreach (Skill skill in SkillRegistry.Skills)
        {
            if (skill.ParentID is not null)
                continue;

            if (skill.Space is { } authored)
            {
                _nodes.Add(new PlacedSkill(skill, authored));

                continue;
            }

            int ring = bands.IndexOf(skill.UnlockThreshold);
            float angle = placed * SPREAD;
            float radius = RING_START + Math.Max(ring, 0) * RING_STEP + MathF.Sin(placed * 1.7f) * RING_JITTER;

            _nodes.Add(new PlacedSkill(skill, angle.ToRotationVector2() * radius));

            placed++;
        }

        PlaceChildren();
    }

    /// <summary>Fans each parent's children across an arc on the side of the parent facing away from the origin.</summary>
    private void PlaceChildren()
    {
        int parents = _nodes.Count;

        for (int p = 0; p < parents; p++)
        {
            PlacedSkill parent = _nodes[p];
            IReadOnlyList<Skill> children = SkillRegistry.ChildrenOf(parent.Skill.ID);

            if (children.Count == 0)
                continue;

            float outward = parent.Space == Vector2.Zero ? -MathHelper.PiOver2 : parent.Space.ToRotation();
            float step = children.Count > 1 ? CHILD_ARC / (children.Count - 1) : 0f;
            float first = outward - step * (children.Count - 1) * 0.5f;

            for (int c = 0; c < children.Count; c++)
            {
                Skill child = children[c];
                Vector2 space = child.Space ?? parent.Space + (first + step * c).ToRotationVector2() * CHILD_RADIUS;

                _nodes.Add(new PlacedSkill(child, space));
            }
        }
    }

    /// <summary>
    /// Links every skill to the nearest skill below it in Threshold, so the lines run inward along the
    /// order the tree is actually climbed. The lowest band has nothing to point at and stays unlinked.
    /// A child links to its parent instead, and is never linked to by anything else.
    /// </summary>
    private void Connect()
    {
        for (int i = 0; i < _nodes.Count; i++)
        {
            if (_nodes[i].Skill.ParentID is { } parentId)
            {
                int parent = IndexOf(parentId);

                if (parent >= 0)
                    _links.Add(new SkillLink(i, parent));

                continue;
            }

            int best = -1;
            float bestDistance = float.MaxValue;

            for (int j = 0; j < _nodes.Count; j++)
            {
                if (_nodes[j].Skill.ParentID is not null
                    || _nodes[j].Skill.UnlockThreshold >= _nodes[i].Skill.UnlockThreshold)
                    continue;

                float distance = Vector2.DistanceSquared(_nodes[i].Space, _nodes[j].Space);

                // Ties break on the id so the edge set cannot depend on iteration order.
                if (distance < bestDistance || (distance == bestDistance && best >= 0
                        && string.CompareOrdinal(_nodes[j].Skill.ID, _nodes[best].Skill.ID) < 0))
                {
                    best = j;
                    bestDistance = distance;
                }
            }

            if (best >= 0)
                _links.Add(new SkillLink(i, best));
        }
    }

    private int IndexOf(string id)
    {
        for (int i = 0; i < _nodes.Count; i++)
        {
            if (_nodes[i].Skill.ID == id)
                return i;
        }

        return -1;
    }

    private void Measure()
    {
        var extent = Vector2.Zero;

        foreach (PlacedSkill node in _nodes)
        {
            extent.X = MathF.Max(extent.X, MathF.Abs(node.Space.X));
            extent.Y = MathF.Max(extent.Y, MathF.Abs(node.Space.Y));
        }

        Extent = extent;
    }
}
