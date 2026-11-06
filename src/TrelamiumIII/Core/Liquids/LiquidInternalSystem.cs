using System.Collections.Generic;
using System.IO;
using System.Linq;
using ModLiquidLib.ModLoader;
using MonoMod.Cil;
using Terraria.Audio;
using Terraria.GameContent.Liquid;
using Terraria.Graphics;
using Terraria.ModLoader.IO;

namespace TrelamiumIII.Core.Liquids;

/// <summary>
///     Keeps <see cref="InternalLiquidData" /> with the liquid: carries it along <see cref="Liquid.Update" /> flow,
///     mixes it by volume, tints the normal liquid draw, saves it with the world and syncs it to clients.
/// </summary>
public sealed class LiquidInternalSystem : ModSystem
{
    /// <summary>Cells one <see cref="Liquid.Update" /> can move liquid between: three either side and the one below.</summary>
    private static readonly Point[] FlowCells = [new(-3, 0), new(-2, 0), new(-1, 0), new(0, 0), new(1, 0), new(2, 0), new(3, 0), new(0, 1)];

    private const int SourceCell = 3;

    /// <summary>Entries per packet: 5 bytes each, well under the 65535-byte packet limit.</summary>
    private const int EntriesPerPacket = 8000;

    private static readonly HashSet<Point> dirty = [];

    private delegate void TintVerticesDelegate(ref VertexColors vertices, int i, int j, byte type);

    private readonly record struct FlowCell(byte Amount, byte Condition);

    internal enum PacketKind : byte
    {
        Request,
        Entries,
        Palette,
        Place,
        Pockets,
        Extract,
        Fizz,
    }

    public override void Load()
    {
        On_Liquid.Update += TrackFlow;
        IL_LiquidRenderer.DrawNormalLiquids += TintNormalLiquids;
    }

    public override void Unload()
    {
        On_Liquid.Update -= TrackFlow;
        IL_LiquidRenderer.DrawNormalLiquids -= TintNormalLiquids;
        dirty.Clear();
    }

    public override void ClearWorld()
    {
        dirty.Clear();

        foreach (InternalLiquidSet liquid in InternalLiquidSet.All)
            liquid.ClearDynamic();
    }

    /// <summary>Sets a tile's condition, queuing it for clients when it changes.</summary>
    public static void Set(int i, int j, byte index)
    {
        ref byte current = ref Main.tile[i, j].InternalLiquid();
        if (current == index)
            return;

        current = index;

        if (Main.netMode == NetmodeID.Server)
            dirty.Add(new Point(i, j));
    }

    /// <summary>
    ///     Fills (i, j) with <paramref name="amount" /> of a liquid in the named condition. From a client this asks the
    ///     server, which resolves the name so runtime conditions keep the server's index.
    /// </summary>
    public static void Place(int i, int j, int liquidType, string? internalType, byte amount)
    {
        if (!WorldGen.InWorld(i, j, 1))
            return;

        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            ModPacket packet = NewPacket(PacketKind.Place);
            packet.Write((ushort)i);
            packet.Write((ushort)j);
            packet.Write((byte)liquidType);
            packet.Write(internalType ?? "");
            packet.Write(amount);
            packet.Send();
            return;
        }

        Tile tile = Main.tile[i, j];
        tile.LiquidType = liquidType;
        tile.LiquidAmount = amount;

        if (InternalLiquidSet.ByType(liquidType) is { } liquid)
            Set(i, j, string.IsNullOrEmpty(internalType) ? (byte)0 : liquid.Resolve(internalType)?.Index ?? 0);

        WorldGen.SquareTileFrame(i, j);
        Liquid.AddWater(i, j);

