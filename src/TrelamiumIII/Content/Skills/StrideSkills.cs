using System.Collections.Generic;
using System.Linq;
using TrelamiumIII.Core.ExperientialSystem.Skills;

namespace TrelamiumIII.Content.Skills;

/// <summary>Tile families <see cref="StrideSkill"/> can be tuned to.</summary>
public enum StrideTerrain
{
    Stone,
    Sand,
    Earth,
    Frost
}

/// <summary>Running boots move 15% faster on one terrain, chosen through its <see cref="StrideTerrainSkill"/> children.</summary>
public sealed class StrideSkill : Skill
{
    public const string Id = "skill.stride";

    public const int THRESHOLD = 8;

    public override string ID => Id;

    public override int RequiredThreshold => THRESHOLD;

    public override SkillGroup Group => SkillGroup.Movement;

    public override float CostFraction => 0.45f;
}

/// <summary>One terrain choice under <see cref="StrideSkill"/>. Free, and swaps out whichever sibling is held.</summary>
public sealed class StrideTerrainSkill(StrideTerrain terrain) : Skill
{
    public StrideTerrain Terrain { get; } = terrain;

    public override string ID => IdFor(Terrain);

    public override int RequiredThreshold => StrideSkill.THRESHOLD;

    public override SkillGroup Group => SkillGroup.Movement;

    public override string ParentID => StrideSkill.Id;

    public override bool ExclusiveWithSiblings => true;

    public override int Cost => 0;

    public static string IdFor(StrideTerrain terrain) => $"{StrideSkill.Id}.{terrain.ToString().ToLowerInvariant()}";

    public static bool Matches(StrideTerrain terrain, int type) => terrain switch
    {
        StrideTerrain.Stone => TileID.Sets.Conversion.Stone[type] || TileID.Sets.Conversion.Moss[type]
            || TileID.Sets.Ore[type] || type is TileID.Granite or TileID.Marble,
        StrideTerrain.Sand => TileID.Sets.Conversion.Sand[type] || TileID.Sets.Conversion.Sandstone[type]
            || TileID.Sets.Conversion.HardenedSand[type],
        StrideTerrain.Earth => TileID.Sets.Dirt[type] || TileID.Sets.Mud[type] || TileID.Sets.Conversion.Grass[type]
            || TileID.Sets.Conversion.JungleGrass[type] || TileID.Sets.Conversion.MushroomGrass[type]
            || type is TileID.ClayBlock or TileID.Ash,
        StrideTerrain.Frost => TileID.Sets.IcesSnow[type] || TileID.Sets.Conversion.Ice[type]
            || TileID.Sets.Conversion.Snow[type] || type == TileID.Slush,
        _ => false
    };
}

public sealed class StrideTerrainSkills : ISkillProvider
{
    public IEnumerable<Skill> Provide() => Enum.GetValues<StrideTerrain>().Select(static t => new StrideTerrainSkill(t));
}

/// <summary>Drives <see cref="StrideSkill"/>.</summary>
public sealed class StridePlayer : ModPlayer
{
    private const float BONUS = 1.15f;

    /// <summary>Vanilla resets <see cref="Player.accRunSpeed"/> to this before equips; running boots raise it to 6 or more.</summary>
    private const float BASE_ACC_RUN_SPEED = 3f;

    /// <summary>Ticks the last floor is remembered after leaving the ground, so a hop keeps the bonus.</summary>
    private const int AIRBORNE_GRACE = 20;

    private static readonly StrideTerrain[] Terrains = Enum.GetValues<StrideTerrain>();

    private bool _boots;
    private int _floorType = -1;
    private int _grace;

    public override void PostUpdateEquips() => _boots = Player.accRunSpeed > BASE_ACC_RUN_SPEED;

    public override void PostUpdateRunSpeeds()
    {
        TrackFloor();

        if (!_boots || Player.mount.Active || _floorType < 0 || Chosen() is not { } terrain)
            return;

        if (!StrideTerrainSkill.Matches(terrain, _floorType))
            return;

        Player.maxRunSpeed *= BONUS;
        Player.accRunSpeed *= BONUS;
        Player.runAcceleration *= BONUS;
    }

    /// <summary>Same floor sample as <see cref="Player.FloorVisuals"/>.</summary>
    private void TrackFloor()
    {
        if (Player.velocity.Y != 0f || Player.grappling[0] != -1)
        {
            if (_grace > 0 && --_grace == 0)
                _floorType = -1;

            return;
        }

        int x = (int)((Player.position.X + Player.width * 0.5f) / 16f);
        int y = Player.gravDir == -1f
            ? (int)(Player.position.Y - 0.1f) / 16
            : (int)((Player.position.Y + Player.height) / 16f);

        _floorType = WorldGen.InWorld(x, y, 1) ? Player.GetFloorTileType(x, y) : -1;
        _grace = AIRBORNE_GRACE;
    }

    private StrideTerrain? Chosen()
    {
        var skills = Player.GetModPlayer<SkillPlayer>();

        if (!skills.Has<StrideSkill>())
            return null;

        foreach (StrideTerrain terrain in Terrains)
        {
            if (skills.Has(StrideTerrainSkill.IdFor(terrain)))
                return terrain;
        }

        return null;
    }
}
