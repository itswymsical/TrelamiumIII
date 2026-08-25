using System.Collections.Generic;
using ReLogic.Content;
using TrelamiumIII.Core.Books.Layout;
using TrelamiumIII.Core.Books.Layout.Blocks;
using TrelamiumIII.Core.Books.Text;

namespace TrelamiumIII.Core.Books.Lore;

/// <summary>Entry point for defining a lore book.</summary>
public static class LoreBook
{
    /// <summary>
    ///     Starts a definition. Finish it with <see cref="LoreBookBuilder.Register" />.
    /// </summary>
    /// <example>
    ///     <code>
    ///     LoreBook.Define("DrownedArchive")
    ///         .Heading("Title")
    ///         .Prose("Opening", BlockStyle.Body with { DropCap = true })
    ///         .Image(Assets.Textures.UI.Lore.ArchiveSeal.Asset, "SealCaption", span: 0.6f)
    ///         .Prose("Continued")
    ///         .StickyNote("MarginNote", offset: new Vector2(10f, -20f), rotation: -0.06f)
    ///         .Register();
    ///     </code>
    /// </example>
    public static LoreBookBuilder Define(string key) => new(key);
}

internal delegate BookBlock BlockFactory(LoreText text, BookContents contents);

internal delegate BookDecoration DecorationFactory(LoreText text);

/// <summary>
///     an ordered list of blocks, plus the decorations pinned to them.
/// </summary>
/// <remarks>
///     A definition holds factories rather than blocks.
/// </remarks>
public sealed class LoreBookDefinition
{
    public required string Key { get; init; }

    // Not `required`: the modifier would have to be as visible as the type, and these are only ever
    // set by the builder in this file.
    internal IReadOnlyList<BlockFactory> Blocks { get; init; } = [];

    internal IReadOnlyList<DecorationFactory> Decorations { get; init; } = [];

    /// <summary>Chapter headings, as (localization key, block index, depth), in reading order.</summary>
    internal IReadOnlyList<(string Key, int BlockIndex, int Depth)> Chapters { get; init; } = [];

    /// <summary>Localized title, for item tooltips and the cover imprint.</summary>
    public string Title => new LoreText(Key).Title;

    /// <summary>Instantiates the book's content in the current language.</summary>
    public LoreBookContent Build()
    {
        var text = new LoreText(Key);
        var contents = new BookContents();

        // Entries are created before the blocks so the contents block, which is itself one of those
        // blocks, has something to measure against. Page numbers are written in after composing.
        foreach ((string chapterKey, int blockIndex, int depth) in Chapters)
        {
            contents.Entries.Add(new ContentsEntry
            {
                Title = text.Get($"{chapterKey}.Title") ?? text.Get(chapterKey) ?? chapterKey,
                BlockIndex = blockIndex,
                Depth = depth,
            });
        }

        var blocks = new List<BookBlock>(Blocks.Count);

        foreach (BlockFactory factory in Blocks)
            blocks.Add(factory(text, contents));

        var decorations = new List<BookDecoration>(Decorations.Count);

        foreach (DecorationFactory factory in Decorations)
            decorations.Add(factory(text));

        return new LoreBookContent
        {
            Title = text.Title,
            Blocks = blocks,
            Decorations = decorations,
            Contents = contents,
        };
    }
}

/// <summary>One reading's worth of built content, ready to be flowed onto pages.</summary>
public sealed class LoreBookContent
{
    public required string Title { get; init; }

    public required List<BookBlock> Blocks { get; init; }

    public required List<BookDecoration> Decorations { get; init; }

    /// <summary>Chapter list. Page numbers are resolved once the blocks have been flowed.</summary>
    public required BookContents Contents { get; init; }
}

/// <summary>
///     Fluent builder for a lore book. Structure and presentation are set here; the words are not.
/// </summary>
/// <remarks>
///     Every method that takes text takes a <c>key</c> naming an entry under
///     <c>LoreLibrary.{book}</c>, never the text itself. Spacing, alignment, size and placement are
///     arguments here because they are decisions about the book, not about the language it is read in.
/// </remarks>
public sealed class LoreBookBuilder(string key)
{
    private readonly List<BlockFactory> blocks = [];
    private readonly List<DecorationFactory> decorations = [];
    private readonly List<(string Key, int BlockIndex, int Depth)> chapters = [];

    /// <summary>Typeface stamped onto blocks declared from here on. Null leaves them on vanilla.</summary>
    private BookTypeface? typeface;

    /// <summary>Index the next block will take. Decorations anchor to it.</summary>
    private int LastBlockIndex => blocks.Count - 1;

    /// <summary>Nested builder for a panel or section, carrying the enclosing typeface inward.</summary>
    internal LoreBookBuilder(string key, BookTypeface? inherited) : this(key) => typeface = inherited;

    #region Type

