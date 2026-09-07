using System.Collections.Generic;
using System.Linq;
using TrelamiumIII.Core.Gas.Layers;

namespace TrelamiumIII.Core.Gas;

public class GasSystem : ModSystem
{
    public static GasSystem? Instance { get; private set; }

    static readonly List<GasField> layers = [];

    public static T GetLayer<T>() where T : GasField
    {
        foreach (var layer in layers)
            if (layer is T match) return match;
        return null;
    }

    public override void OnModLoad()
    {
        Instance = this;

        List<GasField> discovered = [];

        foreach (Type type in Mod.Code.GetTypes())
        {
            if (type.IsAbstract || !type.IsSubclassOf(typeof(GasField)))
                continue;

            discovered.Add((GasField)Activator.CreateInstance(type));
        }

        foreach (var layer in discovered.OrderBy(l => l.LoadOrder))
            RegisterLayer(layer);

        On_Main.CheckMonoliths += static orig =>
        {
            orig();
            if (Instance == null) return;
            foreach (var layer in layers)
                layer.AccumulateField();
        };

        On_Main.DrawDust += static (orig, self) =>
        {
            orig(self);
            if (Instance == null) return;
            foreach (var layer in layers)
                layer.CompositeToScreen();
        };
    }

    public override void OnModUnload()
    {
        foreach (var layer in layers)
            layer.Unload();

        layers.Clear();
        Instance = null;
    }

    public override void PostUpdateEverything()
    {
        foreach (var layer in layers)
            layer.Update();
    }

    static void RegisterLayer(GasField layer)
    {
        layer.Load();
        layers.Add(layer);
    }
}