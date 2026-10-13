using ReLogic.Content;
using Terraria.GameContent;
using TrelamiumIII.Core.Graphics;
using TrelamiumIII.Core.Liquids;

namespace TrelamiumIII.Core.Alchemy;

/// <summary>A bottle sprite: a glass texture and a same-size grayscale liquid mask, recoloured per item.</summary>
public sealed record BottleTemplate(string Glass, string Liquid)
{
    /// <summary>The vanilla Bottled Water shape.</summary>
    public static BottleTemplate Solution { get; } = new(Assets.Textures.SolutionBottle.KEY, Assets.Textures.SolutionBottle_Liquid.KEY);

    public static BottleTemplate Solvent { get; } = new(Assets.Textures.SolventBottle.KEY, Assets.Textures.SolventBottle_Liquid.KEY);
}

/// <summary>
///     Colours for a <see cref="BottleTemplate" /> liquid mask: its darkest shade becomes <see cref="Shadow" />, its lightest
///     <see cref="Highlight" />, with <see cref="Body" /> 40% of the way up.
/// </summary>
public readonly record struct LiquidPalette(Color Shadow, Color Body, Color Highlight)
{
    private const float BodyStop = 0.4f;

    /// <summary>Vanilla Bottled Water.</summary>
    public static LiquidPalette Water { get; } = new(new Color(9, 61, 191), new Color(51, 107, 249), new Color(147, 177, 253));

    /// <param name="shade">0 at the mask's darkest shade, 1 at its lightest.</param>
    public Color At(float shade) => shade < BodyStop
        ? Color.Lerp(Shadow, Body, shade / BodyStop)
        : Color.Lerp(Body, Highlight, (shade - BodyStop) / (1f - BodyStop));
}

/// <summary>
///     A bottle built from a <see cref="BottleTemplate" />. The item texture is the glass over the liquid mask recoloured by
///     <see cref="Palette" />, baked on load. Slots and dropped items also draw <see cref="Surface" /> over the liquid.
/// </summary>
public abstract class BottledLiquidItem : ChemicalItem
{
    [CloneByReference]
    private Asset<Texture2D>? baked;

    [CloneByReference]
    private Asset<Texture2D>? mask;

    protected BottledLiquidItem() : base(null) { }

    protected abstract BottleTemplate Template { get; }

    protected abstract LiquidPalette Palette { get; }

    /// <summary>
    ///     Pattern drawn over the liquid by the same-named technique in <c>BottleLiquid.fx</c>, one sprite pixel per world pixel.
    ///     Null draws none.
    /// </summary>
    protected virtual LiquidSurface? Surface => null;

    public override string Texture => Template.Glass;

    /// <summary>Swaps the item texture for an asset that <see cref="Bake" /> fills on the main thread.</summary>
    public override void AutoStaticDefaults()
    {
        base.AutoStaticDefaults();

        if (Main.dedServ)
            return;

        mask = ModContent.Request<Texture2D>(Template.Liquid);
        baked = new Asset<Texture2D>(FullName);
        baked.SetToLoadingState();
        TextureAssets.Item[Type] = baked;

        Main.QueueMainThreadAction(Bake);
    }

    public override void Unload()
    {
        if (baked is { } asset)
            Main.QueueMainThreadAction(asset.Dispose);
    }

    public override void PostDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor,
        Vector2 origin, float scale)
        => DrawSurface(spriteBatch, position, frame, drawColor, 0f, origin, scale);

    /// <remarks>Placed where <c>Main.DrawItem</c> draws a dropped item's sprite.</remarks>
    public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, float rotation, float scale, int whoAmI)
    {
        if (mask is not { IsLoaded: true })
            return;

        Rectangle frame = mask.Value.Frame();
        Vector2 position = Item.position - Main.screenPosition + new Vector2(Item.width / 2, Item.height - frame.Height * 0.5f);

        DrawSurface(spriteBatch, position, frame, alphaColor, rotation, frame.Size() * 0.5f, scale);
    }

    private void Bake()
    {
        Texture2D glass = ModContent.Request<Texture2D>(Template.Glass, AssetRequestMode.ImmediateLoad).Value;
        Texture2D liquid = ModContent.Request<Texture2D>(Template.Liquid, AssetRequestMode.ImmediateLoad).Value;

        var pixels = new Color[glass.Width * glass.Height];
        var shades = new Color[liquid.Width * liquid.Height];
        glass.GetData(pixels);
        liquid.GetData(shades);

        float darkest = 1f;
        float lightest = 0f;

        foreach (Color shade in shades)
        {
            if (shade.A == 0)
                continue;

            darkest = MathF.Min(darkest, shade.R / (float)shade.A);
            lightest = MathF.Max(lightest, shade.R / (float)shade.A);
        }

        float range = MathF.Max(lightest - darkest, 1f / 255f);

        // Premultiplied glass over premultiplied liquid.
        for (int i = 0; i < pixels.Length; i++)
        {
            Color shade = shades[i];
            if (shade.A == 0)
                continue;

            Color colour = Palette.At((shade.R / (float)shade.A - darkest) / range) * (shade.A / 255f);
            pixels[i] = new Color(pixels[i].ToVector4() + colour.ToVector4() * (1f - pixels[i].A / 255f));
        }

        var texture = new Texture2D(Main.instance.GraphicsDevice, glass.Width, glass.Height);
        texture.SetData(pixels);
        baked!.SubmitLoadedContent(texture, null);
    }

    private void DrawSurface(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color color, float rotation, Vector2 origin, float scale)
    {
        if (Surface is not { } surface || baked is not { IsLoaded: true } || mask is not { IsLoaded: true }
            || Assets.Shaders.BottleLiquid.Asset.Value is not { } effect || effect.Techniques[surface.Technique] is not { } technique)
            return;

        effect.CurrentTechnique = technique;
        effect.Parameters["uNoiseTex"]?.SetValue(Assets.Textures.Noise.Perlin.Asset.Value);
        effect.Parameters["Time"]?.SetValue(Main.GlobalTimeWrappedHourly);
        effect.Parameters["TextureSize"]?.SetValue(mask.Value.Size());
        effect.Parameters["PixelSize"]?.SetValue(2f);
        effect.Parameters["Scale"]?.SetValue(surface.Scale);
        effect.Parameters["Speed"]?.SetValue(surface.Speed);
        effect.Parameters["Strength"]?.SetValue(surface.Strength);
        effect.Parameters["ColorA"]?.SetValue(surface.ColorA);
        effect.Parameters["ColorB"]?.SetValue(surface.ColorB);

        SpriteBatchState home = SpriteBatchState.Capture(spriteBatch);
        spriteBatch.End();
        (home with { SortMode = SpriteSortMode.Immediate, BlendState = BlendState.AlphaBlend, Effect = effect }).Begin(spriteBatch);

        spriteBatch.Draw(mask.Value, position, frame, color, rotation, origin, scale, SpriteEffects.None, 0f);

        spriteBatch.End();
        // FNA rebinds texture parameters on every Apply without checking for disposal.
        effect.Parameters["uNoiseTex"]?.SetValue((Texture?)null);
        home.Begin(spriteBatch);
    }
}
