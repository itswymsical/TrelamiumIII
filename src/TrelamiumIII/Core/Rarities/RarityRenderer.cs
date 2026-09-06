using System.Collections.Generic;
using Terraria.GameContent;
using Terraria.UI.Chat;

namespace TrelamiumIII.Core.Rarities;

/// <summary>
///     Drives every registered <see cref="RarityStyle" />, on every surface an item is shown on.
/// </summary>
public sealed class RarityRenderer : GlobalItem
{
    public override bool InstancePerEntity => true;
    #region Tooltip

    public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
    {
        if (!RarityStyles.Any || !RarityStyles.TryGet(item, out var style) || style.Flavor is null)
            return;

        tooltips.Add(new TooltipLine(Mod, "RarityFlavor", style.Flavor.Value)
        {
            // Dimmed rather than the rarity colour outright: the flavour line is an aside, and at
            // full strength it competes with the name it is supposed to be commenting on.
            OverrideColor = style.FlavorColor ?? style.Color * 0.75f,
        });
    }

    public override bool PreDrawTooltipLine(Item item, DrawableTooltipLine line, ref int yOffset)
    {
        if (!RarityStyles.Any || line.Name != "ItemName")
            return true;

        if (line.OneDropLogo)
            return true;

        if (!RarityStyles.TryGet(item, out var style) || style.Flairs.Count == 0)
            return true;

        DrawName(item, line, style);

        return false;
    }

    /// <summary>Runs the three name passes: behind, the glyphs themselves, in front.</summary>
    private static void DrawName(Item item, DrawableTooltipLine line, RarityStyle style)
    {
        var spriteBatch = Main.spriteBatch;

        var ctx = new RarityNameContext(
            item,
            line,
            spriteBatch,
            style,
            new Vector2(line.X, line.Y),
            line.Font.MeasureString(line.Text) * line.BaseScale,
            line.OverrideColor ?? line.Color
        );

        RunNamePass(style, ctx, over: false);
        PaintName(style, ctx);
        RunNamePass(style, ctx, over: true);
    }

    private static void PaintName(RarityStyle style, in RarityNameContext ctx)
    {
        var line = ctx.Line;

        if (style.NamePainter is not { } painter)
        {
            // Exactly what vanilla would have done with this line, shadow and colour codes included.
            // Reproducing it with a bare DrawString is what costs the drop shadow, and the loss only
            // shows up over a light background.
            ChatManager.DrawColorCodedStringWithShadow(ctx.SpriteBatch, line.Font, line.Text,
                ctx.Position, ctx.Color, line.Rotation, line.Origin, line.BaseScale);

            return;
        }

        if (painter.ManagesOwnBatch)
        {
            painter.PaintName(ctx);
            return;
        }

        RarityBatch.Restart(ctx.SpriteBatch, painter.Blend);
        painter.PaintName(ctx);
        RarityBatch.RestoreInterface(ctx.SpriteBatch);
    }

    /// <summary>
    ///     Draws one side of the name, grouping consecutive flairs that share a blend into a single
    ///     batch.
    /// </summary>
    /// <remarks>
    ///     The batch is only reopened when the blend actually changes, so the common case -- a style
    ///     whose flairs are all additive -- costs one swap in and one swap out no matter how many
    ///     there are.
    /// </remarks>
    private static void RunNamePass(RarityStyle style, in RarityNameContext ctx, bool over)
    {
        BlendState? open = null;

        foreach (var flair in style.Flairs)
        {
            if (flair.ManagesOwnBatch)
            {
                // It is about to Begin for itself, so anything we have open has to be closed first
                // or the End it performs will be unbalanced.
                if (open is not null)
                {
                    RarityBatch.RestoreInterface(ctx.SpriteBatch);
                    open = null;
                }

                if (over)
                    flair.OverName(ctx);
                else
                    flair.UnderName(ctx);

                continue;
            }

            if (open != flair.Blend)
            {
                RarityBatch.Restart(ctx.SpriteBatch, flair.Blend);
                open = flair.Blend;
            }

            if (over)
                flair.OverName(ctx);
            else
                flair.UnderName(ctx);
        }

        if (open is not null)
            RarityBatch.RestoreInterface(ctx.SpriteBatch);
    }

    #endregion

    #region Sprite

