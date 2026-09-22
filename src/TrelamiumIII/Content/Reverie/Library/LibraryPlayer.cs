using System.Collections.Generic;
using Terraria.DataStructures;
using Terraria.ModLoader.IO;

namespace TrelamiumIII.Content.Reverie.Library;

/// <summary> The books a player has borrowed from Reverie, at most <see cref="MaxLoans"/> at a time. </summary>
public sealed class LibraryPlayer : ModPlayer
{
    public const int MaxLoans = 3;

    private readonly List<string> loans = [];

    public IReadOnlyList<string> Loans => loans;

    public bool HasLoan(LibraryBook book) => loans.Contains(book.Id);

    public bool CanCheckOut(LibraryBook book) => loans.Count < MaxLoans && !HasLoan(book);

    /// <summary> Records the loan and hands the player the book. Local player only. </summary>
    public bool CheckOut(LibraryBook book)
    {
        if (!CanCheckOut(book))
            return false;

        loans.Add(book.Id);

        var item = new Item(ModContent.ItemType<LibraryBookItem>());
        ((LibraryBookItem)item.ModItem).SetBook(book);
        Player.QuickSpawnItem(new EntitySource_Gift(Player), item);

        return true;
    }

    /// <summary> Takes every borrowed book out of the inventory and clears the loans, lost books included. Returns how many loans ended. </summary>
    public int ReturnAll()
    {
        int type = ModContent.ItemType<LibraryBookItem>();

        for (int slot = 0; slot < Player.inventory.Length; slot++)
        {
            if (Player.inventory[slot].type == type)
                Player.inventory[slot].TurnToAir();
        }

        if (Main.mouseItem.type == type)
            Main.mouseItem.TurnToAir();

        int returned = loans.Count;
        loans.Clear();

        return returned;
    }

    public override void SaveData(TagCompound tag)
    {
        if (loans.Count > 0)
            tag["loans"] = loans;
    }

    public override void LoadData(TagCompound tag)
    {
        loans.Clear();

        foreach (string id in tag.GetList<string>("loans"))
        {
            if (LibraryCatalog.Get(id) is not null && loans.Count < MaxLoans)
                loans.Add(id);
        }
    }
}
