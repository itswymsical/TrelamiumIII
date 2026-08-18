using TrelamiumIII.Core.Authoring;

namespace BufTest;

/// <summary>Layout checks with synthetic 10px-wide glyphs, so expected positions are exact.</summary>
public static class LayoutTests
{
    // Every glyph 10 wide: no bearings, no spacing. Prefix width of i characters is 10*i.
    private static EditorLayout Build(string text, float width) =>
        new(text, _ => (0f, 10f, 0f), characterSpacing: 0f, scale: 1f, lineHeight: 20f, maxWidth: width);

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

        Console.WriteLine("-- word wrap --");
        // "aaa bbb ccc" = 11 chars at 10px each. A 70px column fits "aaa bbb"; the space that
        // follows is kept on the line it ended, so line 0 is 8 characters and hangs one glyph.
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
        // From the short middle line, continuing down with the original desired x lands back at 5.
        Check("down again keeps desired x", l.MoveVertical(9, 1, 50f), 15);
        Check("up from line 2", l.MoveVertical(15, -1, 50f), 9);
        Check("up past the top clamps to 0", l.MoveVertical(2, -1, 20f), 0);
        Check("down past the end clamps", l.MoveVertical(15, 1, 20f), 16);

        Console.WriteLine("-- home and end --");
        l = Build("aaa bbb ccc", 70f);
        Check("line start from mid line 1", l.LineStartOf(9), 8);
        Check("line end on wrapped line excludes space", l.LineEndOf(1), 7);
        Check("line end on last line", l.LineEndOf(9), 11);

        Console.WriteLine();
        Console.WriteLine(failures == 0 ? "LAYOUT: ALL PASS" : $"LAYOUT: {failures} FAILURE(S)");

        return failures;
    }
}
