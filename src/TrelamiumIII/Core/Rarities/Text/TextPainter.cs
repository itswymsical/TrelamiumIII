using ReLogic.Graphics;
using Terraria.UI.Chat;
using TrelamiumIII.Core.Graphics;

namespace TrelamiumIII.Core.Rarities.Text;

/// <summary>Per-glyph draw state, filled in by a painter before <see cref="TextPainter" /> draws.</summary>
public struct GlyphDraw
{
    /// <summary>Added to the glyph's position, in the batch's pixels.</summary>
    public Vector2 Offset;

    /// <summary>Multiplies the text scale, about the glyph's centre.</summary>
    public float Scale;

    /// <summary>Radians about the glyph's centre, on top of the text's rotation.</summary>
    public float Rotation;

    /// <summary>Body colour. The outline uses its alpha.</summary>
    public Color Color;
}

/// <summary>Sets an effect's parameters for one glyph, after the painter's uImageSize0, uSourceRect and uGlyphPosition.</summary>
public delegate void GlyphParameters(Effect effect, in TextGlyph glyph, in GlyphDraw draw);

/// <summary>Draws text as an outline pass then a body pass, each in its <see cref="TextStyle" /> blend, and restores the caller's batch.</summary>
public static class TextPainter
{
    private static GlyphDraw[] buffer = new GlyphDraw[64];

    /// <summary>A shared per-glyph buffer for <paramref name="layout" />, reset to <paramref name="color" />. Draw before preparing again.</summary>
    public static Span<GlyphDraw> Prepare(GlyphLayout layout, Color color)
    {
        int count = layout.Glyphs.Length;

        if (buffer.Length < count)
            buffer = new GlyphDraw[Math.Max(count, buffer.Length * 2)];

        Span<GlyphDraw> draws = buffer.AsSpan(0, count);
        draws.Fill(new GlyphDraw { Scale = 1f, Color = color });

        return draws;
    }

    /// <summary>Draws the context's text with per-glyph <paramref name="draws" />.</summary>
    public static void Draw(in RarityNameContext ctx, ReadOnlySpan<GlyphDraw> draws, TextStyle style, Effect? bodyEffect = null,
        GlyphParameters? parameters = null)
        => Draw(ctx.SpriteBatch, ctx.Layout, ctx.Position, ctx.Scale, ctx.Rotation, ctx.Origin, draws, style, bodyEffect, parameters);

    /// <summary>
    ///     Draws <paramref name="layout" /> with per-glyph <paramref name="draws" /> into the active batch. With
    ///     <paramref name="bodyEffect" /> the body pass runs immediate, so per-glyph parameters apply to each glyph.
    /// </summary>
    public static void Draw(SpriteBatch spriteBatch, GlyphLayout layout, Vector2 position, Vector2 scale, float rotation, Vector2 origin,
        ReadOnlySpan<GlyphDraw> draws, TextStyle style, Effect? bodyEffect = null, GlyphParameters? parameters = null)
    {
        TextGlyph[] glyphs = layout.Glyphs;
        int count = Math.Min(glyphs.Length, draws.Length);

        if (count == 0)
            return;

        // The transform DynamicSpriteFont.InternalDraw builds: origin, then rotation, about the draw position.
        Matrix transform = Matrix.CreateTranslation(-origin.X * scale.X, -origin.Y * scale.Y, 0f) * Matrix.CreateRotationZ(rotation);

        SpriteBatchState home = SpriteBatchState.Capture(spriteBatch);
        SpriteBatchState current = home;

        if (style.Outline is { } outline)
        {
            Span<Vector2> offsets = stackalloc Vector2[TextOutline.MAX_OFFSETS];
            int offsetCount = outline.WriteOffsets(offsets);

            RarityBatch.Switch(spriteBatch, ref current, home.With(style.OutlinePassBlend));

            for (int i = 0; i < count; i++)
            {
                ref readonly TextGlyph glyph = ref glyphs[i];
                ref readonly GlyphDraw draw = ref draws[i];

                Vector2 center = GlyphCenter(glyph, draw, position, scale, transform);
                Color color = outline.Color * (draw.Color.A / 255f);

                for (int k = 0; k < offsetCount; k++)
                    DrawGlyph(spriteBatch, glyph, draw, center + offsets[k], color, scale, rotation);
            }
        }

        RarityBatch.Switch(spriteBatch, ref current, bodyEffect is null
            ? home.With(style.BodyPassBlend)
            : home.With(style.BodyPassBlend, SpriteSortMode.Immediate, bodyEffect));

        for (int i = 0; i < count; i++)
        {
            ref readonly TextGlyph glyph = ref glyphs[i];
            ref readonly GlyphDraw draw = ref draws[i];

            if (bodyEffect is not null)
            {
                SetGlyphParameters(bodyEffect, glyph);
                parameters?.Invoke(bodyEffect, glyph, draw);
            }

            DrawGlyph(spriteBatch, glyph, draw, GlyphCenter(glyph, draw, position, scale, transform), draw.Color, scale, rotation);
        }

        RarityBatch.Switch(spriteBatch, ref current, home);
    }

