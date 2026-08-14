using System.Collections.Generic;
using System.Linq;
using Terraria.UI;

namespace TrelamiumIII.Core.Graphics.Cinematics;

public class CutsceneSystem : ModSystem
{
    public static Cutscene CurrentCutscene { get; private set; }

    private static readonly string[] UILayersToHide =
    [
        "Vanilla: Inventory",
        "Vanilla: Hotbar",
        "Vanilla: Resource Bars",
        "Vanilla: Map / Minimap",
        "Vanilla: Info Accessories Bar",
        "Vanilla: Builder Accessories Bar",
        "Vanilla: Settings Button",
        "Vanilla: Mouse Over",
        "Vanilla: Radial Hotbars",
        "Vanilla: Player Chat",
        "Vanilla: Cursor",
        "Vanilla: Laser Ruler",
        "Vanilla: Gamepad Lock On",
        "Vanilla: Tile Grid Option",
        // Add this mod's own interface layers here, e.g. "TrelamiumIII: My Layer"
    ];

    public override void Load()
    {
        On_Main.DrawInterface += DrawCutscene;
        On_Main.DoDraw += UpdateCutscene;
    }

    public override void Unload()
    {
        On_Main.DrawInterface -= DrawCutscene;
        On_Main.DoDraw -= UpdateCutscene;
    }

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        if (CurrentCutscene != null)
        {
            foreach (var layer in layers)
            {
                if (UILayersToHide.Contains(layer.Name))
                {
                    layer.Active = false;
                }
                else if (layer.Name == "Vanilla: NPC / Sign Dialog" ||
                         layer.Name == "Vanilla: Achievement Complete Popups")
                {
                    layer.Active = true;
                }
            }
        }
    }
    /// <summary>
    /// Updates the current cutscene each frame.
    /// </summary>
    /// <param name="orig"></param>
    /// <param name="self"></param>
    /// <param name="gameTime"></param>
    private void UpdateCutscene(On_Main.orig_DoDraw orig, Main self, GameTime gameTime)
    {
        orig(self, gameTime);

        if (CurrentCutscene != null)
        {
            CurrentCutscene.Update(gameTime);

            if (!CurrentCutscene.IsPlaying)
            {
                CurrentCutscene = null;
            }
        }
    }

    private void DrawCutscene(On_Main.orig_DrawInterface orig, Main self, GameTime gameTime)
    {
        orig(self, gameTime); // draw vanilla UI first

        if (CurrentCutscene != null)
        {
            Main.spriteBatch.Begin(
                SpriteSortMode.Immediate,
                BlendState.AlphaBlend,
                SamplerState.LinearClamp,
                DepthStencilState.None,
                Main.Rasterizer,
                null,
                Main.UIScaleMatrix
            );

            CurrentCutscene.Draw(Main.spriteBatch);

            Main.spriteBatch.End();
        }
    }

    /// <summary>
    /// Starts playing a new cutscene.
    /// </summary>
    /// <param name="cutscene">The cutscene to play.</param>
    public static void PlayCutscene<T>() where T : Cutscene, new()
    {
        try
        {
            if (CurrentCutscene != null)
            {
                ModContent.GetInstance<TrelamiumIII>().Logger.Warn("Attempting to start a new cutscene while one is already in progress. Ending the current cutscene.");
                CurrentCutscene.End();
            }
            CurrentCutscene = new T();
            CurrentCutscene.Start();
        }
        catch (Exception ex)
        {
            ModContent.GetInstance<TrelamiumIII>().Logger.Error("Error playing cutscene: " + ex.Message);
        }
    }

    public static void EndCurrentCutscene()
    {
        if (CurrentCutscene != null)
        {
            CurrentCutscene.End();
            CurrentCutscene = null;
        }
    }
}
