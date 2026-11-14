using System.Collections.Generic;
using System.Text;

namespace TrelamiumIII.Core.Books.Authoring;

/// <summary>Casings the change-case control cycles through.</summary>
public enum TextCase
{
    Sentence,
    Lower,
    Upper,
    Title,
}

/// <summary>
///     An editable string with caret, selection and undo, in character offsets only (no fonts or drawing) so it's testable.
///     <see cref="EditorLayout" /> supplies any visual geometry.
/// </summary>
public sealed class TextBuffer
{
    /// <summary>Snapshots rather than diffs. A block is a paragraph, not a novel.</summary>
    private readonly record struct Snapshot(
        string Text,
        CharFormat[] Formats,
        ParagraphFormat[] Paragraphs,
        int Caret,
        int Anchor);

    private const int HISTORY_LIMIT = 256;

    private readonly List<Snapshot> undo = [];
    private readonly List<Snapshot> redo = [];

    private StringBuilder text = new();

    /// <summary>
    ///     One format per character, always as long as the text. A parallel array keeps edits simple; runs are rebuilt on serialise.
    /// </summary>
    private readonly List<CharFormat> formats = [];

    /// <summary>Paragraph settings per character, always as long as the text, so splices and merges need no bookkeeping.</summary>
    private readonly List<ParagraphFormat> paragraphs = [];

    /// <summary>
    ///     Caret offset of the last typed character, or -1 if the last edit wasn't typing; collapses keystrokes into one undo step.
    /// </summary>
    private int typingRunEnd = -1;

    public TextBuffer(string? initial = null) => Reset(initial ?? string.Empty);

    /// <summary>Loads a block: plain text plus the formatting parsed off its tags.</summary>
    public TextBuffer(string text, IReadOnlyList<CharFormat> formats) => Reset(text, formats);

    /// <summary>The per-character formatting, for serialising back out to tags.</summary>
    public IReadOnlyList<CharFormat> Formats => formats;

    /// <summary>Format armed for the next typed character when set with no selection; cleared by any caret move.</summary>
    public CharFormat? Pending { get; private set; }

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
    public void Reset(
        string value,
        IReadOnlyList<CharFormat>? loaded = null,
        IReadOnlyList<ParagraphFormat>? loadedParagraphs = null)
    {
        text = new StringBuilder(value);

        formats.Clear();
        paragraphs.Clear();
        trailing = ParagraphFormat.Body;

        for (int i = 0; i < value.Length; i++)
        {
            formats.Add(loaded is not null && i < loaded.Count ? loaded[i] : CharFormat.Inherit);

            paragraphs.Add(loadedParagraphs is not null && i < loadedParagraphs.Count
                ? loadedParagraphs[i]
                : ParagraphFormat.Body);
        }

        Caret = Anchor = value.Length;
        Pending = null;
        undo.Clear();
        redo.Clear();
        typingRunEnd = -1;
    }

    #region Formatting

    /// <summary>Formatting of the character at an offset.</summary>
    public CharFormat FormatAt(int index) =>
        index >= 0 && index < formats.Count ? formats[index] : CharFormat.Inherit;

    /// <summary>The format the toolbar shows: the selection's first character, or what the caret would inherit.</summary>
    public CharFormat ActiveFormat =>
        Pending ?? (HasSelection ? FormatAt(SelectionStart) : InheritedFormat());

    /// <summary>Whether every selected character satisfies a test, for a toggle's lit state.</summary>
    public bool SelectionAll(Func<CharFormat, bool> predicate)
    {
        if (!HasSelection) return predicate(ActiveFormat);

        for (int i = SelectionStart; i < SelectionEnd; i++)
        {
            if (!predicate(formats[i])) return false;
        }

        return true;
    }

    /// <summary>Applies a format change to the selection, or arms it for the next keystroke.</summary>
    public void ApplyFormat(Func<CharFormat, CharFormat> change)
    {
        if (!HasSelection)
        {
            Pending = change(ActiveFormat);

            return;
        }

        PushUndo();

        for (int i = SelectionStart; i < SelectionEnd; i++)
            formats[i] = change(formats[i]);

        typingRunEnd = -1;
    }

    /// <summary>What typed text inherits: the character to the left, or at the very start the one to the right.</summary>
    private CharFormat InheritedFormat()
    {
        if (HasSelection) return FormatAt(SelectionStart);
        if (Caret > 0) return FormatAt(Caret - 1);

        return FormatAt(0);
    }

    /// <summary>Keeps both parallel arrays the same length as the text after a splice.</summary>
    private void SpliceFormats(int at, int removed, int inserted, CharFormat fill) =>
        SpliceFormats(at, removed, inserted, fill, OwningFormatAt(at));

