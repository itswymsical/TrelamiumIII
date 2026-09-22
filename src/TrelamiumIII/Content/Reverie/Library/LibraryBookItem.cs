using System.Collections.Generic;
using System.IO;
using Terraria.ModLoader.IO;

namespace TrelamiumIII.Content.Reverie.Library;

/// <summary>
/// A book borrowed from Reverie. Carries its <see cref="LibraryBook"/> id, takes that book's title and cover, and opens
/// its pages when used. Returned to Edith rather than kept.
/// </summary>
public sealed class LibraryBookItem : ModItem
{
    private string bookId = string.Empty;

    /// <summary> Only sizes the item; the book's own cover is drawn in its place. </summary>
    public override string Texture => $"Terraria/Images/Item_{ItemID.Book}";

    public LibraryBook? Book => LibraryCatalog.Get(bookId);

    public override void SetStaticDefaults() => _ = this.GetLocalization("Borrowed");

    public override void SetDefaults()
    {
        Item.width = 28;
        Item.height = 30;
        Item.maxStack = 1;
        Item.useStyle = ItemUseStyleID.HoldUp;
        Item.useTime = Item.useAnimation = 20;
        Item.rare = ItemRarityID.Blue;
        Item.UseSound = Assets.SFX.PageFlip.Asset;
    }

    public void SetBook(LibraryBook book)
    {
        bookId = book.Id;
        Item.SetNameOverride(book.Title);
    }

    public override bool? UseItem(Player player)
    {
        if (player.whoAmI == Main.myPlayer && !Main.dedServ)
            Book?.Open();

        return true;
    }

    public override void ModifyTooltips(List<TooltipLine> tooltips)
    {
        if (Book is not { } book)
            return;

        tooltips.Add(new TooltipLine(Mod, "LibraryCategory", LibraryCatalog.CategoryName(book.Category)) { OverrideColor = new Color(255, 214, 140) });
        tooltips.Add(new TooltipLine(Mod, "LibraryBorrowed", this.GetLocalization("Borrowed").Value));
    }

    public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
    {
        if (Book is not { } book)
            return true;

        Texture2D cover = book.Cover();
        float fit = scale * MathF.Min(1f, 32f / MathF.Max(cover.Width, cover.Height));
        spriteBatch.Draw(cover, position, null, drawColor, 0f, cover.Size() * 0.5f, fit, SpriteEffects.None, 0f);

        return false;
    }

    public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI)
    {
        if (Book is not { } book)
            return true;

        Texture2D cover = book.Cover();
        Vector2 position = Item.Bottom - Main.screenPosition - new Vector2(0f, cover.Height * 0.5f * scale);
        spriteBatch.Draw(cover, position, null, lightColor, rotation, cover.Size() * 0.5f, scale, SpriteEffects.None, 0f);

        return false;
    }

    public override void SaveData(TagCompound tag) => tag["book"] = bookId;

    public override void LoadData(TagCompound tag) => Restore(tag.GetString("book"));

    public override void NetSend(BinaryWriter writer) => writer.Write(bookId);

    public override void NetReceive(BinaryReader reader) => Restore(reader.ReadString());

    private void Restore(string id)
    {
        bookId = id;

        if (Book is { } book)
            Item.SetNameOverride(book.Title);
    }
}
