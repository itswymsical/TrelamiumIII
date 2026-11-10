using System.Collections.Generic;

namespace TrelamiumIII.Core.Liquids;

/// <summary>
///     The <see cref="InternalLiquid" />s tiles of one liquid type can carry, with their flow mixes and tile reactions.
///     A <see cref="CustomLiquid" /> owns one for its own type; a vanilla type gets one from a subclass.
/// </summary>
public abstract class CustomLiquidSet
{
    /// <summary><see cref="LiquidData.LiquidType" /> is packed in 6 bits.</summary>
    private const int MaxLiquidTypes = 64;

    /// <summary>Tiles a reaction converts at once, so a pool changes from its contact point outward.</summary>
    private const int ReactionSpread = 48;

    /// <summary><see cref="MessageID.TileManipulation" /> action for <c>WorldGen.KillTile(noItem: true)</c>.</summary>
    private const int KillTileNoItem = 4;

    /// <summary>Tiles of a body searched for liquid an item reaction spends.</summary>
    private const int SpendSearch = 256;

    private static readonly CustomLiquidSet?[] byType = new CustomLiquidSet?[MaxLiquidTypes];

    private static readonly List<CustomLiquidSet> all = [];

    public static IReadOnlyList<CustomLiquidSet> All => all;

    public static CustomLiquidSet? ByType(int type) => (uint)type < MaxLiquidTypes ? byType[type] : null;

    public static CustomLiquidSet? ByName(string fullName) => all.Find(set => set.FullName == fullName);

    private readonly List<InternalLiquid> liquids = [];

    private readonly Dictionary<string, InternalLiquid> byName = [];

    private readonly Dictionary<(byte Receiver, byte Donor), byte> mixes = [];

    private readonly List<TileReaction> reactions = [];

    private readonly List<ItemReaction> itemReactions = [];

    private readonly HashSet<int> floating = [];

    private readonly Dictionary<InternalLiquid, int> bottled = [];

    /// <summary>Liquids added before <see cref="Register" />. Those past it are created at runtime and cleared with the world.</summary>
    private int staticCount;

    /// <summary>The <see cref="Tile.LiquidType" /> whose tiles carry these liquids.</summary>
    public abstract int LiquidType { get; }

    /// <summary>Save and lookup key.</summary>
    public abstract string FullName { get; }

    public abstract string Name { get; }

    public IReadOnlyList<InternalLiquid> InternalLiquids => liquids;

    public InternalLiquid Default => liquids[0];

    internal int StaticCount => staticCount;

    public virtual string LiquidDisplayName(InternalLiquid liquid) => liquid.Name;

    /// <summary>Waterfall style for <paramref name="liquid" /> on a vanilla type, or null for the type's own.</summary>
    public virtual int? WaterfallStyle(InternalLiquid liquid) => null;

    /// <summary>Makes this the set <see cref="ByType" /> returns for <see cref="LiquidType" />, after its static liquid are added.</summary>
    protected internal void Register()
    {
        staticCount = liquids.Count;
        byType[LiquidType] = this;
        all.Add(this);
    }

    internal static void ClearRegistry()
    {
        Array.Clear(byType);
        all.Clear();
    }

    protected internal InternalLiquid AddLiquid(InternalLiquid liq)
    {
        if (liquids.Count > byte.MaxValue)
            throw new InvalidOperationException($"{Name} has more than 256 internal types.");

        liq.Index = (byte)liquids.Count;
        liq.Liquid = this;
        liquids.Add(liq);
        byName.Add(liq.Name, liq);
        return liq;
    }

    /// <summary>Liquid of <paramref name="donor" /> flowing into <paramref name="receiver" /> makes <paramref name="result" />, both ways round.</summary>
    protected internal void AddMix(InternalLiquid receiver, InternalLiquid donor, InternalLiquid result)
    {
        mixes[(receiver.Index, donor.Index)] = result.Index;
        mixes[(donor.Index, receiver.Index)] = result.Index;
    }

    /// <summary>
    ///     Liquid in <paramref name="from" /> (any liquid when null) beside one of <paramref name="tiles" /> turns into
    ///     <paramref name="to" />, from the contact point outward. The tile is untouched.
    /// </summary>
    protected internal void AddReaction(InternalLiquid? from, InternalLiquid to, params int[] tiles)
        => AddReaction(from, to, type => Array.IndexOf(tiles, type) >= 0);

