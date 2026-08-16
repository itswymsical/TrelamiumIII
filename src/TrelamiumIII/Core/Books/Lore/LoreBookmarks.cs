using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Terraria.ModLoader.IO;

namespace TrelamiumIII.Core.Books.Lore;

/// <summary>
///     Where a reader left off. Bookmarks are per player, per book, and survive between sessions.
/// </summary>
/// <remarks>
///     <para>
///         A fifty-page book is read across several sittings, so a bookmark that vanished on exit
///         would only ever mark a place within one of them. They are saved on the player rather than
///         the world because it is the reader who is keeping their place, and a character carrying a
///         book between worlds should not lose it.
///     </para>
///     <para>
///         Stored as flat <c>book=page,page,page</c> strings. <see cref="TagCompound" /> has no
///         dictionary support, and a pair of parallel lists would let the two halves drift apart if a
///         save were ever truncated.
///     </para>
/// </remarks>
public sealed class LoreBookmarkPlayer : ModPlayer
{
    /// <summary>Slots a reader gets per book. Enough to hold a few threads, few enough to stay a glance.</summary>
    public const int MAX_BOOKMARKS = 3;

    private const string TAG_KEY = "LoreBookmarks";
    private const char BOOK_SEPARATOR = '=';
    private const char PAGE_SEPARATOR = ',';

    private readonly Dictionary<string, List<int>> bookmarks = new(StringComparer.OrdinalIgnoreCase);

    public static LoreBookmarkPlayer? For(Player? player) =>
        player?.active == true ? player.GetModPlayer<LoreBookmarkPlayer>() : null;

    /// <summary>Pages marked in a book, in the order they were set. Never longer than <see cref="MAX_BOOKMARKS" />.</summary>
    public IReadOnlyList<int> Get(string bookKey) =>
        bookmarks.TryGetValue(bookKey, out List<int>? pages) ? pages : [];

    /// <summary>
    ///     Marks a page. Returns false when the book is already full, or the page already marked --
    ///     silently dropping either would leave the reader wondering which slot they just used.
    /// </summary>
    public bool Add(string bookKey, int page)
    {
        if (page < 0)
            return false;

        if (!bookmarks.TryGetValue(bookKey, out List<int>? pages))
            bookmarks[bookKey] = pages = [];

        if (pages.Count >= MAX_BOOKMARKS || pages.Contains(page))
            return false;

        pages.Add(page);

        return true;
    }

    public bool RemoveAt(string bookKey, int slot)
    {
        if (!bookmarks.TryGetValue(bookKey, out List<int>? pages) || slot < 0 || slot >= pages.Count)
            return false;

        pages.RemoveAt(slot);

        return true;
    }

    public override void SaveData(TagCompound tag)
    {
        var encoded = new List<string>();

        foreach ((string book, List<int> pages) in bookmarks)
        {
            if (pages.Count == 0)
                continue;

            encoded.Add($"{book}{BOOK_SEPARATOR}{string.Join(PAGE_SEPARATOR, pages)}");
        }

        if (encoded.Count > 0)
            tag[TAG_KEY] = encoded;
    }

    public override void LoadData(TagCompound tag)
    {
        bookmarks.Clear();

        if (!tag.ContainsKey(TAG_KEY))
            return;

        foreach (string entry in tag.GetList<string>(TAG_KEY))
        {
            int split = entry.IndexOf(BOOK_SEPARATOR);

            if (split <= 0)
                continue;

            string book = entry[..split];

            List<int> pages = entry[(split + 1)..]
                .Split(PAGE_SEPARATOR, StringSplitOptions.RemoveEmptyEntries)
                .Select(part => int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out int page) ? page : -1)
                .Where(page => page >= 0)
                .Take(MAX_BOOKMARKS)
                .ToList();

            if (pages.Count > 0)
                bookmarks[book] = pages;
        }
    }
}
