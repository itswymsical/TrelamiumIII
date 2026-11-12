using Terraria.Audio;
using TrelamiumIII.Common.Subworlds;
using TrelamiumIII.Common.Subworlds.Realms;
using TrelamiumIII.Core.Graphics.Cinematics.Camera;
using TrelamiumIII.Core.Graphics.ScreenEffects;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Content.Ingress.Transition;

/// <summary>
/// Warps the world around the player, then closes the Ingress mist in from the screen edge and hands off to the realm's
/// loading screen, which keeps drawing the same mist.
/// </summary>
internal sealed class IngressEntryCutscene : RealmEntryCutscene
{
    /// <summary>End of the opening warp, in seconds; the mist starts closing here.</summary>
    private const float STIR_END = 1.4f;

    /// <summary>The mist covers the screen and the realm is entered.</summary>
    private const float COVER_END = 4.6f;

    /// <summary><see cref="Core.Graphics.FbmScreen.FbmScreenRenderer.Zoom"/> reached at full cover.</summary>
    private const float MIST_PUSH = 0.9f;

    /// <summary>Camera zoom reached at full cover, as a multiple of the player's own.</summary>
    private const float CAMERA_PUSH = 1.35f;

    private bool pulling;

    public IngressEntryCutscene()
    {
        EnableLetterbox = true;
        LetterboxHeightPercentage = 0.085f;
        LetterboxAnimationDuration = 60;
    }

    protected override SubworldRealm Realm => ModContent.GetInstance<IngressRealm>();

    protected override void OnDepartureStart()
    {
        SoundEngine.PlaySound(Assets.SFX.Cinematic.RiftOpen.Asset with { Volume = 0.55f, Pitch = -0.3f });

        ScreenWobbleSystem.Set(0.2f, 3f, 80);
        ChromaticScreenEffect.Set(0.15f, 3f, 80);
        ZoomHandler.SetZoomAnimation(Main.GameZoomTarget * CAMERA_PUSH, (int)(COVER_END * 60f));
    }

    protected override void UpdateDeparture(float seconds)
    {
        if (!pulling && seconds >= STIR_END)
        {
            pulling = true;

            SoundEngine.PlaySound(Assets.SFX.Cinematic.RiftOpenAlt.Asset with { Volume = 0.5f, Pitch = -0.45f });

            RadialBlurEffect.Set(0.3f, falloff: 1.4f, rSpin: 0.2f, worldFocus: Main.LocalPlayer.Center, rampTicks: 120);
            ScreenWobbleSystem.Set(0.5f, 5f, 150);
            ChromaticScreenEffect.Set(0.45f, 4f, 150);
        }

        Screen.Coverage = EaseFunction.EaseCubicIn.Ease(Ramp(seconds, STIR_END, COVER_END));
        Screen.Zoom = MIST_PUSH * EaseFunction.EaseQuadIn.Ease(Ramp(seconds, 0f, COVER_END));

        if (seconds >= COVER_END)
            Handoff();
    }

    protected override void OnCutsceneEnd()
    {
        RadialBlurEffect.Stop(30);
        ScreenWobbleSystem.Stop(30);
        ChromaticScreenEffect.Stop(30);

        // Settles back over the arrival reveal, since ZoomHandler's state outlives the world.
        ZoomHandler.SetZoomAnimation(Main.GameZoomTarget, 90);
    }

    private static float Ramp(float now, float from, float to) => Math.Clamp((now - from) / MathF.Max(to - from, 1e-4f), 0f, 1f);
}