    /// <summary>
    ///     Draws a string as a vanilla tooltip line would, colour codes included, with <paramref name="style" />'s outline and blends.
    ///     A vanilla style goes through vanilla's own call.
    /// </summary>
    public static void DrawPlain(SpriteBatch spriteBatch, DynamicSpriteFont font, string text, Vector2 position, Color color, float rotation,
        Vector2 origin, Vector2 scale, TextStyle style)
    {
        if (style.IsVanilla)
        {
            ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, text, position, color, rotation, origin, scale);
            return;
        }

        var parsed = ChatManager.ParseMessage(text, color);
        ChatManager.ConvertNormalSnippets(parsed);
        TextSnippet[] snippets = parsed.ToArray();

        SpriteBatchState home = SpriteBatchState.Capture(spriteBatch);
        SpriteBatchState current = home;

        if (style.Outline is { } outline)
        {
            Span<Vector2> offsets = stackalloc Vector2[TextOutline.MAX_OFFSETS];
            int offsetCount = outline.WriteOffsets(offsets);
            Color outlineColor = outline.Color * (color.A / 255f);

            RarityBatch.Switch(spriteBatch, ref current, home.With(style.OutlinePassBlend));

            for (int k = 0; k < offsetCount; k++)
            {
                ChatManager.DrawColorCodedString(spriteBatch, font, snippets, position + offsets[k], outlineColor, rotation, origin, scale,
                    out _, -1f, ignoreColors: true);
            }
        }

        RarityBatch.Switch(spriteBatch, ref current, home.With(style.BodyPassBlend));
        ChatManager.DrawColorCodedString(spriteBatch, font, snippets, position, Color.White, rotation, origin, scale, out _, -1f);
        RarityBatch.Switch(spriteBatch, ref current, home);
    }

    private static Vector2 GlyphCenter(in TextGlyph glyph, in GlyphDraw draw, Vector2 position, Vector2 scale, in Matrix transform)
        => Vector2.Transform(glyph.Center * scale, transform) + position + draw.Offset;

    private static void DrawGlyph(SpriteBatch spriteBatch, in TextGlyph glyph, in GlyphDraw draw, Vector2 center, Color color, Vector2 scale,
        float rotation)
    {
        spriteBatch.Draw(glyph.Texture, center, glyph.Source, color, rotation + draw.Rotation, glyph.Size * 0.5f, scale * draw.Scale,
            SpriteEffects.None, 0f);
    }

    private static void SetGlyphParameters(Effect effect, in TextGlyph glyph)
    {
        effect.Parameters["uImageSize0"]?.SetValue(new Vector2(glyph.Texture.Width, glyph.Texture.Height));
        effect.Parameters["uSourceRect"]?.SetValue(new Vector4(glyph.Source.X, glyph.Source.Y, glyph.Source.Width, glyph.Source.Height));
        effect.Parameters["uGlyphPosition"]?.SetValue(glyph.Position);
    }
}
