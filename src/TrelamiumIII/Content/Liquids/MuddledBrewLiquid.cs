using System.Collections.Generic;
using Terraria.GameContent;
using Terraria.GameContent.Liquid;
using TrelamiumIII.Content.Alchemy.AlchemyStations;
using TrelamiumIII.Core.Liquids;

namespace TrelamiumIII.Content.Liquids;

/// <summary>
///     Any drinkable potion as a liquid. Each potion is a runtime condition named after its item, created the first
///     time the world needs it. Different brews flowing together follow <see cref="AddMix(int, int, int)" />, and turn
///     muddled when no recipe covers them; meeting any other liquid they submerge in it instead of merging.
/// </summary>
public sealed class MuddledBrewLiquid : CustomLiquid
{
    /// <summary>How long a brew's effect lingers after leaving it.</summary>
    private const int ContactBuffTicks = 120;

    private static readonly Dictionary<(int, int), int> recipes = [];

    public InternalLiquid Muddled { get; private set; } = null!;

    protected override void DefineLiquidTypes()
        => Muddled = AddInternalLiquid(new InternalLiquid("Muddled") { Tint = new Color(168, 132, 120) });

    public override void SetStaticDefaults()
    {
        base.SetStaticDefaults();

        LiquidRenderer.DEFAULT_OPACITY[Type] = 0.7f;
        SlopeOpacity = 0.6f;
        ChecksForDrowning = true;
        AllowEmitBreathBubbles = true;
    }

    public override void Unload()
    {
        base.Unload();
        recipes.Clear();
    }

    /// <summary>
    ///     Brew <paramref name="a" /> and brew <paramref name="b" /> flowing together make <paramref name="result" />,
    ///     either way round. Pairs with no entry turn muddled.
    /// </summary>
    public static void AddMix(int a, int b, int result)
    {
        recipes[(a, b)] = result;
        recipes[(b, a)] = result;
    }

    public static bool IsBrew(int itemType)
    {
        if (itemType <= ItemID.None || itemType >= ItemLoader.ItemCount)
            return false;

        Item item = ContentSamples.ItemsByType[itemType];
        return item.consumable && item.useStyle == ItemUseStyleID.DrinkLiquid
            && (item.buffType > 0 || item.healLife > 0 || item.healMana > 0);
    }

    public static string ItemKey(int itemType)
        => itemType < ItemID.Count ? ItemID.Search.GetName(itemType) : ItemLoader.GetItem(itemType).FullName;

    private static int ItemFromKey(string key)
    {
        if (key.Contains('/'))
            return ModContent.TryFind(key, out ModItem item) ? item.Type : ItemID.None;

        return ItemID.Search.TryGetId(key, out int type) ? type : ItemID.None;
    }

    /// <summary>The condition for a drinkable item, created on the server or in singleplayer if new.</summary>
    public InternalLiquid? ForItem(int itemType) => IsBrew(itemType) ? Resolve(ItemKey(itemType)) : null;

    protected override InternalLiquid? CreateDynamic(string name)
    {
        int itemType = ItemFromKey(name);
        return IsBrew(itemType) ? new BrewLiquid(name, itemType) : null;
    }

    public override byte Mix(byte receiver, byte donor)
    {
        if (receiver == donor)
            return receiver;

        if (Get(receiver) is BrewLiquid a && Get(donor) is BrewLiquid b
            && recipes.TryGetValue((a.ItemType, b.ItemType), out int result) && ForItem(result) is { } mixed)
            return mixed.Index;

        return Muddled.Index;
    }

    public override int BottledItem(InternalLiquid liquid) => liquid is BrewLiquid brew ? brew.ItemType : ItemID.None;

    protected override void OnPlayerContact(Player player, InternalLiquid liquid)
    {
        if (player.whoAmI != Main.myPlayer || liquid is not BrewLiquid brew)
            return;

        Item sample = ContentSamples.ItemsByType[brew.ItemType];

        if (sample.buffType > 0)
            player.AddBuff(sample.buffType, ContactBuffTicks);

        if (sample.healLife > 0)
            player.AddBuff(BuffID.Regeneration, ContactBuffTicks);

        if (sample.healMana > 0)
            player.AddBuff(BuffID.ManaRegeneration, ContactBuffTicks);
    }
}

/// <summary>A brew named after the potion it was made from.</summary>
public sealed class BrewLiquid(string name, int itemType) : InternalLiquid(name)
{
    private Color? tint;

    public int ItemType => itemType;

    /// <summary>Sampled from the potion's sprite on first draw, which runs on the main thread.</summary>
    public override Color Tint => tint ??= Main.dedServ ? Color.White : LiquidColor(itemType);

    public override string DisplayName => Lang.GetItemNameValue(itemType);

    /// <summary>
    ///     The colour of the liquid in a potion's sprite: pixels weighted by saturation squared, so the grey glass and
    ///     cork barely count, then scaled up to full brightness since it multiplies a mid-grey texture.
    /// </summary>
    private static Color LiquidColor(int itemType)
    {
        Main.instance.LoadItem(itemType);

        if (TextureAssets.Item[itemType]?.Value is not { } texture)
            return AdvancedAlchemyStationLiquid.ContentColor(itemType);

        var pixels = new Color[texture.Width * texture.Height];
        texture.GetData(pixels);

        Vector3 sum = Vector3.Zero;
        float weight = 0f;

        foreach (Color pixel in pixels)
        {
            if (pixel.A < 128)
                continue;

            Vector3 rgb = pixel.ToVector3();
            float max = MathF.Max(rgb.X, MathF.Max(rgb.Y, rgb.Z));
            float min = MathF.Min(rgb.X, MathF.Min(rgb.Y, rgb.Z));
            float saturation = max <= 0f ? 0f : (max - min) / max;
            float w = saturation * saturation;

            sum += rgb * w;
            weight += w;
        }

        if (weight <= 0.001f)
            return AdvancedAlchemyStationLiquid.ContentColor(itemType);

        Vector3 average = sum / weight;
        float peak = MathF.Max(average.X, MathF.Max(average.Y, average.Z));
        return new Color(average / Math.Max(peak, 0.001f));
    }
}