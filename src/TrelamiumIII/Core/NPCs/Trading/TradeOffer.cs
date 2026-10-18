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

    /// <summary>Index into the villager's stock pool, which names this offer in packets. -1 if it came from elsewhere.</summary>
    public int PoolIndex { get; internal set; } = -1;

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

    public bool PlayerCanAfford(Player player) => AffordableCount(player, 1) > 0;

    /// <summary>How many times, up to <paramref name="limit"/>, the player can pay for this offer.</summary>
    public int AffordableCount(Player player, int limit)
    {
        int count = limit;

        // CountItem's default stopCountingAt of 0 returns after the first stack.
        foreach (var req in Inputs)
            count = Math.Min(count, player.CountItem(req.ItemId, req.Stack * limit) / Math.Max(1, req.Stack));

        while (count > 0 && CoinCost > 0 && !player.CanAfford((long)CoinCost * count))
            count--;

        return count;
    }

    /// <summary>How many times, up to <paramref name="limit"/>, the NPC's stock and remaining uses allow this offer.</summary>
    public int StockCount(WorldNPC npc, int limit)
    {
        if (!IsUnlocked)
            return 0;

        int count = Math.Min(limit, npc.GetCarriedItemCount(Output.ItemId) / Math.Max(1, Output.Stack));
        return MaxUses > 0 ? Math.Min(count, UsesRemaining) : count;
    }

    public bool CanExecute(Player player, WorldNPC npc)
        => IsUnlocked && InStock(npc) && PlayerCanAfford(player);

    public bool DoTrade(Player player, WorldNPC npc)
    {
        if (!CanExecute(player, npc))
            return false;

        TakeStock(npc, 1);
        PayAndCollect(player, npc);
        return true;
    }

    /// <summary>The NPC's half of <paramref name="count"/> trades: the output stock and the uses.</summary>
    internal void TakeStock(WorldNPC npc, int count)
    {
        int left = Output.Stack * count;
        while (left > 0)
        {
            int taken = npc.TakeCarriedItem(Output.ItemId, left);
            if (taken == 0)
                break;

            left -= taken;
        }

        if (MaxUses > 0)
            UsesRemaining = Math.Max(0, UsesRemaining - count);
    }

    /// <summary>Undoes <see cref="TakeStock"/> for trades that were reserved but never paid for.</summary>
    internal void ReturnStock(WorldNPC npc, int count)
    {
        npc.AddInvItem(Output.ItemId, Output.Stack * count);

        if (MaxUses > 0)
            UsesRemaining = Math.Min(MaxUses, UsesRemaining + count);
    }

    /// <summary>The player's half of one trade: takes the inputs and coins, then spawns the output.</summary>
    internal void PayAndCollect(Player player, WorldNPC npc)
    {
        foreach (var input in Inputs)
        {
            for (int i = 0; i < input.Stack; i++)
                player.ConsumeItem(input.ItemId);
        }

        if (CoinCost > 0)
            player.BuyItem(CoinCost);

        player.QuickSpawnItem(npc.NPC.GetSource_Loot(), Output.ItemId, Output.Stack);
    }

    public void Save(TagCompound tag) => tag["uses"] = UsesRemaining;

    public void Load(TagCompound tag)
    {
        if (tag.ContainsKey("uses"))
            UsesRemaining = tag.GetInt("uses");
    }
}