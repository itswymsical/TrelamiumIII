using System.Collections.Generic;

namespace TrelamiumIII.Core.Liquids;

/// <summary>
///     The <see cref="LiquidCondition" />s tiles of one liquid type can carry, with their flow mixes and tile reactions.
///     A <see cref="ConditionalLiquid" /> owns one for its own type; a vanilla type gets one from a subclass.
/// </summary>
public abstract class LiquidConditionSet
{
    /// <summary><see cref="LiquidData.LiquidType" /> is packed in 6 bits.</summary>
    private const int MaxLiquidTypes = 64;

    /// <summary>Tiles a reaction converts at once, so a pool changes from its contact point outward.</summary>
    private const int ReactionSpread = 48;

    /// <summary><see cref="MessageID.TileManipulation" /> action for <c>WorldGen.KillTile(noItem: true)</c>.</summary>
    private const int KillTileNoItem = 4;

    private static readonly LiquidConditionSet?[] byType = new LiquidConditionSet?[MaxLiquidTypes];

    private static readonly List<LiquidConditionSet> all = [];

    public static IReadOnlyList<LiquidConditionSet> All => all;

    public static LiquidConditionSet? ByType(int type) => (uint)type < MaxLiquidTypes ? byType[type] : null;

    public static LiquidConditionSet? ByName(string fullName) => all.Find(set => set.FullName == fullName);

    private readonly List<LiquidCondition> conditions = [];

    private readonly Dictionary<string, LiquidCondition> byName = [];

    private readonly Dictionary<(byte Receiver, byte Donor), byte> mixes = [];

    private readonly List<TileReaction> reactions = [];

    /// <summary>Conditions added before <see cref="Register" />. Those past it are created at runtime and cleared with the world.</summary>
    private int staticCount;

    /// <summary>The <see cref="Tile.LiquidType" /> whose tiles carry these conditions.</summary>
    public abstract int LiquidType { get; }

    /// <summary>Save and lookup key.</summary>
    public abstract string FullName { get; }

    public abstract string Name { get; }

    public IReadOnlyList<LiquidCondition> Conditions => conditions;

    public LiquidCondition Default => conditions[0];

    internal int StaticCount => staticCount;

    public virtual string ConditionDisplayName(LiquidCondition condition) => condition.Name;

    /// <summary>Waterfall style for <paramref name="condition" /> on a vanilla type, or null for the type's own.</summary>
    public virtual int? WaterfallStyle(LiquidCondition condition) => null;

    /// <summary>Makes this the set <see cref="ByType" /> returns for <see cref="LiquidType" />, after its static conditions are added.</summary>
    protected internal void Register()
    {
        staticCount = conditions.Count;
        byType[LiquidType] = this;
        all.Add(this);
    }

    internal static void ClearRegistry()
    {
        Array.Clear(byType);
        all.Clear();
    }

    protected internal LiquidCondition AddCondition(LiquidCondition condition)
    {
        if (conditions.Count > byte.MaxValue)
            throw new InvalidOperationException($"{Name} has more than 256 conditions.");

        condition.Index = (byte)conditions.Count;
        condition.Liquid = this;
        conditions.Add(condition);
        byName.Add(condition.Name, condition);
        return condition;
    }

    /// <summary>Liquid of <paramref name="donor" /> flowing into <paramref name="receiver" /> makes <paramref name="result" />, both ways round.</summary>
    protected internal void AddMix(LiquidCondition receiver, LiquidCondition donor, LiquidCondition result)
    {
        mixes[(receiver.Index, donor.Index)] = result.Index;
        mixes[(donor.Index, receiver.Index)] = result.Index;
    }

    /// <summary>
    ///     Liquid in <paramref name="from" /> (any condition when null) beside one of <paramref name="tiles" /> turns into
    ///     <paramref name="to" />, from the contact point outward. The tile is untouched.
    /// </summary>
    protected internal void AddReaction(LiquidCondition? from, LiquidCondition to, params int[] tiles)
        => AddReaction(from, to, type => Array.IndexOf(tiles, type) >= 0);

    /// <inheritdoc cref="AddReaction(LiquidCondition?, LiquidCondition, int[])" />
    /// <remarks>Takes a predicate so modded tile types are read after they load.</remarks>
    protected internal void AddReaction(LiquidCondition? from, LiquidCondition to, Predicate<int> tiles)
        => reactions.Add(new TileReaction(from, to, tiles, TileOutcome.Keep, 0));

    /// <summary>
    ///     Liquid in <paramref name="from" /> (any condition when null) dissolves tiles matching <paramref name="tiles" />,
    ///     or turns them into <paramref name="becomes" /> when given. The liquid is unchanged.
    /// </summary>
    protected internal void AddCorrosion(LiquidCondition? from, Predicate<int> tiles, ushort? becomes = null)
        => reactions.Add(new TileReaction(from, null, tiles, becomes is null ? TileOutcome.Dissolve : TileOutcome.Convert, becomes ?? 0));

    public LiquidCondition Get(byte index) => index < conditions.Count ? conditions[index] : Default;

    public LiquidCondition? Find(string name) => byName.GetValueOrDefault(name);

    /// <summary>A condition this set can create on demand from its name, or null. Not yet added to the set.</summary>
    protected internal virtual LiquidCondition? CreateDynamic(string name) => null;

