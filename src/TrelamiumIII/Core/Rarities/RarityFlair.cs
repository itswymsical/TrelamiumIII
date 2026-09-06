using ReLogic.Content;

namespace TrelamiumIII.Core.Rarities;

/// <summary>
///     One visual behaviour a rarity can wear, drawn on whichever of the item's surfaces it cares
///     about.
/// </summary>
/// <remarks>
///     <para>
///         An item shows up in three places that look alike and are not: the tooltip name line and
///         the inventory slot are both interface space under <see cref="Main.UIScaleMatrix" />, while
///         a dropped item is world space under <see cref="Main.GameViewMatrix" />; and a metaball
///         field has to be accumulated a frame ahead of any of them. A flair exists so that "embers
///         come off this" is written once and the renderer decides where it lands, instead of every
///         rarity re-deriving three sets of coordinates and a batch dance.
///     </para>
///     <para>
///         Every hook is a no-op by default, so a flair overrides only the surfaces it has an opinion
///         about. A text treatment implements the name hooks and ignores the sprite; a shader
///         implements <see cref="SpriteShader" /> and ignores the text.
///     </para>
///     <para>
///         Flairs are singletons shared by every item of their rarity (see <see cref="RarityStyle" />),
///         so any state they keep is shared state. That is correct for an emitter -- one field, fed
///         from wherever the item happens to be shown -- and wrong for anything meant to be per-item,
///         which belongs on an item component instead.
///     </para>
/// </remarks>
public abstract class RarityFlair
{
    /// <summary>
    ///     The blend the renderer opens for this flair's draws. Consecutive flairs asking for the
    ///     same blend share one batch.
    /// </summary>
    public virtual BlendState Blend => BlendState.Additive;

    /// <summary>
    ///     Whether this flair takes over drawing the name glyphs, rather than decorating around them.
    /// </summary>
    /// <remarks>
    ///     Implies <see cref="PaintName" /> is implemented. Only the first such flair in the style
    ///     runs; see <see cref="RarityStyle.NamePainter" />.
    /// </remarks>
    public virtual bool PaintsName => false;

    /// <summary>
    ///     Whether the renderer should leave this flair's <see cref="SpriteBatch" /> alone.
    /// </summary>
    /// <remarks>
    ///     Set by flairs that have to run their own <c>Begin</c> -- a render-target composite needs
    ///     screen space and an immediate sort, neither of which the surrounding interface batch has.
    ///     Such a flair is handed the batch as the renderer found it and must hand it back the same
    ///     way; <see cref="RarityBatch.RestoreInterface" /> is the one call that does that.
    /// </remarks>
    public virtual bool ManagesOwnBatch => false;

    /// <summary>Drawn behind the name text.</summary>
    public virtual void UnderName(in RarityNameContext ctx) { }

    /// <summary>Draws the name glyphs. Requires <see cref="PaintsName" />.</summary>
    public virtual void PaintName(in RarityNameContext ctx) { }

    /// <summary>Drawn in front of the name text.</summary>
    public virtual void OverName(in RarityNameContext ctx) { }

    /// <summary>Drawn behind the item sprite, in a slot or in the world.</summary>
    public virtual void UnderSprite(in RaritySpriteContext ctx) { }

    /// <summary>Drawn in front of the item sprite, in a slot or in the world.</summary>
    public virtual void OverSprite(in RaritySpriteContext ctx) { }

    /// <summary>
    ///     An effect to bind while the item sprite draws, or null.
    /// </summary>
    /// <remarks>
    ///     The renderer binds this and lets the game draw the sprite itself rather than drawing a
    ///     copy: an immediate-sorted batch keeps the effect bound across the draws that follow, which
    ///     means the shader lands on the real sprite at the real position, animation frames and world
    ///     bobbing included. Taking the draw over instead means reproducing all of that, and getting
    ///     it subtly wrong for animated items.
    /// </remarks>
    public virtual Asset<Effect>? SpriteShader => null;

    /// <summary>Fills in the effect's parameters for this draw. Requires <see cref="SpriteShader" />.</summary>
    public virtual void ApplySpriteShader(Effect effect, in RaritySpriteContext ctx) { }

    /// <summary>
    ///     Per-tick hook for an item lying in the world, for flairs that emit into a world-space
    ///     system rather than drawing directly.
    /// </summary>
    public virtual void UpdateWorldItem(Item item) { }

    /// <summary>Releases anything the flair allocated. Called once, on mod unload.</summary>
    public virtual void Unload() { }
}

/// <summary>
///     What a flair is given when it decorates the tooltip's name line.
/// </summary>
/// <remarks>
///     Interface space, under <see cref="Main.UIScaleMatrix" />: the same coordinates
///     <see cref="DrawableTooltipLine.X" /> is expressed in, not raw screen pixels.
/// </remarks>
public readonly struct RarityNameContext(
    Item item,
    DrawableTooltipLine line,
    SpriteBatch spriteBatch,
    RarityStyle style,
    Vector2 position,
    Vector2 size,
    Color color)
{
    public readonly Item Item = item;

    public readonly DrawableTooltipLine Line = line;

    public readonly SpriteBatch SpriteBatch = spriteBatch;

    public readonly RarityStyle Style = style;

    /// <summary>Top-left of the name text.</summary>
    public readonly Vector2 Position = position;

    /// <summary>Measured size of the name text, with the line's own scale already applied.</summary>
    public readonly Vector2 Size = size;

    /// <summary>The colour the line resolved to, override included.</summary>
    public readonly Color Color = color;

    public Vector2 Center => Position + Size * 0.5f;

    public Rectangle Bounds => new((int)Position.X, (int)Position.Y, (int)Size.X, (int)Size.Y);

    /// <summary>Seconds, wrapping hourly. The clock every flair animates against.</summary>
    public float Time => Main.GlobalTimeWrappedHourly;
}

/// <summary>
///     What a flair is given when it decorates the item's sprite.
/// </summary>
/// <remarks>
///     The coordinate space is whichever one the caller was already in --
///     <see cref="Main.UIScaleMatrix" /> for a slot, <see cref="Main.GameViewMatrix" /> for a dropped
///     item -- so a flair draws relative to <see cref="Center" /> and never needs to know which.
///     <see cref="InWorld" /> is there for the cases that genuinely differ, such as an emitter that
///     should only run once the item is on the ground.
/// </remarks>
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

    /// <summary>Where the sprite's centre lands, in the active batch's space.</summary>
    public readonly Vector2 Center = center;

    /// <summary>The drawn size of the sprite's frame, scale included.</summary>
    public readonly Vector2 SpriteSize = spriteSize;

    public readonly Color DrawColor = drawColor;

    public readonly float Rotation = rotation;

    public readonly float Scale = scale;

    /// <summary>True for a dropped item, false for an inventory slot.</summary>
    public readonly bool InWorld = inWorld;

    public float Time => Main.GlobalTimeWrappedHourly;

    /// <summary>The matrix the surrounding batch was opened with.</summary>
    public Matrix Matrix => InWorld ? Main.GameViewMatrix.TransformationMatrix : Main.UIScaleMatrix;
}
