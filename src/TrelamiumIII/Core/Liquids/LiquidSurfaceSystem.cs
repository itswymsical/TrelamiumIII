using System.Collections.Generic;
using ModLiquidLib.ModLoader;
using MonoMod.Cil;
using ReLogic.Content;
using Terraria.GameContent.Liquid;

namespace TrelamiumIII.Core.Liquids;

/// <summary>A liquid's animated surface: which <c>LiquidSurface.fx</c> technique draws it, and its palette.</summary>
public sealed record LiquidSurface(string Technique)
{
    public Vector3 ColorA { get; init; } = Vector3.One;

    public Vector3 ColorB { get; init; } = Vector3.Zero;

    public float Scale { get; init; } = 1f;

    public float Speed { get; init; } = 1f;

    public float Strength { get; init; } = 1f;
}

/// <summary>A ModLiquid with an animated surface drawn over it by <see cref="LiquidSurfaceSystem" />.</summary>
public interface ILiquidSurface
{
    LiquidSurface Surface { get; }

    /// <summary>The <c>Tint</c> the shader sees at (i, j), such as the tile's liquid condition colour.</summary>
    Color SurfaceTint(int i, int j) => Color.White;
}

/// <summary>
///     Draws every <see cref="ILiquidSurface" /> liquid's surface each frame, over the composited water. Vanilla only
///     re-renders the water target every fourth frame, so the visible rectangles are collected while it renders
///     and the animated surface is drawn from them on every frame after.
/// </summary>
public sealed class LiquidSurfaceSystem : ModSystem
{
    private readonly record struct Quad(Rectangle World, Color Light);

    private static readonly Dictionary<(int Type, Color Tint), List<Quad>> batches = [];

    private static readonly HashSet<Point> collected = [];

    private static ILiquidSurface?[] surfaces = [];

    private static Asset<Effect>? shader;

    private static Asset<Texture2D>? noise;

    private static Asset<Texture2D>? cells;

    public override void Load()
    {
        if (Main.dedServ)
            return;

        shader = Assets.Shaders.LiquidSurface.Asset;
        noise = Assets.Textures.Noise.Perlin.Asset;
        cells = Assets.Textures.Noise.VoronoiNoise.Asset;

        On_Main.DrawWaters += BeginCollecting;
        IL_Main.DoDraw += DrawAfterWater;
    }

    public override void PostSetupContent()
    {
        surfaces = new ILiquidSurface?[LiquidLoader.LiquidCount];

        for (int type = 0; type < surfaces.Length; type++)
            surfaces[type] = LiquidLoader.GetLiquid(type) as ILiquidSurface;
    }

    public override void Unload()
    {
        batches.Clear();
        collected.Clear();
        surfaces = [];
        shader = null;
        noise = null;
        cells = null;
    }

    private static void BeginCollecting(On_Main.orig_DrawWaters orig, Main self, bool isBackground)
    {
        if (!isBackground)
        {
            foreach (List<Quad> quads in batches.Values)
                quads.Clear();

            collected.Clear();
        }

        orig(self, isBackground);
    }

    /// <summary>Records the visible part of a liquid tile. Liquids draw once per active water style, so repeats are skipped.</summary>
    internal static void Collect(int i, int j, int type, in LiquidRenderer.LiquidDrawCache cache, bool isBackgroundDraw)
    {
        if (isBackgroundDraw || (uint)type >= (uint)surfaces.Length || surfaces[type] is not { } surface || !collected.Add(new Point(i, j)))
            return;

        Rectangle source = cache.SourceRectangle;
        if (source.Width <= 0 || source.Height <= 0)
            return;

        var world = new Rectangle(i * 16 + (int)cache.LiquidOffset.X, j * 16 + (int)cache.LiquidOffset.Y, source.Width, source.Height);
        Color light = Lighting.GetColor(i, j);
        light.A = (byte)(MathHelper.Clamp(cache.Opacity, 0f, 1f) * 255f);

        var key = (type, surface.SurfaceTint(i, j));
        if (!batches.TryGetValue(key, out List<Quad>? quads))
            batches[key] = quads = [];

        quads.Add(new Quad(world, light));
    }

