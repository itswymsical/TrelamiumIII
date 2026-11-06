using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria.ModLoader.IO;

namespace TrelamiumIII.Core.Liquids;

/// <summary>A volume of one liquid held at a position inside a body of another, until bottled or stranded.</summary>
public sealed class SubmergedPocket
{
    public Vector2 Position;

    /// <summary>Liquid units; a full tile holds 255.</summary>
    public int Volume;

    public InternalLiquidSet Liquid = null!;

    /// <summary>Kept by name so runtime conditions survive saving and syncing without agreeing on an index.</summary>
    public string Condition = "";

    public float Radius => SubmergedLiquidSystem.Radius(Volume);

    public InternalLiquidType? Describe() => Liquid.Describe(Condition);
}

/// <summary>
///     Owns every <see cref="SubmergedPocket" />. Pockets sink through their host as it drains, return to open liquid
///     when it is gone, and give up <see cref="BottleVolume" /> at a time to a bottle.
/// </summary>
public sealed class SubmergedLiquidSystem : ModSystem
{
    /// <summary>Liquid units one bottle draws out of a pocket.</summary>
    public const int BottleVolume = 64;

    /// <summary>Pockets of the same liquid and condition closer than this, in pixels, combine.</summary>
    private const float MergeRange = 48f;

    private const int SettleInterval = 15;

    private static readonly List<SubmergedPocket> pockets = [];

    private static bool dirty;

    public static IReadOnlyList<SubmergedPocket> Pockets => pockets;

    /// <summary>Drawn and touched radius in pixels: a dose is about half a tile, a full tile about a tile.</summary>
    public static float Radius(int volume) => 6f + 10f * MathF.Sqrt(Math.Max(volume, 0) / 255f);

    public override void ClearWorld()
    {
        pockets.Clear();
        dirty = false;
    }

