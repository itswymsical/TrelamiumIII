using TrelamiumIII.Core.Authoring;

namespace BufTest;

/// <summary>
///     Layout checks with synthetic metrics, so every expected position is exact.
/// </summary>
/// <remarks>
///     The uniform-metric cases are unchanged from before per-character metrics existed; they are the
///     regression check that widening the model did not move anything that used to work.
/// </remarks>
public static class LayoutTests
{
    /// <summary>Every glyph 10 wide, 20 tall, no bearings or spacing. Prefix width of i chars is 10i.</summary>
    private static EditorLayout Build(string text, float width) =>
        new(text, _ => new EditorLayout.CharMetrics(0f, 10f, 0f, 0f, 16f, 4f), width);

    /// <summary>Widths and heights chosen per index, for the mixed-format cases.</summary>
    private static EditorLayout Build(string text, float width, Func<int, (float W, float A, float D)> pick) =>
        new(text, i =>
        {
            (float w, float a, float d) = pick(i);

            return new EditorLayout.CharMetrics(0f, w, 0f, 0f, a, d);
        }, width);

    public static int Run()
    {
        int failures = 0;

        void Check(string name, object? actual, object? expected)
        {
            bool ok = Equals(actual?.ToString(), expected?.ToString());

            if (!ok) failures++;

            Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {name}"
                              + (ok ? "" : $"\n         got <{actual}> expected <{expected}>"));
        }

        Console.WriteLine("-- prefix widths --");
        var l = Build("abcdef", 1000f);
        Check("x at 0", l.XOf(0), 0f);
        Check("x at 3", l.XOf(3), 30f);
        Check("single line", l.Lines.Count, 1);

        Console.WriteLine("-- empty buffer still has a line --");
        l = Build("", 100f);
        Check("one line", l.Lines.Count, 1);
        Check("caret at origin", l.CaretAt(0).X, 0f);
        Check("and has a height", l.Height, 20f);

        Console.WriteLine("-- word wrap --");
        // "aaa bbb ccc" is 11 chars at 10px. A 70px column fits "aaa bbb"; the space after is kept
        // on the line it ended, so line 0 is 8 characters and hangs one glyph.
        l = Build("aaa bbb ccc", 70f);
        Check("wraps to 2 lines", l.Lines.Count, 2);
        Check("line 0 keeps its trailing space", l.LineAt(0).Length, 8);
        Check("line 1 starts after the space", l.LineAt(1).Start, 8);

        Console.WriteLine("-- hard breaks --");
        l = Build("ab\ncd", 1000f);
        Check("newline splits", l.Lines.Count, 2);
        Check("line 0 excludes the newline", l.LineAt(0).Length, 2);
        Check("line 1 starts past it", l.LineAt(1).Start, 3);
        Check("line 0 is a hard break", l.LineAt(0).HardBreak, true);

        Console.WriteLine("-- trailing blank line after a final newline --");
        l = Build("ab\n", 1000f);
        Check("two lines", l.Lines.Count, 2);
        Check("last line empty", l.LineAt(1).Length, 0);
        Check("but still tall", l.LineAt(1).Height, 20f);

        Console.WriteLine("-- unbreakable word longer than the column --");
        l = Build("aaaaaaaa", 30f);
        Check("splits mid-word", l.Lines.Count > 1, true);
        Check("first line takes what fits", l.LineAt(0).Length, 3);

        Console.WriteLine("-- LineOf --");
        l = Build("aaa bbb ccc", 70f);
        Check("offset 0 on line 0", l.LineOf(0), 0);
        Check("offset 3 on line 0", l.LineOf(3), 0);
        Check("offset 8 on line 1", l.LineOf(8), 1);
        Check("end offset on last line", l.LineOf(11), 1);
        Check("caret at a hard line end stays on its line", Build("ab\ncd", 1000f).LineOf(2), 0);
        Check("offset past a hard break is the next line", Build("ab\ncd", 1000f).LineOf(3), 1);

        Console.WriteLine("-- caret y follows the line --");
        Check("line 1 y", l.CaretAt(9).Y, 20f);

        Console.WriteLine("-- hit testing --");
        l = Build("abcdef", 1000f);
        Check("click before first glyph", l.IndexAt(0f, 0f), 0);
        Check("click just past glyph 2 rounds to 2", l.IndexAt(19f, 0f), 2);
        Check("click at 24 rounds to 2", l.IndexAt(24f, 0f), 2);
        Check("click at 26 rounds to 3", l.IndexAt(26f, 0f), 3);
        Check("click far right clamps to end", l.IndexAt(9999f, 0f), 6);

        Console.WriteLine("-- click past a wrapped line stops before the trailing space --");
        l = Build("aaa bbb ccc", 70f);
        Check("far right of line 0", l.IndexOnLine(0, 9999f), 7);

