using System.Globalization;
using System.Collections.Generic;
using System.Text;

namespace TrelamiumIII.Core.Authoring;

/// <summary>
///     Turns an <see cref="AuthoredBook" /> into the two files a lore book is made of: the hjson the
///     words live in, and the C# definition that declares the structure.
/// </summary>
/// <remarks>
///     Pure string generation, no file system and no game state, so the output can be checked without
///     launching anything.
/// </remarks>
public static class BookExporter
{
    /// <summary>The library file is tab-indented; matching it keeps diffs to the words that changed.</summary>
    private const char INDENT = '\t';

    #region Localization

    /// <summary>
    ///     The book's localization entries, nested to match the dotted book key.
    /// </summary>
    /// <remarks>
    ///     Emits only this book's subtree. Merging it into the existing library file is the caller's
    ///     job -- rewriting the whole file from the editor would discard every book that is not
    ///     currently open.
    /// </remarks>
    public static string ToHjson(AuthoredBook book)
    {
        var output = new StringBuilder();
        string[] path = book.Key.Split('.', StringSplitOptions.RemoveEmptyEntries);

        output.Append("LoreLibrary: {\n");

        for (int i = 0; i < path.Length; i++)
            output.Append(Indent(i + 1)).Append(path[i]).Append(": {\n");

        int depth = path.Length + 1;

        // Collected before emitting so a key is written exactly once. A book almost always opens
        // with .Heading("Title"), which reads the very key the book title lives under; emitting both
        // in sequence produces a duplicate key, and hjson keeps only the last of those.
        List<string> order = [];
        Dictionary<string, (string Text, bool IsChapter)> entries = new(StringComparer.Ordinal);

        void Record(string key, string text, bool isChapter)
        {
            if (!entries.ContainsKey(key)) order.Add(key);

            entries[key] = (text, isChapter);
        }

        Record("Title", book.Title, false);

        foreach (AuthoredBlock block in book.Blocks)
        {
            if (block.HasText) Record(block.Key, block.Text, block.Kind == AuthoredBlockKind.Chapter);
        }

        foreach (string key in order)
        {
            (string text, bool isChapter) = entries[key];

            // Chapters read their heading from {key}.Title, so they nest one level.
            if (isChapter)
            {
                output.Append('\n').Append(Indent(depth)).Append(key).Append(": {\n");
                WriteEntry(output, depth + 1, "Title", text);
                output.Append(Indent(depth)).Append("}\n");

                continue;
            }

            WriteEntry(output, depth, key, text);
        }

        for (int i = path.Length; i >= 1; i--)
            output.Append(Indent(i)).Append("}\n");

        output.Append("}\n");

        return output.ToString();
    }

    private static void WriteEntry(StringBuilder output, int depth, string key, string value)
    {
        output.Append(Indent(depth)).Append(key).Append(':');

        if (NeedsBlockString(value))
        {
            // hjson block strings keep their own indentation, and the closing marker has to sit at
            // the same depth as the opening one.
            output.Append('\n').Append(Indent(depth + 1)).Append("'''\n");

            foreach (string line in value.Split('\n'))
                output.Append(Indent(depth + 1)).Append(line.TrimEnd('\r')).Append('\n');

            output.Append(Indent(depth + 1)).Append("'''\n");

            return;
        }

        output.Append(' ').Append(value).Append('\n');
    }

    /// <summary>
    ///     Whether a value cannot be written bare.
    /// </summary>
    /// <remarks>
    ///     An unquoted hjson value runs to the end of the line, so anything with a newline needs a
    ///     block string. So does anything whose first character would otherwise start a structure or
    ///     a quoted string, and anything with significant leading or trailing space, which a bare
    ///     value silently loses.
    /// </remarks>
    private static bool NeedsBlockString(string value)
    {
        if (value.Length == 0) return false;
        if (value.Contains('\n')) return true;
        if (value != value.Trim()) return true;

        return value[0] is '"' or '\'' or '{' or '[' or '#' or ':' or ',';
    }

    private static string Indent(int depth) => new(INDENT, depth);

    #endregion

    #region Definition

