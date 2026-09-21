using TrelamiumIII.Core.Graphics.FbmScreen;
using TrelamiumIII.Core.Graphics.FbmScreen.Objects;

namespace TrelamiumIII.Content.Ingress.Transition;

/// <summary>Ingress's transition and loading-screen mist: banded teal-green FBM with Ousia motes and peat specks drawn into it.</summary>
internal static class IngressScreen
{
    private static readonly Color OusiaDeep = new(40, 110, 255);
    private static readonly Color OusiaPale = new(120, 210, 255);
    private static readonly Color Peat = new(18, 26, 22);
    private static readonly Color Spore = new(140, 220, 110);

    public static readonly FbmScreenStyle Style = new()
    {
        Scale = 1.4f,
        Warp = 1.1f,
        Swirl = 0.3f,
        SwirlCore = 0.35f,
        Spin = 0.025f,
        Drift = new Vector2(0.006f, 0.012f),
        Density = 0.56f,
        Contrast = 0.2f,
        Bands = 6,

        VoidColor = new Color(5, 11, 12),
        LowColor = new Color(14, 38, 40),
        HighColor = new Color(44, 88, 66),
        GlowColor = OusiaPale,
        Glow = 0.28f,

        EdgeNoise = 0.4f,
        EdgeSoft = 0.015f,
        EdgeWidth = 0.045f,
        EdgeColor = new Color(110, 200, 255),

        Vignette = 0.45f,
        PixelSize = 4f,

        BloomIntensity = 0.5f,
        BloomTint = new Color(170, 230, 255),
    };

    public static void Populate(FbmScreenRenderer screen)
    {
        // Motes show faintly over the world before the mist reaches them; dark specks only once it has.
        screen.Spawn(new ScreenEmitter(10f, 16, 60,
            (random, frame, _) => GlowMote.Anywhere(random, frame, OusiaDeep, OusiaPale, maskFloor: 0.35f)));

        screen.Spawn(new ScreenEmitter(36f, 70, 220,
            (random, frame, scattered) => random.NextBool(6)
                ? DriftSpeck.Spiralling(random, frame, scattered, Spore * 0.7f, glow: true, maskFloor: 0.5f)
                : DriftSpeck.Spiralling(random, frame, scattered, Peat * 0.85f, glow: false, maskFloor: 0.2f)));
    }
}
