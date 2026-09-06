using System.Collections.Generic;
using System.Linq;
using TrelamiumIII.Core.Gas;
using TrelamiumIII.Core.Graphics.Metaballs.Layers;

namespace TrelamiumIII.Core.Graphics.Metaballs;

/// <summary>
///     Owns every <see cref="MetaballField" /> and drives it through the frame.
/// </summary>
/// <remarks>
///     The two draw hooks are not interchangeable.
///     <list type="bullet">
///         <item>
///             <b>CheckMonoliths</b> is the one point in the frame where Terraria expects mods to
///             be binding their own render targets as it runs before the scene is drawn and
///             nothing has claimed the backbuffer yet. Building the field anywhere later would
///             mean stealing a target mid-scene and putting back whatever was there.
///         </item>
///         <item>
///             <b>DrawDust</b> puts the composite in front of tiles, NPCs and projectiles but
///             behind the interface, which is where a cloud of spores belongs.
///         </item>
///     </list>
/// </remarks>
public sealed class MetaballSystem : ModSystem
{
    private static readonly List<MetaballField> layers = [];

    private static bool loaded;

    public static T? GetLayer<T>() where T : MetaballField
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
        List<MetaballField> discovered = [];

        foreach (Type type in Mod.Code.GetTypes())
        {
            if (type.IsAbstract || !type.IsSubclassOf(typeof(MetaballField)))
                continue;

            discovered.Add((MetaballField)Activator.CreateInstance(type));
        }

        foreach (var layer in discovered)
            RegisterLayer(layer);

        loaded = true;

        if (Main.dedServ)
            return;

        On_Main.CheckMonoliths += static orig =>
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
        if (Main.dedServ)
            return;

        foreach (var layer in layers)
            layer.UpdateNodes();
    }

    private static void RegisterLayer(MetaballField layer)
    {
        layer.Load();
        layers.Add(layer);
    }
}
