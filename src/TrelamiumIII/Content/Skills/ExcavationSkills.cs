using TrelamiumIII.Core.ExperientialSystem.Skills;

namespace TrelamiumIII.Content.Skills;

/// <summary>
/// Adds a right click to pickaxes that throws them as returning boomerangs, mining what they pass
/// through. The left click still mines at the cursor. See <see cref="Vanilla.PickaxeBoomerangRework"/>.
/// </summary>
public sealed class PickaxeBoomerangSkill : Skill
{
    public const string Id = "skill.pickaxe.boomerang";

    public override string ID => Id;

    public override int RequiredThreshold => 10;

    public override SkillGroup Group => SkillGroup.Excavation;

    public override float CostFraction => 0.65f;
}

public sealed class BroadswordSkill : Skill
{
    public const string Id = "skill.rBroadsword";

    public override string ID => Id;

    public override int RequiredThreshold => 6;

    public override SkillGroup Group => SkillGroup.Ballistics;

    public override float CostFraction => 0.65f;
}

/// <summary>Mines 14% faster.</summary>
public sealed class MiningSpeedSkill : Skill
{
    public const string Id = "skill.pickaxe.speed";

    /// <summary>Subtracted from <see cref="Player.pickSpeed"/>, the tool use-time multiplier. The Mining Potion subtracts 0.25.</summary>
    private const float MINING_BONUS = 0.14f;

    public override string ID => Id;

    public override int RequiredThreshold => 7;

    public override SkillGroup Group => SkillGroup.Excavation;

    public override void Apply(Player player) => player.pickSpeed -= MINING_BONUS;
}
