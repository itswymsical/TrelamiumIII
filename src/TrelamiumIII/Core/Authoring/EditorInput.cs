using System.Collections.Generic;
using Microsoft.Xna.Framework.Input;
using ReLogic.OS;
using Terraria.GameInput;

namespace TrelamiumIII.Core.Authoring;

/// <summary>
///     Turns a frame of keyboard state into buffer edits. Characters come from <see cref="TextInputEXT" /> to respect layouts, dead keys
///     and IME; they're queued from the message pump and drained on update.
/// </summary>
public sealed class EditorInput
{
    /// <summary>Frames a key is held before it starts repeating, then frames between repeats.</summary>
    private const int REPEAT_DELAY = 26;

    private const int REPEAT_RATE = 2;

    private readonly List<char> pending = [];
    private readonly Dictionary<Keys, int> held = [];

    private bool attached;

    /// <summary>Set when an edit happened this frame, so the caller can invalidate its layout.</summary>
    public bool Dirty { get; private set; }

    /// <summary>True while the buffer holds a one-line value rather than prose.</summary>
    public bool SingleLine { get; set; }

    /// <summary>Set for one frame when Return was pressed on a one-line field.</summary>
    public bool Submitted { get; private set; }

    /// <summary>Sticky caret column for vertical moves, in layout pixels; reset by horizontal moves.</summary>
    private float desiredX;

    private bool desiredXValid;

    /// <summary>Where the Shift+F3 cycle currently sits, so repeated presses walk the casings.</summary>
    private TextCase caseCycle = TextCase.Sentence;

    public void Attach()
    {
        if (attached) return;

        TextInputEXT.TextInput += Queue;
        TextInputEXT.StartTextInput();
        attached = true;
    }

    public void Detach()
    {
        if (!attached) return;

        TextInputEXT.TextInput -= Queue;
        TextInputEXT.StopTextInput();
        pending.Clear();
        held.Clear();
        attached = false;
    }

    private void Queue(char character)
    {
        // The pump delivers control codes here too; those arrive as key presses instead.
        if (!char.IsControl(character)) pending.Add(character);
    }

    /// <summary>Applies one frame of input. <paramref name="layout" /> supplies visual geometry.</summary>
    public void Update(TextBuffer buffer, EditorLayout layout)
    {
        Dirty = false;
        Submitted = false;

        // Holds off Terraria's own bindings, so typing 'i' does not open the inventory.
        PlayerInput.WritingText = true;
        Main.blockInput = true;

        bool control = Down(Keys.LeftControl) || Down(Keys.RightControl);
        bool shift = Down(Keys.LeftShift) || Down(Keys.RightShift);

        if (control) HandleShortcuts(buffer, shift);

        HandleNavigation(buffer, layout, control, shift);
        HandleDeletion(buffer);

        // Characters are dropped while a modifier is down: Ctrl+V must paste, not type a 'v'.
        if (!control && pending.Count > 0)
        {
            foreach (char character in pending) buffer.Insert(character.ToString(), coalesce: true);

            MarkDirty();
        }

        // On a one-line field Return commits instead of breaking the line, and Tab has nothing
        // sensible to insert. The host reads Submitted and moves focus off.
        if (Pressed(Keys.Enter))
        {
            if (SingleLine)
            {
                Submitted = true;
            }
            else
            {
                // Splits the paragraph rather than inserting a bare newline, so both halves keep the
                // kind and alignment the writer set.
                buffer.SplitParagraph();
                MarkDirty();
            }
        }

        if (!SingleLine && Pressed(Keys.Tab))
        {
            buffer.Insert("\t");
            MarkDirty();
        }

        // Shift+F3 cycles casing, the shortcut a writer coming from Word will already have.
        if (shift && Pressed(Keys.F3))
        {
            caseCycle = (TextCase)(((int)caseCycle + 1) % Enum.GetValues<TextCase>().Length);

            buffer.ChangeCase(caseCycle);
            MarkDirty();
        }

        pending.Clear();
        Tick();
    }

