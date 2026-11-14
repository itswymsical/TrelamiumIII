using ReLogic.Content;
using Terraria.GameContent;
using TrelamiumIII.Core.Graphics;
using TrelamiumIII.Core.Liquids;

namespace TrelamiumIII.Core.Alchemy;

/// <summary>A bottle sprite: a glass texture and a same-size grayscale liquid mask, recoloured per item.</summary>
public sealed record BottleTemplate(string Glass, string Liquid)
{
    /// <summary>The vanilla Bottled Water shape.</summary>
<<<<<<< HEAD
    public static BottleTemplate Solution { get; } = new(Assets.Textures.Templates.SolutionBottle.KEY, Assets.Textures.Templates.SolutionBottle_Liquid.KEY);

    public static BottleTemplate Solvent { get; } = new(Assets.Textures.Templates.SolventBottle.KEY, Assets.Textures.Templates.SolventBottle_Liquid.KEY);
=======
    public static BottleTemplate Solution { get; } = new(Assets.Textures.SolutionBottle.KEY, Assets.Textures.SolutionBottle_Liquid.KEY);

    public static BottleTemplate Solvent { get; } = new(Assets.Textures.SolventBottle.KEY, Assets.Textures.SolventBottle_Liquid.KEY);
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb
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
<<<<<<< HEAD
    public static LiquidPalette Oil { get; } = new(new Color(29, 31, 39), new Color(52, 64, 90), new Color(193, 188, 211));

    /// <summary>
    ///     A palette around <paramref name="body" />, such as a principle's colour: the shadow is the same hue darker and more
    ///     saturated, the highlight paler.
    /// </summary>
    public static LiquidPalette Around(Color body) => new(Shade(body, 0.7f, 1.35f), body, Shade(body, 1.2f, 0.4f));
=======
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb

    /// <param name="shade">0 at the mask's darkest shade, 1 at its lightest.</param>
    public Color At(float shade) => shade < BodyStop
        ? Color.Lerp(Shadow, Body, shade / BodyStop)
        : Color.Lerp(Body, Highlight, (shade - BodyStop) / (1f - BodyStop));
<<<<<<< HEAD

    /// <summary><paramref name="color" /> with its HSV value and saturation scaled and its hue kept.</summary>
    private static Color Shade(Color color, float value, float saturation)
    {
        Vector3 rgb = color.ToVector3();
        float max = MathF.Max(rgb.X, MathF.Max(rgb.Y, rgb.Z));
        float min = MathF.Min(rgb.X, MathF.Min(rgb.Y, rgb.Z));
        float v = MathF.Min(max * value, 1f);

        if (max <= min)
            return new Color(new Vector3(v));

        Vector3 hue = (rgb - new Vector3(min)) / (max - min);
        float s = MathF.Min((max - min) / max * saturation, 1f);

        return new Color(v * (Vector3.One - s * (Vector3.One - hue)));
    }
=======
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb
}

/// <summary>
///     A bottle built from a <see cref="BottleTemplate" />. The item texture is the glass over the liquid mask recoloured by
///     <see cref="Palette" />, baked on load. Slots and dropped items also draw <see cref="Surface" /> over the liquid.
/// </summary>
public abstract class BottledLiquidItem : ChemicalItem
{
<<<<<<< HEAD
    /// <summary>What <see cref="Bake" /> measured on the liquid mask: its opaque rectangle and shade range.</summary>
    private sealed class MaskMetrics
    {
        public Vector4 Bounds;
        public Vector2 ShadeRange;
    }

=======
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb
    [CloneByReference]
    private Asset<Texture2D>? baked;

    [CloneByReference]
    private Asset<Texture2D>? mask;

<<<<<<< HEAD
    [CloneByReference]
    private Asset<Texture2D>? bareGlass;

    [CloneByReference]
    private MaskMetrics? metrics;

    /// <param name="name">Internal name when it isn't the class name, such as one kept from an older item or a table instance.</param>
    protected BottledLiquidItem(string? name = null) : base(name) { }

