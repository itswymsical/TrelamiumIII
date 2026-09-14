using System.Collections.Generic;
using Terraria.GameContent;
using TrelamiumIII.Core.Graphics;
using TrelamiumIII.Core.Rarities.Text;
using TrelamiumIII.Core.UI.Projected;

namespace TrelamiumIII.Core.Rarities;

/// <summary>Drives every registered <see cref="RarityStyle" /> on the item tooltip, inventory slots and dropped items.</summary>
public sealed class RarityRenderer : GlobalItem
{
    /// <summary>Text key shared by every item tooltip, so field content carries over while the hovered item changes.</summary>
    internal static readonly object TooltipKey = new();

    /// <summary>Batch state the sprite pre-pass replaced with its shader batch, for the post-pass to restore.</summary>
    private SpriteBatchState shaderHome;

    /// <summary>Whether a shader batch is open. Tracked rather than derived: a shader that is still loading opens none.</summary>
    private bool shaderBatchOpen;

    public override bool InstancePerEntity => true;

    #region Tooltip

    public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
    {
        if (!RarityStyles.Any || !RarityStyles.TryGet(item, out var style) || style.Flavor is null)
            return;

        tooltips.Add(new TooltipLine(Mod, "RarityFlavor", style.Flavor.Value)
        {
            OverrideColor = style.FlavorColor ?? style.Color * 0.75f,
        });
    }

    public override bool PreDrawTooltipLine(Item item, DrawableTooltipLine line, ref int yOffset)
    {
        // A projected panel draws every line itself; drawing here as well leaves a flat copy at the vanilla position.
        if (ProjectedTooltip.OwnsFrame)
            return false;

        if (!RarityStyles.Any || line.OneDropLogo || !RarityStyles.TryGet(item, out var style))
            return true;

        var position = new Vector2(line.X, line.Y);
        Color color = line.OverrideColor ?? line.Color;

        if (line.Name == "ItemName" && line.Mod == "Terraria")
        {
            if (style.Flairs.Count == 0 && style.Text.IsVanilla)
                return true;

            RarityText.Draw(new RarityNameContext(Main.spriteBatch, style, line.Font, line.Text, position, line.BaseScale, line.Rotation,
                line.Origin, color, TooltipKey, item));

            return false;
        }

        if (line.Name == "RarityFlavor" && line.Mod == Mod.Name && style.FlavorText is { } flavorText)
        {
            TextPainter.DrawPlain(Main.spriteBatch, line.Font, line.Text, position, color, line.Rotation, line.Origin, line.BaseScale,
                flavorText);

            return false;
        }

        return true;
    }

    #endregion

    #region Sprite

    public override bool PreDrawInInventory(Item item, SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor,
        Color itemColor, Vector2 origin, float scale)
    {
        if (!RarityStyles.Any || !RarityStyles.TryGet(item, out var style))
            return true;

        return PreDrawSprite(style, SlotContext(item, spriteBatch, style, position, frame, drawColor, origin, scale));
    }

    public override void PostDrawInInventory(Item item, SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor,
        Color itemColor, Vector2 origin, float scale)
    {
        if (!RarityStyles.Any || !RarityStyles.TryGet(item, out var style))
            return;

        PostDrawSprite(style, SlotContext(item, spriteBatch, style, position, frame, drawColor, origin, scale));
    }

    public override bool PreDrawInWorld(Item item, SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation,
        ref float scale, int whoAmI)
    {
        if (!RarityStyles.Any || !RarityStyles.TryGet(item, out var style))
            return true;

        return PreDrawSprite(style, new RaritySpriteContext(item, spriteBatch, style, item.Center - Main.screenPosition,
            WorldSpriteSize(item, scale), lightColor, rotation, scale, inWorld: true));
    }

    public override void PostDrawInWorld(Item item, SpriteBatch spriteBatch, Color lightColor, Color alphaColor, float rotation, float scale,
        int whoAmI)
    {
        if (!RarityStyles.Any || !RarityStyles.TryGet(item, out var style))
            return;

        PostDrawSprite(style, new RaritySpriteContext(item, spriteBatch, style, item.Center - Main.screenPosition,
            WorldSpriteSize(item, scale), lightColor, rotation, scale, inWorld: true));
    }

