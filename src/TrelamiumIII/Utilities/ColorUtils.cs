namespace TrelamiumIII.Utilities;

/// <summary>0–255 and hex colour literals as 0–1 <see cref="Vector3" />s, the scale of <see cref="Color.ToVector3" />.</summary>
/// <remarks>Imported globally with <c>using static</c>, so call sites write <c>Rgb(92, 71, 51)</c> or <c>Rgb(0x5C4733)</c>.</remarks>
public static class ColorUtils
{
    public static Vector3 Rgb(byte r, byte g, byte b) => new(r / 255f, g / 255f, b / 255f);

    /// <summary>Takes <c>0xRRGGBB</c>; bits above the low 24 are ignored.</summary>
    public static Vector3 Rgb(uint hex) => Rgb((byte)(hex >> 16), (byte)(hex >> 8), (byte)hex);
}
