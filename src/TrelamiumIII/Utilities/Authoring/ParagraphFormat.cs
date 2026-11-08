namespace TrelamiumIII.Utilities.Authoring;

/// <summary>Settings shared by a whole paragraph, as opposed to per-character <see cref="CharFormat" />.</summary>
public readonly record struct ParagraphFormat(
    AuthoredBlockKind Kind,
    AuthoredAlign Align,
    bool DropCap,
    int Indent)
{
    /// <summary>Ordinary prose at the left margin. What a fresh paragraph starts as.</summary>
    public static readonly ParagraphFormat Body = new(AuthoredBlockKind.Prose, AuthoredAlign.Left, false, 0);

    /// <summary>Whether this paragraph earns a row in the outline.</summary>
    public bool IsHeading => Kind is AuthoredBlockKind.Heading or AuthoredBlockKind.Chapter;
}

/// <summary>One paragraph as the document sees it: a range of the text and the settings it carries.</summary>
/// <param name="Start">Offset of the first character.</param>
/// <param name="Length">Characters before the mark, excluding the newline itself.</param>
/// <param name="Index">Position in reading order, counting from zero.</param>
public readonly record struct Paragraph(int Start, int Length, int Index, ParagraphFormat Format)
{
    public int End => Start + Length;
}