    public override bool PreDrawInInventory(Item item, SpriteBatch spriteBatch, Vector2 position,
        Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
    {
        if (!RarityStyles.Any || !RarityStyles.TryGet(item, out var style))
            return true;

        // Where vanilla is about to put the frame's centre, given the origin it was handed.
        Vector2 center = position + (frame.Size() * 0.5f - origin) * scale;

        var ctx = new RaritySpriteContext(item, spriteBatch, style, center, frame.Size() * scale,
            drawColor, 0f, scale, inWorld: false);

        return PreDrawSprite(style, ctx);
    }

    public override void PostDrawInInventory(Item item, SpriteBatch spriteBatch, Vector2 position,
        Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
    {
        if (!RarityStyles.Any || !RarityStyles.TryGet(item, out var style))
            return;

        Vector2 center = position + (frame.Size() * 0.5f - origin) * scale;

        var ctx = new RaritySpriteContext(item, spriteBatch, style, center, frame.Size() * scale,
            drawColor, 0f, scale, inWorld: false);

        PostDrawSprite(style, ctx);
    }

    public override bool PreDrawInWorld(Item item, SpriteBatch spriteBatch, Color lightColor,
        Color alphaColor, ref float rotation, ref float scale, int whoAmI)
    {
        if (!RarityStyles.Any || !RarityStyles.TryGet(item, out var style))
            return true;

        var ctx = new RaritySpriteContext(item, spriteBatch, style, item.Center - Main.screenPosition,
            WorldSpriteSize(item, scale), lightColor, rotation, scale, inWorld: true);

        return PreDrawSprite(style, ctx);
    }

    public override void PostDrawInWorld(Item item, SpriteBatch spriteBatch, Color lightColor,
        Color alphaColor, float rotation, float scale, int whoAmI)
    {
        if (!RarityStyles.Any || !RarityStyles.TryGet(item, out var style))
            return;

        var ctx = new RaritySpriteContext(item, spriteBatch, style, item.Center - Main.screenPosition,
            WorldSpriteSize(item, scale), lightColor, rotation, scale, inWorld: true);

        PostDrawSprite(style, ctx);
    }

    /// <summary>
    ///     The drawn size of a dropped item's current frame.
    /// </summary>
    /// <remarks>
    ///     Taken from the sprite rather than from <see cref="Entity.Size" />, which is the hitbox and
    ///     for plenty of items is nothing like the picture -- a flair orbiting at hitbox radius sits
    ///     inside the sprite on some items and well outside it on others.
    /// </remarks>
    private static Vector2 WorldSpriteSize(Item item, float scale)
    {
        if (TextureAssets.Item[item.type] is not { Value: { } sheet })
            return item.Size * scale;

        Rectangle frame = Main.itemAnimations[item.type] is { } animation
            ? animation.GetFrame(sheet)
            : sheet.Frame();

        return frame.Size() * scale;
    }

    /// <summary>
    ///     Whether the pre-pass opened a shader batch that the post-pass still owes a close.
    /// </summary>
    /// <remarks>
    ///     Tracked rather than re-derived from the style, because the two are not the same question:
    ///     a style can name a shader whose asset has not finished loading, in which case no batch was
    ///     opened and closing one would hand the caller a batch it never asked for, with our sampler
    ///     and sort rather than its own. The hooks are called in strict pairs and draws are
    ///     sequential, so a single flag is enough.
    /// </remarks>
    private bool shaderBatchOpen;

    /// <summary>
    ///     Draws the underlays, then leaves the batch in whatever state the sprite needs.
    /// </summary>
    /// <remarks>
    ///     Always returns true. The sprite is never redrawn by hand: when a shader is wanted the
    ///     batch is reopened immediate, the effect applied, and the game left to perform its own
    ///     draw through it -- which keeps animation frames, world bobbing and every other positioning
    ///     rule the game owns, none of which a reimplementation gets right for free.
    /// </remarks>
    private bool PreDrawSprite(RarityStyle style, in RaritySpriteContext ctx)
    {
        RunSpritePass(style, ctx, over: false);

        shaderBatchOpen = false;

        if (style.SpriteShaderSource is not { SpriteShader: { } asset } source || asset.Value is not { } effect)
            return true;

        source.ApplySpriteShader(effect, ctx);

        RarityBatch.Restart(ctx.SpriteBatch, ctx.InWorld, BlendState.AlphaBlend, SpriteSortMode.Immediate);
        effect.CurrentTechnique.Passes[0].Apply();

        shaderBatchOpen = true;

        return true;
    }

    /// <summary>Closes any shader batch the pre-pass opened, then draws the overlays.</summary>
    private void PostDrawSprite(RarityStyle style, in RaritySpriteContext ctx)
    {
        if (shaderBatchOpen)
        {
            RarityBatch.Restore(ctx.SpriteBatch, ctx.InWorld);
            shaderBatchOpen = false;
        }

        RunSpritePass(style, ctx, over: true);
    }

    private static void RunSpritePass(RarityStyle style, in RaritySpriteContext ctx, bool over)
    {
        BlendState? open = null;

        foreach (var flair in style.Flairs)
        {
            if (flair.ManagesOwnBatch)
            {
                if (open is not null)
                {
                    RarityBatch.Restore(ctx.SpriteBatch, ctx.InWorld);
                    open = null;
                }

                if (over)
                    flair.OverSprite(ctx);
                else
                    flair.UnderSprite(ctx);

                continue;
            }

            if (open != flair.Blend)
            {
                RarityBatch.Restart(ctx.SpriteBatch, ctx.InWorld, flair.Blend);
                open = flair.Blend;
            }

            if (over)
                flair.OverSprite(ctx);
            else
                flair.UnderSprite(ctx);
        }

        if (open is not null)
            RarityBatch.Restore(ctx.SpriteBatch, ctx.InWorld);
    }

    #endregion

    /// <summary>Feeds the emitters of items lying in the world.</summary>
    /// <remarks>
    ///     Runs on the server too, where none of this exists, so the guard is load-bearing rather
    ///     than defensive.
    /// </remarks>
    public override void Update(Item item, ref float gravity, ref float maxFallSpeed)
    {
        if (Main.dedServ || !RarityStyles.Any || !RarityStyles.TryGet(item, out var style))
            return;

        foreach (var flair in style.Flairs)
            flair.UpdateWorldItem(item);
    }
}

/// <summary>Releases what the rarity styles allocated when the mod unloads.</summary>
public sealed class RarityStyleSystem : ModSystem
{
    public override void Unload() => RarityStyles.Clear();
}
