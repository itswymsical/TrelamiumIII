using System.Collections.Generic;
using System.Text;

namespace TrelamiumIII.Core.Authoring;

/// <summary>
///     An editable string with a caret, a selection and an undo history.
/// </summary>
/// <remarks>
///     <para>
///         Deliberately knows nothing about fonts, wrapping or drawing. Everything here is expressed
///         in character offsets, so it can be exercised without a graphics device -- which is the only
///         way an editor's edge cases (selection direction, undo coalescing, word jumps over
///         punctuation) get tested at all.
///     </para>
///     <para>
///         Anything needing visual geometry -- caret up and down a wrapped line, Home and End -- takes
///         the line bounds as arguments and stays out of here. <see cref="EditorLayout" /> supplies
///         them.
///     </para>
/// </remarks>
public sealed class TextBuffer
{
    /// <summary>Snapshots rather than diffs. A block is a paragraph, not a novel.</summary>
    private readonly record struct Snapshot(string Text, int Caret, int Anchor);

    private const int HISTORY_LIMIT = 256;

    private readonly List<Snapshot> undo = [];
    private readonly List<Snapshot> redo = [];

    private StringBuilder text = new();

    /// <summary>
    ///     Caret offset the last typed character landed at, or -1 when the last edit was not typing.
    ///     Lets a run of keystrokes collapse into one undo step.
    /// </summary>
    private int typingRunEnd = -1;

    public TextBuffer(string? initial = null) => Reset(initial ?? string.Empty);

    public string Text => text.ToString();

    public int Length => text.Length;

    /// <summary>Where the next character lands.</summary>
    public int Caret { get; private set; }

    /// <summary>The fixed end of the selection. Equal to <see cref="Caret" /> when nothing is selected.</summary>
    public int Anchor { get; private set; }

    public bool HasSelection => Caret != Anchor;

    public int SelectionStart => Math.Min(Caret, Anchor);

    public int SelectionEnd => Math.Max(Caret, Anchor);

    public string SelectedText =>
        HasSelection ? text.ToString(SelectionStart, SelectionEnd - SelectionStart) : string.Empty;

    public bool CanUndo => undo.Count > 0;

    public bool CanRedo => redo.Count > 0;

    /// <summary>Replaces the contents and clears the history. For loading a different block.</summary>
    public void Reset(string value)
    {
        text = new StringBuilder(value);
        Caret = Anchor = value.Length;
        undo.Clear();
        redo.Clear();
        typingRunEnd = -1;
    }

    #region Editing

    /// <summary>Types text at the caret, replacing the selection.</summary>
    /// <param name="coalesce">
    ///     True for single keystrokes, which merge into the previous undo step so one Ctrl+Z does not
    ///     undo one letter. False for pastes and anything programmatic.
    /// </param>
    public void Insert(string value, bool coalesce = false)
    {
        if (value.Length == 0) return;

        // A run breaks at whitespace so undo lands on word boundaries, which is where a writer
        // expects it, and breaks whenever the caret moved since the last keystroke.
        bool continuesRun = coalesce
                            && typingRunEnd == Caret
                            && !HasSelection
                            && value.Length == 1
                            && !char.IsWhiteSpace(value[0]);

        if (!continuesRun) PushUndo();

        DeleteSelectionCore();
        text.Insert(Caret, value);
        Caret = Anchor = Caret + value.Length;

        typingRunEnd = coalesce && !char.IsWhiteSpace(value[^1]) ? Caret : -1;
    }

    /// <summary>Backspace. Deletes the selection if there is one, otherwise the character before the caret.</summary>
    public void Backspace()
    {
        if (!HasSelection && Caret == 0) return;

        PushUndo();

        if (HasSelection)
        {
            DeleteSelectionCore();
        }
        else
        {
            text.Remove(Caret - 1, 1);
            Caret = Anchor = Caret - 1;
        }

        typingRunEnd = -1;
    }

