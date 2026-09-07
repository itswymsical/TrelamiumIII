using TrelamiumIII.Core;

namespace TrelamiumIII.Content.Alchemy.Reagents;

/// <summary>
///     The alkali tier -- the neutralising, cleansing and heat-suppressing half of the
///     conventional line. Made at the Alchemy Station in <c>AlchemyMode.Alchemy</c>.
/// </summary>
/// <remarks>
///     These are the only reagents in the mod that would push a burn metric <em>down</em>. See
///     the fire-triangle section of <c>docs/Alchemy/ConventionalReagents.md</c>.
/// </remarks>
public abstract class AlkaliReagentItem : ModItem
{
    public override string Texture => AssetReferences.Assets.Textures.PlaceholderSolute.KEY;

    public override void SetDefaults()
    {
        base.SetDefaults();
        Item.width = Item.height = 30;
        Item.rare = ItemRarityID.Blue;
        Item.maxStack = Item.CommonMaxStack;
        Item.value = Item.sellPrice(silver: 2);
    }
}

/// <summary>
///     Quicklime that has already had its argument with water. Inert, and a fire suppressant.
/// </summary>
public class SlakedLimeItem : AlkaliReagentItem
{
    public override void SetDefaults()
    {
        base.SetDefaults();
        Item.rare = ItemRarityID.White;
        Item.value = Item.sellPrice(copper: 80);
    }
}

public class CausticLyeItem : AlkaliReagentItem;

/// <summary>
///     A freezing mixture. Draws heat in rather than giving it off.
/// </summary>
public class FrigorificSaltItem : AlkaliReagentItem
{
    public override void SetDefaults()
    {
        base.SetDefaults();
        Item.value = Item.sellPrice(silver: 3);
    }
}
