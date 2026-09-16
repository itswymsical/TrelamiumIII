using TrelamiumIII.Core.Gas;
using TrelamiumIII.Core.Graphics.Fields;

namespace TrelamiumIII.Content.DevTools;

/// <summary>What the wand does with the layer it is pointed at.</summary>
public enum FieldWandMode
{
    /// <summary>A steady stream of nodes thrown from the player toward the cursor.</summary>
    Spray,

    /// <summary>A small cloud welling outward from the cursor, with no initial direction.</summary>
    Puff,

    /// <summary>A full <see cref="BurstProfile.FlaskShatter" />, as a thrown flask produces.</summary>
    Burst,

    /// <summary>Nodes anchored to the nearest NPC, for testing the cling path.</summary>
    Cling,

    /// <summary>Drops every live node in the layer.</summary>
    Clear,
}

/// <summary>
///     The wand's current layer and mode, shared between the item and its picker.
/// </summary>
/// <remarks>
///     Client-side dev state, so it is a plain static rather than anything on the player: there is
///     one cursor and one panel, and nothing here should ever be sent anywhere.
/// </remarks>
public static class FieldWandSelection
{
    private static int index;

    public static FieldWandMode Mode { get; private set; }

    /// <summary>The layer the wand acts on, or null before any layer has registered.</summary>
    public static FieldLayer? Layer
        => FieldSystem.Layers.Count == 0 ? null : FieldSystem.Layers[Math.Clamp(index, 0, FieldSystem.Layers.Count - 1)];

    public static int Index => index;

    public static void Select(int layerIndex)
    {
        index = layerIndex;

        // Burst and Cling only mean anything on a layer that has emitters or contact hooks, so
        // moving to a visual layer while one is selected would silently do nothing.
        if (Layer is not GasField && Mode is FieldWandMode.Burst)
            Mode = FieldWandMode.Spray;
    }

    public static void CycleMode()
    {
        FieldWandMode next = Mode;

        do
        {
            next = (FieldWandMode)(((int)next + 1) % (Enum.GetValues<FieldWandMode>().Length));
        }
        while (!Supports(next));

        Mode = next;
    }

    /// <summary>Whether the selected layer can do anything with this mode.</summary>
    public static bool Supports(FieldWandMode mode)
        => mode is not FieldWandMode.Burst || Layer is GasField;

    /// <summary>Name shown in the picker. The type name is the only identity a layer has.</summary>
    public static string NameOf(FieldLayer layer) => layer.GetType().Name;
}
