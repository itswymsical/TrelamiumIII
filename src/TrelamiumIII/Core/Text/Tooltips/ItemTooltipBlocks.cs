using System.Collections.Generic;

namespace TrelamiumIII.Core.Text.Tooltips;

/// <summary>
///     Contributes <see cref="TooltipBlock" />s to an item's tooltip. Implement on a
///     <see cref="ModItem" />, or on any <see cref="GlobalItem" /> that applies to the item -- an
///     <c>ItemComponent</c> included.
/// </summary>
public interface ITooltipBlockProvider
{
    void AddTooltipBlocks(Item item, List<TooltipBlock> blocks);
}

/// <summary>
///     Gathers every provider's blocks each time a tooltip is built and places each one as a
///     tagged line at its anchor.
/// </summary>
/// <remarks>
///     Runs on every item because which items carry blocks is only known at draw time: an
///     <see cref="GlobalItem.AppliesToEntity" /> gate would be asked before the things that decide
///     (recipe registries, per-instance components) exist. The cost on an item with no providers
///     is one walk of its globals.
/// </remarks>
public sealed class ItemTooltipBlocks : GlobalItem
{
    public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
    {
        List<TooltipBlock>? blocks = null;

        if (item.ModItem is ITooltipBlockProvider own)
            own.AddTooltipBlocks(item, blocks ??= []);

        foreach (GlobalItem globalItem in item.Globals)
        {
            if (globalItem is ITooltipBlockProvider provider)
                provider.AddTooltipBlocks(item, blocks ??= []);
        }

        if (blocks is not { Count: > 0 })
            return;

        TooltipBlockRegistry.Publish(item.type, blocks);

        for (int i = 0; i < blocks.Count; i++)
        {
            var line = new TooltipLine(Mod, $"Block{i}", TooltipBlockTagHandler.Compose(item.type, i, blocks[i]));

            tooltips.Insert(blocks[i].Anchor.Resolve(tooltips), line);
        }
    }
}
