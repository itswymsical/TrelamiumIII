using Terraria.Audio;
using TrelamiumIII.Common.Systems;
using TrelamiumIII.Core.Tiles;

namespace TrelamiumIII.Content.Ingress;

/// <summary>
/// The Ligneous Fortress's reward chest: sealed while it stands in an unsolved room (<see cref="FortressProgress"/>),
/// which blocks opening, keys, quick stacking and mining. Drawn with vanilla's Living Wood chest until it has its own art.
/// </summary>
public sealed class LigneousChestTile : FurnitureActor
{
    /// <summary> The Living Wood chest's style on the vanilla container sheet. </summary>
    public const int Style = 12;

    public override string Texture => $"Terraria/Images/Tiles_{TileID.Containers}";

    public override string HighlightTexture => $"Terraria/Images/Misc/TileOutlines/Tiles_{TileID.Containers}";

    public override void SetStaticDefaults()
    {
        FurnitureType = FurnitureType.Chest;
        DustID = Terraria.ID.DustID.t_LivingWood;
        base.SetStaticDefaults();

        _ = this.GetLocalization("Sealed");
    }

    public override bool IsLockedChest(int i, int j) => FortressProgress.IsSealed(i, j);

    public override bool UnlockChest(int i, int j, ref short frameXAdjustment, ref int dustType, ref bool manual) => false;

    public override bool CanKillTile(int i, int j, ref bool blockDamaged) => !FortressProgress.IsSealed(i, j) && base.CanKillTile(i, j, ref blockDamaged);

    public override void MouseOver(int i, int j)
    {
        if (!FortressProgress.IsSealed(i, j))
        {
            base.MouseOver(i, j);
            return;
        }

        Player player = Main.LocalPlayer;
        player.noThrow = 2;
        player.cursorItemIconEnabled = true;
        player.cursorItemIconID = -1;
        player.cursorItemIconText = this.GetLocalization("Sealed").Value;
    }

    public override bool RightClick(int i, int j)
    {
        if (!FortressProgress.IsSealed(i, j))
            return base.RightClick(i, j);

        SoundEngine.PlaySound(SoundID.Tink, new Vector2(i * 16 + 8, j * 16 + 8));
        CombatText.NewText(new Rectangle(i * 16, j * 16, 16, 16), new Color(200, 230, 170), this.GetLocalization("Sealed").Value);

        return true;
    }
}

/// <summary> Places a <see cref="LigneousChestTile"/>; for authoring fortress rooms. </summary>
public sealed class LigneousChest : ModItem
{
    public override string Texture => $"Terraria/Images/Item_{ItemID.LivingWoodChest}";

    public override void SetDefaults()
    {
        Item.DefaultToPlaceableTile(ModContent.TileType<LigneousChestTile>(), LigneousChestTile.Style);
        Item.width = 26;
        Item.height = 22;
        Item.value = Item.sellPrice(silver: 10);
    }
}
