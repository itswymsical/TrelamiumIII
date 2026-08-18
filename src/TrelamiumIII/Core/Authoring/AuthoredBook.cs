using System.Collections.Generic;

namespace TrelamiumIII.Core.Authoring;

/// <summary>Which builder call an authored block turns into.</summary>
public enum AuthoredBlockKind
{
    Prose,
    Heading,
    Chapter,
    Quote,
    Caption,
    Rule,
    Space,
    PageBreak,
}

/// <summary>Typeface choice as a serialisable value, since the sets themselves hold delegates.</summary>
public enum AuthoredTypeface
{
    Inherit,
    Vanilla,
    Encyclopedia,
    Glossary,
}

/// <summary>Line alignment, mirroring <c>TextAlign</c> without depending on the render layer.</summary>
public enum AuthoredAlign
{
    Left,
    Center,
    Right,
    Justify,
}

/// <summary>
///     One block as the editor holds it: a kind, the localization key it will be written under, and
///     the prose itself.
/// </summary>
/// <remarks>
///     The formatting here is deliberately paragraph-level only. Character formatting stays inside
///     <see cref="Text" /> as the same inline tags the renderer already understands, so it needs no
///     representation of its own and survives the round trip through hjson untouched.
/// </remarks>
public sealed class AuthoredBlock
{
    public AuthoredBlockKind Kind { get; set; } = AuthoredBlockKind.Prose;

    /// <summary>Key suffix under <c>LoreLibrary.{book}</c>. Ignored by blocks that carry no text.</summary>
    public string Key { get; set; } = "Untitled";

    public string Text { get; set; } = string.Empty;

    public AuthoredAlign Align { get; set; } = AuthoredAlign.Left;

    public AuthoredTypeface Typeface { get; set; } = AuthoredTypeface.Inherit;

    public bool Bold { get; set; }

    public bool Italic { get; set; }

    /// <summary>Text size multiplier, overriding the preset when non-zero.</summary>
    public float Scale { get; set; }

    /// <summary>
    ///     Ink colour packed as 0xRRGGBB, or null to keep the preset's.
    /// </summary>
    /// <remarks>
    ///     Held as an int rather than an XNA <c>Color</c> so this model stays free of the graphics
    ///     stack. That is what lets it -- and the exporter over it -- be tested without a device, and
    ///     it is the form the generated C# needs anyway.
    /// </remarks>
    public int? Ink { get; set; }

    public bool DropCap { get; set; }

    /// <summary>Contents depth for <see cref="AuthoredBlockKind.Chapter" />. 0 is a chapter.</summary>
    public int Depth { get; set; }

    /// <summary>Whether a chapter breaks the page before itself.</summary>
    public bool PageBreak { get; set; } = true;

    /// <summary>Whether a heading or chapter is ruled underneath.</summary>
    public bool Rule { get; set; } = true;

    /// <summary>True for the kinds that own a localization entry.</summary>
    public bool HasText => Kind
        is AuthoredBlockKind.Prose
        or AuthoredBlockKind.Heading
        or AuthoredBlockKind.Chapter
        or AuthoredBlockKind.Quote
        or AuthoredBlockKind.Caption;
}

/// <summary>A whole book as the editor holds it, before it becomes hjson and C#.</summary>
public sealed class AuthoredBook
{
    /// <summary>Registry key and localization scope, e.g. <c>Example.FirstAccount</c>.</summary>
    public string Key { get; set; } = "Untitled.Book";

    public string Title { get; set; } = "Untitled";

    public AuthoredTypeface Typeface { get; set; } = AuthoredTypeface.Inherit;

    public List<AuthoredBlock> Blocks { get; } = [];

    /// <summary>Class name for the generated definition: the key with its dots removed, plus Book.</summary>
    public string ClassName
    {
        get
        {
            var name = new System.Text.StringBuilder();

            foreach (char c in Key)
            {
                if (char.IsLetterOrDigit(c)) name.Append(name.Length == 0 ? char.ToUpperInvariant(c) : c);
            }

            if (name.Length == 0) name.Append("Untitled");

            return $"{name}Book";
        }
    }

    /// <summary>
    ///     Reports keys that would collide in the localization file, since two blocks writing the same
    ///     key silently lose one of them.
    /// </summary>
    public IEnumerable<string> DuplicateKeys()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var reported = new HashSet<string>(StringComparer.Ordinal);

        foreach (AuthoredBlock block in Blocks)
        {
            if (!block.HasText) continue;

            if (!seen.Add(block.Key) && reported.Add(block.Key)) yield return block.Key;
        }
    }
}
