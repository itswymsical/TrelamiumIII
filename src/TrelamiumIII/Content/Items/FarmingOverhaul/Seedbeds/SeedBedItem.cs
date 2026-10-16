using TrelamiumIII.Common.Systems;
using TrelamiumIII.Content.Ingress;

namespace TrelamiumIII.Content.Items.FarmingOverhaul.Seedbeds;

/// <summary>A placeable seed bed. One instance per soil, each paired with the <see cref="SeedBedTile" /> it places.</summary>
public sealed class SeedBedItem : ModItem, ICustomAutoload
{
    private readonly string soil;
    private readonly int soilBlock;
    private readonly int tileType;

    private SeedBedItem(string soil, int soilBlock, int tileType)
    {
        this.soil = soil;
        this.soilBlock = soilBlock;
        this.tileType = tileType;
    }

    public static int Dirt { get; private set; }

    public static int Mud { get; private set; }

    public static int Sand { get; private set; }

    public static int Ash { get; private set; }
    public static int Peat { get; private set; }

    /// <summary>Every <see cref="SeedBedTile" /> type, available from load.</summary>
    public static int[] TileTypes { get; private set; } = [];

    protected override bool CloneNewInstances => true;

    public override string Name => $"SeedBedItem_{soil}";

    public static void Autoload(Mod mod)
    {
        Dirt = Add(mod, "Dirt", ItemID.DirtBlock, new(180, 140, 100), DustID.Dirt, [HerbType.Daybloom, HerbType.Blinkroot], out int dirt);

        // Shiverthorn matches its seed tooltip (SeedAmmoGlobalItem) until a peat or snow bed exists.
        Mud = Add(mod, "Mud", ItemID.MudBlock, new(100, 80, 60), DustID.Mud,
            [HerbType.Moonglow, HerbType.Waterleaf, HerbType.Shiverthorn], out int mud);

        Sand = Add(mod, "Sand", ItemID.SandBlock, new(220, 200, 140), DustID.Sand, [HerbType.Waterleaf], out int sand);
        Ash = Add(mod, "Ash", ItemID.AshBlock, new(80, 70, 70), DustID.Ash, [HerbType.Fireblossom, HerbType.Deathweed], out int ash);
        Peat = Add(mod, "Peat", ModContent.ItemType<PeatBlock>(), new(80, 70, 70), DustID.Ash, [HerbType.Fireblossom, HerbType.Deathweed], out int peat);

        TileTypes = [dirt, mud, sand, ash, peat];
    }

    private static int Add(Mod mod, string soil, int soilBlock, Color mapColor, int dust, HerbType[] herbs, out int tileType)
    {
        var tile = new SeedBedTile(soil, mapColor, dust, herbs);
        mod.AddContent(tile);

        var item = new SeedBedItem(soil, soilBlock, tile.Type);
        mod.AddContent(item);

        tile.DropItem = item.Type;
        tileType = tile.Type;

        return item.Type;
    }

    public override void SetDefaults()
    {
        Item.DefaultToPlaceableTile(tileType);
        Item.value = Item.sellPrice(copper: 78);
    }

    public override void AddRecipes()
    {
        CreateRecipe(2)
            .AddIngredient(soilBlock, 5)
            .AddIngredient(ItemID.RedBrick, 2)
            .AddTile(TileID.WorkBenches)
            .Register();
    }
}
