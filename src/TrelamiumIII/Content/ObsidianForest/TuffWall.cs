namespace TrelamiumIII.Content.ObsidianForest;

/// <summary>Obsidian Forest background. Placeholder: the vanilla obsidian back wall's sprite.</summary>
public class TuffWall : ModWall
{
    public override string Texture => $"Terraria/Images/Wall_{WallID.ObsidianBackUnsafe}";

    public override void SetStaticDefaults()
    {
        DustType = DustID.Obsidian;
        AddMapEntry(new Color(52, 44, 58));
    }
}