    /// <summary>The <c>Splash</c> technique: liquid sloshing in the bottle. It draws the whole liquid, so pair it with <see cref="SurfaceIsLiquid" />.</summary>
    protected static LiquidSurface Splash { get; } = new("Splash");
=======
    protected BottledLiquidItem() : base(null) { }
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb

    protected abstract BottleTemplate Template { get; }

    protected abstract LiquidPalette Palette { get; }

    /// <summary>
    ///     Pattern drawn over the liquid by the same-named technique in <c>BottleLiquid.fx</c>, one sprite pixel per world pixel.
    ///     Null draws none.
    /// </summary>
    protected virtual LiquidSurface? Surface => null;

<<<<<<< HEAD
    /// <summary>
    ///     Whether <see cref="Surface" />'s technique draws the whole liquid rather than a pattern over the baked one. Slots and
    ///     dropped items then draw it under the bare glass, so it can leave cells empty.
    /// </summary>
    protected virtual bool SurfaceIsLiquid => false;

=======
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb
    public override string Texture => Template.Glass;

    /// <summary>Swaps the item texture for an asset that <see cref="Bake" /> fills on the main thread.</summary>
    public override void AutoStaticDefaults()
    {
        base.AutoStaticDefaults();

        if (Main.dedServ)
            return;

        mask = ModContent.Request<Texture2D>(Template.Liquid);
<<<<<<< HEAD
        bareGlass = ModContent.Request<Texture2D>(Template.Glass);
        metrics = new MaskMetrics();
=======
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb
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

<<<<<<< HEAD
    public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor,
        Vector2 origin, float scale)
        => !SurfaceIsLiquid || !DrawSurface(spriteBatch, position, frame, drawColor, 0f, origin, scale);

