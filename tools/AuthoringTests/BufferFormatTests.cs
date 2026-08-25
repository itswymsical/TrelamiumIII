using TrelamiumIII.Core.Authoring;

namespace BufTest;

/// <summary>
///     Cover for the format array riding alongside the text.
/// </summary>
/// <remarks>
///     The invariant that matters is length: one format per character, after every operation. If the
///     two ever drift, formatting slides onto the wrong letters and nothing downstream can detect it,
///     so every mutation below asserts the pairing as well as the result.
/// </remarks>
public static class BufferFormatTests
{
    private static readonly CharFormat Bold = new(true, false, 0f, null);
    private static readonly CharFormat Italic = new(false, true, 0f, null);

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

        void Paired(string name, TextBuffer buffer) =>
            Check($"{name}: one format per char", buffer.Formats.Count, buffer.Length);

        Console.WriteLine("-- the pairing survives every mutation --");
        var b = new TextBuffer("hello world");
        Paired("loaded", b);
        b.MoveTo(5, false);
        b.Insert(" there");
        Paired("after insert", b);
        b.Backspace();
        Paired("after backspace", b);
        b.MoveTo(0, false);
        b.Delete();
        Paired("after delete", b);
        b.SelectRange(2, 6);
        b.Insert("XY");
        Paired("after replacing a selection", b);
        b.SelectAll();
        _ = b.CutSelection();
        Paired("after cut", b);
        b.Insert("fresh");
        Paired("after typing into empty", b);
        b.Undo();
        Paired("after undo", b);
        b.Redo();
        Paired("after redo", b);

        Console.WriteLine("-- applying to a selection --");
        b = new TextBuffer("hello world");
        b.SelectRange(6, 11);
        b.ApplyFormat(f => f with { Bold = true });
        Check("selected run is bold", b.FormatAt(6).Bold, true);
        Check("last selected char is bold", b.FormatAt(10).Bold, true);
        Check("before the run is not", b.FormatAt(5).Bold, false);
        Paired("after apply", b);

        Console.WriteLine("-- serialises to the tag the codec expects --");
        Check("round trip out", FormatCodec.Serialize(b.Text, b.Formats), "hello [fmt/b:world]");

        Console.WriteLine("-- toggling with no selection arms the next keystroke --");
        b = new TextBuffer("ab");
        b.MoveTo(2, false);
        b.ApplyFormat(f => f with { Bold = true });
        Check("nothing formatted yet", b.FormatAt(0).Bold, false);
        Check("armed", b.Pending?.Bold, true);
        b.Insert("c", coalesce: true);
        Check("typed char took it", b.FormatAt(2).Bold, true);
        Check("armed state consumed", b.Pending, null);
        b.Insert("d", coalesce: true);
        Check("following char inherits from the left", b.FormatAt(3).Bold, true);

        Console.WriteLine("-- a caret move disarms --");
        b = new TextBuffer("ab");
        b.ApplyFormat(f => f with { Bold = true });
        b.MoveTo(0, false);
        Check("disarmed", b.Pending, null);
        b.Insert("z", coalesce: true);
        Check("typed plain", b.FormatAt(0).Bold, false);

        Console.WriteLine("-- typing inside a run continues it --");
        b = new TextBuffer("bold", [Bold, Bold, Bold, Bold]);
        b.MoveTo(2, false);
        b.Insert("XX");
        Check("inserted text is bold", b.FormatAt(2).Bold, true);
        Check("and 3", b.FormatAt(3).Bold, true);
        Check("one run out", FormatCodec.Serialize(b.Text, b.Formats), "[fmt/b:boXXld]");

        Console.WriteLine("-- typing at the very start borrows from the right --");
        b = new TextBuffer("bold", [Bold, Bold, Bold, Bold]);
        b.MoveTo(0, false);
        b.Insert("X");
        Check("still bold", b.FormatAt(0).Bold, true);

        Console.WriteLine("-- replacing a selection takes the replaced formatting --");
        b = new TextBuffer("ab", [Bold, Bold]);
        b.SelectAll();
        b.Insert("zz");
        Check("replacement is bold", b.FormatAt(0).Bold, true);

        Console.WriteLine("-- undo restores formatting, not just text --");
        b = new TextBuffer("hello");
        b.SelectAll();
        b.ApplyFormat(f => f with { Italic = true });
        Check("italic applied", b.FormatAt(0).Italic, true);
        b.Undo();
        Check("text unchanged", b.Text, "hello");
        Check("italic undone", b.FormatAt(0).Italic, false);
        b.Redo();
        Check("italic redone", b.FormatAt(0).Italic, true);

        Console.WriteLine("-- deleting a run removes its formats too --");
        b = new TextBuffer("ab", [Bold, Italic]);
        b.MoveTo(1, false);
        b.Backspace();
        Check("text", b.Text, "b");
        Check("surviving char kept its own format", b.FormatAt(0).Italic, true);
        Check("and did not inherit the deleted one", b.FormatAt(0).Bold, false);

        Console.WriteLine("-- toggle state over a mixed selection --");
        b = new TextBuffer("ab", [Bold, CharFormat.Inherit]);
        b.SelectAll();
        Check("not all bold", b.SelectionAll(f => f.Bold), false);
        b.ApplyFormat(f => f with { Bold = true });
        Check("now all bold", b.SelectionAll(f => f.Bold), true);

        Console.WriteLine("-- change case preserves formatting --");
        b = new TextBuffer("hello", [Bold, Bold, Bold, Bold, Bold]);
        b.SelectAll();
        b.ChangeCase(TextCase.Upper);
        Check("recased", b.Text, "HELLO");
        Paired("after recase", b);
        Check("still bold", b.FormatAt(0).Bold, true);

        Console.WriteLine("-- reset clears formatting and arming --");
        b = new TextBuffer("ab", [Bold, Bold]);
        b.ApplyFormat(f => f with { Italic = true });
        b.Reset("fresh");
        Paired("after reset", b);
        Check("plain", b.FormatAt(0).IsInherit, true);
        Check("disarmed", b.Pending, null);

        Console.WriteLine();
        Console.WriteLine(failures == 0 ? "BUFFER FORMAT: ALL PASS" : $"BUFFER FORMAT: {failures} FAILURE(S)");

        return failures;
    }
}
