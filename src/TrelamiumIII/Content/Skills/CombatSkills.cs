using TrelamiumIII.Core.ExperientialSystem.Skills;

namespace TrelamiumIII.Content.Skills;

/// <summary>
/// A critical strike opens a one second window in which every hit crits. Each crit refreshes the window,
/// up to five guaranteed crits per chain. See <see cref="CritChainPlayer"/>.
/// </summary>
public sealed class CritChainSkill : Skill
{
    public const string Id = "skill.critchain";

    public override string ID => Id;

    public override int RequiredThreshold => 15;

    public override float CostFraction => 0.5f;
}

/// <summary>Drives <see cref="CritChainSkill"/>. Hit hooks run only on the attacker's client, so nothing is synced.</summary>
public sealed class CritChainPlayer : ModPlayer
{
    private const int WINDOW = 60;

    private const int MAX_CHAINED = 5;

    private int _window;
    private int _chained;

    public override void Initialize() => _window = _chained = 0;

    public override void PostUpdate()
    {
        if (_window > 0 && --_window == 0)
            _chained = 0;
    }

    /// <summary>Classes that opt out of standard crits (summon, chemical) are left alone.</summary>
    public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
    {
        if (_window > 0 && modifiers.DamageType.UseStandardCritCalcs && Player.GetModPlayer<SkillPlayer>().Has<CritChainSkill>())
            modifiers.SetCrit();
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        if (!hit.Crit || !Player.GetModPlayer<SkillPlayer>().Has<CritChainSkill>())
            return;

        if (_window == 0)
        {
            _chained = 0;
            _window = WINDOW;

            return;
        }

        _chained++;

        EmitChainDust(target, _chained);

        if (_chained >= MAX_CHAINED)
            _window = _chained = 0;
        else
            _window = WINDOW;
    }

    private static void EmitChainDust(NPC target, int chained)
    {
        for (int i = 0; i < 3 + chained * 2; i++)
        {
            Dust dust = Dust.NewDustDirect(target.position, target.width, target.height, DustID.GoldFlame,
                Scale: 0.9f + chained * 0.15f);

            dust.velocity = Main.rand.NextVector2Circular(2.5f, 2.5f) * (0.6f + chained * 0.2f);
            dust.noGravity = true;
        }
    }
}
