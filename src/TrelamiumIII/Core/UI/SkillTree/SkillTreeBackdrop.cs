using Terraria.GameContent;
using TrelamiumIII.Core.Graphics;

namespace TrelamiumIII.Core.UI.SkillTree;

/// <summary>One held node's contribution to the backdrop's metaball field. Position and radius are screen pixels.</summary>
internal readonly record struct SkillBlob(Vector2 Screen, float Radius, float Strength, Color Color);

/// <summary>
/// One full-rect pass of <c>SkillNebula.fx</c> behind the node field: FBM nebula, starfield, and metaball
/// blobs at the held nodes. Values are starting points tuned against <c>InkStyle</c>'s measured ones.
/// </summary>
internal sealed class SkillTreeBackdrop
{
    /// <summary>Blob slots the shader carries. Nodes past this many are dropped by distance.</summary>
    public const int BLOB_SLOTS = 4;

    private const float SCALE = 1.9f;

    /// <summary>Where the raw FBM sum is recentred. FbmNoise2's median is 0.682; above it holds the field dark.</summary>
    private const float PIVOT = 0.700f;

    private const float EXPAND = 4.6f;
    private const float WARP = 1.10f;
    private const float SWIRL = 0.62f;
    private const float SWIRL_CORE = 0.22f;
    private const float SPIN = 0.015f;
    private const float DENSITY = 0.50f;
    private const float CONTRAST = 0.30f;

    private const float STAR_SCALE = 26f;
    private const float STAR_CUT = 0.90f;
    private const float STAR_POWER = 11f;
    private const float TWINKLE = 0.55f;

    private const float BLOB_FLOOR = 0.55f;
    private const float BLOB_SOFT = 0.24f;

    private const float VIGNETTE = 0.50f;

    /// <summary>Share of the field's pan the nebula scrolls by, so it sits behind the nodes rather than with them.</summary>
    private const float PARALLAX = 0.35f;

    private static readonly Vector3 VoidColor = new(0.035f, 0.032f, 0.062f);
    private static readonly Vector3 NebulaLow = new(0.100f, 0.085f, 0.200f);
    private static readonly Vector3 NebulaHigh = new(0.340f, 0.220f, 0.500f);
    private static readonly Vector3 StarColor = new(0.860f, 0.890f, 1.000f);

    private WrapperShaderData<Assets.Shaders.SkillNebula.Parameters>? _shader;

    /// <summary>Dropped rather than reused, so a resolution change or a reload cannot leave a dead Effect bound.</summary>
    public void Reset() => _shader = null;

    public void Draw(SpriteBatch spriteBatch, Rectangle area, Vector2 pan, float opacity, ReadOnlySpan<SkillBlob> blobs)
    {
        if (area.Width <= 0 || area.Height <= 0 || opacity <= 0f)
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
        parameters.uOpacity = opacity;

        parameters.uVoidColor = VoidColor;
        parameters.uNebulaLow = NebulaLow;
        parameters.uNebulaHigh = NebulaHigh;
        parameters.uStarColor = StarColor;

        // Tiles, so it is bound through the shader's own wrap sampler: SpriteBatch owns s0 and would
        // clamp the field into a smear at the edges.
        parameters.uNoiseTex = Assets.Textures.Noise.FbmNoise2.Asset.Value;
        parameters.NoiseSampler = new HlslSampler2D
        {
            Texture = Assets.Textures.Noise.FbmNoise2.Asset.Value,
            Sampler = SamplerState.LinearWrap,
        };

        SetBlobs(parameters, area, blobs);

        // Immediate, because the pass has to be bound between Begin and the draw that uses it.
        SpriteBatchState restore = SpriteBatchState.Capture(spriteBatch);

        spriteBatch.End();
        spriteBatch.Begin(SpriteSortMode.Immediate, restore.BlendState, restore.SamplerState,
            restore.DepthStencilState, restore.RasterizerState, null, restore.Transform);

        _shader.Apply();

        // Null source rectangle: TEXCOORD0 has to span the whole texture for uv to map across the rect.
        spriteBatch.Draw(TextureAssets.MagicPixel.Value, area, Color.White);

        spriteBatch.End();
        restore.Begin(spriteBatch);
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
