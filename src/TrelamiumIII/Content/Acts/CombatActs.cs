using TrelamiumIII.Core.ExperientialSystem.Acts;

namespace TrelamiumIII.Content.Acts;

/// <summary>
/// Killing families of enemies.
///
/// <para>
/// These are group acts over an explicit species list, so their payout scales with how many species the
/// player has met. Farming one slime pool forever advances the ladder but never pays it in full.
/// </para>
///
/// <para>
/// Note that this is <i>on top of</i> the per-kill Exp the combat model already pays. Acts reward the
/// shape of what you have hunted; the matchup model rewards the difficulty of each individual fight.
/// </para>
/// </summary>
public sealed class SlimeAct : NpcKillAct
{
    public override string ID => "act.slimes";
    public override ActCategory Category => ActCategory.Combat;
    public override int[] Thresholds => ActCurves.Combat;

    protected override int[] Ids => ActSets.Slimes;
}

public sealed class UndeadAct : NpcKillAct
{
    public override string ID => "act.undead";
    public override ActCategory Category => ActCategory.Combat;
    public override int[] Thresholds => ActCurves.Combat;

    protected override int[] Ids => ActSets.Undead;
}

public sealed class EyeAct : NpcKillAct
{
    public override string ID => "act.eyeballs";
    public override ActCategory Category => ActCategory.Combat;
    public override int[] Thresholds => ActCurves.Combat;
    protected override int[] Ids => ActSets.Eyes;
}