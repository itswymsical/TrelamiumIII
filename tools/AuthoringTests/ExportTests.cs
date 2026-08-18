using TrelamiumIII.Core.Authoring;

namespace BufTest;

public static class ExportTests
{
    public static int Run()
    {
        var book = new AuthoredBook { Key = "Example.FirstAccount", Title = "A First Account", Typeface = AuthoredTypeface.Encyclopedia };

        book.Blocks.Add(new AuthoredBlock { Kind = AuthoredBlockKind.Heading, Key = "Title", Text = "A First Account" });
        book.Blocks.Add(new AuthoredBlock { Kind = AuthoredBlockKind.Prose, Key = "Opening", Text = "They kept the ledgers below the waterline.[p]I went because I was told.", DropCap = true, Align = AuthoredAlign.Justify });
        book.Blocks.Add(new AuthoredBlock { Kind = AuthoredBlockKind.Quote, Key = "Excerpt", Text = "\"Item: one ledger, water-damaged.\"" });
        book.Blocks.Add(new AuthoredBlock { Kind = AuthoredBlockKind.Chapter, Key = "Ch2", Text = "What the Clerk Said" });
        book.Blocks.Add(new AuthoredBlock { Kind = AuthoredBlockKind.Prose, Key = "Closing", Text = "Line one\nLine two", Typeface = AuthoredTypeface.Glossary });
        book.Blocks.Add(new AuthoredBlock { Kind = AuthoredBlockKind.Rule });

        Console.WriteLine("======== HJSON ========");
        Console.WriteLine(BookExporter.ToHjson(book));
        Console.WriteLine("======== C# ========");
        Console.WriteLine(BookExporter.ToCSharp(book));

        int failures = 0;
        void Check(string name, bool ok) { if (!ok) failures++; Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {name}"); }

        string hjson = BookExporter.ToHjson(book);
        string cs = BookExporter.ToCSharp(book);

        Console.WriteLine("-- checks --");
        Check("nests the dotted key", hjson.Contains("Example: {") && hjson.Contains("FirstAccount: {"));
        Check("quote value uses a block string", hjson.Contains("Excerpt:\n") && hjson.Contains("'''"));
        Check("multi-line value uses a block string", hjson.Contains("Line one") && hjson.Contains("Line two"));
        Check("chapter nests under its own key", hjson.Contains("Ch2: {"));
        Check("braces balance", hjson.Count(c => c == '{') == hjson.Count(c => c == '}'));
        Check("class name derived", book.ClassName == "ExampleFirstAccountBook");
        Check("book typeface emitted", cs.Contains("protected override BookTypeface? Typeface => BookTypeface.Encyclopedia;"));
        Check("drop cap + justify emitted", cs.Contains("BlockStyle.Body with { Align = TextAlign.Justify, DropCap = true }"));
        Check("element typeface override emitted", cs.Contains("Typeface = BookTypeface.Glossary"));
        Check("plain heading has no style arg", cs.Contains(".Heading(\"Title\")"));
        Check("rule takes no key", cs.Contains(".Rule()"));
        Check("chain terminates once", cs.Count(c => c == ';') >= 2 && cs.Contains(".Rule();"));
        Check("no duplicate keys reported", !book.DuplicateKeys().Any());
        Check("Title written exactly once", hjson.Split("Title:").Length - 1 == 2);

        book.Blocks.Add(new AuthoredBlock { Kind = AuthoredBlockKind.Prose, Key = "Opening", Text = "clash" });
        Check("duplicate key detected", book.DuplicateKeys().Contains("Opening"));

        Console.WriteLine();
        Console.WriteLine(failures == 0 ? "EXPORT: ALL PASS" : $"EXPORT: {failures} FAILURE(S)");
        return failures;
    }
}
