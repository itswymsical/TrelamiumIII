using System;
using System.Collections.Generic;

namespace TrelamiumIII.Core.Books.Lore;

/// <summary>
///     Every lore book the mod defines, keyed by book id.
/// </summary>
/// <remarks>
///     Filled by <see cref="ModLoreBook" /> as tModLoader autoloads them, so there is nothing to
///     maintain here. Structure is declared in code under <c>Definitions</c>; the words live in
///     <c>Localization/LoreLibrary</c>.
/// </remarks>
public static class LoreBookRegistry
{
    private static readonly Dictionary<string, BookDefinition> books = new(StringComparer.OrdinalIgnoreCase);

    public static void Add(BookDefinition definition) => books[definition.Key] = definition;

    public static BookDefinition? Get(string? key) =>
        key is not null && books.TryGetValue(key, out BookDefinition? definition) ? definition : null;

    public static bool Has(string? key) => key is not null && books.ContainsKey(key);

    public static IEnumerable<BookDefinition> All => books.Values;

    /// <summary>
    ///     Drops every registration. Books re-register themselves on the next load, so this only has
    ///     to run once rather than per book.
    /// </summary>
    public static void Unload() => books.Clear();
}
