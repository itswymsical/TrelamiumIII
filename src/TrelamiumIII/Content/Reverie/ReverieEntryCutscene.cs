using Terraria.Audio;
using TrelamiumIII.Common.Subworlds;
using TrelamiumIII.Common.Subworlds.Realms;
using TrelamiumIII.Core.Graphics.Cinematics.Camera;
using TrelamiumIII.Core.Graphics.ScreenEffects;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Content.Reverie;

/// <summary>
/// Grows Reverie's mist out of the transition's focus (the scar, which widens on its own while a departure focuses on
/// it) until it covers the screen, then hands off to the realm's loading screen.
/// </summary>
internal sealed class ReverieEntryCutscene : RealmEntryCutscene
{
    /// <summary>The mist starts growing out of the focus, in seconds.</summary>
    private const float SPILL_START = 0.6f;

    /// <summary>The mist covers the screen and the realm is entered.</summary>
    private const float COVER_END = 3.6f;

    /// <summary><see cref="Core.Graphics.FbmScreen.FbmScreenRenderer.Zoom"/> reached at full cover.</summary>
    private const float MIST_PUSH = 0.6f;

    /// <summary>Camera zoom reached at full cover, as a multiple of the player's own.</summary>
    private const float CAMERA_PUSH = 1.25f;

    public ReverieEntryCutscene()
    {
        EnableLetterbox = true;
        LetterboxHeightPercentage = 0.085f;
        LetterboxAnimationDuration = 60;
    }

    protected override SubworldRealm Realm => ModContent.GetInstance<ReverieRealm>();

    protected override void OnDepartureStart()
    {
        SoundEngine.PlaySound(Assets.SFX.RiftOpen.Asset with { Volume = 0.55f, Pitch = 0.35f });

        ChromaticScreenEffect.Set(0.12f, 3f, 60);
        ZoomHandler.SetZoomAnimation(Main.GameZoomTarget * CAMERA_PUSH, (int)(COVER_END * 60f));
    }

    protected override void UpdateDeparture(float seconds)
    {
        Screen.Coverage = EaseFunction.EaseCubicIn.Ease(Ramp(seconds, SPILL_START, COVER_END));
        Screen.Zoom = MIST_PUSH * EaseFunction.EaseQuadIn.Ease(Ramp(seconds, 0f, COVER_END));

        if (seconds >= COVER_END)
            Handoff();
    }

    protected override void OnCutsceneEnd()
    {
        ChromaticScreenEffect.Stop(30);

        // Settles back over the arrival reveal, since ZoomHandler's state outlives the world.
        ZoomHandler.SetZoomAnimation(Main.GameZoomTarget, 90);
    }

    private static float Ramp(float now, float from, float to) => Math.Clamp((now - from) / MathF.Max(to - from, 1e-4f), 0f, 1f);
}
