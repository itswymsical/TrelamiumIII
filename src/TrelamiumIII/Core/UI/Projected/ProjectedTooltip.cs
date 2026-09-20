using System.Collections.Generic;
using System.Collections.ObjectModel;
using ReLogic.Graphics;
using TrelamiumIII.Core.Dialogue.Projected;
using TrelamiumIII.Core.Rarities;
using TrelamiumIII.Core.Text.Tooltips;

namespace TrelamiumIII.Core.UI.Projected;

/// <summary>
///     An item tooltip as a card standing next to the cursor, for panels that are themselves
///     standing in the scene.
/// </summary>
/// <remarks>
///     <para>
///         The lines come from vanilla. A slot <see cref="Claim" />s the item, the system puts it
///         into <see cref="Main.HoverItem" /> just before the mouse-text layer, vanilla assembles the
///         tooltip -- prefix, stats, rarity flavour, every modded line -- and
///         <see cref="ProjectedTooltipGlobalItem" /> intercepts the draw. Only a claimed frame is
///         taken over; a tooltip over the inventory is left exactly as it was.
///     </para>
///     <para>
///         Drawn smooth rather than pixelated. The card is the one projected panel whose content
///         belongs to the game rather than to the mod, and a vanilla tooltip thresholded and
///         magnified is unreadable -- see <see cref="ProjectedFonts" />. Its chrome is drawn at one
///         texel per screen pixel to match, so nothing on it is claiming to be pixel art.
///     </para>
///     <para>
///         The face lags the lines by one frame: they are captured in the mouse-text layer, which
///         runs after the point in the frame where faces can be built. The card therefore only draws
///         once the face was built for the item now under the cursor, so the first frame of a hover
///         shows nothing rather than the previous item's card.
///     </para>
/// </remarks>
internal sealed class ProjectedTooltip : IProjectedPanel
{
    #region Tuning

    /// <summary>One texel per screen pixel: the card is sized, not magnified.</summary>
    private const float PIXEL = 1f;

    private const int SURFACE_W = 480;
    private const int SURFACE_H = 520;

    private const int TEXT_W = 340;
    private const int MAX_LINES = 20;

    /// <summary>Space under the name, so the rule beneath it has room.</summary>
    private const int NAME_GAP = 7;

    private const int OPEN_FRAMES = 7;
    private const int CLOSE_FRAMES = 5;

    /// <summary>Screen pixels between the cursor and the card's near edge.</summary>
    private const float CURSOR_GAP = 20f;

    private const float ENTRY_DEPTH = 0.22f;

    private static readonly float RestYaw = MathHelper.ToRadians(9f);
    private static readonly float RestPitch = MathHelper.ToRadians(-5f);
    private static readonly float RestRoll = MathHelper.ToRadians(-1f);

    #endregion

    #region Frame ownership

    private static ProjectedTooltip? instance;

    private static bool claimed;
    private static Item? claimedItem;
    private static float claimedFacing = 1f;

    private static bool boxSuppressed;

    /// <summary>
    ///     True while a projected panel owns this frame's item tooltip and will draw every line of
    ///     it itself.
    /// </summary>
    /// <remarks>
    ///     Read by anything that draws onto a tooltip in screen space -- the rarity flairs, the
    ///     floating tooltip blocks -- because vanilla asks <c>PreDrawTooltipLine</c> before it checks
    ///     whether <c>PreDrawTooltip</c> refused the tooltip, so a hook that draws unconditionally
    ///     lands a second copy of its work at the position vanilla would have used.
    /// </remarks>
    public static bool OwnsFrame { get; private set; }

    /// <summary>
    ///     Marks this frame's item tooltip as one to draw projected, for <paramref name="item" />.
    /// </summary>
    /// <param name="facing">
    ///     Which way the claiming panel is turned, +1 or -1. The card mirrors it, so a tooltip off a
    ///     mirrored board leans and sits on the same side as the board itself rather than always
    ///     opening to the right.
    /// </param>
    public static void Claim(Item item, float facing = 1f)
    {
        claimed = true;
        claimedItem = item;
        claimedFacing = facing < 0f ? -1f : 1f;

        if (instance is not null)
            return;

        instance = new ProjectedTooltip();
        ProjectedPanelSystem.Register(instance);
    }

