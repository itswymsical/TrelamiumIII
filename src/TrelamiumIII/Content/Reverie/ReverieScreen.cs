using TrelamiumIII.Core.Graphics.FbmScreen;
using TrelamiumIII.Core.Graphics.FbmScreen.Objects;

namespace TrelamiumIII.Content.Reverie;

/// <summary>
/// Reverie's transition and loading-screen mist: warm sepia dust behind a white-gold iris edge that grows out of the
/// focus (the altar's slit), with gold motes and paper specks.
/// </summary>
internal static class ReverieScreen
{
    private static readonly Color Gold = new(255, 214, 140);
    private static readonly Color PaleGold = new(255, 244, 214);
    private static readonly Color Paper = new(236, 222, 190);
    private static readonly Color Ink = new(34, 24, 16);

    public static readonly FbmScreenStyle Style = new()
    {
        Scale = 1.2f,
        Warp = 0.8f,
        Swirl = 0.2f,
        SwirlCore = 0.3f,
        Spin = 0.015f,
        Drift = new Vector2(0.004f, -0.01f),
        Density = 0.5f,
        Contrast = 0.2f,
        Bands = 5,

        VoidColor = new Color(18, 12, 8),
        LowColor = new Color(58, 40, 26),
        HighColor = new Color(128, 96, 60),
        GlowColor = Gold,
        Glow = 0.32f,

        Inward = false,
        EdgeNoise = 0.3f,
        EdgeSoft = 0.02f,
        EdgeWidth = 0.06f,
        EdgeColor = new Color(255, 250, 235),

        Vignette = 0.4f,
        PixelSize = 4f,

        BloomIntensity = 0.6f,
        BloomTint = PaleGold,
    };

    public static void Populate(FbmScreenRenderer screen)
    {
        screen.Spawn(new ScreenEmitter(12f, 18, 70,
            (random, frame, _) => GlowMote.Anywhere(random, frame, Gold, PaleGold, maskFloor: 0.3f)));

        screen.Spawn(new ScreenEmitter(30f, 60, 200,
            (random, frame, scattered) => random.NextBool(4)
                ? DriftSpeck.Spiralling(random, frame, scattered, Paper * 0.8f, glow: true, maskFloor: 0.45f)
                : DriftSpeck.Spiralling(random, frame, scattered, Ink * 0.8f, glow: false, maskFloor: 0.2f)));
    }
}
