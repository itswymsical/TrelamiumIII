using System.Collections.Generic;

namespace TrelamiumIII.Core.Rarities.Fields;

/// <summary>Ticks every <see cref="FlairField" /> on UI updates and builds their render targets at <c>DoDraw_UpdateCameraPosition</c>.</summary>
public sealed class FlairFieldSystem : ModSystem
{
    private static readonly List<FlairField> fields = [];

    private static bool loaded;

    /// <summary>UI update ticks since load. Advances while the game is paused, unlike world updates.</summary>
    internal static ulong Ticks { get; private set; }

    /// <summary>Draw frames since load, advanced at <c>DoDraw_UpdateCameraPosition</c>.</summary>
    internal static ulong DrawFrame { get; private set; }

    internal static void Register(FlairField field) => fields.Add(field);

    public override void OnModLoad()
    {
        if (Main.dedServ)
            return;

        loaded = true;

        On_Main.DoDraw_UpdateCameraPosition += static orig =>
        {
            orig();

            if (!loaded)
                return;

            DrawFrame++;

            if (Main.gameMenu)
                return;

            foreach (FlairField field in fields)
                field.Accumulate();
        };
    }

    public override void UpdateUI(GameTime gameTime)
    {
        Ticks++;

        foreach (FlairField field in fields)
            field.Tick();
    }

    public override void OnModUnload()
    {
        loaded = false;

        foreach (FlairField field in fields)
            field.Release();

        fields.Clear();
    }
}
