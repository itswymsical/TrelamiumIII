using System.Collections.Generic;
using ReLogic.Content;
using Terraria.ID;

namespace TrelamiumIII.Core.Books.Authoring;

public static class VFXLabUIAssets
{
    public static readonly Color Panel = new(48, 58, 104);

    public static readonly Color Inset = new(35, 40, 83);

    public static readonly Color ButtonIdle = new(78, 96, 164);

    public static readonly Color ButtonHover = new(104, 126, 206);

    public static readonly Color Selection = new(51, 137, 255);

    public static readonly Color Paper = new(238, 228, 206);

    public static readonly Color Ink = new(62, 44, 30);

    private static readonly Dictionary<string, Asset<Texture2D>> cache = new();

    /// <summary>Gradient slot plate. Every panel and button on the desk.</summary>
    public static Texture2D SlotBack => Get("Images/UI/Bestiary/Slot_Back");

    /// <summary>Lighter slot face, for the raised look on an active control.</summary>
    public static Texture2D SlotFront => Get("Images/UI/Bestiary/Slot_Front");

    public static Texture2D SlotSelection => Get("Images/UI/Bestiary/Slot_Selection");

    public static Texture2D HoverRing => Get("Images/UI/Bestiary/Button_Border");

    public static Texture2D BackArrow => Get("Images/UI/Bestiary/Button_Back");

    public static Texture2D ForwardArrow => Get("Images/UI/Bestiary/Button_Forward");

    /// <summary>Disk icon, for export.</summary>
    public static Texture2D Save => Get("Images/UI/IconQuickload");

    public static Texture2D Copy => Get("Images/UI/CharCreation/Copy");

    public static Texture2D Paste => Get("Images/UI/CharCreation/Paste");

    /// <summary>Journey-mode research gear. The legend's handle.</summary>
    public static Texture2D Gear => Get("Images/UI/Creative/Research_GearA");

    public static Texture2D KeyShift => Get("Images/UI/VK_Shift");

    public static Texture2D KeyBackspace => Get("Images/UI/VK_Backspace");

    private static Texture2D Get(string path)
    {
        if (!cache.TryGetValue(path, out Asset<Texture2D>? asset))
        {
            asset = Main.Assets.Request<Texture2D>(path, AssetRequestMode.ImmediateLoad);
            cache[path] = asset;
        }

        return asset.Value;
    }

    /// <summary>Nine-slice corner inset scaled to the plate, so small textures aren't sliced into overlapping halves.</summary>
    private static int CornerOf(Texture2D plate) =>
        Math.Max(2, Math.Min(10, Math.Min(plate.Width, plate.Height) / 3));

    /// <summary>One plate, nine-sliced to fill the rectangle edge to edge.</summary>
    public static void DrawPlate(SpriteBatch spriteBatch, Texture2D plate, Rectangle area, Color color)
    {
        int corner = CornerOf(plate);

        Utils.DrawSplicedPanel(spriteBatch, plate, area.X, area.Y, area.Width, area.Height,
            corner, corner, corner, corner, color);
    }

    public static void DrawPanel(SpriteBatch spriteBatch, Rectangle area, Color fill) =>
        DrawPlate(spriteBatch, SlotBack, area, fill);

    /// <summary>A button in its three states, raised on hover and lit when active.</summary>
    public static void DrawButton(SpriteBatch spriteBatch, Rectangle area, bool hover, bool active = false)
    {
        DrawPlate(spriteBatch, active ? SlotFront : SlotBack, area,
            active ? Selection : hover ? ButtonHover : ButtonIdle);

        if (hover && !active) DrawPlate(spriteBatch, SlotSelection, area, Colors.FancyUIFatButtonMouseOver * 0.6f);
    }

    /// <summary>An icon button: the icon is the control, the ring only appears on hover.</summary>
    public static void DrawIconButton(SpriteBatch spriteBatch, Texture2D icon, Rectangle area, bool hover)
    {
        spriteBatch.Draw(icon, area, Color.White);

        if (hover) spriteBatch.Draw(HoverRing, area, Color.White);
    }

    /// <summary>An icon centred on a plate, for a labelled control that shows a glyph instead.</summary>
    public static void DrawIconOnPlate(SpriteBatch spriteBatch, Texture2D icon, Rectangle area,
        bool hover, float iconScale = 1f)
    {
        DrawButton(spriteBatch, area, hover);

        int w = (int)(icon.Width * iconScale);
        int h = (int)(icon.Height * iconScale);

        spriteBatch.Draw(icon,
            new Rectangle(area.X + (area.Width - w) / 2, area.Y + (area.Height - h) / 2, w, h),
            Color.White);
    }
}
