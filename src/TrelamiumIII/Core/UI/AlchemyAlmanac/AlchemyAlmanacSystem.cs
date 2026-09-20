using ReLogic.Content;
using TrelamiumIII.Core.Books;

namespace TrelamiumIII.Core.UI.AlchemyAlmanac;

/// <summary>
/// Owns the render targets and composition for the Almanac.
/// <para>
/// Layer contract for the composite:
///   Layer 0: cover, flat at right-page position when no content occupies it.
///   Layer 1: current spread left page (flat).
///   Layer 2: current spread right page (flat). Null = cover shows through.
///   Layer 3: curl mesh is used for opening, closing, and page flipping.
/// </para>
/// </summary>
public class AlchemyAlmanacSystem : ModSystem
{
    public static RenderTarget2D PageA_Left { get; private set; }
    public static RenderTarget2D PageA_Right { get; private set; }
    public static RenderTarget2D PageB_Left { get; private set; }
    public static RenderTarget2D PageB_Right { get; private set; }

    /// <summary>Black cover.</summary>
    public static RenderTarget2D CoverTarget { get; private set; }

    public static RenderTarget2D CompositeTarget { get; private set; }

    private static PageFlipMesh _mesh;

    private static int _pageW;
    private static int _pageH;
    private static int _spreadW;
    private static int _spreadH;

    private static bool _coverDrawn;

    public override void Load()
    {
        if (Main.dedServ) return;

        _mesh = new PageFlipMesh();
        _mesh.Load();
        On_Main.CheckMonoliths += BuildFrame;
    }

    public override void Unload()
    {
        On_Main.CheckMonoliths -= BuildFrame;

        // The mesh contains graphics resources, so it must be disposed on the main thread too.
        // Snapshot to a local before nulling the field: the queued lambda runs after
        // this method returns, and by then _mesh has already been set to null below,
        // so a lambda reading the field directly would just no-op instead of disposing.
        var mesh = _mesh;
        _mesh = null;
        if (mesh != null)
        {
            Main.QueueMainThreadAction(() => mesh.Dispose());
        }

        DisposeAll();
    }

    public override void OnWorldUnload()
    {
        AlchemyAlmanacUI.ResetForWorldChange();
        _coverDrawn = false;
    }

    public static void InitTargets(int pageWidth, int pageHeight)
    {
        _pageW = pageWidth;
        _pageH = pageHeight;
        _spreadW = pageWidth * 2 + AlchemyAlmanacUI.GUTTER;
        _spreadH = pageHeight;
        _coverDrawn = false;

        Main.QueueMainThreadAction(() =>
        {
            DisposeAll();
            var device = Main.graphics.GraphicsDevice;

            PageA_Left = NewPageTarget(device, pageWidth, pageHeight);
            PageA_Right = NewPageTarget(device, pageWidth, pageHeight);
            PageB_Left = NewPageTarget(device, pageWidth, pageHeight);
            PageB_Right = NewPageTarget(device, pageWidth, pageHeight);
            CoverTarget = NewPageTarget(device, pageWidth, pageHeight);
            CompositeTarget = new RenderTarget2D(device, _spreadW, _spreadH,
                false, SurfaceFormat.Color, DepthFormat.None);
        });
    }

    private static RenderTarget2D NewPageTarget(GraphicsDevice d, int w, int h) =>
        new(d, w, h, false, SurfaceFormat.Color, DepthFormat.None);

