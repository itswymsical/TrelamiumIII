using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using static TrelamiumIII.Utilities.EaseFunction;

namespace TrelamiumIII.Core.Graphics.ScreenEffects;

/// <summary>
///     Blows the screen out to white and lets it return through a chromatic haze.
/// </summary>
/// <remarks>
///     <para>
///         Runs as a <see cref="Filters.Scene" /> pass rather than through hand-rolled render
///         targets. And I ain't lazy! <c>FilterManager</c> captures the composed frame into
///         <c>Main.screenTarget</c>, resolving it at a point in <c>DoDraw</c> chosen for that
///         purpose, so a filter never has to bind a target mid-scene.
///         Anything that wants the <em>finished</em> frame should be
///         a filter; only effects that need one layer in isolation, like
///         <see cref="Shockwaves.TileShockwaveSystem" />, justify their own targets.
///     </para>
/// </remarks>
internal sealed class ScreenFlashSystem : ModSystem
{
    private const string Key = $"{nameof(TrelamiumIII)}:ScreenFlash";

    /// <summary>
    ///     Peak channel separation, as a fraction of the half-screen.
    /// </summary>
    /// <remarks>
    ///     Deliberately small. Past roughly 0.06 the fringing stops reading as a lens recovering
    ///     and starts reading as a broken texture, because the red and blue copies separate far
    ///     enough to be legible as three distinct images rather than one aberrated one.
    /// </remarks>
    private const float MaxSpread = 0.045f;

    private static ScreenFlashShaderData? shaderData;

    private static int holdTicks;
    private static int fadeTicks;
    private static int age;
    private static bool running;

    /// <summary>
    ///     Starts (or restarts) a flash on this client.
    /// </summary>
    /// <param name="hold">Frames held at full white before the fade begins.</param>
    /// <param name="fade">Frames spent returning to the untouched frame.</param>
    public static void Flash(int hold = 3, int fade = 48)
    {
        if (Main.dedServ || shaderData is null)
            return;

        // Restarted rather than stacked or queued. Two blowouts landing together should read as
        // one brighter event, and of two competing calls the later one is always the more recent
        // intent, a queue would leave the screen white through both.
        holdTicks = Math.Max(hold, 0);
        fadeTicks = Math.Max(fade, 1);
        age = 0;
        running = true;
    }

    public override void Load()
    {
        if (Main.dedServ)
            return;

        shaderData = new ScreenFlashShaderData(Assets.Shaders.ScreenFlash.Asset, "P0");

        // VeryHigh so the whiteout sits on top of ambient filters.
        // A biome tint applied after a full blowout would put colour back into pixels that are meant to be blown out.
        Filters.Scene[Key] = new Filter(shaderData, EffectPriority.VeryHigh);
        Filters.Scene[Key].Load();
    }

    public override void Unload()
    {
        shaderData = null;
        running = false;
        age = 0;
    }

    public override void OnWorldUnload()
    {
        // Leaving the world mid-flash would otherwise carry an active filter into the menu, where
        // nothing ticks it back down.
        running = false;
        age = 0;

        Deactivate();
    }

    public override void PostUpdateEverything()
    {
        if (Main.dedServ || shaderData is null)
            return;

        if (!running)
        {
            Deactivate();
            return;
        }

        age++;

        if (age >= holdTicks + fadeTicks)
        {
            running = false;
            Deactivate();

            return;
        }

        float flash;
        float haze;

        if (age < holdTicks)
        {
            flash = 1f;
            haze = 1f;
        }
        else
        {
            float progress = (age - holdTicks) / (float)fadeTicks;

            // The two curves are chosen against each other, not individually. White leaves on a
            // quartic (down to a third of its strength in the first quarter of the fade) while the
            // haze leaves on a quadratic that barely moves over the same stretch. That gap is the
            // whole effect: the scene reappears already fringed and then resolves, instead of
            // arriving clean the instant the white is gone.
            flash = 1f - EaseQuarticOut.Ease(progress);
            haze = 1f - EaseQuadIn.Ease(progress);
        }

        var parameters = shaderData.Parameters;
        parameters.uFlash = flash;
        parameters.uHaze = haze;
        parameters.uSpread = MaxSpread;

        if (!Filters.Scene[Key].IsActive())
            Filters.Scene.Activate(Key);
    }

    private static void Deactivate()
    {
        if (shaderData is null)
            return;

        if (Filters.Scene[Key].IsActive())
            Filters.Scene[Key].Deactivate();
    }
}

/// <summary>
///     Carries the generated <c>ScreenFlash</c> parameters on the base type
///     <see cref="Filters.Scene" /> will accept.
/// </summary>
/// <remarks>
///     The SDK's <c>WrapperShaderData</c> cannot be used here: it is sealed, and it derives from
///     <see cref="ShaderData" /> rather than <see cref="ScreenShaderData" />, which is what
///     <see cref="Filter" /> takes. This is the same parameter push it performs, on the right base.
///     <see cref="ScreenShaderData.Apply" /> null-checks every uniform it sets, so the shader is
///     free to declare none of vanilla's, and none of ours collide with the names it looks for.
/// </remarks>
internal sealed class ScreenFlashShaderData(Asset<Effect> shader, string passName) : ScreenShaderData(shader, passName)
{
    // Shadows the primary constructor parameter to avoid CS9107, matching what the generated
    // WrapperShaderData does for the same reason.
    private readonly string passName = passName;

    public Assets.Shaders.ScreenFlash.Parameters Parameters { get; } = new();

    public override void Apply()
    {
        Parameters.Apply(Shader.Parameters, passName);

        base.Apply();
    }
}