    /// <inheritdoc cref="AddReaction(InternalLiquid?, InternalLiquid, int[])" />
    /// <remarks>Takes a predicate so modded tile types are read after they load.</remarks>
    protected internal void AddReaction(InternalLiquid? from, InternalLiquid to, Predicate<int> tiles)
        => reactions.Add(new TileReaction(from, to, tiles, TileOutcome.Keep, 0));

    /// <summary>
    ///     Liquid in <paramref name="from" /> (any liquid when null) dissolves tiles matching <paramref name="tiles" />,
    ///     or turns them into <paramref name="becomes" /> when given. The liquid is unchanged.
    /// </summary>
    protected internal void AddCorrosion(InternalLiquid? from, Predicate<int> tiles, ushort? becomes = null)
        => reactions.Add(new TileReaction(from, null, tiles, becomes is null ? TileOutcome.Dissolve : TileOutcome.Convert, becomes ?? 0));

    /// <summary>Adds what a dropped stack does in this liquid. Products may be modded items, so add these once item types are assigned.</summary>
    protected internal void AddItemReaction(ItemReaction reaction)
    {
        itemReactions.Add(reaction);

        if (reaction.Product > ItemID.None)
            floating.Add(reaction.Product);
    }

    /// <summary>The first item reaction for <paramref name="itemType" /> in <paramref name="liquid" />, or null.</summary>
    public ItemReaction? ItemReactionFor(InternalLiquid liquid, int itemType)
    {
        foreach (ItemReaction reaction in itemReactions)
        {
            if ((reaction.From is null || reaction.From == liquid) && reaction.Matches(itemType))
                return reaction;
        }

        return null;
    }

    /// <summary>Whether <paramref name="itemType" /> floats at this liquid's surface, as every item reaction product does.</summary>
    public bool Floats(int itemType) => floating.Contains(itemType);

    public InternalLiquid Get(byte index) => index < liquids.Count ? liquids[index] : Default;

    public InternalLiquid? Find(string name) => byName.GetValueOrDefault(name);

    /// <summary>A internal type this set can create on demand from its name, or null. Not yet added to the set.</summary>
    protected internal virtual InternalLiquid? CreateDynamic(string name) => null;

    /// <summary>
    ///     The internal type saved or synced under <paramref name="name" />, creating it on demand. Clients never create
    ///     one this way; they receive the server's through <see cref="ReceiveDynamic" /> so indices agree.
    /// </summary>
    public InternalLiquid? Resolve(string name)
    {
        if (Find(name) is { } existing)
            return existing;

        if (Main.netMode == NetmodeID.MultiplayerClient || liquids.Count > byte.MaxValue || CreateDynamic(name) is not { } created)
            return null;

        AddLiquid(created);
        LiquidInternalSystem.OnDynamicAdded(created);
        return created;
    }

    /// <summary>The internal type named <paramref name="name" /> for reading its look and effects only, without adding it.</summary>
    public InternalLiquid? Describe(string name)
    {
        if (Find(name) is { } existing)
            return existing;

        if (CreateDynamic(name) is not { } created)
            return null;

        created.Liquid = this;
        return created;
    }

    /// <summary>Client side: puts a server-created internal type at the server's index.</summary>
    internal void ReceiveDynamic(byte index, string name)
    {
        if (index < staticCount || CreateDynamic(name) is not { } created)
            return;

        while (liquids.Count < index)
            AddLiquid(new InternalLiquid("_" + liquids.Count));

        if (index == liquids.Count)
        {
            AddLiquid(created);
            return;
        }

        byName.Remove(liquids[index].Name);
        byName.Remove(name);
        created.Index = index;
        created.Liquid = this;
        liquids[index] = created;
        byName[name] = created;
    }

    internal void ClearDynamic()
    {
        for (int i = liquids.Count - 1; i >= staticCount; i--)
        {
            byName.Remove(liquids[i].Name);
            liquids.RemoveAt(i);
        }
    }

    internal IEnumerable<InternalLiquid> DynamicLiquids()
    {
        for (int i = staticCount; i < liquids.Count; i++)
            yield return liquids[i];
    }

    /// <summary>The item a bottle draws from liquid in <paramref name="liquid" />, or 0 for none.</summary>
    public virtual int BottledItem(InternalLiquid liquid) => BottledItemByTable(liquid);

