using System.IO;

namespace TrelamiumIII.Core.NPCs.Trading;

/// <summary>
/// Runs trades in multiplayer. The server owns a villager's offers and stock, which reach clients through
/// <see cref="Villager.SendExtraAI"/>; each client owns its inventory. So a client asks the server to reserve stock,
/// the server takes it and grants a count back, and the client pays for and collects that many. Anything it can no
/// longer pay for goes back to the server's stock.
/// </summary>
public static class TradeNet
{
    private enum Op : byte
    {
        Request,
        Grant,
        Return,
    }

    private const int MaxCount = 999;

    /// <summary>The server's range check. <see cref="TradeUI"/> closes at 180 px, and the server's player position lags.</summary>
    private const float MaxRange = 400f;

    /// <summary>A request with no grant after this many ticks stops blocking the next one.</summary>
    private const uint PendingTimeout = 120;

    private static bool pending;
    private static bool pendingBulk;
    private static uint pendingSince;

    /// <summary>The server's last grant per player. A return is only accepted against it.</summary>
    private static readonly (int Npc, int PoolIndex, int Count)[] lastGrant = new (int, int, int)[Main.maxPlayers];

    /// <summary>
    /// Trades up to <paramref name="count"/> times. Singleplayer trades at once. A multiplayer client sends a request
    /// and trades when the grant comes back, one request at a time.
    /// </summary>
    public static void Trade(Villager trader, TradeOffer offer, int count, bool bulk)
    {
        Player player = Main.LocalPlayer;
        count = Math.Min(count, MaxCount);

        if (Main.netMode == NetmodeID.SinglePlayer)
        {
            int done = 0;
            while (done < count && offer.DoTrade(player, trader))
                done++;

            TradeUI.OnTraded(done, bulk);
            return;
        }

        if (pending && Main.GameUpdateCount - pendingSince < PendingTimeout)
            return;

        count = offer.AffordableCount(player, offer.StockCount(trader, count));
        if (count <= 0 || offer.PoolIndex < 0)
            return;

        pending = true;
        pendingBulk = bulk;
        pendingSince = Main.GameUpdateCount;

        Send(Op.Request, trader.NPC.whoAmI, trader.Type, offer.PoolIndex, count, toClient: -1);
    }

    internal static void Receive(BinaryReader reader, int whoAmI)
    {
        var op = (Op)reader.ReadByte();
        int npcIndex = reader.ReadByte();
        int npcType = reader.Read7BitEncodedInt();
        int poolIndex = reader.Read7BitEncodedInt();
        int count = Math.Min(reader.Read7BitEncodedInt(), MaxCount);

        Villager? trader = npcIndex < Main.maxNPCs && Main.npc[npcIndex] is { active: true, ModNPC: Villager villager } && villager.Type == npcType
            ? villager
            : null;
        TradeOffer? offer = trader?.FindTrade(poolIndex);

        switch (op)
        {
            case Op.Request when Main.netMode == NetmodeID.Server:
                Reserve(whoAmI, trader, offer, npcIndex, npcType, poolIndex, count);
                break;
            case Op.Grant when Main.netMode == NetmodeID.MultiplayerClient:
                Collect(trader, offer, npcIndex, npcType, poolIndex, count);
                break;
            case Op.Return when Main.netMode == NetmodeID.Server:
                Restock(whoAmI, trader, offer, npcIndex, poolIndex, count);
                break;
        }
    }

    /// <summary>Server: takes as much of the request as stock and uses allow, then grants that count to the requester.</summary>
    private static void Reserve(int whoAmI, Villager? trader, TradeOffer? offer, int npcIndex, int npcType, int poolIndex, int count)
    {
        Player player = Main.player[whoAmI];
        int granted = 0;

        if (trader != null && offer != null && player.active && !player.dead && player.Distance(trader.NPC.Center) <= MaxRange)
        {
            granted = offer.StockCount(trader, count);
            if (granted > 0)
            {
                offer.TakeStock(trader, granted);
                trader.NPC.netUpdate = true;
            }
        }

        lastGrant[whoAmI] = (npcIndex, poolIndex, granted);
        Send(Op.Grant, npcIndex, npcType, poolIndex, granted, toClient: whoAmI);
    }

    /// <summary>
    /// Client: pays for and collects each granted trade it can still afford, and returns the rest. The local stock
    /// mirrors the server's change until its sync arrives.
    /// </summary>
    private static void Collect(Villager? trader, TradeOffer? offer, int npcIndex, int npcType, int poolIndex, int count)
    {
        pending = false;

        Player player = Main.LocalPlayer;
        int paid = 0;

        if (trader != null && offer != null)
        {
            offer.TakeStock(trader, count);

            while (paid < count && offer.PlayerCanAfford(player))
            {
                offer.PayAndCollect(player, trader);
                paid++;
            }

            if (paid < count)
                offer.ReturnStock(trader, count - paid);
        }

        if (paid < count)
            Send(Op.Return, npcIndex, npcType, poolIndex, count - paid, toClient: -1);

        TradeUI.OnTraded(paid, pendingBulk);
    }

    /// <summary>Server: puts back granted trades the client didn't pay for, never more than it was granted.</summary>
    private static void Restock(int whoAmI, Villager? trader, TradeOffer? offer, int npcIndex, int poolIndex, int count)
    {
        var last = lastGrant[whoAmI];
        if (trader == null || offer == null || last.Npc != npcIndex || last.PoolIndex != poolIndex)
            return;

        count = Math.Min(count, last.Count);
        if (count <= 0)
            return;

        lastGrant[whoAmI] = (npcIndex, poolIndex, last.Count - count);
        offer.ReturnStock(trader, count);
        trader.NPC.netUpdate = true;
    }

    private static void Send(Op op, int npcIndex, int npcType, int poolIndex, int count, int toClient)
    {
        ModPacket packet = TrelamiumIII.Instance.GetPacket();

        packet.Write((byte)TrelamiumIII.MessageType.VillagerTrade);
        packet.Write((byte)op);
        packet.Write((byte)npcIndex);
        packet.Write7BitEncodedInt(npcType);
        packet.Write7BitEncodedInt(poolIndex);
        packet.Write7BitEncodedInt(count);

        packet.Send(toClient);
    }
}
