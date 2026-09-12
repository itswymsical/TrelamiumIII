using System.Collections.Generic;
using ReLogic.Content;
using TrelamiumIII.Core.Books.Layout;
using TrelamiumIII.Core.Books.Layout.Blocks;
using TrelamiumIII.Core.Books.Text;

namespace TrelamiumIII.Core.Books.Lore;

/// <summary>Entry point for defining a lore book.</summary>
public static class BookNovel
{
    /// <summary>Starts a definition; finish with <see cref="BookBuilder.Register" />. See <c>ExampleLoreBook</c> for usage.</summary>
    public static BookBuilder Define(string key) => new(key);
}

internal delegate BookBlock BlockFactory(BookProseText text, BookContents contents);

internal delegate BookDecoration DecorationFactory(BookProseText text);

/// <summary>An ordered list of block factories, plus the decorations pinned to them.</summary>
public sealed class BookDefinition
{
    public required string Key { get; init; }

    // Not `required`: the modifier would have to be as visible as the type, and these are only ever
    // set by the builder in this file.
    internal IReadOnlyList<BlockFactory> Blocks { get; init; } = [];

    internal IReadOnlyList<DecorationFactory> Decorations { get; init; } = [];

    /// <summary>Chapter headings, as (localization key, block index, depth), in reading order.</summary>
    internal IReadOnlyList<(string Key, int BlockIndex, int Depth)> Chapters { get; init; } = [];

    /// <summary>Localized title, for item tooltips and the cover imprint.</summary>
    public string Title => new BookProseText(Key).Title;

    /// <summary>Instantiates the book's content in the current language.</summary>
    public BookContent Build()
    {
        var text = new BookProseText(Key);
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

        return new BookContent
        {
            Title = text.Title,
            Blocks = blocks,
            Decorations = decorations,
            Contents = contents,
        };
    }
}

/// <summary>One reading's worth of built content, ready to be flowed onto pages.</summary>
public sealed class BookContent
{
    public required string Title { get; init; }

    public required List<BookBlock> Blocks { get; init; }

    public required List<BookDecoration> Decorations { get; init; }

    /// <summary>Chapter list. Page numbers are resolved once the blocks have been flowed.</summary>
    public required BookContents Contents { get; init; }
}

/// <summary>Fluent lore book builder: structure and presentation here, words by localization key under <c>LoreLibrary.{book}</c>.</summary>
public sealed class BookBuilder(string key)
{
    private readonly List<BlockFactory> blocks = [];
    private readonly List<DecorationFactory> decorations = [];
    private readonly List<(string Key, int BlockIndex, int Depth)> chapters = [];

    /// <summary>Typeface stamped onto blocks declared from here on. Null leaves them on vanilla.</summary>
    private BookTypeface? typeface;

    /// <summary>Index the next block will take. Decorations anchor to it.</summary>
    private int LastBlockIndex => blocks.Count - 1;

    /// <summary>Nested builder for a panel or section, carrying the enclosing typeface inward.</summary>
    internal BookBuilder(string key, BookTypeface? inherited) : this(key) => typeface = inherited;

    #region Type

    /// <summary>
    ///     Sets the typeface for blocks declared after this, until the next call (before any block, it's the book's). Use
    ///     <see cref="Section" /> for a change that closes again; elements that name their own typeface keep it.
    /// </summary>
    public BookBuilder Typeface(BookTypeface? face)
    {
        typeface = face;
        return this;
    }

    /// <summary>
    ///     A run of blocks in <paramref name="face" />, after which the previous typeface resumes. Runs on this same builder, since
    ///     decoration anchors are block indices captured at declaration.
    /// </summary>
    public BookBuilder Section(BookTypeface? face, Action<BookBuilder> contents)
    {
        BookTypeface? previous = typeface;

        typeface = face;
        contents(this);
        typeface = previous;

        return this;
    }

    /// <summary>
    ///     Picks a block's style and stamps the current typeface. Call at declaration, not in the factory, which runs later with the wrong typeface.
    /// </summary>
    private BlockStyle Resolve(BlockStyle? style, BlockStyle fallback)
    {
        BlockStyle resolved = style ?? fallback;

        return resolved.Typeface is null ? resolved with { Typeface = typeface } : resolved;
    }

    #endregion

    #region Flow

    /// <summary>A section heading, ruled underneath by default.</summary>
    public BookBuilder Heading(string key, bool rule = true, BlockStyle? style = null)
    {
        BlockStyle resolved = Resolve(style, BlockStyle.Heading);

        blocks.Add((text, _) => new HeadingBlock(text.Get(key), rule) { Style = resolved });
        return this;
    }

    /// <summary>
    ///     A heading that also gets a contents row. Reads <c>{key}.Title</c> (falling back to <c>{key}</c>) and breaks the page first
    ///     unless <paramref name="pageBreak" /> is false. A <paramref name="depth" /> of 0 is a chapter, 1 a section beneath it.
    /// </summary>
    public BookBuilder Chapter(
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

    /// <summary>The table of contents, filled from every <see cref="Chapter" /> wherever it's declared.</summary>
    public BookBuilder Contents(string? header = null, BlockStyle? style = null)
    {
        BlockStyle resolved = Resolve(style, BlockStyle.Body with { Scale = 0.78f, SpaceAfter = 12f });

        blocks.Add((text, contents) => new ContentsBlock(contents, header is null ? null : text.Get(header))
        {
            Style = resolved,
        });

        return this;
    }

    /// <summary>
    ///     A paragraph that breaks across pages. One entry can hold a whole chapter, with paragraphs split by <see cref="BookText.PARAGRAPH_TOKEN" />.
    /// </summary>
    public BookBuilder Prose(string key, BlockStyle? style = null)
    {
        BlockStyle resolved = Resolve(style, BlockStyle.Body);

        blocks.Add((text, _) => new ProseBlock(text.Get(key)) { Style = resolved });
        return this;
    }

    /// <summary>Quoted or excerpted matter, indented and lightened.</summary>
    public BookBuilder Quote(string key, BlockStyle? style = null) =>
        Prose(key, style ?? BlockStyle.Quote);

    /// <summary>A horizontal divider.</summary>
    public BookBuilder Rule(float span = 1f, float thickness = 1f, BlockStyle? style = null)
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
    public BookBuilder Space(float height = 32f)
    {
        BlockStyle resolved = Resolve(null, BlockStyle.Body with { SpaceAfter = 0f });

        blocks.Add((_, _) => new SpacerBlock(height) { Style = resolved });
        return this;
    }

    /// <summary>Pushes everything after this onto a fresh page.</summary>
    public BookBuilder PageBreak()
    {
        blocks.Add((_, _) => new PageBreakBlock());
        return this;
    }

    /// <summary>A figure set into the page, optionally captioned.</summary>
    public BookBuilder Image(
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
    public BookBuilder Panel(Action<BookBuilder> contents, float padding = 8f, BlockStyle? style = null)
    {
        var inner = new BookBuilder(key, typeface);
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

    /// <summary>A note stuck over the page, anchored to the latest block (or <paramref name="anchorBlock" />) so it moves with reflow.</summary>
    public BookBuilder StickyNote(
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

    /// <summary>Handwriting in the margin, with no paper behind it.</summary>
    public BookBuilder Marginalia(
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
    public BookDefinition Register()
    {
        var definition = new BookDefinition
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
