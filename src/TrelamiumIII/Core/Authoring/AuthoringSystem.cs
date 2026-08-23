using Microsoft.Xna.Framework.Input;
using Terraria.GameInput;
using Terraria.UI;

namespace TrelamiumIII.Core.Authoring;

/// <summary>Owns the authoring desk: whether it is up, and who has the screen while it is.</summary>
/// <remarks>
///     <para>
///         Handed to <see cref="IngameFancyUI" /> rather than pushed on as an interface layer of its
///         own. That is the route the Bestiary, the achievements list and the keybind menu take, and
///         it is what actually takes the screen: it closes the inventory, stops the other layers
///         drawing and swallows their input. A custom layer draws on top of the inventory but leaves
///         it live underneath, which is why the first version let you rearrange your armour through
///         the editor.
///     </para>
///     <para>
///         A development tool. Only <c>/write</c> opens it, so it never appears in a normal game and
///         is not localized.
///     </para>
/// </remarks>
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

        // Unwinds one layer per press -- picker, prompt, dropdown -- and only closes the desk once
        // nothing is left open, so Escape never discards work the writer could not see.
        if (state?.DismissTopmost() == true) return;

        Close();
    }
}
