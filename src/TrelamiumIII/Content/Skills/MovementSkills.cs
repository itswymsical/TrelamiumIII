using Terraria.Audio;
using TrelamiumIII.Core.ExperientialSystem.Skills;

namespace TrelamiumIII.Content.Skills;

public sealed class DashSkill : Skill
{
    public const string Id = "skill.dash";

    public override string ID => Id;

    public override int RequiredThreshold => 5;

    public override SkillGroup Group => SkillGroup.Movement;

    public override float CostFraction => 0.5f;
}

public sealed class DoubleJumpSkill : Skill
{
    public const string Id = "skill.doublejump";

    public override string ID => Id;

    public override int RequiredThreshold => 6;

    public override SkillGroup Group => SkillGroup.Movement;

    public override float CostFraction => 0.5f;

    public override void Apply(Player player) => player.GetJumpState<SkillJump>().Enable();
}

/// <summary>The jump <see cref="DoubleJumpSkill"/> grants. Sorted after the bottles, so theirs go first.</summary>
public sealed class SkillJump : ExtraJump
{
    public override Position GetDefaultPosition() => AfterBottleJumps;

    public override float GetDurationMultiplier(Player player) => 0.85f;

    public override void OnStarted(Player player, ref bool playSound)
    {
        for (int i = 0; i < 14; i++)
        {
            Dust dust = Dust.NewDustDirect(player.position, player.width, player.height, DustID.Smoke,
                Scale: Main.rand.NextFloat(0.8f, 1.3f));

            dust.velocity = new Vector2(Main.rand.NextFloat(-1.6f, 1.6f), Main.rand.NextFloat(0.4f, 2.2f) * player.gravDir);
            dust.noGravity = true;
        }
    }
}

/// <summary>
/// Drives <see cref="DashSkill"/>.
/// </summary>
public sealed class SkillDashPlayer : ModPlayer
{
    private const int DASH_RIGHT = 2;
    private const int DASH_LEFT = 3;

    private const int COOLDOWN = 48;
    private const int DURATION = 20;
    private const float VELOCITY = 5.5f;

    /// <summary>-1 when no dash was requested this tick.</summary>
    private int _direction = -1;

    private int _delay;
    private int _timer;

    public bool Dashing => _timer > 0;

    public override void Initialize()
    {
        _direction = -1;
        _delay = _timer = 0;
    }

    public override void ResetEffects()
    {
        if (Player.controlRight && Player.releaseRight
            && Player.doubleTapCardinalTimer[DASH_RIGHT] < 15 && Player.doubleTapCardinalTimer[DASH_LEFT] == 0)
            _direction = DASH_RIGHT;
        else if (Player.controlLeft && Player.releaseLeft
                 && Player.doubleTapCardinalTimer[DASH_LEFT] < 15 && Player.doubleTapCardinalTimer[DASH_RIGHT] == 0)
            _direction = DASH_LEFT;
        else
            _direction = -1;
    }

    public override void PreUpdateMovement()
    {
        if (_delay > 0)
            _delay--;

        if (CanDash() && _direction != -1 && _delay == 0)
            Start();

        if (_timer <= 0)
            return;

        Player.eocDash = _timer;
        Player.armorEffectDrawShadowEOCShield = true;

        _timer--;
    }

    /// <summary>Held skills are synced, so a remote player's dash is read from their own skill set.</summary>
    private bool CanDash()
        => Player.GetModPlayer<SkillPlayer>().Has<DashSkill>()
           && Player.dashType == DashID.None
           && !Player.setSolar
           && !Player.mount.Active
           && !Player.CCed;

    private void Start()
    {
        float sign = _direction == DASH_RIGHT ? 1f : -1f;

        // Already moving faster than the dash would make it, so taking it would be a slowdown.
        if (Player.velocity.X * sign >= VELOCITY)
            return;

        Player.velocity.X = sign * VELOCITY;

        _delay = COOLDOWN;
        _timer = DURATION;

        SoundEngine.PlaySound(SoundID.Run, Player.Center);

        for (int i = 0; i < 10; i++)
        {
            Dust dust = Dust.NewDustDirect(Player.position, Player.width, Player.height, DustID.Smoke,
                Scale: Main.rand.NextFloat(0.9f, 1.4f));

            dust.velocity = new Vector2(-sign * Main.rand.NextFloat(1f, 3f), Main.rand.NextFloat(-0.8f, 0.8f));
            dust.noGravity = true;
        }
    }
}