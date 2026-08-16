using Terraria.Localization;

namespace TrelamiumIII.Core.Books.Lore;

/// <summary>
///     Resolves a book's prose out of localization.
/// </summary>
/// <remarks>
///     <para>
///         Keys live under <c>LoreLibrary.{book}.{name}</c>, and every text-bearing block names its
///         own. Named keys rather than positional ones is deliberate: a book's blocks get reordered
///         and inserted between constantly, and an index-keyed translation would silently shuffle
///         onto the wrong blocks the first time that happened.
///     </para>
///     <para>
///         Resolution happens when a book is opened, not when it is defined, so a language change
///         takes effect on the next reading without any invalidation of its own.
///     </para>
/// </remarks>
public sealed class LoreText(string bookKey)
{
    public string BookKey => bookKey;

    /// <summary>Localized text for a block's key, or null when the key is absent.</summary>
    public string? Get(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        string key = TrelamiumIII.Instance.GetLocalizationKey($"LoreLibrary.{bookKey}.{name}");
        string value = Language.GetTextValue(key);

        // GetTextValue echoes the key path back when there is no entry, which would otherwise be
        // drawn on the page as if it were prose.
        return value == key ? null : value;
    }

    /// <summary>The book's title, from <c>LoreLibrary.{book}.Title</c>. Falls back to the key.</summary>
    public string Title => Get("Title") ?? bookKey;
}
