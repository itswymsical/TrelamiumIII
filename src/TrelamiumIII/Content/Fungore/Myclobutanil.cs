using System.Collections.Generic;
using Terraria.ModLoader.IO;
using TrelamiumIII.Content.Alchemy;

namespace TrelamiumIII.Content.Fungore;

public class Myclobutanil : ModItem
{
    public override void SetDefaults()
    {
        Item.width = Item.height = 20;

        Item.consumable = true;
        Item.useTime = Item.useAnimation = 20;
        Item.useStyle = ItemUseStyleID.DrinkOld;
        Item.UseSound = SoundID.Item3;

        Item.expert = true;
        Item.rare = ItemRarityID.Expert;
        Item.value = Item.sellPrice(gold: 1);
    }

    public override bool CanUseItem(Player player) => !player.GetModPlayer<MyclobutanilPlayer>().Dosed;

    public override bool? UseItem(Player player) => player.GetModPlayer<MyclobutanilPlayer>().Dosed = true;

    public override void ModifyTooltips(List<TooltipLine> tooltips)
    {
        TooltipLine? line = tooltips.Find(l => l.Mod == "Terraria" && l.Name == "Consumable");

        line?.Text = this.GetLocalization("PermanentConsumable", () => "Permanent {0}").Format(line.Text);
    }
}

public class MyclobutanilMushroom : GlobalItem
{
    /// <summary>
    ///     What counts as a mushroom. More will be added to the list, eventually.
    /// </summary>
    public static readonly HashSet<int> Mushrooms = [ItemID.Mushroom, ModContent.ItemType<FlyAgaricItem>()];

    public override void OnConsumeItem(Item item, Player player)
    {
        if (!Mushrooms.Contains(item.type))
            return;

        MyclobutanilPlayer dosed = player.GetModPlayer<MyclobutanilPlayer>();

        if (dosed.Dosed)
            dosed.BeginSurge();
    }
}

public class MyclobutanilPlayer : ModPlayer
{
    public const int SURGE_TICKS = 3 * 60;

    public const int MUSHROOM_SICKNESS = 25 * 60;

    /// <summary>
    ///     Added to <see cref="Player.lifeRegen" /> during the surge. That field counts in half a
    ///     health per second, so 30 is fifteen a second and about forty-five over the window, against
    ///     the mushroom's own fifteen.
    /// </summary>
    public const int SURGE_REGEN = 30;

    public bool Dosed;

    public int Surge;

    public bool MushroomSickness;

    public bool SurgeEnd => MushroomSickness && Surge <= 0;

    public void BeginSurge()
    {
        Surge = SURGE_TICKS;
        MushroomSickness = true;
    }

    public override void UpdateLifeRegen()
    {
        if (Surge > 0)
            Player.lifeRegen += SURGE_REGEN;
    }

    public override void PostUpdateEquips()
    {
        if (SurgeEnd)
            Player.statDefense *= 0f;
    }

    public override void PostUpdate()
    {
        if (Surge > 0)
            Surge--;

        if (!MushroomSickness)
            return;

        if (Player.potionDelay > MUSHROOM_SICKNESS)
            CutPotionSickness();

        if (Surge <= 0 && Player.potionDelay <= 0)
            MushroomSickness = false;
    }

    public override void OnRespawn()
    {
        MushroomSickness = false;
        Surge = 0;
    }

    private void CutPotionSickness()
    {
        Player.potionDelay = MUSHROOM_SICKNESS;

        int index = Player.FindBuffIndex(BuffID.PotionSickness);

        if (index >= 0)
            Player.buffTime[index] = MUSHROOM_SICKNESS;
    }

    public override void SaveData(TagCompound tag)
    {
        if (Dosed)
            tag["dosed"] = true;

        if (MushroomSickness)
            tag["mushroomSickness"] = true;
    }

    public override void LoadData(TagCompound tag)
    {
        Dosed = tag.GetBool("dosed");
        MushroomSickness = tag.GetBool("mushroomSickness");
    }
}
