using System.Collections.Generic;
using System.Globalization;
using System.IO;
using TrelamiumIII.Common.Configs;
using TrelamiumIII.Core.Books.Layout;

namespace TrelamiumIII.Core.Books.Authoring;

/// <summary>Which of the editable strings the shared buffer is pointed at.</summary>
public enum EditorField
{
    /// <summary>The prose itself. The only field that is more than one line.</summary>
    BlockText,

    BlockKey,

    /// <summary>Text size, typed rather than stepped. Parsed on commit.</summary>
    BlockScale,

    BookKey,
    BookTitle,
}

/// <summary>Extensions over <see cref="EditorField" />.</summary>
public static class EditorFieldExtensions
{
    /// <summary>A one-line value rather than prose: measured in the chrome font, never wraps, and Return means "done".</summary>
    public static bool IsShort(this EditorField field) => field != EditorField.BlockText;
}

/// <summary>
///     One editing sitting. Only the selected block's text lives in the buffer, so each block keeps its own undo history.
/// </summary>
public sealed class AuthoringSession
{
    public AuthoringSession()
    {
        Book.Blocks.Add(new AuthoredBlock { Kind = AuthoredBlockKind.Heading, Key = "Title", Text = "Untitled" });
        Book.Blocks.Add(new AuthoredBlock { Kind = AuthoredBlockKind.Prose, Key = "Opening" });

        // Commit's guard makes this harmless: the buffer hasn't been loaded from anything yet.
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
    ///     Points the shared buffer at another field, saving it first, so selection, clipboard and undo work the same everywhere.
    /// </summary>
    public void FocusField(EditorField field)
    {
        if (field == Focus) return;

        Commit();
        Focus = field;
        Reload();
    }

    /// <summary>
    ///     Writes the buffer back to where it was loaded from; a no-op if focus moved, since writing elsewhere would silently overwrite text.
    /// </summary>
    public void Commit()
    {
        if (loadedFrom != SelectedIndex || loadedField != Focus) return;

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

            // Unparseable input leaves the size alone rather than snapping to a default: a half-typed
            // "1." is a value mid-edit, not a mistake to correct.
            case EditorField.BlockScale when Selected is { } sized:
                if (float.TryParse(Buffer.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
                    sized.Scale = Math.Clamp(parsed, 0.2f, 3f);

                break;
            // Stored as tags, so the block's text is always exactly what exports, with no parallel representation to drift.
            case EditorField.BlockText when Selected is { HasText: true } block:
                block.Text = FormatCodec.Serialize(Buffer.Text, Buffer.Formats);
                break;
        }
    }

    /// <summary>Block index the buffer's contents came from, or -1 when it holds nothing's text.</summary>
    private int loadedFrom = -1;

    private EditorField loadedField = EditorField.BlockText;

    private void Reload()
    {
        loadedFrom = SelectedIndex;
        loadedField = Focus;

        ReloadCore();
    }

    private void ReloadCore()
    {
        if (Focus == EditorField.BlockText)
        {
            // Tags come off on the way in and go back on the way out, so a book written by hand
            // loads into the editor and survives the round trip untouched.
            (string plain, CharFormat[] formats) =
                FormatCodec.Parse(Selected is { HasText: true } block ? block.Text : string.Empty);

            Buffer.Reset(plain, formats);

            return;
        }

        Buffer.Reset(Focus switch
        {
            EditorField.BookKey    => Book.Key,
            EditorField.BookTitle  => Book.Title,
            EditorField.BlockKey   => Selected?.Key ?? string.Empty,
            EditorField.BlockScale => ScaleText,
            _                      => string.Empty,
        });
    }

    /// <summary>The effective size as text, seeded from the preset so typing starts from what's on the page.</summary>
    public string ScaleText => EffectiveScale.ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>The size the selected block renders at, override or preset.</summary>
    public float EffectiveScale
    {
        get
        {
            if (Selected is not { } block) return 1f;

            if (block.Scale > 0f) return block.Scale;

            // Read from the presets, not restated, so the size box can't disagree with the render after a retune.
            return block.Kind switch
            {
                AuthoredBlockKind.Heading or AuthoredBlockKind.Chapter => BlockStyle.Heading.Scale,
                AuthoredBlockKind.Quote                                => BlockStyle.Quote.Scale,
                AuthoredBlockKind.Caption                              => BlockStyle.Caption.Scale,
                _                                                      => BlockStyle.Body.Scale,
            };
        }
    }

    /// <summary>Nudges the size, seeding from the preset when there was no override yet.</summary>
    public void StepScale(float delta)
    {
        if (Selected is not { } block) return;

        block.Scale = Math.Clamp(EffectiveScale + delta, 0.2f, 3f);

        if (Focus == EditorField.BlockScale) ReloadCore();
    }

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

        int target = SelectedIndex + 1;

        Book.Blocks.Insert(target, new AuthoredBlock
        {
            Kind = kind,
            Key = UniqueKey(kind.ToString()),
        });

        SelectedIndex = -1;
        SelectBlock(target);
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

        // The buffer still holds this block's text at its new position, so loadedFrom follows it; otherwise the commit guard
        // would drop every later edit.
        loadedFrom = target;

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
    ///     Writes the hjson fragment and C# definition and reports where. The hjson isn't merged into the library; that stays a manual paste.
    /// </summary>
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
        DeveloperConfig config = DeveloperConfig.Instance;

        if (config.HasSourceDirectory && Directory.Exists(config.SourceDirectory))
            return config.SourceDirectory;

        return Path.Combine(Main.SavePath, "TrelamiumIII", "Authoring");
    }

    #endregion
}