    private static void BuildFrame(On_Main.orig_CheckMonoliths orig)
    {
        orig();

        var ui = AlchemyAlmanacUI.Current;
        if (ui == null || PageA_Left == null || CoverTarget == null || CompositeTarget == null
            || Main.gameMenu || Main.dedServ)
            return;

        var device = Main.graphics.GraphicsDevice;
        var sb = Main.spriteBatch;

        // Cover is rendered once, its content never changes during a single book lifetime.
        if (!_coverDrawn)
        {
            device.SetRenderTarget(CoverTarget);
            device.Clear(Color.Black);

            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                     SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
            AlchemyAlmanacUI.DrawCoverImprint(sb);
            sb.End();

            device.SetRenderTarget(null);
            _coverDrawn = true;
        }

        // Always redraw the current spread's pages live so dynamic content is fresh.
        DrawPageToTarget(device, sb, PageA_Left, ui, ui.CurrentSpread, isLeft: true);
        DrawPageToTarget(device, sb, PageA_Right, ui, ui.CurrentSpread, isLeft: false);

        // Only draw "next" pages while a flip is active.
        bool flipping = ui.PageTurnProgress > 0f;
        if (flipping)
        {
            DrawPageToTarget(device, sb, PageB_Left, ui, ui.NextSpread, isLeft: true);
            DrawPageToTarget(device, sb, PageB_Right, ui, ui.NextSpread, isLeft: false);
        }

        // Composite
        float pageTurn = ui.PageTurnProgress;
        float openProg = ui.OpenProgress;
        float closeProg = ui.CloseProgress;
        float bookSpineX = _pageW + AlchemyAlmanacUI.GUTTER * 0.5f;
        float rightSpineX = _pageW + AlchemyAlmanacUI.GUTTER;
        var proj = Matrix.CreateOrthographicOffCenter(0f, _spreadW, _spreadH, 0f, -1000f, 1000f);

        device.SetRenderTarget(CompositeTarget);
        device.Clear(Color.Transparent);

        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                 SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);

        Texture2D leftFlat;
        Texture2D rightFlat;
        Texture2D curlFront = null;
        Texture2D curlBack = null;

        if (flipping)
        {
            if (ui.IsForwardFlip)
            {
                leftFlat = ResolveLeftFlat(ui.CurrentSpread, PageA_Left);
                rightFlat = ResolveRightFlat(ui.NextSpread, PageB_Right);
                curlFront = ResolveRightFlat(ui.CurrentSpread, PageA_Right);
                curlBack = ResolveLeftFlat(ui.NextSpread, PageB_Left);
            }
            else
            {
                leftFlat = ResolveLeftFlat(ui.NextSpread, PageB_Left);
                rightFlat = ResolveRightFlat(ui.CurrentSpread, PageA_Right);
                curlFront = ResolveLeftFlat(ui.CurrentSpread, PageA_Left);
                curlBack = ResolveRightFlat(ui.NextSpread, PageB_Right);
            }
        }
        else
        {
            leftFlat = ResolveLeftFlat(ui.CurrentSpread, PageA_Left);
            rightFlat = ResolveRightFlat(ui.CurrentSpread, PageA_Right);
        }

        if (closeProg <= 0f)
        {
            if (leftFlat != null)
                sb.Draw(leftFlat, Vector2.Zero, Color.White);
            if (rightFlat != null)
                sb.Draw(rightFlat, new Vector2(_pageW + AlchemyAlmanacUI.GUTTER, 0f), Color.White);
        }

        sb.End();

        // Curl mesh: flipping, opening, or closing
        if (pageTurn > 0f && curlFront != null)
        {
            _mesh.Draw(device,
                front: curlFront,
                back: curlBack,
                spineX: bookSpineX,
                pageWidth: _pageW,
                pageHeight: _pageH,
                progress: pageTurn,
                backward: !ui.IsForwardFlip,
                projection: proj);
        }
        else if (openProg > 0f)
        {
            // Right page uncurls open from the right spine on first show.
            _mesh.Draw(device,
                front: rightFlat,
                back: null,
                spineX: rightSpineX,
                pageWidth: _pageW,
                pageHeight: _pageH,
                progress: openProg,
                backward: false,
                projection: proj);
        }
        else if (closeProg > 0f)
        {
            // Both pages fold inward to the spine when closing.
            // flipProgress 0..0.5 takes a page from flat to vertical-at-spine;
            // 0.5 reads as edge-on, which combined with the fading opacity disappears
            // cleanly into the cover beneath as the close completes.
            float curlProg = closeProg * 0.5f;

            if (rightFlat != null)
            {
                _mesh.Draw(device,
                    front: rightFlat,
                    back: null,
                    spineX: bookSpineX,
                    pageWidth: _pageW,
                    pageHeight: _pageH,
                    progress: curlProg,
                    backward: false,
                    projection: proj);
            }

            if (leftFlat != null)
            {
                _mesh.Draw(device,
                    front: leftFlat,
                    back: null,
                    spineX: bookSpineX,
                    pageWidth: _pageW,
                    pageHeight: _pageH,
                    progress: curlProg,
                    backward: true,
                    projection: proj);
            }
        }

