using TrelamiumIII.Core.ExperientialSystem.Acts;

namespace TrelamiumIII.Content.Acts;

/// <summary>
/// Killing families of enemies. Payout scales with how many listed species the player has met, on top of the
/// per-kill Exp the combat model already pays.
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
