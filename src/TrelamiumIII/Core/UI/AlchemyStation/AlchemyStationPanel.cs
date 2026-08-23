using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;

namespace TrelamiumIII.Core.UI.AlchemyStation;

/// <summary>
/// The basic station's frame. Its one job beyond being a panel is drawing the linkage between the
/// slots: the solvent dropping into the solute row, the solutes chained across it, then the outer
/// two funnelling down into the solution. Drawn in <see cref="DrawSelf"/>, so it lands on the
/// panel background but under the slots that sit on top of it.
/// </summary>
internal class AlchemyStationPanel : UIPanel
{
    /// <summary>Line segments in panel-local pixels. Set once by the station UI after layout.</summary>
    public (Vector2 From, Vector2 To)[] Links = System.Array.Empty<(Vector2, Vector2)>();

    private static readonly Color LinkColor = new(120, 140, 190);

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        base.DrawSelf(spriteBatch);

        Vector2 origin = GetDimensions().Position();
        foreach (var (from, to) in Links)
            DrawLink(spriteBatch, origin + from, origin + to);
    }

    /// <summary>
    /// A straight line of arbitrary angle, stretched and rotated out of a single pixel.
    /// <para>The 1x1 source rectangle is not optional. MagicPixel is bigger than one pixel, so with
    /// a null source the scale below would multiply by the whole texture and smear these short
    /// segments clear across the screen.</para>
    /// </summary>
    private static void DrawLink(SpriteBatch sb, Vector2 from, Vector2 to, float thickness = 2f)
    {
        Vector2 delta = to - from;
        float length = delta.Length();
        if (length < 0.5f) return;

        // Origin on the source pixel's left edge, vertically centred, so the line grows along
        // from -> to and its thickness straddles that axis.
        sb.Draw(TextureAssets.MagicPixel.Value, from, new Rectangle(0, 0, 1, 1), LinkColor,
            delta.ToRotation(), new Vector2(0f, 0.5f), new Vector2(length, thickness),
            SpriteEffects.None, 0f);
    }
}