    /// <summary>Forward delete.</summary>
    public void Delete()
    {
        if (!HasSelection && Caret >= text.Length) return;

        PushUndo();

        if (HasSelection)
            DeleteSelectionCore();
        else
            text.Remove(Caret, 1);

        typingRunEnd = -1;
    }

    /// <summary>Removes the selection and returns what it held, for Cut.</summary>
    public string CutSelection()
    {
        if (!HasSelection) return string.Empty;

        string cut = SelectedText;

        PushUndo();
        DeleteSelectionCore();
        typingRunEnd = -1;

        return cut;
    }

    private void DeleteSelectionCore()
    {
        if (!HasSelection) return;

        int start = SelectionStart;

        text.Remove(start, SelectionEnd - start);
        Caret = Anchor = start;
    }

    #endregion

    #region Movement

    /// <summary>Places the caret at an absolute offset, extending the selection when asked.</summary>
    public void MoveTo(int index, bool extend)
    {
        Caret = Math.Clamp(index, 0, text.Length);

        if (!extend) Anchor = Caret;

        typingRunEnd = -1;
    }

    /// <summary>
    ///     Left or right by one character. An unextended move with a selection collapses to its edge
    ///     rather than stepping, which is what every editor does and what stops an arrow key from
    ///     losing the selection boundary.
    /// </summary>
    public void MoveByChar(int direction, bool extend)
    {
        if (HasSelection && !extend)
        {
            MoveTo(direction < 0 ? SelectionStart : SelectionEnd, false);

            return;
        }

        MoveTo(Caret + Math.Sign(direction), extend);
    }

    /// <summary>Ctrl+Left / Ctrl+Right: over whitespace, then over the run of word characters.</summary>
    public void MoveByWord(int direction, bool extend)
    {
        int index = Caret;

        if (direction < 0)
        {
            while (index > 0 && char.IsWhiteSpace(text[index - 1])) index--;
            while (index > 0 && !char.IsWhiteSpace(text[index - 1])) index--;
        }
        else
        {
            while (index < text.Length && char.IsWhiteSpace(text[index])) index++;
            while (index < text.Length && !char.IsWhiteSpace(text[index])) index++;
        }

        MoveTo(index, extend);
    }

    public void SelectAll()
    {
        Anchor = 0;
        Caret = text.Length;
        typingRunEnd = -1;
    }

    public void SelectRange(int start, int end)
    {
        Anchor = Math.Clamp(start, 0, text.Length);
        Caret = Math.Clamp(end, 0, text.Length);
        typingRunEnd = -1;
    }

    /// <summary>Selects the word under an offset, for double-click.</summary>
    public void SelectWordAt(int index)
    {
        index = Math.Clamp(index, 0, text.Length);

        int start = index;
        int end = index;

        while (start > 0 && !char.IsWhiteSpace(text[start - 1])) start--;
        while (end < text.Length && !char.IsWhiteSpace(text[end])) end++;

        SelectRange(start, end);
    }

    #endregion

    #region History

    private void PushUndo()
    {
        undo.Add(new Snapshot(text.ToString(), Caret, Anchor));

        if (undo.Count > HISTORY_LIMIT) undo.RemoveAt(0);

        redo.Clear();
    }

    public void Undo()
    {
        if (undo.Count == 0) return;

        Snapshot restore = undo[^1];

        undo.RemoveAt(undo.Count - 1);
        redo.Add(new Snapshot(text.ToString(), Caret, Anchor));

        Apply(restore);
    }

    public void Redo()
    {
        if (redo.Count == 0) return;

        Snapshot restore = redo[^1];

        redo.RemoveAt(redo.Count - 1);
        undo.Add(new Snapshot(text.ToString(), Caret, Anchor));

        Apply(restore);
    }

    private void Apply(in Snapshot snapshot)
    {
        text = new StringBuilder(snapshot.Text);
        Caret = Math.Clamp(snapshot.Caret, 0, text.Length);
        Anchor = Math.Clamp(snapshot.Anchor, 0, text.Length);
        typingRunEnd = -1;
    }

    #endregion
}
