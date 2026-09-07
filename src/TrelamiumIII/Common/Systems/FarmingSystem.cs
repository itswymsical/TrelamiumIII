using System.Collections.Generic;
using Terraria.Enums;
using TrelamiumIII.Content.FarmingOverhaul.Seedbeds;

namespace TrelamiumIII.Common.Systems;

public enum HerbType
{
    Daybloom = 0,
    Moonglow = 1,
    Blinkroot = 2,
    Deathweed = 3,
    Waterleaf = 4,
    Fireblossom = 5,
    Shiverthorn = 6
}

public class FarmingSystem : GlobalTile
{
    public static readonly Dictionary<int, SeedBedTile> SeedBedTypes = [];

    public override void Load()
    {
        On_WorldGen.CanCutTile += StopCut;
        On_WorldGen.PlaceAlch += ForcePlaceAlch;
        On_WorldGen.GrowAlch += CustomGrowAlch;
        On_WorldGen.CheckAlch += CustomCheckAlch;
    }

    private static bool StopCut(On_WorldGen.orig_CanCutTile orig, int x, int y, TileCuttingContext context)
    {
        if (Main.tile[x, y + 1] != null && SeedBedTypes.ContainsKey(Main.tile[x, y + 1].TileType))
            return false;

        return orig(x, y, context);
    }

    private static bool ForcePlaceAlch(On_WorldGen.orig_PlaceAlch orig, int x, int y, int style)
    {
        if (Main.tile[x, y + 1] != null && SeedBedTypes.ContainsKey(Main.tile[x, y + 1].TileType))
        {
            var tile = Main.tile[x, y];
            tile.HasTile = true;
            tile.TileType = TileID.ImmatureHerbs;
            tile.TileFrameX = (short)(18 * style);
            tile.TileFrameY = 0;

            var seedBed = SeedBedTypes[Main.tile[x, y + 1].TileType];
            var herbName = HerbName(style * 18);
            TrelamiumIII.Instance.Logger.Info($"[FarmingSystem] Placed {herbName} (Immature) on {seedBed.GetType().Name} at ({x}, {y})");

            return true;
        }

        return orig(x, y, style);
    }

    private static void CustomGrowAlch(On_WorldGen.orig_GrowAlch orig, int i, int j)
    {
        var tile = Framing.GetTileSafely(i, j);
        var tileBelow = Framing.GetTileSafely(i, j + 1);

        if (!IsHerbTile(tile.TileType) || !tileBelow.HasTile || !SeedBedTypes.TryGetValue(tileBelow.TileType, out var seedBed))
        {
            orig(i, j);
            return;
        }

        var herb = Main.tile[i, j];
        if (!herb.HasTile) return;

        for (int attempt = 0; attempt < seedBed.GrowthAttempts; attempt++)
        {
            if (!WorldGen.genRand.NextBool(seedBed.GrowthChance))
                continue;

            short frame = herb.TileFrameX;
            string herbName = HerbName(frame);
            string fromStage, toStage;

            if (herb.TileType == TileID.ImmatureHerbs)
            {
                fromStage = "Immature";
                toStage = "Mature";
                herb.TileType = TileID.MatureHerbs;
                herb.TileFrameX = frame;
                herb.TileFrameY = 0;
            }
            else if (herb.TileType == TileID.MatureHerbs)
            {
                fromStage = "Mature";
                toStage = "Blooming";
                herb.TileType = TileID.BloomingHerbs;
                herb.TileFrameX = frame;
                herb.TileFrameY = 0;
            }
            else break; // already blooming, nothing to do

            TrelamiumIII.Instance.Logger.Info(
                $"[FarmingSystem] {herbName} {fromStage} => {toStage} " +
                $"on {seedBed.GetType().Name} at ({i}, {j}) " +
                $"[attempt {attempt + 1}/{seedBed.GrowthAttempts}, chance 1/{seedBed.GrowthChance}]"
            );

            if (Main.netMode != NetmodeID.SinglePlayer)
                NetMessage.SendTileSquare(-1, i, j, 1, 1, TileChangeType.None);

            break;
        }
    }

    private static void CustomCheckAlch(On_WorldGen.orig_CheckAlch orig, int x, int y)
    {
        var herb = Framing.GetTileSafely(x, y);
        var tileBelow = Framing.GetTileSafely(x, y + 1);

        if (IsHerbTile(herb.TileType) && tileBelow.HasTile && SeedBedTypes.ContainsKey(tileBelow.TileType))
            return;

        orig(x, y);
    }

