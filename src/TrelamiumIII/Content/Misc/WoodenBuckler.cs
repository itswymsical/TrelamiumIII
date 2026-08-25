using System.Collections.Generic;
using System.Linq;
using Terraria.Audio;
using TrelamiumIII.Common.Systems;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Content.Misc;

public class WoodenBuckler : ModItem
{
    public override void SetDefaults()
    {
        Item.accessory = true;
        Item.width = Item.height = 28;
        Item.rare = ItemRarityID.White;
        Item.value = Item.sellPrice(silver: 2);
        Item.defense = 1;
    }

    public override void UpdateEquip(Player player)
        => player.GetModPlayer<WoodenBucklerPlayer>().buckled = true;

    public override void AddRecipes()
    {
        CreateRecipe()
        .AddIngredient(ItemID.Wood, 12)
        .AddIngredient(ItemID.IronBar)
        .AddTile(TileID.Anvils);
    }
    public override void ModifyTooltips(List<TooltipLine> tooltips)
    {
        string keyName = "Unbound";

        var assignedKeys = KeybindsSystem.BucklerKeybind.GetAssignedKeys();
        if (assignedKeys.Count > 0)
        {
            keyName = assignedKeys[0];
        }

        TooltipLine tooltipLine = tooltips.FirstOrDefault(x => x.Name == "Tooltip0" && x.Mod == "Terraria");

        if (tooltipLine != null)
        {
            tooltipLine.Text = tooltipLine.Text.Replace("[KEY]", keyName);
        }
    }
}

public class WoodenBucklerPlayer : ModPlayer
{
    public bool buckled;
    public bool buckleParry;
    public int buckleTimer;
    public int buckleCooldown;

    public override void ResetEffects() => buckled = false;

    public override void PreUpdate()
    {
        if (buckleCooldown > 0)
        {
            buckleCooldown--;
        }

        if (KeybindsSystem.BucklerKeybind.JustPressed && buckleCooldown == 0 && buckled)
        {
            buckleParry = true;
            buckleCooldown = TimeConversion.SetTime(secs: 5);
            SoundEngine.PlaySound(SoundID.Research with { Pitch = -0.05f }, Player.Center);
        }
    }

    public override void UpdateEquips()
    {
        if (buckleParry && buckleTimer < TimeConversion.SetTime(secs: 1, ticks: 30))
        {
            buckleTimer++;
            Player.endurance += 0.5f;
            if (buckleTimer >= TimeConversion.SetTime(secs: 1, ticks: 30))
            {
                buckleParry = false;
                buckleTimer = 0;
            }
        }
    }
}