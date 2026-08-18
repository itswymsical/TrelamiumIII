using TrelamiumIII.Core.Authoring;

namespace BufTest;

public static class CaseTests
{
    public static int Run()
    {
        int failures = 0;
        void Check(string name, object? a, object? e)
        {
            bool ok = Equals(a?.ToString(), e?.ToString());
            if (!ok) failures++;
            Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {name}" + (ok ? "" : $"\n         got <{a}> expected <{e}>"));
        }

        Console.WriteLine("-- change case on a selection --");
        var b = new TextBuffer("the drowned archive");
        b.SelectRange(4, 11);
        b.ChangeCase(TextCase.Upper);
        Check("upper", b.Text, "the DROWNED archive");
        Check("selection survives for cycling", b.SelectedText, "DROWNED");
        b.ChangeCase(TextCase.Lower);
        Check("lower", b.Text, "the drowned archive");

        Console.WriteLine("-- title case --");
        b = new TextBuffer("the DROWNED archive");
        b.SelectAll();
        b.ChangeCase(TextCase.Title);
        Check("title", b.Text, "The Drowned Archive");

        Console.WriteLine("-- sentence case --");
        b = new TextBuffer("HE WENT DOWN. THE CLERK WAITED! WAS SHE THERE?");
        b.SelectAll();
        b.ChangeCase(TextCase.Sentence);
        Check("sentence", b.Text, "He went down. The clerk waited! Was she there?");

        Console.WriteLine("-- falls back to the word under the caret --");
        b = new TextBuffer("alpha beta gamma");
        b.MoveTo(8, false);
        b.ChangeCase(TextCase.Upper);
        Check("word recased", b.Text, "alpha BETA gamma");

        Console.WriteLine("-- undo restores in one step --");
        b = new TextBuffer("alpha beta");
        b.SelectAll();
        b.ChangeCase(TextCase.Upper);
        b.Undo();
        Check("undo", b.Text, "alpha beta");

        Console.WriteLine("-- no-op does not push undo --");
        b = new TextBuffer("ALPHA");
        b.SelectAll();
        b.ChangeCase(TextCase.Upper);
        Check("already upper, nothing to undo", b.CanUndo, false);

        Console.WriteLine();
        Console.WriteLine(failures == 0 ? "CASE: ALL PASS" : $"CASE: {failures} FAILURE(S)");
        return failures;
    }
}
