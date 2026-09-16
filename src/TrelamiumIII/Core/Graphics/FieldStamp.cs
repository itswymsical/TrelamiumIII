namespace TrelamiumIII.Core.Graphics;

/// <summary>
///     The colour convention every accumulated field in this mod is stamped with, and the one place
///     it is written down.
/// </summary>
/// <remarks>
///     <para>
///         A field target sums stamps additively and a shader reads the sum back.
///     </para>
/// </remarks>
public static class FieldStamp
{
    /// <summary>
    ///     How far past its own coverage a stamp counts toward the threshold, applied by the shader
    ///     rather than folded into the stamp.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Overdrive is what makes neighbouring blobs fuse. A node reaching exactly 1.0 at its
    ///         centre and falling off from there never sums past the cut in the gap between it and
    ///         the next one, so the silhouette comes apart into separate circles.
    ///     </para>
    /// </remarks>
    public const float Gain = 1.25f;

    /// <summary>
    ///     Field units between the threshold and a fully shaded interior. The default every layer
    ///     gets unless it sets its own.
    /// </summary>
    public const float DepthRange = 1f;

    /// <summary>
    ///     World brightness at a point, 0..1.
    /// </summary>
    public static float Brightness(Vector2 worldPosition)
    {
        Vector3 light = Lighting.GetSubLight(worldPosition);

        return MathHelper.Clamp(Math.Max(light.X, Math.Max(light.Y, light.Z)), 0f, 1f);
    }

    /// <summary>
    ///     The vertex colour for one stamp: coverage in red, coverage-weighted brightness in green.
    /// </summary>
    /// <param name="coverage">
    ///     What this stamp is worth to the field, before the texture's own falloff, in 0..1. The
    ///     overdrive that makes neighbours fuse is <see cref="Gain" /> and belongs to the shader;
    ///     passing it here is the mistake <see cref="Gain" />'s remarks describe.
    /// </param>
    public static Color Lit(Vector2 worldPosition, float coverage)
        => new Color(1f, Brightness(worldPosition), 0f, 1f) * coverage;

    /// <summary>
    ///     What one stamp is worth to the field, from how much life the node has left.
    /// </summary>
    public static float Weight(float alpha)
    {
        alpha = MathHelper.Clamp(alpha, 0f, 1f);

        return alpha * alpha;
    }

    /// <summary>
    ///     The density target's four channels for one stamp.
    /// </summary>
    /// <param name="weight">From <see cref="Weight" />. Everything else is scaled by it.</param>
    /// <param name="brightness">World light on the node, 0..1.</param>
    /// <param name="temperature">Normalised into 0..1 by the layer's own range.</param>
    /// <param name="age">0 at spawn, 1 at expiry. The complement of the node's alpha.</param>
    public static Color Field(float weight, float brightness, float temperature, float age)
        => new(weight, weight * brightness, weight * temperature, weight * age);

    /// <summary>
    ///     The attribute target's four channels for one stamp.
    /// </summary>
    /// <param name="flow">
    ///     Velocity, already normalised against the layer's reference speed and clamped to -1..1.
    ///     Bias-encoded here rather than by the caller, so there is one place the convention lives.
    /// </param>
    /// <param name="bind">How tightly the node is held to an anchor, 0..1. Free-drifting is 0.</param>
    /// <param name="variant">Layer-defined sub-material, 0..1.</param>
    public static Color Attributes(float weight, Vector2 flow, float bind, float variant)
        => new(
            weight * (flow.X * 0.5f + 0.5f),
            weight * (flow.Y * 0.5f + 0.5f),
            weight * bind,
            weight * variant);

    /// <summary>
    ///     The vertex colour for a stamp that is not standing anywhere in the world: coverage in red,
    ///     brightness pinned open in green.
    /// </summary>
    /// <remarks>
    ///     For fields drawn in interface space, where there is no world position to sample and so no
    ///     light to carry. Passing <see cref="Lit" /> a screen coordinate would sample whatever tile
    ///     happens to sit at those numbers in world space, which is not merely wrong but wrong in a
    ///     way that changes as the player walks.
    /// </remarks>
    public static Color Flat(float coverage) => new Color(1f, 1f, 0f, 1f) * coverage;
}
