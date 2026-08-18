using System.Collections.Generic;
using System.Text;

namespace TrelamiumIII.Core.Authoring;

/// <summary>Casings the change-case control cycles through.</summary>
public enum TextCase
{
    Sentence,
    Lower,
    Upper,
    Title,
}

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
    ///     One format per character, always exactly as long as <see cref="text" />.
    /// </summary>
    /// <remarks>
    ///     A parallel array rather than a list of styled runs. Runs make every edit a
    ///     boundary-remapping problem -- split this one, merge those two, coalesce the result --
    ///     whereas here an insert or a delete is simply the same splice applied twice. Runs are
    ///     recovered by grouping equal neighbours when the text is serialised or drawn, which is the
    ///     only place they are actually needed.
    /// </remarks>
    private readonly List<CharFormat> formats = [];

    /// <summary>
    ///     The paragraph settings each character belongs to, again exactly as long as the text.
    /// </summary>
    /// <remarks>
    ///     Stored per character rather than per paragraph for the same reason the character formats
    ///     are: a paragraph's identity moves with every edit, so any structure keyed by position needs
    ///     resyncing after each one, whereas a parallel array is spliced by the same code that splices
    ///     the text. Every character of a paragraph holds the same value, which is redundant and
    ///     cheap -- and it means a merge is a range overwrite rather than a bookkeeping problem.
    /// </remarks>
    private readonly List<ParagraphFormat> paragraphs = [];

    /// <summary>
    ///     Caret offset the last typed character landed at, or -1 when the last edit was not typing.
    ///     Lets a run of keystrokes collapse into one undo step.
    /// </summary>
    private int typingRunEnd = -1;

    public TextBuffer(string? initial = null) => Reset(initial ?? string.Empty);

    /// <summary>Loads a block: plain text plus the formatting parsed off its tags.</summary>
    public TextBuffer(string text, IReadOnlyList<CharFormat> formats) => Reset(text, formats);

    /// <summary>The per-character formatting, for serialising back out to tags.</summary>
    public IReadOnlyList<CharFormat> Formats => formats;

    /// <summary>
    ///     Formatting the next typed character will take, when the writer set it with nothing selected.
    /// </summary>
    /// <remarks>
    ///     Toggling bold with a selection formats that selection. Toggling it with none is a statement
    ///     about what comes next, which has to be held somewhere until a character arrives to wear it.
    ///     Cleared by any caret move, matching what every editor does -- a pending style that survived
    ///     a click would surprise the writer several words later.
    /// </remarks>
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

    /// <summary>
    ///     The formatting the toolbar should show as current: the selection's first character, or the
    ///     one the caret would inherit from.
    /// </summary>
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

    /// <summary>
    ///     Applies a formatting change to the selection, or arms it for the next keystroke.
    /// </summary>
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

    /// <summary>
    ///     What newly typed text inherits when nothing is armed.
    /// </summary>
    /// <remarks>
    ///     The character to the left, so typing on the end of a bold word stays bold. At the very
    ///     start there is nothing to the left, so the character to the right is used instead rather
    ///     than dropping to unformatted and breaking the run the writer is typing into.
    /// </remarks>
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

    /// <summary>
    ///     Settings of the final paragraph while it holds no characters to carry them.
    /// </summary>
    /// <remarks>
    ///     The one gap in storing paragraph settings per character: a paragraph of zero length has no
    ///     characters, so it has nowhere to keep them. That is not an edge case -- it is the paragraph
    ///     you are standing in every time you press Return at the end of the text, and the one you
    ///     then want to make a heading. Only the last paragraph can be empty and unmarked, since an
    ///     empty paragraph anywhere else is still terminated by a mark that can hold its settings.
    /// </remarks>
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

    /// <summary>
    ///     Settings of the paragraph an offset falls in, including the unmarked trailing one.
    /// </summary>
    /// <remarks>
    ///     <see cref="ParagraphAt" /> reads the parallel array directly and clamps, which lands on the
    ///     previous paragraph's last character when asked about an empty trailing paragraph. Insertions
    ///     resolve their owner through here instead so text typed into a fresh heading is a heading.
    /// </remarks>
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

    /// <summary>
    ///     Applies a change to every paragraph the selection touches, or to the caret's own.
    /// </summary>
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

    /// <summary>
    ///     Splits the paragraph at the caret, both halves keeping its settings.
    /// </summary>
    /// <remarks>
    ///     Pressing Return in the middle of a heading gives two headings, which is what the writer
    ///     asked for. Changing one of them afterwards is a separate act.
    /// </remarks>
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
    ///     Merges the paragraph at the caret into the one before it, if the caret is at its start.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The combined paragraph takes the <em>following</em> paragraph's settings, because the
    ///         mark that survives the deletion is the following one and a mark is what carries a
    ///         paragraph's formatting. It is the rule Word follows and it is the only self-consistent
    ///         one: the alternative leaves the surviving mark describing formatting nothing has.
    ///     </para>
    ///     <para>
    ///         Returns false when there is nothing to merge, so the caller can fall back to an
    ///         ordinary backspace.
    ///     </para>
    /// </remarks>
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

        // Both resolved before the text moves. The character format has to be read before the
        // selection goes, since a replacement takes the formatting of what it replaced rather than of
        // whatever ends up left of the collapsed caret. The paragraph has to be read before the
        // insert, because growing the text turns an empty trailing paragraph into a non-empty one and
        // the settings of an empty trailing paragraph live off to the side -- resolving afterwards
        // finds a paragraph that no longer looks empty and falls back to the mark above it.
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

    /// <summary>
    ///     Recases the selection, or the word under the caret when nothing is selected.
    /// </summary>
    /// <remarks>
    ///     Falling back to the word is what makes this usable from a hotkey mid-sentence, and it is
    ///     what every word processor does with Shift+F3. The selection is preserved across the change
    ///     so the shortcut can be pressed repeatedly to cycle.
    /// </remarks>
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

        // Recasing is length-preserving for every casing here, but not guaranteed to be across all
        // cultures, so the formats are only carried through one-to-one when the lengths agree. When
        // they do not, the whole replacement takes the formatting the range opened with -- losing
        // detail inside the range rather than letting the two arrays drift out of step.
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
