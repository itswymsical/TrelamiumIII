namespace TrelamiumIII.Core.Graphics.FbmScreen;

/// <summary>An <see cref="FbmScreenRenderer"/>'s state for one step or draw. Positions are target pixels.</summary>
internal readonly record struct ScreenFrame(int Width, int Height, float Time, Vector2 Focus, float Coverage, FbmScreenStyle Style)
{
    /// <summary>Target height over 1080, for lengths authored at 1080p.</summary>
    public float Scale => Height / 1080f;

    public Vector2 Size => new(Width, Height);

    public bool OnScreen(Vector2 position, float margin)
        => position.X >= -margin && position.X <= Width + margin && position.Y >= -margin && position.Y <= Height + margin;

    /// <summary>
    /// The iris mask at <paramref name="position"/>, 0 clear to 1 covered. Matches <c>ScreenFbm.fx</c> without its
    /// noise on the edge, so the feather is widened by that noise's reach instead.
    /// </summary>
    public float MaskAt(Vector2 position)
    {
        if (Width <= 0 || Height <= 0)
            return 0f;

        Vector2 aspect = new(Width / (float)Height, 1f);
        Vector2 focus = Focus / Size;
        Vector2 far = Vector2.Max(focus, Vector2.One - focus) * aspect;

        float radius = ((position / Size - focus) * aspect).Length() / MathF.Max(far.Length(), 1e-4f);
        float t = Style.Inward ? 1f - radius : radius;

        float margin = 0.5f * Style.EdgeNoise + Style.EdgeSoft + Style.EdgeWidth;
        float front = MathHelper.Lerp(-margin, 1f + margin, Coverage);
        float soft = MathF.Max(Style.EdgeSoft + 0.5f * Style.EdgeNoise, 1e-4f);

        // Hermite, as HLSL's smoothstep; Terraria's Utils.SmoothStep is a linear clamp.
        float x = Math.Clamp((t - (front - soft)) / (2f * soft), 0f, 1f);

        return 1f - x * x * (3f - 2f * x);
    }
}
