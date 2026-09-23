namespace TrelamiumIII.Content.Reverie;

/// <summary> Shelved books as a solid block. Uses 1.4.5's Librarian Block art as a placeholder. </summary>
public sealed class LibrarianBlockTile : ModTile
{
    public override string Texture => Assets.Textures.Placeholder.LibrarianBlock.KEY;

    public override void SetStaticDefaults()
    {
        Main.tileSolid[Type] = true;
        Main.tileBlockLight[Type] = true;

        DustType = DustID.WoodFurniture;
        HitSound = SoundID.Dig;

        AddMapEntry(new Color(96, 62, 44));
    }
}

/// <summary> Background bookshelves. Uses 1.4.5's Librarian Wall art as a placeholder. </summary>
public sealed class LibrarianWall : ModWall
{
    public override string Texture => Assets.Textures.Placeholder.LibrarianWall.KEY;

    public override void SetStaticDefaults()
    {
        Main.wallHouse[Type] = true;

        DustType = DustID.WoodFurniture;

        AddMapEntry(new Color(52, 36, 28));
    }
}