    private void HandleShortcuts(TextBuffer buffer, bool shift)
    {
        IClipboard clipboard = Platform.Get<IClipboard>();

        if (Pressed(Keys.A)) buffer.SelectAll();

        if (Pressed(Keys.C) && buffer.HasSelection) clipboard.Value = buffer.SelectedText;

        if (Pressed(Keys.X) && buffer.HasSelection)
        {
            clipboard.Value = buffer.CutSelection();
            MarkDirty();
        }

        if (Pressed(Keys.V))
        {
            string paste = clipboard.MultiLineValue ?? string.Empty;

            if (paste.Length > 0)
            {
                // Normalise line endings; a Windows clipboard hands back \r\n and the layout counts
                // a stray \r as a character with no advance, which desynchronises the caret.
                buffer.Insert(paste.Replace("\r\n", "\n").Replace('\r', '\n'));
                MarkDirty();
            }
        }

        // Ctrl+Shift+Z redoes, matching the convention Ctrl+Y also follows.
        if (Pressed(Keys.Z))
        {
            if (shift) buffer.Redo();
            else buffer.Undo();

            MarkDirty();
        }

        if (Pressed(Keys.Y))
        {
            buffer.Redo();
            MarkDirty();
        }
    }

    private void HandleNavigation(TextBuffer buffer, EditorLayout layout, bool control, bool shift)
    {
        if (Repeats(Keys.Left))
        {
            if (control) buffer.MoveByWord(-1, shift);
            else buffer.MoveByChar(-1, shift);

            desiredXValid = false;
        }

        if (Repeats(Keys.Right))
        {
            if (control) buffer.MoveByWord(1, shift);
            else buffer.MoveByChar(1, shift);

            desiredXValid = false;
        }

        if (Repeats(Keys.Up)) MoveVertical(buffer, layout, -1, shift);
        if (Repeats(Keys.Down)) MoveVertical(buffer, layout, 1, shift);

        if (Pressed(Keys.Home))
        {
            buffer.MoveTo(control ? 0 : layout.LineStartOf(buffer.Caret), shift);
            desiredXValid = false;
        }

        if (Pressed(Keys.End))
        {
            buffer.MoveTo(control ? buffer.Length : layout.LineEndOf(buffer.Caret), shift);
            desiredXValid = false;
        }
    }

    private void MoveVertical(TextBuffer buffer, EditorLayout layout, int direction, bool shift)
    {
        if (!desiredXValid)
        {
            desiredX = layout.XOf(buffer.Caret);
            desiredXValid = true;
        }

        buffer.MoveTo(layout.MoveVertical(buffer.Caret, direction, desiredX), shift);
    }

    private void HandleDeletion(TextBuffer buffer)
    {
        if (Repeats(Keys.Back))
        {
            // Backspacing onto a paragraph mark merges the two paragraphs; anything else is an
            // ordinary character deletion.
            if (!buffer.MergeParagraphBackwards()) buffer.Backspace();

            MarkDirty();
        }

        if (Repeats(Keys.Delete))
        {
            buffer.Delete();
            MarkDirty();
        }
    }

    private void MarkDirty()
    {
        Dirty = true;
        desiredXValid = false;
    }

    #region Key state

    private static bool Down(Keys key) => Main.keyState.IsKeyDown(key);

    private static bool Pressed(Keys key) => Main.keyState.IsKeyDown(key) && !Main.oldKeyState.IsKeyDown(key);

    /// <summary>True on the initial press, then again on the repeat cadence while held.</summary>
    private bool Repeats(Keys key)
    {
        if (!Main.keyState.IsKeyDown(key))
        {
            held.Remove(key);

            return false;
        }

        if (!held.TryGetValue(key, out int frames))
        {
            held[key] = 0;

            return true;
        }

        return frames >= REPEAT_DELAY && (frames - REPEAT_DELAY) % REPEAT_RATE == 0;
    }

    /// <summary>Ages the held-key counters. Called once per frame, after every query.</summary>
    private void Tick()
    {
        foreach (Keys key in new List<Keys>(held.Keys))
        {
            if (Main.keyState.IsKeyDown(key)) held[key]++;
            else held.Remove(key);
        }
    }

    #endregion
}
