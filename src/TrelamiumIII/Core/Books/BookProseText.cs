using Terraria.Localization;

namespace TrelamiumIII.Core.Books;

/// <summary>
///     Resolves prose from <c>LoreLibrary.{book}.{name}</c> when a book opens. Keys are named, so reordering blocks can't shuffle translations.
/// </summary>
public sealed class BookProseText(string bookKey)
{
    public string BookKey => bookKey;

    /// <summary>Localized text for a block's key, or null when the key is absent.</summary>
    public string? Get(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        string key = TrelamiumIII.Instance.GetLocalizationKey($"BookLibrary.{bookKey}.{name}");
        string value = Language.GetTextValue(key);

        // GetTextValue echoes the key path back when there is no entry, which would otherwise be
        // drawn on the page as if it were prose.
        return value == key ? null : value;
    }

    /// <summary>The book's title, from <c>LoreLibrary.{book}.Title</c>. Falls back to the key.</summary>
    public string Title => Get("Title") ?? bookKey;
}
