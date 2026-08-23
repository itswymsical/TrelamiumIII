using TrelamiumIII.Core.Authoring;

namespace BufTest;

/// <summary>
///     Cover for paragraphs as the buffer models them: enumeration, splitting, merging, and the
///     settings that ride along.
/// </summary>
public static class ParagraphTests
{
    public static int Run()
    {
        int failures = 0;

        void Check(string name, object? actual, object? expected)
        {
            bool ok = Equals(actual?.ToString(), expected?.ToString());

            if (!ok) failures++;

            Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {name}"
                              + (ok ? "" : $"\n         got      <{actual}>\n         expected <{expected}>"));
        }

        void Paired(string name, TextBuffer buffer)
        {
            Check($"{name}: formats paired", buffer.Formats.Count, buffer.Length);

            int walked = 0;

            foreach (Paragraph p in buffer.Paragraphs()) walked += p.Length;

            // Every character belongs to exactly one paragraph, plus one mark between each pair.
            int marks = 0;

            foreach (char c in buffer.Text)
            {
                if (c == '\n') marks++;
            }

            Check($"{name}: paragraphs cover the text", walked + marks, buffer.Length);
        }

        Console.WriteLine("-- an empty buffer is one paragraph --");
        var b = new TextBuffer();
        Check("count", Count(b), 1);
        Check("kind", b.CurrentParagraph().Format.Kind, AuthoredBlockKind.Prose);

        Console.WriteLine("-- a heading style set before typing survives the first keystroke --");
        b = new TextBuffer();
        b.ApplyParagraph(p => p with { Kind = AuthoredBlockKind.Heading });
        Check("armed on the empty paragraph", b.CurrentParagraph().Format.Kind, AuthoredBlockKind.Heading);
        b.Insert("A header");
        Check("still a heading after typing", b.CurrentParagraph().Format.Kind, AuthoredBlockKind.Heading);
        Paired("typed heading", b);

        Console.WriteLine("-- Return splits and both halves keep the kind --");
        b = new TextBuffer();
        b.ApplyParagraph(p => p with { Kind = AuthoredBlockKind.Heading });
        b.Insert("A header");
        b.SplitParagraph();
        Check("two paragraphs", Count(b), 2);
        Check("first is a heading", First(b).Format.Kind, AuthoredBlockKind.Heading);
        Check("second inherits it", b.CurrentParagraph().Format.Kind, AuthoredBlockKind.Heading);
        Paired("after split", b);

        Console.WriteLine("-- the new paragraph can then be changed alone --");
        b.ApplyParagraph(p => p with { Kind = AuthoredBlockKind.Prose });
        Check("second is prose", b.CurrentParagraph().Format.Kind, AuthoredBlockKind.Prose);
        Check("first is untouched", First(b).Format.Kind, AuthoredBlockKind.Heading);

        Console.WriteLine("-- splitting mid-paragraph --");
        b = new TextBuffer("abcdef");
        b.MoveTo(3, false);
        b.SplitParagraph();
        Check("text", b.Text.Replace("\n", "|"), "abc|def");
        Check("two paragraphs", Count(b), 2);
        Check("caret after the mark", b.Caret, 4);

        Console.WriteLine("-- merge takes the following paragraph's settings --");
        // Heading, then prose. Backspacing at the start of the prose merges them, and the surviving
        // mark is the prose one, so the combined paragraph is prose.
        b = new TextBuffer();
        b.ApplyParagraph(p => p with { Kind = AuthoredBlockKind.Heading });
        b.Insert("head");
        b.SplitParagraph();
        b.ApplyParagraph(p => p with { Kind = AuthoredBlockKind.Quote });
        b.Insert("quote");
        Check("two before merge", Count(b), 2);
        b.MoveTo(5, false);
        Check("merged", b.MergeParagraphBackwards(), true);
        Check("one paragraph now", Count(b), 1);
        Check("text joined", b.Text, "headquote");
        Check("takes the surviving mark's kind", b.CurrentParagraph().Format.Kind, AuthoredBlockKind.Quote);
        Paired("after merge", b);

