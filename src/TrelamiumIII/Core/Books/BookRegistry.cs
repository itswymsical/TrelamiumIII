using System;
using System.Collections.Generic;

namespace TrelamiumIII.Core.Books;

/// <summary>Every lore book by id, filled automatically as <see cref="ModBook" />s autoload.</summary>
public static class BookRegistry
{
    private static readonly Dictionary<string, BookDefinition> books = new(StringComparer.OrdinalIgnoreCase);

    public static void Add(BookDefinition definition) => books[definition.Key] = definition;

    public static BookDefinition? Get(string? key) =>
        key is not null && books.TryGetValue(key, out BookDefinition? definition) ? definition : null;

    public static bool Has(string? key) => key is not null && books.ContainsKey(key);

    public static IEnumerable<BookDefinition> All => books.Values;

    /// <summary>Drops every registration; books re-register on the next load.</summary>
    public static void Unload() => books.Clear();
}
