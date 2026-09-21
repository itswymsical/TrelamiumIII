using SubworldLibrary;
using TrelamiumIII.Core.Graphics.Cinematics;
using TrelamiumIII.Core.Graphics.FbmScreen;

namespace TrelamiumIII.Common.Subworlds;

/// <summary>
/// A cutscene that ends by entering <see cref="Realm"/>. Subclasses animate <see cref="Screen"/> and call
/// <see cref="Handoff"/> once it covers the screen; skipping covers it over <see cref="RushSeconds"/> and hands off.
/// </summary>
internal abstract class RealmEntryCutscene : Cutscene
{
    private const float RushSeconds = 0.35f;

    private bool rushing;
    private bool handedOff;

    protected abstract SubworldRealm Realm { get; }

    /// <summary>The realm's renderer, reset to zero coverage when the cutscene starts.</summary>
    protected FbmScreenRenderer Screen { get; private set; } = null!;

    protected sealed override void OnCutsceneStart()
    {
        if (RealmTransition.BeginDeparture(Realm) is not { } screen)
        {
            // Nothing to cover the screen with, so there is nothing to play.
            handedOff = true;
            SubworldSystem.Enter(Realm.FullName);

            return;
        }

        Screen = screen;

        OnDepartureStart();
    }

    protected sealed override void OnCutsceneUpdate(GameTime gameTime)
    {
        if (handedOff)
            return;

        if (!rushing)
        {
            UpdateDeparture(ElapsedSeconds);

            return;
        }

        Screen.Coverage = MathF.Min(Screen.Coverage + (float)gameTime.ElapsedGameTime.TotalSeconds / RushSeconds, 1f);

        if (Screen.Coverage >= 1f)
            Handoff();
    }

    protected virtual void OnDepartureStart()
    {
    }

    /// <param name="seconds">Seconds since the cutscene started.</param>
    protected abstract void UpdateDeparture(float seconds);

    protected override void OnSkipTriggered() => rushing = true;

    protected void Handoff()
    {
        if (handedOff)
            return;

        handedOff = true;

        RealmTransition.Handoff();
    }

    public override bool IsFinished() => handedOff;
}
