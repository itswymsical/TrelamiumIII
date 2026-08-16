using System;
using System.Collections.Generic;
using TrelamiumIII.Core.Books.Lore.Definitions;

namespace TrelamiumIII.Core.Books.Lore;

/// <summary>
///     Every lore book the mod defines, keyed by book id.
/// </summary>
/// <remarks>
///     Mirrors <see cref="Core.Dialogue.DialogueRegistry" />: definitions are declared in code under
///     <c>Definitions</c> and registered once at load, while their words live in
///     <c>Localization/LoreLibrary</c>.
/// </remarks>
public static class LoreBookRegistry
{
    private static readonly Dictionary<string, LoreBookDefinition> books = new(StringComparer.OrdinalIgnoreCase);

    public static void Add(LoreBookDefinition definition) => books[definition.Key] = definition;

    public static LoreBookDefinition? Get(string? key) =>
        key is not null && books.TryGetValue(key, out LoreBookDefinition? definition) ? definition : null;

    public static bool Has(string? key) => key is not null && books.ContainsKey(key);

    public static IEnumerable<LoreBookDefinition> All => books.Values;

    /// <summary>Registers every code-defined lore book. Called once from mod load.</summary>
    public static void Load()
    {
        ExampleLoreBook.Register();
        IlluminanceBook.Register();
        // Register further lore book definitions here.
    }

    public static void Unload() => books.Clear();
}
