using System.Collections.Generic;
using Microsoft.Xna.Framework.Input;
using Terraria.Audio;
using Terraria.GameInput;
using Terraria.UI;
using TrelamiumIII.Common.Systems;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Core.UI.SkillTree;

/// <summary>Owns the skill tree's interface and whether it is up.</summary>
public sealed class SkillTreeSystem : ModSystem
{
    private const float OPEN_SECONDS = 0.22f;
    private const float CLOSE_SECONDS = 0.14f;

    private static UserInterface? _interface;
    private static SkillTreeUI? _ui;

    public static bool Visible { get; private set; }

    /// <summary>0..1 through the open transition. Stays above 0 after <see cref="Close"/> until the fade finishes.</summary>
    public static float Openness { get; private set; }

    /// <summary>Eased <see cref="Openness"/>: the alpha and slide offset every layer of the tree fades on.</summary>
    public static float Fade => EaseFunction.EaseCubicOut.Ease(Openness);

    /// <summary>Whether the tree takes input. False through both transitions, when the panel is still moving.</summary>
    public static bool Interactive => Visible && Openness >= 1f;

    public override void Load()
    {
        if (Main.dedServ)
            return;

        _ui = new SkillTreeUI();
        _ui.Activate();

        _interface = new UserInterface();
    }

    public override void Unload()
    {
        SkillGlyphFlash.Unload();

        _interface = null;
        _ui = null;
        Visible = false;
        Openness = 0f;
    }

    public override void UpdateUI(GameTime gameTime)
    {
        if (!Visible && Openness <= 0f)
            return;

        // Leaving the world drops the tree outright; there is nothing left to fade it over.
        if (Main.gameMenu)
        {
            Visible = false;
            Openness = 0f;
            _interface?.SetState(null);

            return;
        }

        if (Visible && (Main.LocalPlayer.dead || EscapePressed()))
            Close();

        float step = (float)gameTime.ElapsedGameTime.TotalSeconds / (Visible ? OPEN_SECONDS : CLOSE_SECONDS);
        Openness = Math.Clamp(Openness + (Visible ? step : -step), 0f, 1f);

        if (!Visible && Openness <= 0f)
        {
            _interface?.SetState(null);

            return;
        }

        _interface?.Update(gameTime);
    }

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int inventoryIndex = layers.FindIndex(layer => layer.Name == "Vanilla: Inventory");

        if (inventoryIndex == -1)
            return;

        layers.Insert(inventoryIndex + 1, new LegacyGameInterfaceLayer(
            "TrelamiumIII: Skill Tree",
            () =>
            {
                if (Visible || Openness > 0f)
                    _interface?.Draw(Main.spriteBatch, new GameTime());

                return true;
            },
            InterfaceScaleType.UI
        ));
    }

    /// <summary>Escape closes the tree the same way it closes the inventory.</summary>
    private static bool EscapePressed()
        => Main.keyState.IsKeyDown(Keys.Escape) && !Main.oldKeyState.IsKeyDown(Keys.Escape);

    public static void Toggle()
    {
        if (Visible)
            Close();
        else
            Open();
    }

    public static void Open()
    {
        if (_ui is null || _interface is null || Visible)
            return;

        // Reopening mid-close keeps the state that is still fading, so the pan does not jump back.
        if (Openness <= 0f)
        {
            _interface.SetState(_ui);
            _ui.OnOpened();
        }

        Visible = true;

        SoundEngine.PlaySound(SoundID.MenuOpen);
    }

    /// <summary>Starts the close fade. The state is cleared by <see cref="UpdateUI"/> once it finishes.</summary>
    public static void Close()
    {
        if (!Visible)
            return;

        Visible = false;

        SoundEngine.PlaySound(SoundID.MenuClose);
    }
}

/// <summary>Toggles the tree from its keybind.</summary>
public sealed class SkillTreeInput : ModPlayer
{
    public override void ProcessTriggers(TriggersSet triggersSet)
    {
        if (KeybindsSystem.SkillTreeKeybind?.JustPressed == true)
            SkillTreeSystem.Toggle();
    }
}