    private void SpliceFormats(int at, int removed, int inserted, CharFormat fill, ParagraphFormat owning)
    {
        if (removed > 0)
        {
            formats.RemoveRange(at, removed);
            paragraphs.RemoveRange(at, removed);
        }

        for (int i = 0; i < inserted; i++)
        {
            formats.Insert(at + i, fill);
            paragraphs.Insert(at + i, owning);
        }
    }

    #endregion

    #region Paragraphs

    /// <summary>Paragraph settings of the character at an offset.</summary>
    public ParagraphFormat ParagraphAt(int index)
    {
        if (paragraphs.Count == 0) return trailing;

        return paragraphs[Math.Clamp(index, 0, paragraphs.Count - 1)];
    }

    /// <summary>Settings of an empty final paragraph, which has no characters to hold them (e.g. right after Return at the end).</summary>
    private ParagraphFormat trailing = ParagraphFormat.Body;

    /// <summary>Every paragraph in reading order. Always at least one.</summary>
    public IEnumerable<Paragraph> Paragraphs()
    {
        int start = 0;
        int index = 0;

        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] != '\n') continue;

            yield return new Paragraph(start, i - start, index++, ParagraphAt(start));

            start = i + 1;
        }

        int length = text.Length - start;

        yield return new Paragraph(start, length, index, length == 0 ? trailing : ParagraphAt(start));
    }

    /// <summary>Settings of the paragraph at an offset, including the empty trailing one that <see cref="ParagraphAt" /> misses.</summary>
    private ParagraphFormat OwningFormatAt(int index)
    {
        foreach (Paragraph paragraph in Paragraphs())
        {
            if (index <= paragraph.End) return paragraph.Format;
        }

        return trailing;
    }

    /// <summary>The paragraph the caret sits in.</summary>
    public Paragraph CurrentParagraph()
    {
        foreach (Paragraph paragraph in Paragraphs())
        {
            if (Caret <= paragraph.End) return paragraph;
        }

        return new Paragraph(0, text.Length, 0, ParagraphAt(0));
    }

    /// <summary>Applies a change to every paragraph the selection touches, or to the caret's own.</summary>
    public void ApplyParagraph(Func<ParagraphFormat, ParagraphFormat> change)
    {
        PushUndo();

        int from = HasSelection ? SelectionStart : Caret;
        int to = HasSelection ? SelectionEnd : Caret;

        foreach (Paragraph paragraph in Paragraphs())
        {
            if (paragraph.End < from || paragraph.Start > to) continue;

            ParagraphFormat updated = change(paragraph.Format);

            // The mark itself is written too, so a paragraph that is later merged carries its
            // settings on the character the merge inherits from.
            for (int i = paragraph.Start; i <= paragraph.End && i < paragraphs.Count; i++)
                paragraphs[i] = updated;

            // A zero-length final paragraph has no characters, so its settings live off to the side.
            if (paragraph.End >= text.Length && paragraph.Length == 0) trailing = updated;
        }

        typingRunEnd = -1;
    }

    /// <summary>Splits the paragraph at the caret; both halves keep its settings.</summary>
    public void SplitParagraph()
    {
        ParagraphFormat owning = OwningFormatAt(HasSelection ? SelectionStart : Caret);

        PushUndo();

        CharFormat applied = Pending ?? InheritedFormat();

        DeleteSelectionCore();
        text.Insert(Caret, '\n');
        SpliceFormats(Caret, 0, 1, applied, owning);
        Caret = Anchor = Caret + 1;
        Pending = null;
        typingRunEnd = -1;

        // Splitting at the very end leaves a new final paragraph with no characters, so its
        // inherited settings have to be parked where an empty trailing paragraph keeps them.
        if (Caret >= text.Length) trailing = owning;
    }

    /// <summary>
    ///     Merges into the previous paragraph when the caret is at a paragraph start, taking the following paragraph's settings.
    ///     False when there's nothing to merge.
    /// </summary>
    public bool MergeParagraphBackwards()
    {
        if (HasSelection || Caret == 0 || text[Caret - 1] != '\n') return false;

        PushUndo();

        ParagraphFormat surviving = ParagraphAt(Caret);
        int mark = Caret - 1;

        text.Remove(mark, 1);
        formats.RemoveAt(mark);
        paragraphs.RemoveAt(mark);
        Caret = Anchor = mark;

        // Everything now sharing the merged paragraph adopts the surviving mark's settings.
        Paragraph merged = CurrentParagraph();

        for (int i = merged.Start; i < merged.End && i < paragraphs.Count; i++)
            paragraphs[i] = surviving;

        if (merged.End < paragraphs.Count) paragraphs[merged.End] = surviving;

        typingRunEnd = -1;

        return true;
    }

    #endregion

    #region Editing

    /// <summary>Types text at the caret, replacing the selection.</summary>
    /// <param name="coalesce">True for keystrokes, which merge into the previous undo step; false for pastes.</param>
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

        // Resolve both before the text changes: a replacement takes the replaced text's format, and growing an empty
        // trailing paragraph would hide its off-side settings.
        CharFormat applied = Pending ?? InheritedFormat();
        ParagraphFormat owning = OwningFormatAt(HasSelection ? SelectionStart : Caret);

        DeleteSelectionCore();
        text.Insert(Caret, value);
        SpliceFormats(Caret, 0, value.Length, applied, owning);
        Caret = Anchor = Caret + value.Length;
        Pending = null;

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
            SpliceFormats(Caret - 1, 1, 0, CharFormat.Inherit);
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
        {
            DeleteSelectionCore();
        }
        else
        {
            text.Remove(Caret, 1);
            SpliceFormats(Caret, 1, 0, CharFormat.Inherit);
        }

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
        int length = SelectionEnd - start;

        text.Remove(start, length);
        SpliceFormats(start, length, 0, CharFormat.Inherit);
        Caret = Anchor = start;
    }

    #endregion

    #region Movement

    /// <summary>Places the caret at an absolute offset, extending the selection when asked.</summary>
    public void MoveTo(int index, bool extend)
    {
        Caret = Math.Clamp(index, 0, text.Length);

        if (!extend) Anchor = Caret;

        Pending = null;
        typingRunEnd = -1;
    }

    /// <summary>Moves one character; an unextended move with a selection collapses to its edge instead.</summary>
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

    /// <summary>Recases the selection, or the word under the caret, keeping the selection so the hotkey can cycle.</summary>
    public void ChangeCase(TextCase mode)
    {
        if (!HasSelection) SelectWordAt(Caret);
        if (!HasSelection) return;

        int start = SelectionStart;
        int end = SelectionEnd;
        string source = text.ToString(start, end - start);
        string result = mode switch
        {
            TextCase.Upper    => source.ToUpperInvariant(),
            TextCase.Lower    => source.ToLowerInvariant(),
            TextCase.Title    => Title(source),
            TextCase.Sentence => Sentence(source),
            _                 => source,
        };

        if (result == source) return;

        PushUndo();

        // Formats carry one-to-one only when recasing preserved length (not guaranteed across cultures); otherwise the
        // whole range takes its opening format.
        if (result.Length == end - start)
        {
            text.Remove(start, end - start);
            text.Insert(start, result);
        }
        else
        {
            CharFormat opening = FormatAt(start);

            text.Remove(start, end - start);
            text.Insert(start, result);
            SpliceFormats(start, end - start, result.Length, opening);
        }

        // Re-selecting the same span lets the hotkey be pressed again to move to the next case.
        Anchor = start;
        Caret = start + result.Length;
        typingRunEnd = -1;
    }

    private static string Title(string value)
    {
        char[] chars = value.ToCharArray();
        bool atWordStart = true;

        for (int i = 0; i < chars.Length; i++)
        {
            if (char.IsWhiteSpace(chars[i]))
            {
                atWordStart = true;

                continue;
            }

            chars[i] = atWordStart ? char.ToUpperInvariant(chars[i]) : char.ToLowerInvariant(chars[i]);
            atWordStart = false;
        }

        return new string(chars);
    }

    private static string Sentence(string value)
    {
        char[] chars = value.ToLowerInvariant().ToCharArray();
        bool atSentenceStart = true;

        for (int i = 0; i < chars.Length; i++)
        {
            if (chars[i] is '.' or '!' or '?')
            {
                atSentenceStart = true;

                continue;
            }

            if (char.IsWhiteSpace(chars[i])) continue;

            if (atSentenceStart)
            {
                chars[i] = char.ToUpperInvariant(chars[i]);
                atSentenceStart = false;
            }
        }

        return new string(chars);
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

    /// <summary>The current state, formatting included, so undo restores both together.</summary>
    private Snapshot Current => new(text.ToString(), [.. formats], [.. paragraphs], Caret, Anchor);

    private void PushUndo()
    {
        undo.Add(Current);

        if (undo.Count > HISTORY_LIMIT) undo.RemoveAt(0);

        redo.Clear();
    }

    public void Undo()
    {
        if (undo.Count == 0) return;

        Snapshot restore = undo[^1];

        undo.RemoveAt(undo.Count - 1);
        redo.Add(Current);

        Apply(restore);
    }

    public void Redo()
    {
        if (redo.Count == 0) return;

        Snapshot restore = redo[^1];

        redo.RemoveAt(redo.Count - 1);
        undo.Add(Current);

        Apply(restore);
    }

    private void Apply(in Snapshot snapshot)
    {
        text = new StringBuilder(snapshot.Text);

        formats.Clear();
        formats.AddRange(snapshot.Formats);

        paragraphs.Clear();
        paragraphs.AddRange(snapshot.Paragraphs);

        Caret = Math.Clamp(snapshot.Caret, 0, text.Length);
        Anchor = Math.Clamp(snapshot.Anchor, 0, text.Length);
        Pending = null;
        typingRunEnd = -1;
    }

    #endregion
}
