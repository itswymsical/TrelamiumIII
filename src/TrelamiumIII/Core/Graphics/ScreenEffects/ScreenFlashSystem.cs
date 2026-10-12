using ReLogic.Content;
using Terraria.Graphics.Effects;
using static TrelamiumIII.Utilities.EaseFunction;

namespace TrelamiumIII.Core.Graphics.ScreenEffects;

/// <summary>Blows the screen out to white, then returns through a chromatic haze.</summary>
internal sealed class ScreenFlashSystem : ScreenFilterSystem<ScreenFlashSystem, Assets.Shaders.ScreenFlash.Parameters>
{
    /// <summary>Peak channel separation as a fraction of the half-screen. Past about 0.06 the copies read as separate images.</summary>
    private const float MaxSpread = 0.045f;

    private int hold;
    private int fade;
    private int age;
    private bool running;

    protected override Asset<Effect> Shader => Assets.Shaders.ScreenFlash.Asset;

    /// <summary>Above ambient filters, so nothing tints the blown-out pixels.</summary>
    protected override EffectPriority Priority => EffectPriority.VeryHigh;

    protected override bool Active => running;

    /// <summary>Starts or restarts a flash: <paramref name="hold" /> frames of white, then <paramref name="fade" /> frames back.</summary>
    public static void Flash(int hold = 3, int fade = 48)
    {
        if (Main.dedServ)
            return;

        ScreenFlashSystem self = Instance;

        self.hold = Math.Max(hold, 0);
        self.fade = Math.Max(fade, 1);
        self.age = 0;
        self.running = true;
    }

    protected override void Cut()
    {
        running = false;
        age = 0;
        base.Cut();
    }

    protected override void Tick()
    {
        if (running && ++age >= hold + fade)
            running = false;
    }

    protected override bool Push(Assets.Shaders.ScreenFlash.Parameters parameters)
    {
        float flash = 1f;
        float haze = 1f;

        if (age >= hold)
        {
            float progress = (age - hold) / (float)fade;

            // White leaves on a quartic and the haze on a quadratic, so the scene returns fringed and then resolves.
            flash = 1f - EaseQuarticOut.Ease(progress);
            haze = 1f - EaseQuadIn.Ease(progress);
        }

        parameters.uFlash = flash;
        parameters.uHaze = haze;
        parameters.uSpread = MaxSpread;

        return true;
    }
}
