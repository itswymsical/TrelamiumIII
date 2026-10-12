using Terraria.GameContent.Drawing;

namespace TrelamiumIII.Core.Tiles;

/// <summary>Wind counters for <see cref="TileDrawing.GetWindCycle" />, advanced at the rates of vanilla's private ones.</summary>
/// <remarks>Credit: Spirit Reforged's TileSwaySystem.</remarks>
internal sealed class TileWindSystem : ModSystem
{
    public static double TreeCounter { get; private set; }

    public static double GrassCounter { get; private set; }

    public override void Load() => On_TileDrawing.Update += Advance;

    private static void Advance(On_TileDrawing.orig_Update orig, TileDrawing self)
    {
        orig(self);

        if (Main.dedServ)
            return;

        double wind = Utils.GetLerpValue(0.08f, 1.2f, Math.Abs(Main.WindForVisuals), clamped: true);

        TreeCounter += (1.0 + wind * 2.0) / 240.0;
        GrassCounter += (1.0 + wind * 4.0) / 180.0;
    }
}
