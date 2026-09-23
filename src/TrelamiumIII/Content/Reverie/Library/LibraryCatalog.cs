using System.Collections.Generic;
using System.Linq;
using Terraria.GameContent;
using Terraria.Localization;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Books.Lore;

namespace TrelamiumIII.Content.Reverie.Library;

public enum LibraryCategory
{
    Philosophy,
    History,
    Fiction
}

/// <summary>
/// A book Edith can lend. <paramref name="LoreKey"/> is the <see cref="ModLoreBook"/> it opens; placeholder entries borrow
/// an existing one. The cover is a mod texture path or, when <paramref name="CoverItem"/> is set, a vanilla item's art.
/// </summary>
public sealed record LibraryBook(string Id, LibraryCategory Category, string LoreKey, string? CoverPath = null, int CoverItem = 0, bool Illuminance = false)
{
    /// <summary> The catalog's title when it has one, otherwise the lore book's own. </summary>
    public string Title => Language.Exists(Key("Title"))
        ? Language.GetTextValue(Key("Title"))
        : LoreBookRegistry.Get(LoreKey)?.Title ?? Id;

    public string Blurb => Language.GetTextValue(Key("Blurb"));

    private string Key(string field) => $"Mods.{nameof(TrelamiumIII)}.Library.Books.{Id}.{field}";

    /// <summary> Client only: loads vanilla item art on first use. </summary>
    public Texture2D Cover()
    {
        if (CoverItem > ItemID.None)
        {
            Main.instance.LoadItem(CoverItem);
            return TextureAssets.Item[CoverItem].Value;
        }

        return ModContent.Request<Texture2D>(CoverPath!, ReLogic.Content.AssetRequestMode.ImmediateLoad).Value;
    }

    public void Open()
    {
        if (Illuminance)
            IlluminanceBookUI.Open(LoreKey);
        else
            BasicBookUI.Open(LoreKey);
    }
}

/// <summary> Every book in Reverie, by category. Titles and blurbs live under <c>Mods.TrelamiumIII.Library.Books</c>. </summary>
public static class LibraryCatalog
{
    /// <summary> The example lore book, which placeholder entries open. </summary>
    public const string PlaceholderKey = "Example.FirstAccount";

    private const string Lore = $"{nameof(TrelamiumIII)}/Content/Lore/";
    private const string IlluminanceCover = Lore + "IlluminanceBookItem";
    private const string ArchaicCover = Lore + "ArchaicBook1";
    private const string AlmanacCover = AssetReferences.Content.Items.Alchemy.AlchemyBookItem.KEY;

    public static readonly IReadOnlyList<LibraryBook> Books =
    [
        new("IlluminanceI", LibraryCategory.Philosophy, "IlluminanceSeries.IlluminanceI", IlluminanceCover, Illuminance: true),
        new("IlluminanceII", LibraryCategory.Philosophy, "IlluminanceSeries.IlluminanceII", IlluminanceCover, Illuminance: true),
        new("RootAndReason", LibraryCategory.Philosophy, PlaceholderKey, CoverItem: ItemID.Book),
        new("QuietSeasons", LibraryCategory.Philosophy, PlaceholderKey, CoverItem: ItemID.SpellTome),
        new("WeightOfLight", LibraryCategory.Philosophy, PlaceholderKey, CoverItem: ItemID.MagnetSphere),

        new("AnniesDiary", LibraryCategory.History, "Misc.Diary", ArchaicCover),
        new("PrimordialSands", LibraryCategory.History, "PrimordialSands1", CoverItem: ItemID.RazorbladeTyphoon),
        new("TreatiseOnSalts", LibraryCategory.History, PlaceholderKey, AlmanacCover),
        new("AnnalsOfTheDeep", LibraryCategory.History, PlaceholderKey, CoverItem: ItemID.DemonScythe),

        new("CorridorOfDarkness", LibraryCategory.Fiction, "Lore.CorridorOfDarkness", CoverItem: ItemID.BookofSkulls),
        new("FirstAccount", LibraryCategory.Fiction, PlaceholderKey, CoverItem: ItemID.Book),
        new("DrownedLantern", LibraryCategory.Fiction, PlaceholderKey, CoverItem: ItemID.WaterBolt),
        new("GlassOrchard", LibraryCategory.Fiction, PlaceholderKey, CoverItem: ItemID.CrystalStorm),
        new("CandleForTheMoth", LibraryCategory.Fiction, PlaceholderKey, CoverItem: ItemID.CursedFlames)
    ];

    public static LibraryBook? Get(string? id) => Books.FirstOrDefault(book => book.Id == id);

    public static IEnumerable<LibraryBook> In(LibraryCategory category) => Books.Where(book => book.Category == category);

    public static string CategoryName(LibraryCategory category) => Language.GetTextValue($"Mods.{nameof(TrelamiumIII)}.Library.Categories.{category}");
}