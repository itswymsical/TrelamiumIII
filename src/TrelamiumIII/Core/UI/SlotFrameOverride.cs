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

    private static FieldInfo[] Backdrops() => backdrops ??= typeof(TextureAssets)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.FieldType == typeof(Asset<Texture2D>) && field.Name.StartsWith("InventoryBack"))
        .ToArray();
}