        Console.WriteLine("-- merge only fires on a mark --");
        b = new TextBuffer("abc");
        b.MoveTo(2, false);
        Check("mid-word does not merge", b.MergeParagraphBackwards(), false);
        Check("text untouched", b.Text, "abc");
        b.MoveTo(0, false);
        Check("at offset zero does not merge", b.MergeParagraphBackwards(), false);

        Console.WriteLine("-- merge is a no-op with a selection --");
        b = new TextBuffer("ab\ncd");
        b.SelectRange(1, 4);
        Check("declined", b.MergeParagraphBackwards(), false);

        Console.WriteLine("-- undo restores paragraph settings --");
        b = new TextBuffer("abc");
        b.ApplyParagraph(p => p with { Kind = AuthoredBlockKind.Heading, DropCap = true });
        Check("applied", b.CurrentParagraph().Format.DropCap, true);
        b.Undo();
        Check("undone", b.CurrentParagraph().Format.DropCap, false);
        Check("kind too", b.CurrentParagraph().Format.Kind, AuthoredBlockKind.Prose);
        b.Redo();
        Check("redone", b.CurrentParagraph().Format.Kind, AuthoredBlockKind.Heading);

        Console.WriteLine("-- undo restores a merge --");
        b = new TextBuffer("ab\ncd");
        b.MoveTo(3, false);
        b.MergeParagraphBackwards();
        Check("merged", Count(b), 1);
        b.Undo();
        Check("split again", Count(b), 2);
        Paired("after undoing a merge", b);

        Console.WriteLine("-- a selection spanning paragraphs restyles all of them --");
        b = new TextBuffer("aa\nbb\ncc");
        b.SelectRange(1, 7);
        b.ApplyParagraph(p => p with { Align = AuthoredAlign.Center });
        foreach (Paragraph p in b.Paragraphs())
            Check($"paragraph {p.Index} centred", p.Format.Align, AuthoredAlign.Center);

        Console.WriteLine("-- a selection inside one paragraph leaves its neighbours alone --");
        b = new TextBuffer("aa\nbb\ncc");
        b.SelectRange(3, 5);
        b.ApplyParagraph(p => p with { Align = AuthoredAlign.Right });
        var all = new List<Paragraph>(b.Paragraphs());
        Check("first untouched", all[0].Format.Align, AuthoredAlign.Left);
        Check("second changed", all[1].Format.Align, AuthoredAlign.Right);
        Check("third untouched", all[2].Format.Align, AuthoredAlign.Left);

        Console.WriteLine("-- paragraph indices and ranges --");
        b = new TextBuffer("aa\nbbb\nc");
        all = [.. b.Paragraphs()];
        Check("three paragraphs", all.Count, 3);
        Check("p0 range", $"{all[0].Start}..{all[0].End}", "0..2");
        Check("p1 range", $"{all[1].Start}..{all[1].End}", "3..6");
        Check("p2 range", $"{all[2].Start}..{all[2].End}", "7..8");
        Check("indices in reading order", $"{all[0].Index}{all[1].Index}{all[2].Index}", "012");

        Console.WriteLine("-- a trailing mark leaves an empty final paragraph --");
        b = new TextBuffer("aa\n");
        all = [.. b.Paragraphs()];
        Check("two paragraphs", all.Count, 2);
        Check("last is empty", all[1].Length, 0);

        Console.WriteLine("-- headings are identifiable for the outline --");
        b = new TextBuffer();
        b.ApplyParagraph(p => p with { Kind = AuthoredBlockKind.Chapter });
        Check("chapter counts as a heading", b.CurrentParagraph().Format.IsHeading, true);
        b.ApplyParagraph(p => p with { Kind = AuthoredBlockKind.Prose });
        Check("prose does not", b.CurrentParagraph().Format.IsHeading, false);

        Console.WriteLine();
        Console.WriteLine(failures == 0 ? "PARAGRAPH: ALL PASS" : $"PARAGRAPH: {failures} FAILURE(S)");

        return failures;
    }

    private static int Count(TextBuffer buffer)
    {
        int n = 0;

        foreach (Paragraph _ in buffer.Paragraphs()) n++;

        return n;
    }

    private static Paragraph First(TextBuffer buffer)
    {
        foreach (Paragraph p in buffer.Paragraphs()) return p;

        return default;
    }
}