    /// <summary>
    ///     Sets the typeface for every block declared after this call.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Called before any block, this is the book's typeface. Called partway through, it opens
    ///         a new section and runs to the end of the book or the next call -- which is what a
    ///         section is here, since a book is a linear sequence of blocks. Use
    ///         <see cref="Section" /> instead when the change should close again afterwards.
    ///     </para>
    ///     <para>
    ///         Only fills in styles that did not name a typeface themselves, so an element-level
    ///         override survives.
    ///     </para>
    /// </remarks>
    public LoreBookBuilder Typeface(BookTypeface? face)
    {
        typeface = face;
        return this;
    }

    /// <summary>
    ///     A run of blocks set in <paramref name="face" />, after which the previous typeface resumes.
    /// </summary>
    /// <example>
    ///     <code>
    ///     book.Typeface(BookTypeface.Encyclopedia)
    ///         .Chapter("Entries")
    ///         .Prose("Definition")
    ///         .Section(BookTypeface.Glossary, s => s
    ///             .Heading("AsideTitle")
    ///             .Prose("AsideBody"))
    ///         .Prose("BackToEncyclopedia");
    ///     </code>
    /// </example>
    /// <remarks>
    ///     Runs the callback against this same builder rather than a nested one. A section is a run of
    ///     ordinary sibling blocks, so collecting them separately and merging afterwards would leave
    ///     every decoration inside the section anchored to the wrong block -- anchors are captured as
    ///     indices into the block list at the moment they are declared.
    /// </remarks>
    public LoreBookBuilder Section(BookTypeface? face, Action<LoreBookBuilder> contents)
    {
        BookTypeface? previous = typeface;

        typeface = face;
        contents(this);
        typeface = previous;

        return this;
    }

    /// <summary>
    ///     Picks the style for a block and stamps the current typeface onto it if it has none.
    /// </summary>
    /// <remarks>
    ///     Callers must invoke this when the block is <em>declared</em>, not inside the factory
    ///     lambda. The factory runs at <see cref="LoreBookDefinition.Build" />, by which point
    ///     <see cref="typeface" /> holds whatever the last section left behind, and every block in the
    ///     book would come out in that one face.
    /// </remarks>
    private BlockStyle Resolve(BlockStyle? style, BlockStyle fallback)
    {
        BlockStyle resolved = style ?? fallback;

        return resolved.Typeface is null ? resolved with { Typeface = typeface } : resolved;
    }

    #endregion

    #region Flow

    /// <summary>A section heading, ruled underneath by default.</summary>
    public LoreBookBuilder Heading(string key, bool rule = true, BlockStyle? style = null)
    {
        BlockStyle resolved = Resolve(style, BlockStyle.Heading);

        blocks.Add((text, _) => new HeadingBlock(text.Get(key), rule) { Style = resolved });
        return this;
    }

    /// <summary>
    ///     Opens a chapter: a heading that also earns a row in the table of contents.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Reads its title from <c>{key}.Title</c>, falling back to <c>{key}</c>, so a chapter can
    ///         keep its prose under the same key: <c>Ch1.Title</c> beside <c>Ch1.Body</c>.
    ///     </para>
    ///     <para>
    ///         Breaks the page before itself by default, which is what a chapter does in a printed
    ///         book and what keeps contents page numbers meaning something. Pass
    ///         <paramref name="pageBreak" /> false for a run of short entries -- diary dates, catalogue
    ///         items -- that would waste most of a page each.
    ///     </para>
    /// </remarks>
    /// <param name="depth">0 for a chapter, 1 for a section beneath it. Indents the contents row.</param>
    public LoreBookBuilder Chapter(
        string key,
        int depth = 0,
        bool pageBreak = true,
        bool rule = true,
        BlockStyle? style = null)
    {
        if (pageBreak && blocks.Count > 0)
            PageBreak();

        chapters.Add((key, blocks.Count, Math.Max(0, depth)));

        BlockStyle resolved = Resolve(style, depth > 0
            ? BlockStyle.Heading with { Scale = 0.5f, SpaceBefore = 8f }
            : BlockStyle.Heading);

        blocks.Add((text, _) => new HeadingBlock(text.Get($"{key}.Title") ?? text.Get(key), rule)
        {
            Style = resolved,
        });

        return this;
    }

    /// <summary>
    ///     The table of contents. Rows are filled from every <see cref="Chapter" /> in the book,
    ///     wherever they are declared relative to this call.
    /// </summary>
    public LoreBookBuilder Contents(string? header = null, BlockStyle? style = null)
    {
        BlockStyle resolved = Resolve(style, BlockStyle.Body with { Scale = 0.78f, SpaceAfter = 12f });

        blocks.Add((text, contents) => new ContentsBlock(contents, header is null ? null : text.Get(header))
        {
            Style = resolved,
        });

        return this;
    }

    /// <summary>A paragraph. The only block that breaks across pages.</summary>
    /// <remarks>
    ///     One call takes a whole chapter's prose: paragraphs are parted by
    ///     <see cref="BookText.PARAGRAPH_TOKEN" /> inside a single localization entry, and the block
    ///     flows across as many pages as it needs. There is no reason to declare a block per paragraph.
    /// </remarks>
    public LoreBookBuilder Prose(string key, BlockStyle? style = null)
    {
        BlockStyle resolved = Resolve(style, BlockStyle.Body);

        blocks.Add((text, _) => new ProseBlock(text.Get(key)) { Style = resolved });
        return this;
    }

