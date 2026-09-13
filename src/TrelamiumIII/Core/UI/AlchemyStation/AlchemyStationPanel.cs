using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;

namespace TrelamiumIII.Core.UI.AlchemyStation;

/// <summary>Link segment in panel-local pixels. <see cref="SourceHasMeter" /> reports whether the slot at <see cref="From" /> draws a potency meter.</summary>
internal readonly record struct SlotLink(Vector2 From, Vector2 To, Func<bool>? SourceHasMeter = null);

/// <summary>The basic station's panel. Draws <see cref="Links" /> in <see cref="DrawSelf"/>, above the background and below the slots.</summary>
internal class AlchemyStationPanel : UIPanel
{
    /// <summary>Set once by the station UI after layout.</summary>
    public SlotLink[] Links = [];

    /// <summary>Vertical distance a link's start moves down while its source slot draws a potency meter.</summary>
    public float MeterClearance;

    private static readonly Color LinkColor = new(120, 140, 190);

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        base.DrawSelf(spriteBatch);

        Vector2 origin = GetDimensions().Position();

        foreach (SlotLink link in Links)
        {
            Vector2 from = link.From;
            float drop = link.To.Y - from.Y;

            // Moves the start along the segment so an angled link keeps its direction.
            if (drop > MeterClearance && link.SourceHasMeter?.Invoke() == true)
                from = Vector2.Lerp(from, link.To, MeterClearance / drop);

            DrawLink(spriteBatch, origin + from, origin + link.To);
        }
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
