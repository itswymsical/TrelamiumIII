using Microsoft.Xna.Framework.Input;
using Terraria.GameInput;
using Terraria.UI;

namespace TrelamiumIII.Core.Books.Authoring;

/// <summary>Owns the authoring desk (a dev tool opened by <c>/write</c>): whether it's up, and who has the screen.</summary>
/// <remarks>Uses <see cref="IngameFancyUI" />, like the Bestiary, so the inventory closes and other layers stop taking input.</remarks>
public sealed class AuthoringSystem : ModSystem
{
    private static AuthoringUIState? state;

    public static bool Open { get; private set; }

    public override void Load()
    {
        if (Main.dedServ) return;

        state = new AuthoringUIState();
        state.Activate();
    }

    public override void Unload()
    {
        state?.Detach();
        state = null;
        Open = false;
    }

    public static void Show()
    {
        if (state is null || Open) return;

        Open = true;
        state.Attach();
        IngameFancyUI.OpenUIState(state);
    }

    /// <summary>Asks the desk to fade out. It calls <see cref="Finish" /> when it has.</summary>
    public static void Close()
    {
        if (!Open) return;

        state?.BeginClose();
    }

    /// <summary>Tears down for real, once the fade has finished.</summary>
    internal static void Finish()
    {
        if (!Open) return;

        Open = false;
        state?.Detach();

        IngameFancyUI.Close();

        // Left set, the player cannot move once the desk is gone.
        PlayerInput.WritingText = false;
        Main.blockInput = false;
    }

    public override void UpdateUI(GameTime gameTime)
    {
        if (!Open) return;

        // Read here rather than in the editor: the editor treats every key as text, and a buffer
        // that swallows Escape traps the player in the tool.
        if (!Main.keyState.IsKeyDown(Keys.Escape) || Main.oldKeyState.IsKeyDown(Keys.Escape)) return;


        if (state?.DismissTopmost() == true) return;

        Close();
    }
}