        if (Main.netMode == NetmodeID.Server)
            NetMessage.sendWater(i, j);
    }

    internal static ModPacket NewPacket(PacketKind kind)
    {
        ModPacket packet = TrelamiumIII.Instance.GetPacket();
        packet.Write((byte)TrelamiumIII.MessageType.InternalLiquids);
        packet.Write((byte)kind);
        return packet;
    }

    /// <summary>Server side: tells clients where a runtime condition sits, before any tile uses it.</summary>
    internal static void OnDynamicAdded(InternalLiquidType internalType)
    {
        if (Main.netMode == NetmodeID.Server)
            SendPalette([internalType], -1);
    }

    private static void SendPalette(IEnumerable<InternalLiquidType> internalTypes, int toClient)
    {
        foreach (InternalLiquidType[] chunk in internalTypes.Chunk(256))
        {
            ModPacket packet = NewPacket(PacketKind.Palette);
            packet.Write((ushort)chunk.Length);

            foreach (InternalLiquidType internalType in chunk)
            {
                packet.Write((byte)internalType.Liquid.LiquidType);
                packet.Write(internalType.Index);
                packet.Write(internalType.Name);
            }

            packet.Send(toClient);
        }
    }

    #region Flow

    private static void TrackFlow(On_Liquid.orig_Update orig, Liquid self)
    {
        int x = self.x;
        int y = self.y;

        if (!WorldGen.InWorld(x, y, 4))
        {
            orig(self);
            return;
        }

        Tile source = Main.tile[x, y];
        InternalLiquidSet? liquid = source.LiquidAmount > 0 ? InternalLiquidSet.ByType(source.LiquidType) : null;

        if (liquid is null)
        {
            orig(self);
            return;
        }

        Span<FlowCell> before = stackalloc FlowCell[FlowCells.Length];
        for (int k = 0; k < FlowCells.Length; k++)
        {
            Tile tile = Main.tile[x + FlowCells[k].X, y + FlowCells[k].Y];
            before[k] = new FlowCell(tile.LiquidType == liquid.LiquidType ? tile.LiquidAmount : (byte)0, tile.InternalLiquid());
        }

        orig(self);

        Resolve(liquid, x, y, before);
    }

    /// <summary>
    ///     The cell that lost the most liquid is the donor. A cell that was empty takes the donor's condition; one that
    ///     gained at least half its own volume mixes with it. Resting pools exchange nothing, so they never mix.
    /// </summary>
    private static void Resolve(InternalLiquidSet liquid, int x, int y, ReadOnlySpan<FlowCell> before)
    {
        Span<byte> after = stackalloc byte[FlowCells.Length];
        int donor = SourceCell;
        int largestLoss = 0;

        for (int k = 0; k < FlowCells.Length; k++)
        {
            Tile tile = Main.tile[x + FlowCells[k].X, y + FlowCells[k].Y];
            after[k] = tile.LiquidType == liquid.LiquidType ? tile.LiquidAmount : (byte)0;

            int loss = before[k].Amount - after[k];
            if (loss > largestLoss)
            {
                largestLoss = loss;
                donor = k;
            }
        }

        byte incoming = before[donor].Condition;

        for (int k = 0; k < FlowCells.Length; k++)
        {
            int i = x + FlowCells[k].X;
            int j = y + FlowCells[k].Y;
            int gained = after[k] - before[k].Amount;

            if (after[k] == 0 && before[k].Amount > 0)
                Main.tile[i, j].InternalLiquid() = 0;
            else if (gained > 0 && before[k].Amount == 0)
                Set(i, j, incoming);
            else if (gained > 0 && gained * 2 >= before[k].Amount)
                Set(i, j, liquid.Mix(before[k].Condition, incoming));
        }
    }

    #endregion

    #region Drawing

    private void TintNormalLiquids(ILContext il)
    {
        var c = new ILCursor(il);
        int cache = -1;
        int i = -1;
        int j = -1;
        int vertices = -1;

        if (!c.TryGotoNext(
                x => x.MatchLdloc(out cache),
                x => x.MatchLdfld<LiquidRenderer.LiquidDrawCache>(nameof(LiquidRenderer.LiquidDrawCache.IsVisible)))
            || !c.TryGotoNext(MoveType.After,
                x => x.MatchLdloc(out i),
                x => x.MatchLdloc(out j),
                x => x.MatchLdloca(out vertices),
                x => x.MatchLdcR4(1f),
                x => x.MatchCall(typeof(Lighting), nameof(Lighting.GetCornerColors))))
        {
            Mod.Logger.Warn("Could not patch LiquidRenderer.DrawNormalLiquids; liquid conditions will draw untinted.");
            return;
        }

        c.EmitLdloca(vertices);
        c.EmitLdloc(i);
        c.EmitLdloc(j);
        c.EmitLdloc(cache);
        c.EmitLdfld(typeof(LiquidRenderer.LiquidDrawCache).GetField(nameof(LiquidRenderer.LiquidDrawCache.Type))!);
        c.EmitDelegate<TintVerticesDelegate>(TintVertices);
    }

    private static void TintVertices(ref VertexColors vertices, int i, int j, byte type)
    {
        if (InternalLiquidSet.ByType(type) is { } liquid)
            InternalLiquid.Tint(ref vertices, liquid.InternalLiqAt(i, j).Tint);
    }

    #endregion

    #region Save and load

    public override void SaveWorldData(TagCompound tag)
    {
        if (InternalLiquidSet.All.Count == 0)
            return;

        var positions = new List<int>[LiquidLoader.LiquidCount];
        var values = new List<byte>[LiquidLoader.LiquidCount];
        var last = new int[LiquidLoader.LiquidCount];

        for (int x = 0; x < Main.maxTilesX; x++)
        {
            for (int y = 0; y < Main.maxTilesY; y++)
            {
                Tile tile = Main.tile[x, y];
                byte internalType = tile.InternalLiquid();

                if (internalType == 0 || tile.LiquidAmount == 0 || InternalLiquidSet.ByType(tile.LiquidType) is null)
                    continue;

                int type = tile.LiquidType;
                int index = x * Main.maxTilesY + y;

                (positions[type] ??= []).Add(index - last[type]);
                (values[type] ??= []).Add(internalType);
                last[type] = index;
            }
        }

        var bases = new List<TagCompound>();

        foreach (InternalLiquidSet liquid in InternalLiquidSet.All)
        {
            if (positions[liquid.LiquidType] is not { } deltas)
                continue;

            bases.Add(new TagCompound
            {
                ["liquid"] = liquid.FullName,
                ["names"] = liquid.InternalLiquids.Select(internalType => internalType.Name).ToList(),
                ["positions"] = deltas.ToArray(),
                ["values"] = values[liquid.LiquidType].ToArray(),
            });
        }

        if (bases.Count > 0)
            tag["liquidConditions"] = bases;
    }

    public override void LoadWorldData(TagCompound tag)
    {
        if (!tag.TryGet("liquidConditions", out List<TagCompound> bases))
            return;

        int tiles = Main.maxTilesX * Main.maxTilesY;

        foreach (TagCompound saved in bases)
        {
            if (InternalLiquidSet.ByName(saved.GetString("liquid")) is not { } liquid)
                continue;

            byte[] remap = saved.GetList<string>("names").Select(name => liquid.Resolve(name)?.Index ?? 0).ToArray();
            int[] deltas = saved.GetIntArray("positions");
            byte[] conditions = saved.GetByteArray("values");
            int index = 0;

            for (int k = 0; k < deltas.Length && k < conditions.Length; k++)
            {
                index += deltas[k];
                if ((uint)index >= (uint)tiles)
                    break;

                byte condition = conditions[k] < remap.Length ? remap[conditions[k]] : (byte)0;
                Main.tile[index / Main.maxTilesY, index % Main.maxTilesY].InternalLiquid() = condition;
            }
        }
    }

    #endregion

    #region Sync

    public override void PostUpdateWorld()
    {
        if (Main.netMode != NetmodeID.Server || dirty.Count == 0)
            return;

        SendEntries(dirty, -1);
        dirty.Clear();
    }

    /// <summary>Client side: asks the server for every marked tile and pocket once the player is in the world.</summary>
    internal static void RequestAll()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            NewPacket(PacketKind.Request).Send();
    }

    internal static void Receive(BinaryReader reader, int whoAmI)
    {
        switch ((PacketKind)reader.ReadByte())
        {
            case PacketKind.Request when Main.netMode == NetmodeID.Server:
                SendPalette(InternalLiquidSet.All.SelectMany(liquid => liquid.DynamicConditions()), whoAmI);
                SendEntries(MarkedTiles(), whoAmI);
                SubmergedLiquidSystem.SendPockets(whoAmI);
                break;

            case PacketKind.Entries:
                ReceiveEntries(reader);
                break;

            case PacketKind.Palette:
                ReceivePalette(reader);
                break;

            case PacketKind.Place when Main.netMode == NetmodeID.Server:
                Place(reader.ReadUInt16(), reader.ReadUInt16(), reader.ReadByte(), reader.ReadString(), reader.ReadByte());
                break;

            case PacketKind.Pockets:
                SubmergedLiquidSystem.ReceivePockets(reader);
                break;

            case PacketKind.Extract when Main.netMode == NetmodeID.Server:
                SubmergedLiquidSystem.ReceiveExtract(reader, whoAmI);
                break;

            case PacketKind.Fizz:
                FizzEffect(reader.ReadUInt16(), reader.ReadUInt16());
                break;
        }
    }

    /// <summary>Hiss and fumes where two liquids just reacted. Played locally, or sent to clients from a server.</summary>
    public static void Fizz(int i, int j)
    {
        if (Main.netMode != NetmodeID.Server)
        {
            FizzEffect(i, j);
            return;
        }

        ModPacket packet = NewPacket(PacketKind.Fizz);
        packet.Write((ushort)i);
        packet.Write((ushort)j);
        packet.Send();
    }

    private static void FizzEffect(int i, int j)
    {
        var position = new Vector2(i * 16, j * 16);

        if (Main.rand.NextBool(3))
            SoundEngine.PlaySound(SoundID.LiquidsWaterLava with { Volume = 0.5f, Pitch = 0.4f }, position);

        for (int k = 0; k < 4; k++)
        {
            Dust dust = Dust.NewDustDirect(position, 16, 8, DustID.Smoke, 0f, -1.5f, 120, new Color(200, 255, 170), 1.1f);
            dust.velocity *= 0.5f;
            dust.noGravity = true;
        }
    }

    private static void ReceiveEntries(BinaryReader reader)
    {
        int count = reader.ReadUInt16();

        for (int k = 0; k < count; k++)
        {
            int i = reader.ReadUInt16();
            int j = reader.ReadUInt16();
            byte condition = reader.ReadByte();

            if (WorldGen.InWorld(i, j))
                Main.tile[i, j].InternalLiquid() = condition;
        }
    }

    private static void ReceivePalette(BinaryReader reader)
    {
        int count = reader.ReadUInt16();

        for (int k = 0; k < count; k++)
        {
            InternalLiquidSet? liquid = InternalLiquidSet.ByType(reader.ReadByte());
            byte index = reader.ReadByte();
            string name = reader.ReadString();

            liquid?.ReceiveDynamic(index, name);
        }
    }

    private static IEnumerable<Point> MarkedTiles()
    {
        for (int x = 0; x < Main.maxTilesX; x++)
        {
            for (int y = 0; y < Main.maxTilesY; y++)
            {
                Tile tile = Main.tile[x, y];
                if (tile.InternalLiquid() != 0 && tile.LiquidAmount > 0 && InternalLiquidSet.ByType(tile.LiquidType) is not null)
                    yield return new Point(x, y);
            }
        }
    }

    private static void SendEntries(IEnumerable<Point> tiles, int toClient)
    {
        foreach (Point[] chunk in tiles.Chunk(EntriesPerPacket))
        {
            ModPacket packet = NewPacket(PacketKind.Entries);
            packet.Write((ushort)chunk.Length);

            foreach (Point tile in chunk)
            {
                packet.Write((ushort)tile.X);
                packet.Write((ushort)tile.Y);
                packet.Write(Main.tile[tile.X, tile.Y].InternalLiquid());
            }

            packet.Send(toClient);
        }
    }

    #endregion
}

/// <summary>Requests the world's liquid internal types when this client's player enters.</summary>
public sealed class InternalLiquidPlayer : ModPlayer
{
    public override void OnEnterWorld() => LiquidInternalSystem.RequestAll();
}

/// <summary>Runs <see cref="InternalLiquidSet.TryReact" /> from each tile's random update against the liquid beside it.</summary>
public sealed class LiquidReactionGlobalTile : GlobalTile
{
    public override void RandomUpdate(int i, int j, int type)
    {
        if (InternalLiquidSet.All.Count == 0 || !WorldGen.InWorld(i, j, 2))
            return;

        TryNeighbour(i, j, i, j - 1);
        TryNeighbour(i, j, i - 1, j);
        TryNeighbour(i, j, i + 1, j);
        TryNeighbour(i, j, i, j + 1);
    }

    private static void TryNeighbour(int tileX, int tileY, int i, int j)
    {
        Tile tile = Main.tile[i, j];
        if (tile.LiquidAmount > 0 && InternalLiquidSet.ByType(tile.LiquidType) is { } liquid)
            liquid.TryReact(i, j, tileX, tileY);
    }
}