    public override void PostDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor,
        Vector2 origin, float scale)
    {
        if (!SurfaceIsLiquid)
            DrawSurface(spriteBatch, position, frame, drawColor, 0f, origin, scale);
    }

    public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale,
        int whoAmI)
        => !SurfaceIsLiquid || !DrawSurfaceInWorld(spriteBatch, alphaColor, rotation, scale);

    public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, float rotation, float scale, int whoAmI)
    {
        if (!SurfaceIsLiquid)
            DrawSurfaceInWorld(spriteBatch, alphaColor, rotation, scale);
    }

    /// <remarks>Placed where <c>Main.DrawItem</c> draws a dropped item's sprite.</remarks>
    private bool DrawSurfaceInWorld(SpriteBatch spriteBatch, Color color, float rotation, float scale)
    {
        if (mask is not { IsLoaded: true })
            return false;
=======
    public override void PostDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor,
        Vector2 origin, float scale)
        => DrawSurface(spriteBatch, position, frame, drawColor, 0f, origin, scale);

    /// <remarks>Placed where <c>Main.DrawItem</c> draws a dropped item's sprite.</remarks>
    public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, float rotation, float scale, int whoAmI)
    {
        if (mask is not { IsLoaded: true })
            return;
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb

        Rectangle frame = mask.Value.Frame();
        Vector2 position = Item.position - Main.screenPosition + new Vector2(Item.width / 2, Item.height - frame.Height * 0.5f);

<<<<<<< HEAD
        return DrawSurface(spriteBatch, position, frame, color, rotation, frame.Size() * 0.5f, scale);
=======
        DrawSurface(spriteBatch, position, frame, alphaColor, rotation, frame.Size() * 0.5f, scale);
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb
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
<<<<<<< HEAD
        var bounds = new Vector4(liquid.Width, liquid.Height, 0f, 0f);

        for (int i = 0; i < shades.Length; i++)
        {
            Color shade = shades[i];
=======

        foreach (Color shade in shades)
        {
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb
            if (shade.A == 0)
                continue;

            darkest = MathF.Min(darkest, shade.R / (float)shade.A);
            lightest = MathF.Max(lightest, shade.R / (float)shade.A);
<<<<<<< HEAD

            int x = i % liquid.Width;
            int y = i / liquid.Width;
            bounds = new Vector4(MathF.Min(bounds.X, x), MathF.Min(bounds.Y, y), MathF.Max(bounds.Z, x + 1), MathF.Max(bounds.W, y + 1));
=======
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb
        }

        float range = MathF.Max(lightest - darkest, 1f / 255f);

<<<<<<< HEAD
        metrics!.Bounds = bounds;
        metrics.ShadeRange = new Vector2(darkest, range);

=======
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb
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

<<<<<<< HEAD
    /// <summary>Draws <see cref="Surface" />, then with <see cref="SurfaceIsLiquid" /> the glass over it. False when nothing drew.</summary>
    private bool DrawSurface(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color color, float rotation, Vector2 origin, float scale)
    {
        if (Surface is not { } surface || baked is not { IsLoaded: true } || metrics is null
            || mask is not { IsLoaded: true } || bareGlass is not { IsLoaded: true }
            || Assets.Shaders.BottleLiquid.Asset.Value is not { } effect || effect.Techniques[surface.Technique] is not { } technique)
            return false;

        effect.CurrentTechnique = technique;
        effect.Parameters["uNoiseTex"]?.SetValue(Assets.Textures.Noise.Perlin.Asset.Value);
        effect.Parameters["uGlassTex"]?.SetValue(bareGlass.Value);
        // Offset per item type, so different bottles side by side don't move in step.
        effect.Parameters["Time"]?.SetValue(Main.GlobalTimeWrappedHourly + Type * 3.7f % 60f);
=======
    private void DrawSurface(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color color, float rotation, Vector2 origin, float scale)
    {
        if (Surface is not { } surface || baked is not { IsLoaded: true } || mask is not { IsLoaded: true }
            || Assets.Shaders.BottleLiquid.Asset.Value is not { } effect || effect.Techniques[surface.Technique] is not { } technique)
            return;

        effect.CurrentTechnique = technique;
        effect.Parameters["uNoiseTex"]?.SetValue(Assets.Textures.Noise.Perlin.Asset.Value);
        effect.Parameters["Time"]?.SetValue(Main.GlobalTimeWrappedHourly);
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb
        effect.Parameters["TextureSize"]?.SetValue(mask.Value.Size());
        effect.Parameters["PixelSize"]?.SetValue(2f);
        effect.Parameters["Scale"]?.SetValue(surface.Scale);
        effect.Parameters["Speed"]?.SetValue(surface.Speed);
        effect.Parameters["Strength"]?.SetValue(surface.Strength);
        effect.Parameters["ColorA"]?.SetValue(surface.ColorA);
        effect.Parameters["ColorB"]?.SetValue(surface.ColorB);
<<<<<<< HEAD
        effect.Parameters["Shadow"]?.SetValue(Palette.Shadow.ToVector3());
        effect.Parameters["Body"]?.SetValue(Palette.Body.ToVector3());
        effect.Parameters["Highlight"]?.SetValue(Palette.Highlight.ToVector3());
        effect.Parameters["ShadeRange"]?.SetValue(metrics.ShadeRange);
        effect.Parameters["LiquidBounds"]?.SetValue(metrics.Bounds);
=======
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb

        SpriteBatchState home = SpriteBatchState.Capture(spriteBatch);
        spriteBatch.End();
        (home with { SortMode = SpriteSortMode.Immediate, BlendState = BlendState.AlphaBlend, Effect = effect }).Begin(spriteBatch);

        spriteBatch.Draw(mask.Value, position, frame, color, rotation, origin, scale, SpriteEffects.None, 0f);

        spriteBatch.End();
        // FNA rebinds texture parameters on every Apply without checking for disposal.
        effect.Parameters["uNoiseTex"]?.SetValue((Texture?)null);
<<<<<<< HEAD
        effect.Parameters["uGlassTex"]?.SetValue((Texture?)null);
        home.Begin(spriteBatch);

        if (SurfaceIsLiquid)
            spriteBatch.Draw(bareGlass.Value, position, frame, color, rotation, origin, scale, SpriteEffects.None, 0f);

        return true;
=======
        home.Begin(spriteBatch);
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb
    }
}