    /// <summary>The C# <c>ModLoreBook</c> that declares this book's structure.</summary>
    public static string ToCSharp(AuthoredBook book)
    {
        var output = new StringBuilder();

        output.Append("using TrelamiumIII.Core.Books.Layout;\n");
        output.Append("using TrelamiumIII.Core.Books.Text;\n\n");
        output.Append("namespace TrelamiumIII.Core.Books.Lore.Definitions;\n\n");
        output.Append("/// <summary>Generated by the in-game authoring tool. Safe to edit by hand.</summary>\n");
        output.Append($"public sealed class {book.ClassName} : ModLoreBook\n");
        output.Append("{\n");
        output.Append($"    public override string Key => \"{Escape(book.Key)}\";\n");

        if (book.Typeface != AuthoredTypeface.Inherit)
        {
            output.Append('\n');
            output.Append($"    protected override BookTypeface? Typeface => BookTypeface.{book.Typeface};\n");
        }

        output.Append('\n');
        output.Append("    protected override void Build(LoreBookBuilder book) => book\n");

        List<string> calls = [];

        foreach (AuthoredBlock block in book.Blocks) calls.Add(CallFor(block));

        for (int i = 0; i < calls.Count; i++)
            output.Append("        ").Append(calls[i]).Append(i == calls.Count - 1 ? ";\n" : "\n");

        if (calls.Count == 0) output.Append("        .Space(0f);\n");

        output.Append("}\n");

        return output.ToString();
    }

    private static string CallFor(AuthoredBlock block)
    {
        string style = StyleFor(block);
        string key = $"\"{Escape(block.Key)}\"";

        return block.Kind switch
        {
            AuthoredBlockKind.Heading   => $".Heading({key}{Arg("rule", block.Rule, true)}{style})",
            AuthoredBlockKind.Chapter   => $".Chapter({key}{Arg("depth", block.Depth, 0)}"
                                           + $"{Arg("pageBreak", block.PageBreak, true)}"
                                           + $"{Arg("rule", block.Rule, true)}{style})",
            AuthoredBlockKind.Quote     => $".Quote({key}{style})",
            AuthoredBlockKind.Caption   => $".Prose({key}, {StyleExpression(block, "BlockStyle.Caption")})",
            AuthoredBlockKind.Rule      => ".Rule()",
            AuthoredBlockKind.Space     => ".Space()",
            AuthoredBlockKind.PageBreak => ".PageBreak()",
            _                           => $".Prose({key}{style})",
        };
    }

    /// <summary>A trailing <c>style:</c> argument, or nothing when the block is entirely default.</summary>
    private static string StyleFor(AuthoredBlock block)
    {
        string baseStyle = block.Kind switch
        {
            AuthoredBlockKind.Heading => "BlockStyle.Heading",
            AuthoredBlockKind.Chapter => "BlockStyle.Heading",
            AuthoredBlockKind.Quote   => "BlockStyle.Quote",
            AuthoredBlockKind.Caption => "BlockStyle.Caption",
            _                         => "BlockStyle.Body",
        };

        List<string> parts = Modifiers(block);

        return parts.Count == 0 ? string.Empty : $", {baseStyle} with {{ {string.Join(", ", parts)} }}";
    }

    private static string StyleExpression(AuthoredBlock block, string baseStyle)
    {
        List<string> parts = Modifiers(block);

        return parts.Count == 0 ? baseStyle : $"{baseStyle} with {{ {string.Join(", ", parts)} }}";
    }

    private static List<string> Modifiers(AuthoredBlock block)
    {
        List<string> parts = [];

        if (block.Align != AuthoredAlign.Left) parts.Add($"Align = TextAlign.{block.Align}");
        if (block.DropCap) parts.Add("DropCap = true");
        if (block.Scale > 0f) parts.Add($"Scale = {block.Scale.ToString("0.###", CultureInfo.InvariantCulture)}f");

        if (block.Ink is { } packed)
            parts.Add($"Color = new Color({(packed >> 16) & 0xFF}, {(packed >> 8) & 0xFF}, {packed & 0xFF})");

        if (block.Typeface != AuthoredTypeface.Inherit)
            parts.Add($"Typeface = BookTypeface.{block.Typeface}");

        return parts;
    }

    /// <summary>Named argument, omitted when it already holds the builder's default.</summary>
    private static string Arg<T>(string name, T value, T defaultValue) where T : notnull =>
        value.Equals(defaultValue)
            ? string.Empty
            : $", {name}: {(value is bool flag ? flag ? "true" : "false" : value.ToString())}";

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    #endregion
}
