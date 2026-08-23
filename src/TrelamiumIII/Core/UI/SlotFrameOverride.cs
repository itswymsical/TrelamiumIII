// Credits: Spirit Mod, GabeHasWon
// https://github.com/GabeHasWon/SpiritReforged/blob/master/Common/UI/Misc/BasicItemSlot.cs#L8

using ReLogic.Content;
using System.Linq;
using System.Reflection;
using Terraria.GameContent;
using Terraria.UI;

namespace TrelamiumIII.Core.UI;

/// <summary>
///     Draws a vanilla item slot with its backdrop swapped for custom art.
/// </summary>
/// <remarks>
///     Custom frames cannot simply be painted over <see cref="ItemSlot.Draw" />'s output: art with
///     soft or rounded corners lets the grey vanilla slot bleed through the gaps. So instead of
///     drawing on top, this points the vanilla backdrop assets at the replacement for the length of
///     one draw call and puts them straight back, which keeps everything else vanilla owns -- item
///     scaling and animation, stack counts, shimmer, hover -- and changes only the frame underneath.
/// </remarks>
internal static class SlotFrameOverride
{
    private static FieldInfo[]? backdrops;
    private static Asset<Texture2D>[]? saved;

    /// <summary>
    ///     <see cref="ItemSlot.Draw" /> with <paramref name="frame" /> standing in for the slot
    ///     backdrop. A null or still-loading frame falls through to the vanilla appearance, so a
    ///     missing asset costs a wrong-looking slot rather than an exception mid-draw.
    /// </summary>
    public static void Draw(SpriteBatch spriteBatch, ref Item item, int context, Vector2 position, Asset<Texture2D>? frame)
    {
        if (frame is not { IsLoaded: true })
        {
            ItemSlot.Draw(spriteBatch, ref item, context, position);

            return;
        }

        FieldInfo[] fields = Backdrops();
        saved ??= new Asset<Texture2D>[fields.Length];

        for (int i = 0; i < fields.Length; i++)
        {
            saved[i] = (Asset<Texture2D>)fields[i].GetValue(null)!;
            fields[i].SetValue(null, frame);
        }

        // Restoring in a finally matters more than usual here: these are global statics, so an
        // exception escaping mid-swap would leave every inventory in the game wearing this frame.
        try
        {
            ItemSlot.Draw(spriteBatch, ref item, context, position);
        }
        finally
        {
            for (int i = 0; i < fields.Length; i++)
                fields[i].SetValue(null, saved[i]);
        }
    }

    /// <summary>
    ///     Every vanilla slot-backdrop asset, found once by name. <see cref="ItemSlot.Draw" />
    ///     chooses among these from the slot context by rules we do not own and which shift between
    ///     versions, so all of them are redirected rather than trying to predict which one it will
    ///     reach for.
    /// </summary>
    private static FieldInfo[] Backdrops() => backdrops ??= typeof(TextureAssets)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.FieldType == typeof(Asset<Texture2D>) && field.Name.StartsWith("InventoryBack"))
        .ToArray();
}