    /// <summary>
    ///     Puts the claimed item into vanilla's hover slot and holds back the opaque box, from a
    ///     layer immediately ahead of the mouse text.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The hover has to be re-asserted because the World UI layer that claimed it runs early,
    ///         and vanilla's own "Interface Logic 3" clears <see cref="Main.hoverItemName" /> between
    ///         there and the tooltip.
    ///     </para>
    ///     <para>
    ///         The opaque box is the player's own setting, and vanilla draws it before it asks
    ///         whether anything wants to replace the tooltip -- so refusing the tooltip leaves the box
    ///         behind it. It is switched off for the length of the tooltip draw and put back by
    ///         <see cref="Release" />, which runs on the far side of the mouse-text layer whether or
    ///         not a card was drawn.
    ///     </para>
    /// </remarks>
    internal static void Assert()
    {
        if (!claimed || claimedItem is null || Main.mouseItem.type > ItemID.None)
            return;

        OwnsFrame = true;

        Main.HoverItem = claimedItem.Clone();
        Main.hoverItemName = claimedItem.Name;

        if (!Main.SettingsEnabled_OpaqueBoxBehindTooltips)
            return;

        Main.SettingsEnabled_OpaqueBoxBehindTooltips = false;
        boxSuppressed = true;
    }

    /// <summary>Hands the frame back, from a layer just past the mouse text.</summary>
    internal static void Release()
    {
        if (boxSuppressed)
        {
            Main.SettingsEnabled_OpaqueBoxBehindTooltips = true;
            boxSuppressed = false;
        }

        OwnsFrame = false;
        claimed = false;
        claimedItem = null;
    }

    internal static void Reset()
    {
        Release();
        instance = null;
    }

    /// <returns>False when the tooltip was drawn here and vanilla should draw nothing.</returns>
    internal static bool HandlePreDraw(Item item, ReadOnlyCollection<TooltipLine> tooltip)
    {
        if (!OwnsFrame || instance is null)
            return true;

        instance.Capture(item, tooltip);
        instance.DrawAtCursor(Main.spriteBatch);

        return false;
    }

    #endregion

    private readonly ProjectedSurface surface = new(SURFACE_W, SURFACE_H, ProjectedInk.Smooth);
    private readonly List<(List<string> Lines, Color Color)> blocks = [];
    private readonly QuadWarp warp = QuadWarp.Handmade(77, 2f);

    private Color ghost = Color.Gray;
    private Point content;
    private int lineHeight;
    private float facing = 1f;

    /// <summary>Identity of the item the lines were captured for, and the one the face was built for.</summary>
    private int capturedFor = -1;

    private int builtFor = -1;

    private float presence;
    private bool wantedThisFrame;

    private ProjectedTooltip() => surface.Initialize();

    private static DynamicSpriteFont Font => ProjectedFonts.Vanilla;

    private static float Scale => PIXEL * Main.UIScale;

    /// <summary>The card never retires on its own; the system disposes it on unload.</summary>
    public bool ShouldBeRemoved => false;

    private void Capture(Item item, ReadOnlyCollection<TooltipLine> tooltip)
    {
        wantedThisFrame = true;
        facing = claimedFacing;

        int identity = HashCode.Combine(item.type, item.prefix, item.stack);

        if (identity == capturedFor)
            return;

        capturedFor = identity;
        blocks.Clear();

        DynamicSpriteFont font = Font;
        lineHeight = ProjectedText.LineHeight(font);

        float widest = 0f;
        int rows = 0;

        foreach (TooltipLine line in tooltip)
        {
            if (string.IsNullOrWhiteSpace(line.Text))
                continue;

            // The card paints raw glyphs, and its width is its own, so a reservation made for the
            // vanilla box would only be drawn as its tag.
            List<string> wrapped = ProjectedText.Wrap(font, TooltipReserveTagHandler.Strip(line.Text), TEXT_W);

            foreach (string row in wrapped)
                widest = MathF.Max(widest, ProjectedText.Width(font, row));

            rows += wrapped.Count;
            blocks.Add((wrapped, ColorFor(item, line)));

            if (rows >= MAX_LINES)
                break;
        }

        ghost = GlowFor(item) * 0.55f;

        content = new Point(
            (int)MathF.Ceiling(MathF.Max(widest, 24f)),
            Math.Max(rows, 1) * lineHeight + (blocks.Count > 1 ? NAME_GAP : 0));
    }

