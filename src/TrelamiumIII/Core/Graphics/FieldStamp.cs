namespace TrelamiumIII.Core.Graphics;

/// <summary>
///     The colour convention every accumulated field in this mod is stamped with, and the one place
///     it is written down.
/// </summary>
/// <remarks>
///     <para>
///         A field target sums stamps additively and a shader reads the sum back. Historically the
///         stamps were white, so all four channels held the same number and the shader read
///         whichever one it felt like -- which meant the field could say how much of something was
///         on a pixel and nothing else. Every layer therefore had to be told how lit the world was
///         by a uniform, and a uniform is one value for the whole screen: a cloud spanning a lit
///         room and a dark shaft got the average of the two, so in practice every layer was floored
///         near fullbright and none of them respected world lighting at all.
///     </para>
///     <para>
///         The convention here spends a second channel to fix that. <b>Red</b> is coverage, exactly
///         what the white stamp used to put in every channel, so nothing downstream of density
///         changes. <b>Green</b> is that same coverage scaled by how lit the stamp is, so
///         green ÷ red is the coverage-weighted average brightness of everything drawn on that
///         pixel -- per pixel, from the field itself, at no extra draw.
///     </para>
///     <para>
///         Brightness rather than colour, because there is only one channel going spare. Coverage
///         claims one, and alpha cannot hold either half: the stamp textures disagree about where
///         they keep their falloff -- <c>RadialGradient</c> and <c>BloomFlare</c> are fully opaque
///         and carry it in RGB, <c>Bloom</c> is white and carries it in alpha -- and the product
///         that the source-alpha blend forms is the only reading that works for both. Two channels
///         for a three-channel quantity means dropping the hue of the light, which is not the part
///         that was wrong.
///     </para>
/// </remarks>
public static class FieldStamp
{
    /// <summary>
    ///     World brightness at a point, 0..1.
    /// </summary>
    /// <remarks>
    ///     The brightest channel, not a luminance dot product. Under a red torch, luminance
    ///     weighting calls a lit room thirty percent lit and takes the effect in it nearly black,
    ///     which is a worse answer than the fullbright one this replaced. The question being asked
    ///     is "is there light on this spot", and the strongest channel answers it.
    /// </remarks>
    public static float Brightness(Vector2 worldPosition)
    {
        Vector3 light = Lighting.GetSubLight(worldPosition);

        return MathHelper.Clamp(Math.Max(light.X, Math.Max(light.Y, light.Z)), 0f, 1f);
    }

    /// <summary>
    ///     The vertex colour for one stamp: coverage in red, coverage-weighted brightness in green.
    /// </summary>
    /// <param name="coverage">
    ///     What this stamp is worth to the field, before the texture's own falloff. Values past 1
    ///     are normal and intended -- a blob that only just reaches full at its centre never sums
    ///     past the shader's threshold with its neighbours, and so never fuses.
    /// </param>
    public static Color Lit(Vector2 worldPosition, float coverage)
        => new Color(1f, Brightness(worldPosition), 0f, 1f) * coverage;
}