    /// <summary>Holds <paramref name="volume" /> of <paramref name="condition" /> at <paramref name="position" />, joining a pocket of it nearby.</summary>
    public static void Submerge(InternalLiquidType condition, Vector2 position, int volume)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || volume <= 0)
            return;

        foreach (SubmergedPocket pocket in pockets)
        {
            if (pocket.Liquid == condition.Liquid && pocket.Condition == condition.Name && Vector2.Distance(pocket.Position, position) < MergeRange)
            {
                pocket.Volume += volume;
                dirty = true;
                return;
            }
        }

        pockets.Add(new SubmergedPocket { Position = position, Volume = volume, Liquid = condition.Liquid, Condition = condition.Name });
        dirty = true;
    }

    /// <summary>The pocket whose edge is nearest <paramref name="world" />, within <paramref name="range" /> pixels of it.</summary>
    public static SubmergedPocket? Nearest(Vector2 world, float range)
    {
        SubmergedPocket? best = null;
        float bestDistance = float.MaxValue;

        foreach (SubmergedPocket pocket in pockets)
        {
            float distance = Vector2.Distance(pocket.Position, world) - pocket.Radius;
            if (distance <= range && distance < bestDistance)
            {
                best = pocket;
                bestDistance = distance;
            }
        }

        return best;
    }

    public static bool CanBottle(SubmergedPocket pocket)
        => pocket.Volume >= BottleVolume / 2 && pocket.Describe() is { } condition && pocket.Liquid.BottledItem(condition) > ItemID.None;

    /// <summary>Bottles a dose of <paramref name="pocket" /> for <paramref name="player" />, asking the server from a client.</summary>
    public static void RequestExtract(SubmergedPocket pocket, Player player)
    {
        if (Main.netMode != NetmodeID.MultiplayerClient)
        {
            TryExtract(pocket, player);
            return;
        }

        ModPacket packet = LiquidConditionSystem.NewPacket(LiquidConditionSystem.PacketKind.Extract);
        packet.Write((byte)pocket.Liquid.LiquidType);
        packet.Write(pocket.Condition);
        packet.WriteVector2(pocket.Position);
        packet.Send();
    }

    internal static void ReceiveExtract(BinaryReader reader, int whoAmI)
    {
        int type = reader.ReadByte();
        string condition = reader.ReadString();
        Vector2 position = reader.ReadVector2();

        SubmergedPocket? pocket = pockets
            .Where(p => p.Liquid.LiquidType == type && p.Condition == condition)
            .MinBy(p => Vector2.DistanceSquared(p.Position, position));

        if (pocket is not null && Vector2.Distance(pocket.Position, position) < MergeRange * 2f)
            TryExtract(pocket, Main.player[whoAmI]);
    }

    private static bool TryExtract(SubmergedPocket pocket, Player player)
    {
        if (!CanBottle(pocket))
            return false;

        int item = pocket.Liquid.BottledItem(pocket.Describe()!);

        pocket.Volume -= BottleVolume;
        if (pocket.Volume <= 0)
            pockets.Remove(pocket);

        player.QuickSpawnItem(player.GetSource_FromThis(), item);
        dirty = true;
        return true;
    }

    public override void PostUpdateWorld()
    {
        if (Main.GameUpdateCount % SettleInterval == 0)
            Settle();

        if (Main.netMode == NetmodeID.Server && dirty)
        {
            SendPockets(-1);
            dirty = false;
        }
    }

    /// <summary>Keeps every pocket inside a host: sinking, sidestepping, or returning to open liquid when there is none.</summary>
    private static void Settle()
    {
        for (int k = pockets.Count - 1; k >= 0; k--)
        {
            SubmergedPocket pocket = pockets[k];
            int x = (int)(pocket.Position.X / 16f);
            int y = (int)(pocket.Position.Y / 16f);

            if (!WorldGen.InWorld(x, y, 2))
            {
                pockets.RemoveAt(k);
                dirty = true;
                continue;
            }

            // A pocket rests where its host is full, so it sits under the surface rather than on it.
            if (Hosts(pocket, x, y) && (Main.tile[x, y].LiquidAmount == byte.MaxValue || !Hosts(pocket, x, y + 1)))
                continue;

            if (Hosts(pocket, x, y + 1))
                Move(pocket, 0, 1);
            else if (Hosts(pocket, x - 1, y))
                Move(pocket, -1, 0);
            else if (Hosts(pocket, x + 1, y))
                Move(pocket, 1, 0);
            else
            {
                Release(pocket, x, y);
                pockets.RemoveAt(k);
            }

            dirty = true;
        }
    }

    private static bool Hosts(SubmergedPocket pocket, int x, int y)
    {
        Tile tile = Main.tile[x, y];
        return tile.LiquidAmount > 0 && tile.LiquidType != pocket.Liquid.LiquidType && !WorldGen.SolidTile(x, y);
    }

    private static void Move(SubmergedPocket pocket, int dx, int dy) => pocket.Position += new Vector2(dx, dy) * 16f;

    /// <summary>Puts a stranded pocket back as open liquid around (x, y). Whatever finds no room is lost.</summary>
    private static void Release(SubmergedPocket pocket, int x, int y)
    {
        if (pocket.Liquid.Resolve(pocket.Condition) is not { } condition)
            return;

        int remaining = pocket.Volume;

        foreach (Point point in (ReadOnlySpan<Point>)[new(x, y), new(x, y - 1), new(x - 1, y), new(x + 1, y), new(x, y + 1)])
        {
            if (remaining <= 0)
                break;

            Tile tile = Main.tile[point.X, point.Y];
            bool own = tile.LiquidAmount > 0 && tile.LiquidType == pocket.Liquid.LiquidType;

            if (WorldGen.SolidTile(point.X, point.Y) || tile.LiquidAmount > 0 && !own)
                continue;

            int placed = Math.Min(byte.MaxValue - tile.LiquidAmount, remaining);
            if (placed <= 0)
                continue;

            byte index = own ? pocket.Liquid.Mix(tile.LiquidCondition(), condition.Index) : condition.Index;

            tile.LiquidType = pocket.Liquid.LiquidType;
            tile.LiquidAmount += (byte)placed;
            LiquidConditionSystem.Set(point.X, point.Y, index);
            Liquid.AddWater(point.X, point.Y);

            if (Main.netMode == NetmodeID.Server)
                NetMessage.sendWater(point.X, point.Y);

            remaining -= placed;
        }
    }

    #region Save and sync

    public override void SaveWorldData(TagCompound tag)
    {
        if (pockets.Count == 0)
            return;

        tag["submergedPockets"] = pockets.Select(pocket => new TagCompound
        {
            ["liquid"] = pocket.Liquid.FullName,
            ["condition"] = pocket.Condition,
            ["position"] = pocket.Position,
            ["volume"] = pocket.Volume,
        }).ToList();
    }

    public override void LoadWorldData(TagCompound tag)
    {
        if (!tag.TryGet("submergedPockets", out List<TagCompound> saved))
            return;

        foreach (TagCompound entry in saved)
        {
            if (InternalLiquidSet.ByName(entry.GetString("liquid")) is not { } liquid)
                continue;

            pockets.Add(new SubmergedPocket
            {
                Liquid = liquid,
                Condition = entry.GetString("condition"),
                Position = entry.Get<Vector2>("position"),
                Volume = entry.GetInt("volume"),
            });
        }
    }

    internal static void SendPockets(int toClient)
    {
        ModPacket packet = LiquidConditionSystem.NewPacket(LiquidConditionSystem.PacketKind.Pockets);
        packet.Write((ushort)pockets.Count);

        foreach (SubmergedPocket pocket in pockets)
        {
            packet.Write((byte)pocket.Liquid.LiquidType);
            packet.Write(pocket.Condition);
            packet.WriteVector2(pocket.Position);
            packet.Write(pocket.Volume);
        }

        packet.Send(toClient);
    }

    internal static void ReceivePockets(BinaryReader reader)
    {
        pockets.Clear();
        int count = reader.ReadUInt16();

        for (int k = 0; k < count; k++)
        {
            InternalLiquidSet? liquid = InternalLiquidSet.ByType(reader.ReadByte());
            string condition = reader.ReadString();
            Vector2 position = reader.ReadVector2();
            int volume = reader.ReadInt32();

            if (liquid is not null)
                pockets.Add(new SubmergedPocket { Liquid = liquid, Condition = condition, Position = position, Volume = volume });
        }
    }

    #endregion
}

/// <summary>Applies the contact effect of any pocket the local player is inside.</summary>
public sealed class SubmergedLiquidPlayer : ModPlayer
{
    public override void PostUpdate()
    {
        if (Player.whoAmI != Main.myPlayer || SubmergedLiquidSystem.Pockets.Count == 0)
            return;

        Rectangle hitbox = Player.Hitbox;

        foreach (SubmergedPocket pocket in SubmergedLiquidSystem.Pockets)
        {
            Vector2 closest = Vector2.Clamp(pocket.Position, hitbox.TopLeft(), hitbox.BottomRight());

            if (Vector2.Distance(closest, pocket.Position) <= pocket.Radius && pocket.Describe() is { } condition)
                pocket.Liquid.ApplyContact(Player, condition);
        }
    }
}
