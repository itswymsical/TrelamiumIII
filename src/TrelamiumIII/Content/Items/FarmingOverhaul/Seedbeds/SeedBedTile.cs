using TrelamiumIII.Common.Systems;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ObjectData;

namespace TrelamiumIII.Content.Items.FarmingOverhaul.Seedbeds;

/// <summary>A seed bed for one soil. Registered by <see cref="SeedBedItem" />.</summary>
public sealed class SeedBedTile : ModTile
{
    private readonly string soil;
    private readonly Color mapColor;

    public SeedBedTile(string soil, Color mapColor, int soilDust, HerbType[] preferredHerbs)
    {
        this.soil = soil;
        this.mapColor = mapColor;

        SoilDust = soilDust;
        PreferredHerbs = preferredHerbs;
    }

    public override string Name => $"SeedBedTile_{soil}";

    /// <summary>Growth attempts per random tick.</summary>
    public int GrowthAttempts => 3;

    /// <summary>1-in-N chance per attempt to advance a growth stage.</summary>
    public int GrowthChance => 10;

    /// <summary>Herbs that thrive on this soil. Read by Silvian's botany job (PlantInSeedBedTask); growth ignores it so far.</summary>
    public HerbType[] PreferredHerbs { get; }

    public int SoilDust { get; }

    /// <summary>The <see cref="SeedBedItem" /> this tile drops. Set at load.</summary>
    internal int DropItem { get; set; }

    public override void SetStaticDefaults()
    {
        Main.tileSolid[Type] = false;
        Main.tileSolidTop[Type] = true;
        Main.tileFrameImportant[Type] = true;
        Main.tileLavaDeath[Type] = true;
        Main.tileBlockLight[Type] = true;
        Main.tileLighted[Type] = false;

        TileObjectData.newTile.CopyFrom(TileObjectData.Style1x1);
        TileObjectData.newTile.CoordinateWidth = 18;
        TileObjectData.newTile.CoordinateHeights = [16];
        TileObjectData.newTile.DrawYOffset = -(16 - 18);
        TileObjectData.newTile.UsesCustomCanPlace = true;
        TileObjectData.newTile.AnchorBottom = new AnchorData(
            AnchorType.SolidTile | AnchorType.SolidWithTop, 1, 0);
        TileObjectData.newTile.AnchorInvalidTiles = [TileID.MagicalIceBlock];

        AdjTiles = [TileID.PlanterBox, TileID.ClayPot];
        TileID.Sets.IgnoredByNpcStepUp[Type] = true;

        AddMapEntry(mapColor, CreateMapEntryName());
        DustType = SoilDust;
        HitSound = SoundID.Dig;

        TileObjectData.addTile(Type);
        FarmingSystem.SeedBedTypes.Add(Type, this);

        RegisterItemDrop(DropItem);
    }

    public override void PostSetDefaults() => Main.tileNoSunLight[Type] = false;

    public override bool CanPlace(int i, int j)
    {
        var below = Framing.GetTileSafely(i, j + 1);
        return below.HasTile
            && (Main.tileSolid[below.TileType] || below.TileType == Type)
            && !below.IsHalfBlock
            && below.Slope == 0;
    }

    public override void PlaceInWorld(int i, int j, Item item) => TriggerFramingUpdate(i, j);

    public override void KillTile(int i, int j, ref bool fail, ref bool effectOnly, ref bool noItem)
    {
        if (fail) return;
        TriggerFramingUpdate(i - 1, j);
        TriggerFramingUpdate(i + 1, j);
    }

    public override void RandomUpdate(int i, int j)
    {
        var tile = Framing.GetTileSafely(i, j);
        if (!tile.HasTile || tile.TileType != Type) return;
        if (!WorldGen.genRand.NextBool(20)) return;

        var pos = new Vector2(i * 16 + 8, j * 16 + 8);
        var dust = Dust.NewDustDirect(pos, 0, 0, SoilDust,
            Main.rand.NextFloat(-0.5f, 0.5f),
            Main.rand.NextFloat(-1f, 0f));
        dust.fadeIn = 0.8f;
        dust.scale = 0.6f;
    }

    private void TriggerFramingUpdate(int i, int j)
    {
        if (!WorldGen.InWorld(i, j)) return;
        var tile = Main.tile[i, j];
        if (!tile.HasTile || !FarmingSystem.SeedBedTypes.ContainsKey(tile.TileType)) return;

        bool reset = false;
        bool noBreak = false;
        WorldGen.TileFrame(i, j, reset, noBreak);
    }
}
