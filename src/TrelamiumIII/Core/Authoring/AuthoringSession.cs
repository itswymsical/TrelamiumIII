using System.Collections.Generic;
using System.IO;
using TrelamiumIII.Common.Configs;

namespace TrelamiumIII.Core.Authoring;

/// <summary>Which of the editable strings the shared buffer is pointed at.</summary>
public enum EditorField
{
    BlockText,
    BlockKey,
    BookKey,
    BookTitle,
}

/// <summary>
///     One editing sitting: the book being written, which block is open, and the buffer holding it.
/// </summary>
/// <remarks>
///     Only the selected block's text lives in the <see cref="TextBuffer" />. Switching blocks
///     commits the buffer back and reloads, which is also what keeps each block's undo history its
///     own rather than one stack spanning the book.
/// </remarks>
public sealed class AuthoringSession
{
    public AuthoringSession()
    {
        Book.Blocks.Add(new AuthoredBlock { Kind = AuthoredBlockKind.Heading, Key = "Title", Text = "Untitled" });
        Book.Blocks.Add(new AuthoredBlock { Kind = AuthoredBlockKind.Prose, Key = "Opening" });
        SelectBlock(1);
    }

    public AuthoredBook Book { get; } = new();

    public TextBuffer Buffer { get; } = new();

    public int SelectedIndex { get; private set; }

    public AuthoredBlock? Selected =>
        SelectedIndex >= 0 && SelectedIndex < Book.Blocks.Count ? Book.Blocks[SelectedIndex] : null;

    /// <summary>Last thing that happened, shown in the status strip.</summary>
    public string Status { get; set; } = "Ready.";

    /// <summary>Which string the shared buffer is currently editing.</summary>
    public EditorField Focus { get; private set; } = EditorField.BlockText;

    /// <summary>
    ///     Points the buffer at a different string, saving whatever it held first.
    /// </summary>
    /// <remarks>
    ///     One buffer serves the prose and the short fields alike, so selection, clipboard and undo
    ///     behave identically in a key box and in a paragraph rather than the fields getting a
    ///     second, poorer input path.
    /// </remarks>
    public void FocusField(EditorField field)
    {
        if (field == Focus) return;

        Commit();
        Focus = field;
        Reload();
    }

    /// <summary>Writes the buffer back into whatever it is currently editing.</summary>
    public void Commit()
    {
        switch (Focus)
        {
            case EditorField.BookKey when Buffer.Text.Length > 0:
                Book.Key = Buffer.Text;
                break;
            case EditorField.BookTitle:
                Book.Title = Buffer.Text;
                break;
            case EditorField.BlockKey when Selected is { } keyed && Buffer.Text.Length > 0:
                keyed.Key = Buffer.Text;
                break;
            case EditorField.BlockText when Selected is { HasText: true } block:
                block.Text = Buffer.Text;
                break;
        }
    }

    private void Reload() => Buffer.Reset(Focus switch
    {
        EditorField.BookKey   => Book.Key,
        EditorField.BookTitle => Book.Title,
        EditorField.BlockKey  => Selected?.Key ?? string.Empty,
        _                     => Selected is { HasText: true } block ? block.Text : string.Empty,
    });

    public void SelectBlock(int index)
    {
        if (index == SelectedIndex && Selected is not null) return;

        Commit();

        SelectedIndex = Math.Clamp(index, 0, Math.Max(0, Book.Blocks.Count - 1));
        Focus = EditorField.BlockText;
        Reload();
    }

    public void AddBlock(AuthoredBlockKind kind)
    {
        Commit();

        Book.Blocks.Insert(SelectedIndex + 1, new AuthoredBlock
        {
            Kind = kind,
            Key = UniqueKey(kind.ToString()),
        });

        SelectedIndex = -1;
        SelectBlock(SelectedIndex + 2);
        Status = $"Added {kind}.";
    }

    public void DeleteBlock()
    {
        if (Book.Blocks.Count <= 1)
        {
            Status = "A book needs at least one block.";

            return;
        }

        Book.Blocks.RemoveAt(SelectedIndex);

        int target = Math.Clamp(SelectedIndex, 0, Book.Blocks.Count - 1);

        SelectedIndex = -1;
        SelectBlock(target);
        Status = "Deleted block.";
    }

    public void MoveBlock(int direction)
    {
        int target = SelectedIndex + direction;

        if (target < 0 || target >= Book.Blocks.Count) return;

        Commit();
        (Book.Blocks[SelectedIndex], Book.Blocks[target]) = (Book.Blocks[target], Book.Blocks[SelectedIndex]);
        SelectedIndex = target;
        Status = "Moved block.";
    }

    /// <summary>A key not already taken, since two blocks sharing one would lose an entry on export.</summary>
    private string UniqueKey(string basis)
    {
        var taken = new HashSet<string>(StringComparer.Ordinal);

        foreach (AuthoredBlock block in Book.Blocks)
        {
            if (block.HasText) taken.Add(block.Key);
        }

        if (taken.Add(basis)) return basis;

        for (int i = 2; ; i++)
        {
            string candidate = $"{basis}{i}";

            if (!taken.Contains(candidate)) return candidate;
        }
    }

    #region Export

    /// <summary>
    ///     Writes the hjson fragment and the C# definition, and reports where they landed.
    /// </summary>
    /// <remarks>
    ///     The hjson goes out as a fragment beside the library rather than into it. Merging it in
    ///     would mean parsing and rewriting a file holding every other book, and a bug there costs
    ///     prose that only exists in that file -- so the merge stays a deliberate, human paste.
    /// </remarks>
    public string Export()
    {
        Commit();

        List<string> duplicates = [.. Book.DuplicateKeys()];

        if (duplicates.Count > 0)
            return Status = $"Not exported: duplicate keys ({string.Join(", ", duplicates)}).";

        try
        {
            string root = Root();
            string definitions = Path.Combine(root, "Core", "Books", "Lore", "Definitions");
            string library = Path.Combine(root, "Localization", "LoreLibrary");

            Directory.CreateDirectory(definitions);
            Directory.CreateDirectory(library);

            string csPath = Path.Combine(definitions, $"{Book.ClassName}.cs");
            string hjsonPath = Path.Combine(library, $"{Book.ClassName}.fragment.hjson");

            File.WriteAllText(csPath, BookExporter.ToCSharp(Book));
            File.WriteAllText(hjsonPath, BookExporter.ToHjson(Book));

            return Status = $"Exported to {root}";
        }
        catch (Exception exception)
        {
            return Status = $"Export failed: {exception.Message}";
        }
    }

    /// <summary>The configured source tree, or a folder in the save directory when none is set.</summary>
    private static string Root()
    {
        AuthoringConfig config = AuthoringConfig.Instance;

        if (config.HasSourceDirectory && Directory.Exists(config.SourceDirectory))
            return config.SourceDirectory;

        return Path.Combine(Main.SavePath, "TrelamiumIII", "Authoring");
    }

    #endregion
}
