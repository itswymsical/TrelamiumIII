using System.Reflection;
using ReLogic.Content;
using ReLogic.Graphics;
using TrelamiumIII.Core.Rarities.Text;

namespace TrelamiumIII.Core.Rarities;

/// <summary>Hooks a flair overrides, so passes skip flairs that draw nothing on a surface.</summary>
[Flags]
internal enum FlairHooks
{
    None = 0,
    UnderName = 1,
    OverName = 2,
    UnderSprite = 4,
    OverSprite = 8,
}

/// <summary>
///     One visual behaviour of a <see cref="RarityStyle" />: on the item name or any text drawn through <see cref="RarityText" />,
///     and on the item sprite in a slot or the world. Every hook is a no-op by default. Flairs are shared by every item of their
///     style, so any state they keep is shared.
/// </summary>
public abstract class RarityFlair
{
    private FlairHooks? hooks;

    /// <summary>Blend the renderer opens for this flair's under and over draws. Consecutive flairs with the same blend share a batch.</summary>
    public virtual BlendState Blend { get; set; } = BlendState.Additive;

    /// <summary>Whether this flair draws the text glyphs itself. Only the first such flair in a style runs.</summary>
    public virtual bool PaintsName => false;

    /// <summary>
    ///     Whether the renderer leaves the batch in the caller's state instead of opening <see cref="Blend" />. The flair may
    ///     restart it but must hand it back in that state, as <see cref="PaintName" /> always must.
    /// </summary>
    public virtual bool ManagesOwnBatch => false;

    /// <summary>Drawn behind the text.</summary>
    public virtual void UnderName(in RarityNameContext ctx) { }

    /// <summary>Draws the text glyphs. Requires <see cref="PaintsName" />.</summary>
    public virtual void PaintName(in RarityNameContext ctx) { }

    /// <summary>Drawn in front of the text.</summary>
    public virtual void OverName(in RarityNameContext ctx) { }

    /// <summary>Drawn behind the item sprite, in a slot or in the world.</summary>
    public virtual void UnderSprite(in RaritySpriteContext ctx) { }

    /// <summary>Drawn in front of the item sprite, in a slot or in the world.</summary>
    public virtual void OverSprite(in RaritySpriteContext ctx) { }

    /// <summary>
    ///     An effect bound while the game draws the item sprite itself, keeping animation frames and world bobbing. The batch
    ///     binds the whole sheet, so <c>uv</c> spans the sheet rather than the frame. Null for none.
    /// </summary>
    public virtual Asset<Effect>? SpriteShader => null;

    /// <summary>Fills in the effect's parameters for this draw. Requires <see cref="SpriteShader" />.</summary>
    public virtual void ApplySpriteShader(Effect effect, in RaritySpriteContext ctx) { }

    /// <summary>Per-tick hook for an item lying in the world.</summary>
    public virtual void UpdateWorldItem(Item item) { }

    /// <summary>Releases anything the flair allocated. Called once, on mod unload.</summary>
    public virtual void Unload() { }

    internal FlairHooks Hooks => hooks ??= FindHooks(GetType());

    private static FlairHooks FindHooks(Type type)
    {
        var found = FlairHooks.None;

        if (Overrides(type, nameof(UnderName), typeof(RarityNameContext)))
            found |= FlairHooks.UnderName;

        if (Overrides(type, nameof(OverName), typeof(RarityNameContext)))
            found |= FlairHooks.OverName;

        if (Overrides(type, nameof(UnderSprite), typeof(RaritySpriteContext)))
            found |= FlairHooks.UnderSprite;

        if (Overrides(type, nameof(OverSprite), typeof(RaritySpriteContext)))
            found |= FlairHooks.OverSprite;

        return found;
    }

    private static bool Overrides(Type type, string name, Type context)
        => type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public, [context.MakeByRefType()])?.DeclaringType != typeof(RarityFlair);
}

/// <summary>What a flair is given when it decorates text, in the drawing batch's space (interface space for tooltips).</summary>
public readonly struct RarityNameContext(
    SpriteBatch spriteBatch,
    RarityStyle style,
    DynamicSpriteFont font,
    string text,
    Vector2 position,
    Vector2 scale,
    float rotation,
    Vector2 origin,
    Color color,
    object key,
    Item? item)
{
    public readonly SpriteBatch SpriteBatch = spriteBatch;

    public readonly RarityStyle Style = style;

    public readonly DynamicSpriteFont Font = font;

    public readonly string Text = text;

    /// <summary>Per-glyph placement of <see cref="Text" />, with ink bounds and baseline.</summary>
    public readonly GlyphLayout Layout = GlyphLayout.For(font, text);

    /// <summary>The draw position, as passed to <c>DrawString</c>.</summary>
    public readonly Vector2 Position = position;

    public readonly Vector2 Scale = scale;

    public readonly float Rotation = rotation;

    public readonly Vector2 Origin = origin;

    /// <summary>The colour the text resolved to, alpha included.</summary>
    public readonly Color Color = color;

    /// <summary>Identifies this text to fields that keep per-text content.</summary>
    public readonly object Key = key;

    /// <summary>The item the text belongs to, or null.</summary>
    public readonly Item? Item = item;

    /// <summary>Top-left of the line box.</summary>
    public Vector2 TopLeft => Position - Origin * Scale;

    /// <summary>The line box, scale applied.</summary>
    public Vector2 Size => Layout.Size * Scale;

    public Vector2 Center => TopLeft + Size * 0.5f;

    public Rectangle Bounds => new((int)TopLeft.X, (int)TopLeft.Y, (int)Size.X, (int)Size.Y);

    /// <summary>Top-left of the glyph ink. Ignores <see cref="Rotation" />.</summary>
    public Vector2 InkMin => TopLeft + Layout.InkMin * Scale;

    /// <summary>Bottom-right of the glyph ink. Ignores <see cref="Rotation" />.</summary>
    public Vector2 InkMax => TopLeft + Layout.InkMax * Scale;

    /// <summary>Y of the first line's baseline.</summary>
    public float BaselineY => TopLeft.Y + Layout.Baseline * Scale.Y;

    /// <summary>Seconds, wrapping hourly.</summary>
    public float Time => Main.GlobalTimeWrappedHourly;
}

/// <summary>What a flair is given when it decorates the item sprite, in the active batch's space (a slot's or the game view's).</summary>
public readonly struct RaritySpriteContext(
    Item item,
    SpriteBatch spriteBatch,
    RarityStyle style,
    Vector2 center,
    Vector2 spriteSize,
    Color drawColor,
    float rotation,
    float scale,
    bool inWorld)
{
    public readonly Item Item = item;

    public readonly SpriteBatch SpriteBatch = spriteBatch;

    public readonly RarityStyle Style = style;

    /// <summary>Where the sprite's centre lands.</summary>
    public readonly Vector2 Center = center;

    /// <summary>The drawn size of the sprite's frame, scale included.</summary>
    public readonly Vector2 SpriteSize = spriteSize;

    public readonly Color DrawColor = drawColor;

    public readonly float Rotation = rotation;

    public readonly float Scale = scale;

    /// <summary>True for a dropped item, false for an inventory slot.</summary>
    public readonly bool InWorld = inWorld;

    /// <summary>Seconds, wrapping hourly.</summary>
    public float Time => Main.GlobalTimeWrappedHourly;

    /// <summary>The transform of the batch the sprite is drawn in.</summary>
    public Matrix Matrix => SpriteBatch.transformMatrix;
}
