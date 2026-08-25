using Terraria.Localization;

namespace TrelamiumIII.Core.Alchemy;

/// <summary>How a guide chapter renders in the Almanac's Notes section.</summary>
public enum NoteKind
{
    /// <summary>Localized body text, paginated across the spread.</summary>
    Prose,
    /// <summary>The "Seven Principles" glossary table (special renderer; body is a short intro).</summary>
    Principles,
    /// <summary>Chemical-type axes + solute/solvent interaction rules (special renderer; body is a short intro).</summary>
    Chemistry,
}

public sealed class AlmanacNote
{
    public string Id { get; init; }
    public NoteKind Kind { get; init; } = NoteKind.Prose;

    public string Title
    {
        get
        {
            string key = TrelamiumIII.Instance.GetLocalizationKey($"Almanac.Notes.{Id}.Title");
            string value = Language.GetTextValue(key);
            return value == key ? Id : value;
        }
    }

    public string Body
    {
        get
        {
            string key = TrelamiumIII.Instance.GetLocalizationKey($"Almanac.Notes.{Id}.Body");
            string value = Language.GetTextValue(key);
            return value == key ? string.Empty : value;
        }
    }
}

public static class AlmanacNotes
{
    private static readonly AlmanacNote[] _chapters =
    {
        new() { Id = "Foreword" },
        new() { Id = "HowToUse" },
        new() { Id = "Principles", Kind = NoteKind.Principles },
        new() { Id = "Chemistry", Kind = NoteKind.Chemistry },
        new() { Id = "OnPotency" },
    };

    public static int Count => _chapters.Length;

    public static AlmanacNote At(int index) =>
        index >= 0 && index < _chapters.Length ? _chapters[index] : null;
}