    /// <summary>
    ///     The condition saved or synced under <paramref name="name" />, creating it on demand. Clients never create
    ///     one this way; they receive the server's through <see cref="ReceiveDynamic" /> so indices agree.
    /// </summary>
    public LiquidCondition? Resolve(string name)
    {
        if (Find(name) is { } existing)
            return existing;

        if (Main.netMode == NetmodeID.MultiplayerClient || conditions.Count > byte.MaxValue || CreateDynamic(name) is not { } created)
            return null;

        AddCondition(created);
        LiquidConditionSystem.OnDynamicAdded(created);
        return created;
    }

    /// <summary>The condition named <paramref name="name" /> for reading its look and effects only, without adding it.</summary>
    public LiquidCondition? Describe(string name)
    {
        if (Find(name) is { } existing)
            return existing;

        if (CreateDynamic(name) is not { } created)
            return null;

        created.Liquid = this;
        return created;
    }

    /// <summary>Client side: puts a server-created condition at the server's index.</summary>
    internal void ReceiveDynamic(byte index, string name)
    {
        if (index < staticCount || CreateDynamic(name) is not { } created)
            return;

        while (conditions.Count < index)
            AddCondition(new LiquidCondition("_" + conditions.Count));

        if (index == conditions.Count)
        {
            AddCondition(created);
            return;
        }

        byName.Remove(conditions[index].Name);
        byName.Remove(name);
        created.Index = index;
        created.Liquid = this;
        conditions[index] = created;
        byName[name] = created;
    }

    internal void ClearDynamic()
    {
        for (int i = conditions.Count - 1; i >= staticCount; i--)
        {
            byName.Remove(conditions[i].Name);
            conditions.RemoveAt(i);
        }
    }

    internal IEnumerable<LiquidCondition> DynamicConditions()
    {
        for (int i = staticCount; i < conditions.Count; i++)
            yield return conditions[i];
    }

    /// <summary>The item a bottle draws from liquid in <paramref name="condition" />, or 0 for none.</summary>
    public virtual int BottledItem(LiquidCondition condition) => 0;

    /// <summary>
    ///     The condition at (i, j), or of the neighbour it shows through when the tile holds none of this type, as half
    ///     blocks and slopes do.
    /// </summary>
    public LiquidCondition ConditionAt(int i, int j)
    {
        if (Holds(i, j))
            return Get(Main.tile[i, j].LiquidCondition());

        foreach (Point next in (ReadOnlySpan<Point>)[new(i, j - 1), new(i - 1, j), new(i + 1, j), new(i, j + 1)])
        {
            if (WorldGen.InWorld(next.X, next.Y) && Holds(next.X, next.Y))
                return Get(Main.tile[next.X, next.Y].LiquidCondition());
        }

        return Default;
    }

    private bool Holds(int i, int j)
    {
        Tile tile = Main.tile[i, j];
        return tile.LiquidAmount > 0 && tile.LiquidType == LiquidType;
    }

    /// <summary>The condition left after liquid of <paramref name="donor" /> flows into <paramref name="receiver" /> in volume.</summary>
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

    /// <summary>Applies the first reaction between the condition at (i, j) and the tile at (tileX, tileY).</summary>
    public bool TryReact(int i, int j, int tileX, int tileY)
    {
        Tile liquidTile = Main.tile[i, j];
        Tile solid = Main.tile[tileX, tileY];
        LiquidCondition current = Get(liquidTile.LiquidCondition());

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
    private void Spread(int i, int j, byte from, byte to)
    {
        var pending = new Queue<Point>();
        var visited = new HashSet<Point> { new(i, j) };
        pending.Enqueue(new Point(i, j));
        int converted = 0;

        while (pending.TryDequeue(out Point point) && converted < ReactionSpread)
        {
            LiquidConditionSystem.Set(point.X, point.Y, to);
            converted++;

            foreach (Point next in (ReadOnlySpan<Point>)[new(point.X - 1, point.Y), new(point.X + 1, point.Y), new(point.X, point.Y - 1), new(point.X, point.Y + 1)])
            {
                if (!WorldGen.InWorld(next.X, next.Y, 1) || !visited.Add(next))
                    continue;

                Tile tile = Main.tile[next.X, next.Y];
                if (tile.LiquidAmount > 0 && tile.LiquidType == LiquidType && tile.LiquidCondition() == from)
                    pending.Enqueue(next);
            }
        }
    }

    /// <summary>The condition holding the most liquid inside <paramref name="area" />, in world pixels.</summary>
    public LiquidCondition ConditionIn(Rectangle area)
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
                    best = tile.LiquidCondition();
                }
            }
        }

        return Get(best);
    }

    /// <summary>Contact effect on a player in this liquid, given the condition around them.</summary>
    protected internal virtual void OnPlayerContact(Player player, LiquidCondition condition) { }

    /// <summary>Contact effect on an NPC in this liquid, given the condition around them.</summary>
    protected internal virtual void OnNPCContact(NPC npc, LiquidCondition condition) { }

    /// <summary>Contact with <paramref name="condition" />, such as from a submerged pocket of it.</summary>
    public void ApplyContact(Player player, LiquidCondition condition) => OnPlayerContact(player, condition);

    /// <inheritdoc cref="ApplyContact(Player, LiquidCondition)" />
    public void ApplyContact(NPC npc, LiquidCondition condition) => OnNPCContact(npc, condition);

    private enum TileOutcome
    {
        Keep,
        Dissolve,
        Convert,
    }

    private sealed record TileReaction(LiquidCondition? From, LiquidCondition? To, Predicate<int> Matches, TileOutcome Outcome, ushort Becomes);
}
