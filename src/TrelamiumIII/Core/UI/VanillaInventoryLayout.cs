using Terraria.DataStructures;

namespace TrelamiumIII.Core.UI;

internal static class VanillaInventoryLayout
{
    public const float SLOT_BOX = 52f;

    public const float SLOT_GAP = 4f;

    private const float SCALE_ROOMY = 0.85f;

    private const float SCALE_COMPACT = 0.755f;

    public static bool ContainerOpen
        => (Main.LocalPlayer.chest != -1 || Main.npcShop > 0) && !Main.PipsUseGrid;

    /// <summary>
    /// Where the trash slot sits this frame and the scale it draws at. Vanilla drops it clear of the
    /// container grid (and shrinks it) while one is open, and otherwise honours
    /// <see cref="Main.trashSlotOffset"/>, which is how other mods shove it aside.
    /// </summary>
    public static (Vector2 Position, float Scale) TrashSlot()
    {
        // Vanilla's literals, kept literal: it draws at (448, 258) from a (0,0) pivot.
        float x = 448f;
        float y = 258f;
        float scale = SCALE_ROOMY;

        if (ContainerOpen)
        {
            x += 5f;
            y += 168f;
            scale = SCALE_COMPACT;
        }
        else if ((Main.LocalPlayer.chest == -1 || Main.npcShop == -1) && Main.trashSlotOffset != Point16.Zero)
        {
            x += Main.trashSlotOffset.X;
            y += Main.trashSlotOffset.Y;
            scale = SCALE_COMPACT;
        }

        return (new Vector2(x, y), scale);
    }

    /// <summary>
    /// The 10x4 chest or shop slot grid, or null when no container is open. Both use the same box.
    /// </summary>
    public static Rectangle? ContainerGrid()
    {
        if (!ContainerOpen) return null;

        return new Rectangle(73, Main.instance.invBottom,
            (int)(560f * SCALE_COMPACT), (int)(224f * SCALE_COMPACT));
    }
}