    /// <summary>The item set for <paramref name="liquid" /> with <see cref="SetBottledItem" />, or 0.</summary>
    public int BottledItemByTable(InternalLiquid liquid) => bottled.GetValueOrDefault(liquid);

    /// <summary>Makes <paramref name="itemType" /> what a bottle draws from <paramref name="liquid" />, as each solvent does for its liquid.</summary>
    public void SetBottledItem(InternalLiquid liquid, int itemType) => bottled[liquid] = itemType;

    /// <summary>Whether a near-full tile in <paramref name="liquid" /> is in the area vanilla checks for adjacent water.</summary>
    public static bool IsNear(Player player, InternalLiquid liquid)
    {
        int centerX = (int)((player.position.X + player.width * 0.5f) / 16f);
        int feetY = (int)((player.position.Y + player.height) / 16f);
        int type = liquid.Liquid.LiquidType;

        for (int x = centerX - 4; x <= centerX + 4; x++)
        {
            for (int y = feetY - 3; y < feetY + 3; y++)
            {
                if (!WorldGen.InWorld(x, y))
                    continue;

                Tile tile = Main.tile[x, y];
                if (tile.LiquidAmount > 200 && tile.LiquidType == type && tile.InternalLiquid() == liquid.Index)
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     The internal liquid at (i, j), or of the neighbour it shows through when the tile holds none of this type, as half
    ///     blocks and slopes do.
    /// </summary>
    public InternalLiquid InternalLiqAt(int i, int j)
    {
        if (Holds(i, j))
            return Get(Main.tile[i, j].InternalLiquid());

        foreach (Point next in (ReadOnlySpan<Point>)[new(i, j - 1), new(i - 1, j), new(i + 1, j), new(i, j + 1)])
        {
            if (WorldGen.InWorld(next.X, next.Y) && Holds(next.X, next.Y))
                return Get(Main.tile[next.X, next.Y].InternalLiquid());
        }

        return Default;
    }

    private bool Holds(int i, int j)
    {
        Tile tile = Main.tile[i, j];
        return tile.LiquidAmount > 0 && tile.LiquidType == LiquidType;
    }

    /// <summary>The internal liquid left after liquid of <paramref name="donor" /> flows into <paramref name="receiver" /> in volume.</summary>
    public virtual byte Mix(byte receiver, byte donor) => MixByTable(receiver, donor);

    /// <summary>The mix table first; otherwise anything replaces the default and the default never dilutes anything.</summary>
    public byte MixByTable(byte receiver, byte donor)
    {
        if (receiver == donor)
            return receiver;

        if (mixes.TryGetValue((receiver, donor), out byte result))
            return result;

        return receiver == 0 ? donor : receiver;
    }

    /// <summary>Applies the first reaction between the internal liquid at (i, j) and the tile at (tileX, tileY).</summary>
    public bool TryReact(int i, int j, int tileX, int tileY)
    {
        Tile liquidTile = Main.tile[i, j];
        Tile solid = Main.tile[tileX, tileY];
        InternalLiquid current = Get(liquidTile.InternalLiquid());

        foreach (TileReaction reaction in reactions)
        {
            if (reaction.From is { } from && from != current || !reaction.Matches(solid.TileType))
                continue;

            if (reaction.To is { } to && to != current)
                Spread(i, j, current.Index, to.Index);

            switch (reaction.Outcome)
            {
                case TileOutcome.Dissolve:
                    WorldGen.KillTile(tileX, tileY, noItem: true);

                    if (Main.netMode == NetmodeID.Server && !solid.HasTile)
                        NetMessage.SendData(MessageID.TileManipulation, number: KillTileNoItem, number2: tileX, number3: tileY);

                    break;

                case TileOutcome.Convert:
                    solid.TileType = reaction.Becomes;
                    WorldGen.SquareTileFrame(tileX, tileY);

                    if (Main.netMode == NetmodeID.Server)
                        NetMessage.SendTileSquare(-1, tileX, tileY);

                    break;
            }

            return true;
        }

        return false;
    }

    /// <summary>Converts up to <see cref="ReactionSpread" /> connected tiles of this type still in <paramref name="from" />.</summary>
    internal void Spread(int i, int j, byte from, byte to)
    {
        var pending = new Queue<Point>();
        var visited = new HashSet<Point> { new(i, j) };
        pending.Enqueue(new Point(i, j));
        int converted = 0;

        while (pending.TryDequeue(out Point point) && converted < ReactionSpread)
        {
            LiquidInternalSystem.Set(point.X, point.Y, to);
            converted++;

            foreach (Point next in (ReadOnlySpan<Point>)[new(point.X - 1, point.Y), new(point.X + 1, point.Y), new(point.X, point.Y - 1), new(point.X, point.Y + 1)])
            {
                if (!WorldGen.InWorld(next.X, next.Y, 1) || !visited.Add(next))
                    continue;

                Tile tile = Main.tile[next.X, next.Y];
                if (tile.LiquidAmount > 0 && tile.LiquidType == LiquidType && tile.InternalLiquid() == from)
                    pending.Enqueue(next);
            }
        }
    }

    /// <summary>
    ///     Removes <paramref name="amount" /> liquid units from the body of this type connected to (i, j), from its
    ///     surface down. Removes nothing and returns false when the body holds less.
    /// </summary>
    internal bool TrySpend(int i, int j, int amount)
    {
        var body = new List<Point>();
        var pending = new Queue<Point>();
        var visited = new HashSet<Point> { new(i, j) };
        pending.Enqueue(new Point(i, j));
        int available = 0;

        while (pending.TryDequeue(out Point point) && body.Count < SpendSearch)
        {
            if (!Holds(point.X, point.Y))
                continue;

            body.Add(point);
            available += Main.tile[point.X, point.Y].LiquidAmount;

            foreach (Point next in (ReadOnlySpan<Point>)[new(point.X - 1, point.Y), new(point.X + 1, point.Y), new(point.X, point.Y - 1), new(point.X, point.Y + 1)])
            {
                if (WorldGen.InWorld(next.X, next.Y, 1) && visited.Add(next))
                    pending.Enqueue(next);
            }
        }

        if (available < amount)
            return false;

        body.Sort((a, b) => a.Y.CompareTo(b.Y));

        foreach (Point point in body)
        {
            if (amount <= 0)
                break;

            Tile tile = Main.tile[point.X, point.Y];
            int taken = Math.Min(tile.LiquidAmount, amount);
            tile.LiquidAmount -= (byte)taken;
            amount -= taken;

            Liquid.AddWater(point.X, point.Y);

            if (Main.netMode == NetmodeID.Server)
                NetMessage.sendWater(point.X, point.Y);
        }

        return true;
    }

    /// <summary>The internal liquid holding the most liquid inside <paramref name="area" />, in world pixels.</summary>
    public InternalLiquid InternalLiqIn(Rectangle area)
    {
        int left = Math.Max(area.Left / 16, 0);
        int right = Math.Min(area.Right / 16, Main.maxTilesX - 1);
        int top = Math.Max(area.Top / 16, 0);
        int bottom = Math.Min(area.Bottom / 16, Main.maxTilesY - 1);

        int bestAmount = 0;
        byte best = 0;

        for (int x = left; x <= right; x++)
        {
            for (int y = top; y <= bottom; y++)
            {
                Tile tile = Main.tile[x, y];
                if (tile.LiquidType == LiquidType && tile.LiquidAmount > bestAmount)
                {
                    bestAmount = tile.LiquidAmount;
                    best = tile.InternalLiquid();
                }
            }
        }

        return Get(best);
    }

    /// <summary>Contact effect on a player in this liquid, given the internal liquid around them.</summary>
    protected internal virtual void OnPlayerContact(Player player, InternalLiquid liquid) { }

    /// <summary>Contact effect on an NPC in this liquid, given the internal liquid around them.</summary>
    protected internal virtual void OnNPCContact(NPC npc, InternalLiquid liquid) { }

    /// <summary>Contact with <paramref name="liquid" />, such as from a submerged pocket of it.</summary>
    public void ApplyContact(Player player, InternalLiquid liquid) => OnPlayerContact(player, liquid);

    /// <inheritdoc cref="ApplyContact(Player, InternalLiquid)" />
    public void ApplyContact(NPC npc, InternalLiquid liquid) => OnNPCContact(npc, liquid);

    private enum TileOutcome
    {
        Keep,
        Dissolve,
        Convert,
    }

    private sealed record TileReaction(InternalLiquid? From, InternalLiquid? To, Predicate<int> Matches, TileOutcome Outcome, ushort Becomes);
}
