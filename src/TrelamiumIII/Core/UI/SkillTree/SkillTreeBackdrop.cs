using Terraria.GameContent;

namespace TrelamiumIII.Core.UI.SkillTree;

/// <summary>One held node's contribution to the backdrop's metaball field. Position and radius are UI pixels.</summary>
internal readonly record struct SkillBlob(Vector2 Screen, float Radius, float Strength, Color Color);

/// <summary>
/// One full-rect pass of <c>SkillNebula.fx</c>: FBM haze, starfield, and metaball blobs at the held nodes.
/// Drawn into <see cref="SkillTreeRenderer"/>'s target, never straight to the screen.
/// </summary>
internal sealed class SkillTreeBackdrop
{
    /// <summary>Blob slots the shader carries. Nodes past this many are dropped by distance.</summary>
    public const int BLOB_SLOTS = 4;

    private const float SCALE = 1f;

    /// <summary>
    /// Where the raw FBM sum is recentred. FbmNoise2's median is 0.682, so sitting just under it puts the
    /// median pixel inside the body's feather band: haze almost everywhere, rather than billows in a void.
    /// </summary>
    private const float PIVOT = 0.57f;

    private const float EXPAND = 1.2f;
    private const float WARP = 0.85f;

    /// <summary>Low, so the field drifts rather than winding into a vortex.</summary>
    private const float SWIRL = 0.18f;

    private const float SWIRL_CORE = 0.45f;
    private const float SPIN = 0.008f;
    private const float DENSITY = 0.535f;

    /// <summary>
    /// Half-width of the body's feather band. Set so the band spans the whole stretched histogram: every
    /// pixel then sits somewhere on the ramp, which is what makes the field read as mist and not coastline.
    /// </summary>
    private const float CONTRAST = 0.26f;

    private const float STAR_SCALE = 40f;
    private const float STAR_CUT = 0.93f;
    private const float STAR_POWER = 5f;
    private const float TWINKLE = 0.55f;

    private const float BLOB_FLOOR = 0.55f;
    private const float BLOB_SOFT = 0.18f;

    private const float VIGNETTE = 0.42f;

    /// <summary>Share of the field's pan the nebula scrolls by, so it sits behind the nodes rather than with them.</summary>
    private const float PARALLAX = 0.35f;

    // The Experiential Meter's own ramp, dark to highlight: 07316 5, 024AA2, 0F77D0, 44B1F2, AAECFF.
    private static readonly Vector3 VoidColor = new(0.016f, 0.035f, 0.078f);
    private static readonly Vector3 NebulaLow = new(0.031f, 0.114f, 0.243f);
    private static readonly Vector3 NebulaHigh = new(0.086f, 0.255f, 0.500f);
    private static readonly Vector3 StarColor = new(0.667f, 0.925f, 1.000f);

    private WrapperShaderData<Assets.Shaders.SkillNebula.Parameters>? _shader;

    /// <summary>Dropped rather than reused, so a resolution change or a reload cannot leave a dead Effect bound.</summary>
    public void Reset() => _shader = null;

    /// <summary>
    /// Fills <paramref name="target"/> on the currently bound surface. <paramref name="area"/> is the field's
    /// rect in UI pixels, which the blobs' positions are measured against.
    /// </summary>
    public void Draw(SpriteBatch spriteBatch, Rectangle target, Rectangle area, Vector2 pan, ReadOnlySpan<SkillBlob> blobs)
    {
        if (target.Width <= 0 || target.Height <= 0 || area.Height <= 0)
            return;

        _shader ??= Assets.Shaders.SkillNebula.CreateP0();

        Assets.Shaders.SkillNebula.Parameters parameters = _shader.Parameters;

        parameters.uTime = Main.GlobalTimeWrappedHourly;
        parameters.uAspect = new Vector2(area.Width / (float)area.Height, 1f);
        parameters.uPan = -pan / area.Height * PARALLAX;

        parameters.uScale = SCALE;
        parameters.uPivot = PIVOT;
        parameters.uExpand = EXPAND;
        parameters.uWarp = WARP;
        parameters.uSwirl = SWIRL;
        parameters.uSwirlCore = SWIRL_CORE;
        parameters.uSpin = SPIN;
        parameters.uDensity = DENSITY;
        parameters.uContrast = CONTRAST;

        parameters.uStarScale = STAR_SCALE;
        parameters.uStarCut = STAR_CUT;
        parameters.uStarPower = STAR_POWER;
        parameters.uTwinkle = TWINKLE;

        parameters.uBlobFloor = BLOB_FLOOR;
        parameters.uBlobSoft = BLOB_SOFT;

        parameters.uVignette = VIGNETTE;

        // Opaque here; the panel's open/close fade is applied when the target is blitted to the screen.
        parameters.uOpacity = 1f;

        parameters.uVoidColor = VoidColor;
        parameters.uNebulaLow = NebulaLow;
        parameters.uNebulaHigh = NebulaHigh;
        parameters.uStarColor = StarColor;

        // Tiles, so it is bound through the shader's own wrap sampler: SpriteBatch owns s0 and would
        // clamp the field into a smear at the edges.
        parameters.uNoiseTex = Assets.Textures.Noise.VoronoiNoise.Asset.Value;
        parameters.NoiseSampler = new HlslSampler2D
        {
            Texture = Assets.Textures.Noise.VoronoiNoise.Asset.Value,
            Sampler = SamplerState.LinearWrap,
        };

        SetBlobs(parameters, area, blobs);

        // Immediate, because the pass has to be bound between Begin and the draw that uses it.
        spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.LinearClamp,
            DepthStencilState.None, Main.Rasterizer);

        _shader.Apply();

        // Null source rectangle: TEXCOORD0 has to span the whole texture for uv to map across the rect.
        spriteBatch.Draw(TextureAssets.MagicPixel.Value, target, Color.White);

        spriteBatch.End();
    }

    /// <summary>Converts blobs to the shader's aspect space, where one unit is <paramref name="area"/>'s height.</summary>
    private static void SetBlobs(Assets.Shaders.SkillNebula.Parameters parameters, Rectangle area, ReadOnlySpan<SkillBlob> blobs)
    {
        for (int slot = 0; slot < BLOB_SLOTS; slot++)
        {
            Vector4 blob = Vector4.Zero;
            Vector3 color = Vector3.Zero;

            if (slot < blobs.Length && blobs[slot].Strength > 0f)
            {
                SkillBlob source = blobs[slot];

                blob = new Vector4(
                    (source.Screen.X - area.Center.X) / area.Height,
                    (source.Screen.Y - area.Center.Y) / area.Height,
                    source.Radius / area.Height,
                    source.Strength);

                color = source.Color.ToVector3();
            }

            switch (slot)
            {
                case 0:
                    parameters.uBlob0 = blob;
                    parameters.uBlobColor0 = color;
                    break;
                case 1:
                    parameters.uBlob1 = blob;
                    parameters.uBlobColor1 = color;
                    break;
                case 2:
                    parameters.uBlob2 = blob;
                    parameters.uBlobColor2 = color;
                    break;
                default:
                    parameters.uBlob3 = blob;
                    parameters.uBlobColor3 = color;
                    break;
            }
        }
    }
}