    public override bool TileFrame(int i, int j, int type, ref bool resetFrame, ref bool noBreak)
    {
        if ((type is TileID.Plants or TileID.Plants2 || Main.tileAlch[type]) &&
            SeedBedTypes.ContainsKey(Framing.GetTileSafely(i, j + 1).TileType))
        {
            noBreak = true;
        }
        else if (SeedBedTypes.ContainsKey(type))
        {
            UpdateSeedBedFraming(i, j);
            return false;
        }

        return true;
    }

    private static void UpdateSeedBedFraming(int ci, int cj)
    {
        if (!IsSeedBed(ci, cj)) return;

        int left = ci;
        int right = ci;

        while (IsSeedBed(left - 1, cj)) left--;
        while (IsSeedBed(right + 1, cj)) right++;

        for (int x = left; x <= right; x++)
        {
            if (IsSeedBed(x, cj))
                UpdateSeedBedTileFrame(x, cj, left, right);
        }
    }

    private static void UpdateSeedBedTileFrame(int i, int j, int left, int right)
    {
        var tile = Framing.GetTileSafely(i, j);
        if (!tile.HasTile || !SeedBedTypes.ContainsKey(tile.TileType)) return;

        int length = right - left + 1;
        int position = i - left;
        int frameX;

        if (length == 1)
        {
            frameX = 7;
        }
        else if (position == 0)
        {
            frameX = 0;
        }
        else if (position == length - 1)
        {
            frameX = 6;
        }
        else
        {
            int center = length / 2;

            if (position == center && length % 2 == 1)
                frameX = 3;
            else if (position < center)
                frameX = 1 + (position - 1) % 2;
            else
                frameX = 4 + (position - center - length % 2) % 2;
        }

        short newFrameX = (short)(frameX * 18);
        if (tile.TileFrameX == newFrameX) return;

        tile.TileFrameX = newFrameX;
        tile.TileFrameY = 0;

        if (Main.netMode != NetmodeID.SinglePlayer)
            NetMessage.SendTileSquare(-1, i, j, 1, 1, TileChangeType.None);
    }

    private static bool IsSeedBed(int i, int j)
    {
        var tile = Framing.GetTileSafely(i, j);
        return tile.HasTile && SeedBedTypes.ContainsKey(tile.TileType);
    }

    /// <summary>
    /// Attempts a single growth tick on a herb at (i, j).
    /// Requires the herb to be sitting on a registered seedbed.
    /// <para>chanceMultiplier scales the seedbed's GrowthChance upward (worse odds).
    /// Pass a value greater than 1 for rapid-fire sources like the watering can.</para>
    /// Returns true if the herb advanced a stage.
    /// </summary>
    public static bool TryGrowHerb(int i, int j, int chanceMultiplier = 1)
    {
        var herb = Framing.GetTileSafely(i, j);
        var tileBelow = Framing.GetTileSafely(i, j + 1);

        if (!herb.HasTile || !IsHerbTile(herb.TileType))
            return false;

        if (!tileBelow.HasTile || !SeedBedTypes.TryGetValue(tileBelow.TileType, out var seedBed))
            return false;

        if (herb.TileType == TileID.BloomingHerbs)
            return false;

        if (!WorldGen.genRand.NextBool(seedBed.GrowthChance * chanceMultiplier))
            return false;

        short frame = herb.TileFrameX;

        if (herb.TileType == TileID.ImmatureHerbs)
        {
            herb.TileType = TileID.MatureHerbs;
            herb.TileFrameX = frame;
            herb.TileFrameY = 0;
        }
        else
        {
            herb.TileType = TileID.BloomingHerbs;
            herb.TileFrameX = frame;
            herb.TileFrameY = 0;
        }

        TrelamiumIII.Instance.Logger.Info(
            $"[FarmingSystem] TryGrowHerb: {HerbName(frame)} advanced on {seedBed.GetType().Name} at ({i}, {j})"
        );

        if (Main.netMode != NetmodeID.SinglePlayer)
            NetMessage.SendTileSquare(-1, i, j, 1, 1, TileChangeType.None);

        return true;
    }

    /// <summary>Derives a readable herb name from TileFrameX.</summary>
    private static string HerbName(int frameX)
        => (HerbType)(frameX / 18) switch
        {
            HerbType.Daybloom => "Daybloom",
            HerbType.Moonglow => "Moonglow",
            HerbType.Blinkroot => "Blinkroot",
            HerbType.Deathweed => "Deathweed",
            HerbType.Waterleaf => "Waterleaf",
            HerbType.Fireblossom => "Fireblossom",
            HerbType.Shiverthorn => "Shiverthorn",
            _ => $"Unknown (frame {frameX})"
        };

    public static bool IsHerbTile(int type)
        => type is TileID.BloomingHerbs or TileID.MatureHerbs or TileID.ImmatureHerbs;

    public override void Unload() => SeedBedTypes.Clear();
}