    /// <summary>Quoted or excerpted matter, indented and lightened.</summary>
    public LoreBookBuilder Quote(string key, BlockStyle? style = null) =>
        Prose(key, style ?? BlockStyle.Quote);

    /// <summary>A horizontal divider.</summary>
    public LoreBookBuilder Rule(float span = 1f, float thickness = 1f, BlockStyle? style = null)
    {
        BlockStyle resolved = Resolve(style, BlockStyle.Body with { SpaceBefore = 6f, SpaceAfter = 8f });

        blocks.Add((_, _) => new RuleBlock
        {
            Span = span,
            Thickness = thickness,
            Style = resolved,
        });

        return this;
    }

    /// <summary>Blank vertical space.</summary>
    public LoreBookBuilder Space(float height = 32f)
    {
        BlockStyle resolved = Resolve(null, BlockStyle.Body with { SpaceAfter = 0f });

        blocks.Add((_, _) => new SpacerBlock(height) { Style = resolved });
        return this;
    }

    /// <summary>Pushes everything after this onto a fresh page.</summary>
    public LoreBookBuilder PageBreak()
    {
        blocks.Add((_, _) => new PageBreakBlock());
        return this;
    }

    /// <summary>A figure set into the page, optionally captioned.</summary>
    public LoreBookBuilder Image(
        Asset<Texture2D> texture,
        string? captionKey = null,
        float span = 1f,
        TextAlign align = TextAlign.Center,
        bool framed = true,
        Rectangle? frame = null,
        BlockStyle? style = null)
    {
        BlockStyle resolved = Resolve(style, BlockStyle.Body) with { Align = align };

        blocks.Add((text, _) => new ImageBlock(texture, text.Get(captionKey))
        {
            Span = span,
            Framed = framed,
            Frame = frame,
            Style = resolved,
        });

        return this;
    }

    /// <summary>A bordered aside holding its own blocks.</summary>
    public LoreBookBuilder Panel(Action<LoreBookBuilder> contents, float padding = 8f, BlockStyle? style = null)
    {
        var inner = new LoreBookBuilder(key, typeface);
        contents(inner);

        List<BlockFactory> children = inner.blocks;
        BlockStyle resolved = Resolve(style, BlockStyle.Body with { SpaceBefore = 6f, SpaceAfter = 10f });

        blocks.Add((text, bookContents) =>
        {
            var built = new List<BookBlock>(children.Count);

            foreach (BlockFactory factory in children)
                built.Add(factory(text, bookContents));

            return new PanelBlock(built)
            {
                Padding = padding,
                Style = resolved,
            };
        });

        return this;
    }

    #endregion

    #region Decorations

    /// <summary>
    ///     A note stuck over the page. Anchored to the block most recently added unless
    ///     <paramref name="anchorBlock" /> says otherwise, so it travels with that block through
    ///     reflow instead of being pinned to a page number that shifts.
    /// </summary>
    public LoreBookBuilder StickyNote(
        string key,
        Vector2 offset = default,
        float rotation = 0f,
        float width = 130f,
        DecorationAnchor anchor = DecorationAnchor.BlockTopRight,
        int? anchorBlock = null,
        Color? paper = null,
        BlockStyle? style = null)
    {
        int target = anchorBlock ?? LastBlockIndex;
        BlockStyle handStyle = Resolve(style, BlockStyle.Hand);

        decorations.Add(text => new BookDecoration
        {
            Content = new StickyNoteBlock(text.Get(key))
            {
                Style = handStyle,
                Paper = paper ?? new Color(232, 214, 142),
            },
            AnchorBlockIndex = target,
            Anchor = anchor,
            Offset = offset,
            Rotation = rotation,
            Width = width,
        });

        return this;
    }

    /// <summary>
    ///     Handwriting in the margin, with no paper behind it. Reads as something added to the page
    ///     rather than stuck onto it.
    /// </summary>
    public LoreBookBuilder Marginalia(
        string key,
        Vector2 offset = default,
        float rotation = 0f,
        float width = 110f,
        DecorationAnchor anchor = DecorationAnchor.BlockTopRight,
        int? anchorBlock = null,
        BlockStyle? style = null)
    {
        int target = anchorBlock ?? LastBlockIndex;
        BlockStyle handStyle = Resolve(style, BlockStyle.Hand);

        decorations.Add(text => new BookDecoration
        {
            Content = new ProseBlock(text.Get(key)) { Style = handStyle },
            AnchorBlockIndex = target,
            Anchor = anchor,
            Offset = offset,
            Rotation = rotation,
            Width = width,
        });

        return this;
    }

    #endregion

    /// <summary>Adds the finished definition to the registry.</summary>
    public LoreBookDefinition Register()
    {
        var definition = new LoreBookDefinition
        {
            Key = key,
            Blocks = blocks,
            Decorations = decorations,
            Chapters = chapters,
        };

        LoreBookRegistry.Add(definition);

        return definition;
    }
}