    /// <summary>
    ///     The colour vanilla would have drawn the line in: the rarity's for the name, green or red
    ///     for a modifier, whatever a mod asked for, and plain text otherwise.
    /// </summary>
    private static Color ColorFor(Item item, TooltipLine line)
    {
        if (line.OverrideColor.HasValue)
            return line.OverrideColor.Value;

        if (line.Name == "ItemName" && line.Mod == "Terraria")
            return NameColor(item);

        if (line.IsModifier)
            return line.IsModifierBad ? new Color(190, 120, 120) : new Color(120, 190, 120);

        return PanelStyle.Text;
    }

    /// <summary>
    ///     The item name's colour: its <see cref="RarityStyle" /> when it has one, so a designated
    ///     item or a restyled tier reads here exactly as it does on a vanilla tooltip.
    /// </summary>
    /// <remarks>
    ///     The flairs themselves are not reproduced. They are screen-space effects written against
    ///     a flat line of text -- an offset halo, a per-glyph ramp -- and re-rasterising them onto a
    ///     plane in perspective is a different piece of work than colouring the name.
    /// </remarks>
    private static Color NameColor(Item item)
        => RarityStyles.TryGet(item, out RarityStyle? style) ? style.Color : VanillaRarityColor(item);

    private static Color GlowFor(Item item)
        => RarityStyles.TryGet(item, out RarityStyle? style) ? style.GlowColor : VanillaRarityColor(item);

    private static Color VanillaRarityColor(Item item)
    {
        if (item.expert || item.rare == ItemRarityID.Expert)
            return Main.DiscoColor;

        if (item.master || item.rare == ItemRarityID.Master)
            return new Color(255, (byte)(Main.masterColor * 200f), 0);

        return item.rare switch
        {
            ItemRarityID.Quest => new Color(255, 175, 0),
            ItemRarityID.Gray => new Color(130, 130, 130),
            ItemRarityID.Blue => new Color(150, 150, 255),
            ItemRarityID.Green => new Color(150, 255, 150),
            ItemRarityID.Orange => new Color(255, 200, 150),
            ItemRarityID.LightRed => new Color(255, 150, 150),
            ItemRarityID.Pink => new Color(255, 150, 255),
            ItemRarityID.LightPurple => new Color(210, 160, 255),
            ItemRarityID.Lime => new Color(150, 255, 10),
            ItemRarityID.Yellow => new Color(255, 255, 10),
            ItemRarityID.Cyan => new Color(5, 200, 255),
            ItemRarityID.Red => new Color(255, 40, 100),
            ItemRarityID.Purple => new Color(180, 40, 255),
            >= ItemRarityID.Count => RarityLoader.GetRarity(item.rare)?.RarityColor ?? Color.White,
            _ => Color.White,
        };
    }

    /// <remarks>
    ///     Doubles as the per-frame tick, since it is the one call this gets every frame whether or
    ///     not anything is hovered: presence is stepped from what last frame wanted, and a claim
    ///     nothing consumed is dropped so it cannot hijack a later tooltip.
    /// </remarks>
    public void BuildSurfaces()
    {
        claimed = false;
        claimedItem = null;

        presence = wantedThisFrame
            ? MathF.Min(presence + 1f / OPEN_FRAMES, 1f)
            : MathF.Max(presence - 1f / CLOSE_FRAMES, 0f);

        wantedThisFrame = false;

        if (presence <= 0f || blocks.Count == 0)
            return;

        surface.Build(Paint);
        builtFor = capturedFor;
    }