        device.SetRenderTarget(null);
    }

    /// <summary>
    /// Cover spread (browse, index 0) draws the actual cover on the left.
    /// </summary>
    private static Texture2D ResolveLeftFlat(AlchemyAlmanacUI.SpreadKey key, RenderTarget2D livePage)
    {
        if (key.Category == AlchemyAlmanacUI.AlmanacCategory.Glossary
            && key.View == AlchemyAlmanacUI.ViewMode.Browse && key.Index == 0)
            return CoverTarget;
        return livePage;
    }

    private static Texture2D ResolveRightFlat(AlchemyAlmanacUI.SpreadKey key, RenderTarget2D livePage)
    {
        return livePage;
    }

    private static void DrawPageToTarget(GraphicsDevice device, SpriteBatch sb, RenderTarget2D target,
                                         AlchemyAlmanacUI ui, AlchemyAlmanacUI.SpreadKey key, bool isLeft)
    {
        device.SetRenderTarget(target);
        device.Clear(Color.White);

        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                 SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);

        if (isLeft) DrawPageBackground(sb, _pageW, _pageH, SpriteEffects.FlipHorizontally);
        else DrawPageBackground(sb, _pageW, _pageH, SpriteEffects.None);

        ui.DrawPageToTarget(sb, key, isLeft);

        sb.End();
        device.SetRenderTarget(null);
    }

    private static void DrawPageBackground(SpriteBatch sb, int width, int height, SpriteEffects dir)
    {
        var page = ModContent.Request<Texture2D>(
            $"{Assets.Textures.UI.AlchemyBookPage.KEY}",
            AssetRequestMode.ImmediateLoad).Value;
        sb.Draw(page, new Rectangle(0, 0, width, height), null, QuantizeColor(Color.White, 16), 0f, Vector2.Zero, dir, 0f);
    }

    private static Color QuantizeColor(Color color, byte intensity)
    {
        if (intensity == 0) return color;

        byte Quantize(byte channel) => (byte)((channel / intensity) * intensity);

        return new Color(Quantize(color.R), Quantize(color.G), Quantize(color.B), color.A);
    }

    private static void DisposeAll()
    {
        // Snapshot the live targets into locals FIRST. The queued lambda below only
        // runs later, and a lambda over static fields reads whatever they hold at
        // that later moment, not now. If we queued a lambda referencing the fields
        // directly, then nulled/reassigned them (as InitTargets does immediately
        // after calling this), the lambda would dispose whatever the fields point
        // to by the time it runs, which is the newly-created replacement set, not
        // the old one. Capturing locals pins down which objects actually get disposed.
        var oldA_Left = PageA_Left;
        var oldA_Right = PageA_Right;
        var oldB_Left = PageB_Left;
        var oldB_Right = PageB_Right;
        var oldCover = CoverTarget;
        var oldComposite = CompositeTarget;

        // Immediately drop the references on the current thread so they can't be used again
        PageA_Left = PageA_Right = PageB_Left = PageB_Right = CoverTarget = CompositeTarget = null;

        Main.QueueMainThreadAction(() =>
        {
            oldA_Left?.Dispose();
            oldA_Right?.Dispose();
            oldB_Left?.Dispose();
            oldB_Right?.Dispose();
            oldCover?.Dispose();
            oldComposite?.Dispose();
        });
    }
}