    /// <summary>A slot context centred where vanilla puts the frame's centre for the origin it was given.</summary>
    private static RaritySpriteContext SlotContext(Item item, SpriteBatch spriteBatch, RarityStyle style, Vector2 position, Rectangle frame,
        Color drawColor, Vector2 origin, float scale)
        => new(item, spriteBatch, style, position + (frame.Size() * 0.5f - origin) * scale, frame.Size() * scale, drawColor, 0f, scale,
            inWorld: false);

    /// <summary>The drawn size of a dropped item's current frame. <see cref="Entity.Size" /> is the hitbox, not the picture.</summary>
    private static Vector2 WorldSpriteSize(Item item, float scale)
    {
        if (TextureAssets.Item[item.type] is not { Value: { } sheet })
            return item.Size * scale;

        Rectangle frame = Main.itemAnimations[item.type] is { } animation
            ? animation.GetFrame(sheet)
            : sheet.Frame();

        return frame.Size() * scale;
    }

    /// <summary>Draws the underlays, then binds the style's sprite shader so the game's own sprite draw goes through it.</summary>
    private bool PreDrawSprite(RarityStyle style, in RaritySpriteContext ctx)
    {
        RunSpritePass(style, ctx, over: false);

        shaderBatchOpen = false;

        if (style.SpriteShaderSource is not { SpriteShader: { } asset } source || asset.Value is not { } effect)
            return true;

        source.ApplySpriteShader(effect, ctx);

        shaderHome = SpriteBatchState.Capture(ctx.SpriteBatch);
        RarityBatch.Restart(ctx.SpriteBatch, shaderHome.With(BlendState.AlphaBlend, SpriteSortMode.Immediate, effect));
        shaderBatchOpen = true;

        return true;
    }

    /// <summary>Closes any shader batch the pre-pass opened, then draws the overlays.</summary>
    private void PostDrawSprite(RarityStyle style, in RaritySpriteContext ctx)
    {
        if (shaderBatchOpen)
        {
            RarityBatch.Restart(ctx.SpriteBatch, shaderHome);
            shaderBatchOpen = false;
        }

        RunSpritePass(style, ctx, over: true);
    }

    /// <summary>Draws one side of the sprite, sharing a batch between consecutive flairs with the same blend.</summary>
    private static void RunSpritePass(RarityStyle style, in RaritySpriteContext ctx, bool over)
    {
        FlairHooks hook = over ? FlairHooks.OverSprite : FlairHooks.UnderSprite;
        SpriteBatch spriteBatch = ctx.SpriteBatch;

        SpriteBatchState home = SpriteBatchState.Capture(spriteBatch);
        SpriteBatchState current = home;

        foreach (RarityFlair flair in style.Flairs)
        {
            if ((flair.Hooks & hook) == 0)
                continue;

            RarityBatch.Switch(spriteBatch, ref current, flair.ManagesOwnBatch ? home : home.With(flair.Blend));

            if (over)
                flair.OverSprite(ctx);
            else
                flair.UnderSprite(ctx);
        }

        RarityBatch.Switch(spriteBatch, ref current, home);
    }

    #endregion

    /// <summary>Per-tick world hook. Also runs on servers, where flairs must not touch graphics.</summary>
    public override void Update(Item item, ref float gravity, ref float maxFallSpeed)
    {
        if (Main.dedServ || !RarityStyles.Any || !RarityStyles.TryGet(item, out var style))
            return;

        foreach (RarityFlair flair in style.Flairs)
            flair.UpdateWorldItem(item);
    }
}

/// <summary>Releases what rarity styles allocated when the mod unloads.</summary>
public sealed class RarityStyleSystem : ModSystem
{
    public override void Unload()
    {
        RarityStyles.Clear();
        GlyphLayout.ClearCache();
    }
}