        Console.WriteLine("-- vertical movement holds the column --");
        l = Build("abcdef\nxy\nabcdef", 1000f);
        Check("down from x=50 on line 0", l.MoveVertical(5, 1, 50f), 9);
        Check("down again keeps desired x", l.MoveVertical(9, 1, 50f), 15);
        Check("up from line 2", l.MoveVertical(15, -1, 50f), 9);
        Check("up past the top clamps to 0", l.MoveVertical(2, -1, 20f), 0);
        Check("down past the end clamps", l.MoveVertical(15, 1, 20f), 16);

        Console.WriteLine("-- home and end --");
        l = Build("aaa bbb ccc", 70f);
        Check("line start from mid line 1", l.LineStartOf(9), 8);
        Check("line end on wrapped line excludes space", l.LineEndOf(1), 7);
        Check("line end on last line", l.LineEndOf(9), 11);

        Console.WriteLine("-- selection spans measured against their own line --");
        Check("range across line 0", l.WidthOfRange(0, 7), 70f);
        Check("range is not line-relative", l.WidthOfRange(8, 11), 30f);
        Check("XOf at a soft wrap resolves to the next line", l.XOf(8), 0f);
        Check("which is why the highlight uses WidthOfRange", l.WidthOfRange(l.LineAt(0).Start, 8) > l.XOf(8), true);

        Console.WriteLine("-- per-character widths --");
        // "ab" where 'a' is 10 wide and 'b' is 30.
        l = Build("ab", 1000f, i => (i == 0 ? 10f : 30f, 16f, 4f));
        Check("first advance", l.XOf(1), 10f);
        Check("second advance is its own", l.XOf(2), 40f);

        Console.WriteLine("-- wrapping respects per-character widths --");
        // Four glyphs of 30 in a 70px column: two fit, then it breaks mid-word.
        l = Build("aaaa", 70f, _ => (30f, 16f, 4f));
        Check("breaks after two", l.LineAt(0).Length, 2);

        Console.WriteLine("-- a line is as tall as its tallest character --");
        // Index 1 is double height; the rest are the 16/4 default.
        l = Build("abc", 1000f, i => (10f, i == 1 ? 32f : 16f, i == 1 ? 8f : 4f));
        Check("ascent is the max", l.LineAt(0).Ascent, 32f);
        Check("descent is the max", l.LineAt(0).Descent, 8f);
        Check("height is their sum", l.LineAt(0).Height, 40f);

        Console.WriteLine("-- ascent and descent maxed separately --");
        // Char 0 is tall with a shallow descender, char 1 short with a deep one. The line needs both.
        l = Build("ab", 1000f, i => (10f, i == 0 ? 30f : 10f, i == 0 ? 2f : 12f));
        Check("ascent from the tall one", l.LineAt(0).Ascent, 30f);
        Check("descent from the deep one", l.LineAt(0).Descent, 12f);
        Check("taller than either character alone", l.LineAt(0).Height, 42f);

        Console.WriteLine("-- baselines align across sizes --");
        l = Build("ab", 1000f, i => (10f, i == 0 ? 32f : 16f, 4f));
        Check("tall char sits at the top", l.BaselineOffset(0), 0f);
        Check("short char is pushed down to meet it", l.BaselineOffset(1), 16f);

        Console.WriteLine("-- mixed line heights accumulate rather than multiply --");
        // Line 0 holds the tall char, line 1 does not.
        l = Build("a\nb", 1000f, i => (10f, i == 0 ? 40f : 16f, 4f));
        Check("line 0 height", l.LineAt(0).Height, 44f);
        Check("line 1 top is line 0's bottom", l.LineAt(1).Top, 44f);
        Check("total height", l.Height, 64f);

        Console.WriteLine("-- hit testing walks uneven lines --");
        Check("click in line 0", l.IndexAt(0f, 10f), 0);
        Check("click below line 0 lands on line 1", l.IndexAt(0f, 50f), 2);
        Check("caret height follows its line", l.CaretHeight(0), 44f);
        Check("and differs on the next", l.CaretHeight(2), 20f);

        Console.WriteLine("-- per-character spacing is charged to the following glyph --");
        // 2px of spacing before every glyph but the first on a line.
        l = new EditorLayout("abc", _ => new EditorLayout.CharMetrics(0f, 10f, 0f, 2f, 16f, 4f), 1000f);
        Check("first glyph is not charged", l.XOf(1), 10f);
        Check("second is", l.XOf(2), 22f);
        Check("third too", l.XOf(3), 34f);

        Console.WriteLine();
        Console.WriteLine(failures == 0 ? "LAYOUT: ALL PASS" : $"LAYOUT: {failures} FAILURE(S)");

        return failures;
    }
}