    private void DrawAfterWater(ILContext il)
    {
        var c = new ILCursor(il);

        if (c.TryGotoNext(i => i.MatchLdsfld<Main>(nameof(Main.waterTarget)))
            && c.TryGotoNext(MoveType.After, i => i.MatchCallvirt<SpriteBatch>(nameof(SpriteBatch.Draw))))
            c.EmitDelegate(DrawSurfaces);
        else
            Mod.Logger.Warn("Could not find the water target draw in Main.DoDraw; liquid surfaces will not draw.");

        c.Index = 0;

        if (c.TryGotoNext(MoveType.After, i => i.MatchLdcI4(0), i => i.MatchCall<Main>("DrawWaters")))
            c.EmitDelegate(DrawSurfaces);
        else
            Mod.Logger.Warn("Could not find the draw-to-screen DrawWaters call in Main.DoDraw; liquid surfaces will not draw there.");
    }

    /// <summary>Runs inside DoDraw's world batch, which it ends and restarts with the same state.</summary>
    private static void DrawSurfaces()
    {
        if (shader?.Value is not { } effect || noise?.Value is not { } noiseTexture || cells?.Value is not { } cellTexture || !HasQuads())
            return;

        SpriteBatch spriteBatch = Main.spriteBatch;
        spriteBatch.End();

        effect.Parameters["Time"]?.SetValue(Main.GlobalTimeWrappedHourly);
        effect.Parameters["NoiseSize"]?.SetValue((float)noiseTexture.Width);
        effect.Parameters["PixelSize"]?.SetValue(2f);
        effect.Parameters["uCellTex"]?.SetValue(cellTexture);

        Rectangle view = new((int)Main.screenPosition.X - 32, (int)Main.screenPosition.Y - 32, Main.screenWidth + 64, Main.screenHeight + 64);

        foreach (((int type, Color tint), List<Quad> quads) in batches)
        {
            if (quads.Count == 0 || surfaces[type] is not { } owner)
                continue;

            LiquidSurface surface = owner.Surface;
            effect.CurrentTechnique = effect.Techniques[surface.Technique];
            effect.Parameters["Tint"]?.SetValue(tint.ToVector3());
            effect.Parameters["ColorA"]?.SetValue(surface.ColorA);
            effect.Parameters["ColorB"]?.SetValue(surface.ColorB);
            effect.Parameters["Scale"]?.SetValue(surface.Scale);
            effect.Parameters["Speed"]?.SetValue(surface.Speed);
            effect.Parameters["Strength"]?.SetValue(surface.Strength);

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearWrap, DepthStencilState.None, Main.Rasterizer, effect, Main.Transform);

            foreach (Quad quad in quads)
            {
                if (!view.Intersects(quad.World))
                    continue;

                var destination = new Rectangle(quad.World.X - (int)Main.screenPosition.X, quad.World.Y - (int)Main.screenPosition.Y, quad.World.Width, quad.World.Height);
                spriteBatch.Draw(noiseTexture, destination, quad.World, quad.Light);
            }

            spriteBatch.End();
        }

        // FNA rebinds texture parameters on every Apply without checking for disposal.
        effect.Parameters["uCellTex"]?.SetValue((Texture?)null);

        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.Transform);
    }

    private static bool HasQuads()
    {
        foreach (List<Quad> quads in batches.Values)
        {
            if (quads.Count > 0)
                return true;
        }

        return false;
    }
}

/// <summary>Feeds <see cref="LiquidSurfaceSystem" /> the visible rectangle of every liquid tile as it renders.</summary>
public sealed class LiquidSurfaceGlobalLiquid : GlobalLiquid
{
    public override void PostDraw(int i, int j, int type, LiquidRenderer.LiquidDrawCache liquidDrawCache, Vector2 drawOffset, bool isBackgroundDraw, int waterStyle, float waterAlpha)
        => LiquidSurfaceSystem.Collect(i, j, type, in liquidDrawCache, isBackgroundDraw);
}
