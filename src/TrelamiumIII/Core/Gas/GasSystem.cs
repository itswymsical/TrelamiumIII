using TrelamiumIII.Core.Gas.Layers;
using System.Collections.Generic;

namespace TrelamiumIII.Core.Gas;

public class GasSystem : ModSystem
{
    public static GasSystem? Instance { get; private set; }

    static readonly List<GasLayer> layers = [];

    public static T GetLayer<T>() where T : GasLayer
    {
        foreach (var layer in layers)
            if (layer is T match) return match;
        return null;
    }

    public override void OnModLoad()
    {
        Instance = this;
        RegisterLayer(new BloodLayer());
        RegisterLayer(new IceMistLayer());
        RegisterLayer(new ThermiteLayer());
        RegisterLayer(new PrismaticStarflaskLayer());
        RegisterLayer(new CorruptedSporeLayer());
        RegisterLayer(new PhytoncideSporeLayer());

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
            layer.UpdateGasNodes();
    }

    static void RegisterLayer(GasLayer layer)
    {
        layer.Load();
        layers.Add(layer);
    }
}