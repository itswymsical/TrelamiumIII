using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ModLoader.IO;

namespace TrelamiumIII.Core.NPCs.Trading;

public readonly record struct ItemReq(int ItemId, int Stack);

public class TradeOffer
{
    public List<ItemReq> Inputs { get; } = new();
    public ItemReq Output { get; private set; }
    public int CoinCost { get; private set; }

    public int MaxUses { get; private set; } = -1;
    public int UsesRemaining { get; set; } = -1;
    public List<Condition> Conditions { get; } = new();

    public TradeOffer For(int itemId, int stack = 1)
    {
        Inputs.Add(new ItemReq(itemId, stack));
        return this;
    }

    public TradeOffer And(int itemId, int stack = 1) => For(itemId, stack);

    /// <summary>
    /// Sets a coin cost in copper-equivalent value.
    /// Pair with <c>Item.buyPrice(platinum, gold, silver, copper)</c> or <c>Item.sellPrice(...)</c>.
    /// </summary>
    public TradeOffer ForCoins(int copperValue)
    {
        CoinCost = copperValue;
        return this;
    }

    public TradeOffer Grants(int itemId, int stack = 1)
    {
        Output = new ItemReq(itemId, stack);
        return this;
    }

    public TradeOffer WithUses(int uses)
    {
        MaxUses = uses;
        UsesRemaining = uses;
        return this;
    }

    public TradeOffer When(Condition condition)
    {
        Conditions.Add(condition);
        return this;
    }

    public bool IsUnlocked => Conditions.All(c => c.IsMet()) && UsesRemaining != 0;

    public bool InStock(WorldNPC npc)
        => npc.HasCarriedItem(Output.ItemId, Output.Stack);

    public bool PlayerCanAfford(Player player)
    {
        foreach (var req in Inputs)
            if (player.CountItem(req.ItemId) < req.Stack) return false;

        if (CoinCost > 0 && !player.CanAfford(CoinCost))
            return false;

        return true;
    }

    public bool CanExecute(Player player, WorldNPC npc)
        => IsUnlocked && InStock(npc) && PlayerCanAfford(player);

    public bool DoTrade(Player player, WorldNPC npc)
    {
        if (!CanExecute(player, npc))
            return false;

        foreach (var input in Inputs)
        {
            for (int i = 0; i < input.Stack; i++)
                player.ConsumeItem(input.ItemId);
        }

        if (CoinCost > 0)
            player.BuyItem(CoinCost);

        npc.TakeCarriedItem(Output.ItemId, Output.Stack);

        var source = npc.NPC.GetSource_Loot();
        player.QuickSpawnItem(source, Output.ItemId, Output.Stack);

        if (MaxUses > 0)
            UsesRemaining--;

        return true;
    }

    public void Save(TagCompound tag) => tag["uses"] = UsesRemaining;

    public void Load(TagCompound tag)
    {
        if (tag.ContainsKey("uses"))
            UsesRemaining = tag.GetInt("uses");
    }
}