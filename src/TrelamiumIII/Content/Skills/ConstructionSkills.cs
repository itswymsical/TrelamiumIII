using TrelamiumIII.Core.ExperientialSystem.Skills;

namespace TrelamiumIII.Content.Skills;

/// <summary>Places blocks 20% faster.</summary>
/// <remarks>
///     Flat priced rather than a fraction of the meter: it is the opening purchase, and 20 Exp is meant to
///     be affordable the moment skills become available.
/// </remarks>
public sealed class BaselineSkill : Skill
{
    public const string Id = "skill.baseline";

    /// <summary>Added to <see cref="Player.tileSpeed"/>, which vanilla inverts into a use-time multiplier.</summary>
    private const float PLACEMENT_BONUS = 0.2f;

    public override string ID => Id;

    public override int RequiredThreshold => 0;

    public override SkillGroup Group => SkillGroup.Construction;

    public override int Cost => 20;

    public override void Apply(Player player) => player.tileSpeed += PLACEMENT_BONUS;
}