    private void DrawAtCursor(SpriteBatch spriteBatch)
    {
        if (presence <= 0f || !surface.Ready || builtFor != capturedFor)
            return;

        Color tint = Color.White * presence;
        QuadPlacement quad = Placement();

        ProjectedPanelSystem.DrawQuads(spriteBatch, (mesh, device) => surface.Draw(mesh, device, quad, tint));
    }

    /// <summary>
    ///     Beside the cursor on the side the claiming panel faces, and below it, the way a tooltip
    ///     sits. Flipped when that would run off the window, and kept on screen either way.
    /// </summary>
    private QuadPlacement Placement()
    {
        float entrance = BubblePlacer.Entrance(presence, closing: false);
        float settle = 1f - entrance;

        Vector2 size = new Vector2(surface.Region.Width, surface.Region.Height) * Scale;
        Vector2 mouse = Main.MouseScreen;

        float half = size.X * 0.5f;
        float x = mouse.X + (CURSOR_GAP + half) * facing;

        // Only the far edge can force a flip; the near one is what the cursor is already past.
        if (x + half > Main.screenWidth - 8f || x - half < 8f)
            x = mouse.X - (CURSOR_GAP + half) * facing;

        Vector2 centre = new(
            BubblePlacer.OnScreen(x, half, Main.screenWidth),
            BubblePlacer.OnScreen(mouse.Y + CURSOR_GAP + size.Y * 0.5f, size.Y * 0.5f, Main.screenHeight));

        return new QuadPlacement
        {
            Center = centre,
            Size = size * MathHelper.Lerp(0.88f, 1f, entrance),

            // Everything with a handedness takes the claiming panel's, so the card and the panel it
            // came from are lit and turned as one object rather than two.
            Yaw = (RestYaw + settle * 0.25f) * facing,
            Pitch = RestPitch,
            Roll = RestRoll * facing,
            Depth = settle * ENTRY_DEPTH * PanelCamera.Distance,
            Warp = warp * facing,
            FarShade = 0.18f,
        };
    }

    private Rectangle Body => new(
        PanelStyle.MARGIN,
        PanelStyle.MARGIN,
        content.X + PanelStyle.PAD_X * 2,
        content.Y + PanelStyle.PAD_Y * 2);

    private Point Paint(ProjectedPainter painter)
    {
        PixelCanvas canvas = painter.Canvas;
        DynamicSpriteFont font = Font;
        Rectangle body = Body;

        ProjectedChrome.Card(canvas, body, ghost);

        float x = body.X + PanelStyle.PAD_X;
        float y = body.Y + PanelStyle.PAD_Y;

        for (int i = 0; i < blocks.Count; i++)
        {
            (List<string> lines, Color color) = blocks[i];

            for (int row = 0; row < lines.Count; row++)
                painter.Text(font, lines[row], new Vector2(x, y + row * lineHeight), color);

            y += lines.Count * lineHeight;

            // A rule under the name separates it from the stats, which is the job the vanilla
            // card's bold name does without one.
            if (i != 0 || blocks.Count <= 1)
                continue;

            ProjectedChrome.Rule(painter.Canvas, (int)x, (int)y + 2, content.X, false, ProjectedChrome.Dim);
            y += NAME_GAP;
        }

        return ProjectedChrome.CardFootprint(new Point(body.Width, body.Height));
    }

    public void Dispose() => surface.Dispose();
}

/// <summary>Hands item tooltips to <see cref="ProjectedTooltip" /> on the frames a panel claims.</summary>
public sealed class ProjectedTooltipGlobalItem : GlobalItem
{
    public override bool PreDrawTooltip(Item item, ReadOnlyCollection<TooltipLine> lines, ref int x, ref int y) =>
        ProjectedTooltip.HandlePreDraw(item, lines);

    public override void Unload() => ProjectedTooltip.Reset();
}
