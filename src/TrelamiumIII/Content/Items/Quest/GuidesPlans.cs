using TrelamiumIII.Core.Jobs.Blueprints;

namespace TrelamiumIII.Content.Items.Quest;

/// <summary>
///     The Guide's plans for the town, handed over for <see cref="Jobs.HomeForager"/>. Using them holds the blueprint
///     underway at the cursor to be staked (<see cref="BlueprintPlayer.Toggle"/>), the same as the Stake Blueprint key.
/// </summary>
/// <remarks>
///     Not reserved with <see cref="Core.Jobs.MarkedItem"/>, which would stop it being used; the job hands over another
///     copy at each placing stage if the player has lost theirs, and takes it back when it's done. No sprite yet:
///     PlaceholderTextures draws it.
/// </remarks>
public class GuidesPlans : ModItem
{
    public override string Texture => "Terraria/Images/Item_" + ItemID.ThePlan;
    public override void SetDefaults()
    {
        Item.width = 26;
        Item.height = 26;
        Item.maxStack = 1;
        Item.rare = ItemRarityID.Quest;
        Item.useStyle = ItemUseStyleID.HoldUp;
        Item.useTime = Item.useAnimation = 20;
    }

    /// <summary>Only while there's a blueprint to place; the click would otherwise do nothing.</summary>
    public override bool CanUseItem(Player player) => player.GetModPlayer<BlueprintPlayer>().HasMovable;

    public override bool? UseItem(Player player)
    {
        if (player.whoAmI == Main.myPlayer)
            player.GetModPlayer<BlueprintPlayer>().Toggle();

        return true;
    }
}
