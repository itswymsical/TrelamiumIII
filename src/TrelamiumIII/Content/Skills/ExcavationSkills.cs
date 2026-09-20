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
