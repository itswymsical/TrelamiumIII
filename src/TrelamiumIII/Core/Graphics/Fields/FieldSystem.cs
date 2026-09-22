using System.Collections.Generic;
using System.Linq;

namespace TrelamiumIII.Core.Graphics.Fields;

/// <summary>
///     Owns every <see cref="FieldLayer" /> and drives it through the frame.
/// </summary>
/// <remarks>
///     <para>
///         The two draw hooks are not interchangeable.
///         <list type="bullet">
///             <item>
///                 <b>DoDraw_UpdateCameraPosition</b> is the one point in the frame where Terraria expects mods
///                 to be binding their own render targets, as it runs before the scene is drawn and
///                 nothing has claimed the backbuffer yet. Building the field anywhere later would
///                 mean stealing a target mid-scene and putting back whatever was there.
///             </item>
///             <item>
///                 <b>DrawDust</b> puts the composite in front of tiles, NPCs and projectiles but
///                 behind the interface, which is where a cloud of spores belongs.
///             </item>
///         </list>
///     </para>
///     <para>
///         One system rather than the two this replaces, because two meant two sets of the same
///         hooks and no defined order between them: which family composited on top came down to
///         which <see cref="ModSystem" /> loaded first. Sorting one list by
///         <see cref="FieldLayer.LoadOrder" /> is what makes that answerable.
///     </para>
/// </remarks>
public sealed class FieldSystem : ModSystem
{
    private static readonly List<FieldLayer> layers = [];

    private static bool loaded;

    /// <summary>Every registered layer, in composite order. For tooling that enumerates them.</summary>
    public static IReadOnlyList<FieldLayer> Layers => layers;

    public static T? GetLayer<T>() where T : FieldLayer
    {
        foreach (var layer in layers)
        {
            if (layer is T match)
                return match;
        }

        return null;
    }

    public override void OnModLoad()
    {
        List<FieldLayer> discovered = [];

        foreach (Type type in Mod.Code.GetTypes())
        {
            if (type.IsAbstract || !type.IsSubclassOf(typeof(FieldLayer)))
                continue;

            discovered.Add((FieldLayer)Activator.CreateInstance(type)!);
        }

        foreach (var layer in discovered.OrderBy(layer => layer.LoadOrder))
        {
            layer.Load();
            layers.Add(layer);
        }

        loaded = true;

        if (Main.dedServ)
            return;

        On_Main.DoDraw_UpdateCameraPosition += static orig =>
        {
            orig();

            if (!loaded)
                return;

            foreach (var layer in layers)
                layer.AccumulateField();
        };

        On_Main.DrawDust += static (orig, self) =>
        {
            orig(self);

            if (!loaded)
                return;

            foreach (var layer in layers)
                layer.CompositeToScreen();
        };
    }

    public override void OnModUnload()
    {
        loaded = false;

        foreach (var layer in layers)
            layer.Unload();

        layers.Clear();
    }

    public override void PostUpdateEverything()
    {
        foreach (var layer in layers)
            layer.Update();
    }
}
