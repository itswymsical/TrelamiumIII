using Terraria.Audio;
using Terraria.DataStructures;
using TrelamiumIII.Common.Systems;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Alchemy;
using TrelamiumIII.Core.Alchemy.Brewing;
using TrelamiumIII.Core.Alchemy.Brewing.Behaviors;
using TrelamiumIII.Core.UI;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Content.Items.Alchemy.Solutions;

/// <summary>Brewed from a Fortunate Tonic: mined ore and gems pay out extra. Replaces the Microlith accessory.</summary>
public class HarvestPotionItem : SolutionItem
{
    public HarvestPotionItem()
    {
        Rare = ItemRarityID.Blue;
        Value = Item.sellPrice(silver: 2);
        TexturePath = AssetReferences.Content.Items.Alchemy.Solutions.HarvestPotion.KEY;

        AlmanacPotency = 3;
        Quality = ChemicalQuality.Catalytic;
        Principle = PrincipleType.Fortune;

        Defaults = item =>
        {
            item.useStyle = ItemUseStyleID.DrinkLiquid;
            item.useAnimation = item.useTime = 17;
            item.useTurn = true;
            item.consumable = true;
            item.UseSound = SoundID.Item3;
            item.buffType = ModContent.BuffType<HarvestBuff>();
            item.buffTime = TimeConversion.SetTime(mins: 8);
        };
    }
}

public class HarvestBuff : ModBuff
{
    public override void SetStaticDefaults() => EffectBehaviors.Register(Type, new HarvestBehavior());
}

/// <summary>Describes the brewed Harvest effect; the payout itself runs from <see cref="HarvestDropTile" />.</summary>
public sealed class HarvestBehavior : IEffectBehavior
{
    public void Apply(Player player, in AlchemicalEffect fx) { }

    public string Describe(in AlchemicalEffect fx) =>
        $"{(int)MathF.Round(HarvestDrops.Chance(fx.StrengthPercent) * 100)}% chance to mine "
        + $"up to {HarvestDrops.MaxExtra(fx.Potency)} extra ore or gems";
}

/// <summary>Payout odds for <see cref="HarvestBuff" />, shared by the drop hook and its description.</summary>
public static class HarvestDrops
{
    /// <summary>Chance at stock strength, as the Microlith accessory paid out.</summary>
    private const float BASE_CHANCE = 0.15f;

    /// <summary>Potency at which a payout can reach three extra items.</summary>
    private const int GENEROUS_POTENCY = 4;

    public static float Chance(int strengthPercent) => BASE_CHANCE * (strengthPercent / 100f);

    public static int MaxExtra(int potency) => potency >= GENEROUS_POTENCY ? 3 : 2;
}

/// <summary>Pays out extra ore and gems while <see cref="HarvestBuff" /> is up.</summary>
public class HarvestDropTile : GlobalTile
{
    public override void Drop(int i, int j, int type)
    {
        base.Drop(i, j, type);

        // Drops resolve on the server in multiplayer, where LocalPlayer is dummy 255; GetPlayerForTile finds who caused it.
        Player player = WorldGen.GetPlayerForTile(i, j);
        if (player is null || !player.active)
            return;

        if (!TryGetBrew(player, out int strengthPercent, out int potency))
            return;

        // Kept a subset of PlacedTileTracker.IsValuable, so nothing can be paid out on that the
        // tracker never had a chance to record.
        if (!Main.tileSolid[type] || !PlacedTileTracker.IsValuable(type))
            return;

        // Only found blocks pay, or ore could be placed and re-mined forever. The mark is released either way.
        bool wasPlacedByPlayer = PlacedTileTracker.WasPlacedByPlayer(i, j);
        PlacedTileTracker.Forget(i, j);

        if (wasPlacedByPlayer)
            return;

        if (Main.rand.NextFloat() >= HarvestDrops.Chance(strengthPercent))
            return;

        int itemType = GetGemOrOreItem(i, j, type);

        // GetItemDropFromTypeAndStyle returns 0 for a tile that drops nothing. Spawning type 0 yields
        // a blank item and a notification titled after it, so there is nothing to award here.
        if (itemType <= 0)
            return;

        int extraItems = Main.rand.Next(1, HarvestDrops.MaxExtra(potency) + 1);
        player.QuickSpawnItem(new EntitySource_TileBreak(i, j), itemType, extraItems);

        PlayHarvestFeedback(i, j, player, itemType, extraItems);
    }

    /// <summary>
    /// The brew riding the player's buff. A server never sees the brew payload, so a buff without one pays
    /// out at stock strength rather than not at all.
    /// </summary>
    private static bool TryGetBrew(Player player, out int strengthPercent, out int potency)
    {
        strengthPercent = 100;
        potency = 1;

        int buff = ModContent.BuffType<HarvestBuff>();

        if (!player.HasBuff(buff))
            return false;

        if (player.GetModPlayer<AlchemyEffectPlayer>().TryGetActiveBrew(buff, out AlchemicalEffect fx))
        {
            strengthPercent = fx.StrengthPercent;
            potency = fx.Potency;
        }

        return true;
    }

    /// <remarks>Runs from <see cref="Drop" />, not <see cref="KillTile" />, which fires before drops resolve and would lag a tile behind.</remarks>
    private static void PlayHarvestFeedback(int i, int j, Player player, int itemType, int count)
    {
        // Dust, sound and the popup are all local presentation. On a server there is no one to show
        // them to, and in multiplayer they belong to the client that did the mining.
        if (Main.dedServ || player.whoAmI != Main.myPlayer)
            return;

        Vector2 tilePos = new Vector2(i * 16, j * 16);
        for (int dustCount = 0; dustCount < 30; dustCount++)
        {
            float velX = Main.rand.NextFloat(-2f, 2f);
            float velY = Main.rand.NextFloat(-4f, -1f);
            Dust.NewDust(tilePos, 16, 16, DustID.SpelunkerGlowstickSparkle, velX, velY, Scale: 1.2f);
        }

        SoundEngine.PlaySound(SoundID.CoinPickup, tilePos);
        HarvestNotificationManager.ShowHarvestNotification(itemType, count);
    }

    private static int GetGemOrOreItem(int i, int j, int tileType)
    {
        // gem blocks drop actual gems instead of blocks
        switch (tileType)
        {
            case TileID.Sapphire: return ItemID.Sapphire;
            case TileID.Ruby: return ItemID.Ruby;
            case TileID.Emerald: return ItemID.Emerald;
            case TileID.Topaz: return ItemID.Topaz;
            case TileID.Amethyst: return ItemID.Amethyst;
            case TileID.Diamond: return ItemID.Diamond;
        }

        // Style comes from the tile, or multi-style ores always pay out as style 0.
        Tile tile = Framing.GetTileSafely(i, j);
        int style = tile.TileFrameX / 18;

        return TileLoader.GetItemDropFromTypeAndStyle(tileType, style);
    }
}
