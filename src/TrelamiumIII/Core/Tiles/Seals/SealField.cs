using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
<<<<<<< HEAD
using TrelamiumIII.Core.Residues;
=======
using TrelamiumIII.Core.Tiles.Conditions;
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb

namespace TrelamiumIII.Core.Tiles.Seals;

public enum SealImpulseKind : byte
{
    /// <summary>Crests running out from the point struck: footsteps, landings, tool hits.</summary>
    Ripple,

    /// <summary>One band out to the reach, over a flash: explosions.</summary>
    Shockwave,
}

/// <summary>
///     How seals answer being struck. Each impulse wakes the sealed tiles within its reach, which the seal canvases stamp
///     while it runs, and adds to their charge, which glows and decays, so a seal struck often stays bright.
/// </summary>
/// <remarks>Purely visual and client side. Impulses a client raises for itself are relayed through the server.</remarks>
public sealed class SealField : ModSystem
{
    /// <summary>Must match MAX_IMPULSES in SealField.fx.</summary>
    public const int MaxImpulses = 16;

    private const int MaxAwake = 8192;

    /// <summary>Ticks an impulse's tiles stay stamped after it ends, so the last crest is not cut off.</summary>
    private const int HoldPadding = 10;

    private const float ChargeDecay = 0.992f;

    /// <summary>Charge is dropped below this, once the tile is no longer held awake.</summary>
    private const float ChargeFloor = 0.01f;

    private struct Impulse
    {
        public Vector2 Position;
        public float Reach;
        public float Strength;
        public SealImpulseKind Kind;
        public int Age;
        public int Life;

        public readonly float Progress => Life <= 0 ? 1f : (float)Age / Life;

        public readonly float Remaining => Strength * (1f - Progress);
    }

    private struct Wake
    {
        public float Charge;
        public int Hold;
    }

    private static readonly List<Impulse> impulses = [];

    private static readonly Dictionary<Point, Wake> awake = [];

    private static readonly List<Point> expired = [];

    private static readonly Vector4[] impulseA = new Vector4[MaxImpulses];

    private static readonly Vector4[] impulseB = new Vector4[MaxImpulses];

    internal static bool AnyAwake => awake.Count > 0;

    internal static bool IsAwake(Point tile) => awake.ContainsKey(tile);

    /// <summary>Writes the awake solid tiles of seal <paramref name="index" /> in <paramref name="area" />: full coverage, charge in data x.</summary>
    internal static void Collect(byte index, Rectangle area, TileCanvasWriter write)
    {
        foreach (KeyValuePair<Point, Wake> pair in awake)
        {
            Point tile = pair.Key;

            if (area.Contains(tile) && IsSolidSealed(tile, index))
                write(tile, new TileCanvasSample(1f, new Vector3(pair.Value.Charge, 0f, 0f)), 1f);
        }
    }

    internal static bool IsSolidSealed(Point tile, byte index)
    {
        Tile t = Main.tile[tile.X, tile.Y];

        return t.Seal() == index && t.HasTile && !t.IsActuated && Main.tileSolid[t.TileType];
    }

    /// <summary>
    ///     Strikes the seals around <paramref name="position" />. Does nothing unless a sealed tile is in reach.
    /// </summary>
    /// <param name="reach">World pixels the wave travels.</param>
    /// <param name="strength">0..1: wave height, and how much charge it adds.</param>
    /// <param name="life">Ticks the wave takes to reach <paramref name="reach" /> and fade.</param>
    /// <param name="sync">Relay to the other clients. Leave off for what every client sees for itself, like players walking.</param>
    public static void Strike(Vector2 position, float reach, float strength, SealImpulseKind kind, int life, bool sync = true)
    {
        if (!SealSystem.Any)
            return;

        if (sync && Main.netMode != NetmodeID.SinglePlayer)
            SendImpulse(position, reach, strength, kind, life, -1, Main.netMode == NetmodeID.Server ? -1 : Main.myPlayer);

        if (Main.dedServ)
            return;

        strength = MathHelper.Clamp(strength, 0f, 1f);

        if (!WakeTiles(position, reach, strength, life + HoldPadding))
            return;

        var impulse = new Impulse { Position = position, Reach = reach, Strength = strength, Kind = kind, Life = Math.Max(life, 1) };

        if (impulses.Count < MaxImpulses)
        {
            impulses.Add(impulse);
            return;
        }

        int weakest = 0;

        for (int k = 1; k < impulses.Count; k++)
        {
            if (impulses[k].Remaining < impulses[weakest].Remaining)
                weakest = k;
        }

        if (impulses[weakest].Remaining < strength)
            impulses[weakest] = impulse;
    }

    /// <summary>Wakes and charges the sealed tiles in reach, more toward the centre. False if none are sealed.</summary>
    private static bool WakeTiles(Vector2 position, float reach, float strength, int hold)
    {
        int radius = (int)MathF.Ceiling(reach / 16f) + 1;
        int cx = (int)(position.X / 16f);
        int cy = (int)(position.Y / 16f);
        float gain = strength * 0.3f;
        bool any = false;

        for (int x = cx - radius; x <= cx + radius; x++)
        {
            for (int y = cy - radius; y <= cy + radius; y++)
            {
                if (!WorldGen.InWorld(x, y) || Main.tile[x, y].Seal() == 0)
                    continue;

                float distance = Vector2.Distance(new Vector2(x * 16f + 8f, y * 16f + 8f), position);

                if (distance > reach + 24f)
                    continue;

                var tile = new Point(x, y);
                ref Wake wake = ref CollectionsMarshal.GetValueRefOrAddDefault(awake, tile, out bool existed);

                if (!existed && awake.Count > MaxAwake)
                {
                    awake.Remove(tile);
                    continue;
                }

                float falloff = MathF.Max(1f - distance / reach, 0f);

                wake.Charge = MathF.Min(wake.Charge + gain * falloff * falloff, 1f);
                wake.Hold = Math.Max(wake.Hold, hold);
                any = true;
            }
        }

        return any;
    }

    public override void PostUpdateEverything()
    {
        if (Main.dedServ)
            return;

        for (int k = impulses.Count - 1; k >= 0; k--)
        {
            Impulse impulse = impulses[k];

            if (++impulse.Age >= impulse.Life)
            {
                impulses.RemoveAt(k);
                continue;
            }

            impulses[k] = impulse;
        }

        expired.Clear();

        foreach (KeyValuePair<Point, Wake> pair in awake)
        {
            ref Wake wake = ref CollectionsMarshal.GetValueRefOrNullRef(awake, pair.Key);

            wake.Charge *= ChargeDecay;

            if (--wake.Hold <= 0 && wake.Charge < ChargeFloor)
                expired.Add(pair.Key);
        }

        foreach (Point tile in expired)
            awake.Remove(tile);
    }

    public override void ClearWorld()
    {
        impulses.Clear();
        awake.Clear();
    }

    /// <summary>Sets ImpulseA and ImpulseB; unused slots get zero strength and reach.</summary>
    internal static void ApplyImpulses(Effect effect)
    {
        for (int k = 0; k < MaxImpulses; k++)
        {
            if (k < impulses.Count)
            {
                Impulse impulse = impulses[k];

                impulseA[k] = new Vector4(impulse.Position.X, impulse.Position.Y, impulse.Reach, impulse.Strength);
                impulseB[k] = new Vector4(impulse.Progress, 0f, (float)impulse.Kind, 0f);
            }
            else
            {
                impulseA[k] = Vector4.Zero;
                impulseB[k] = new Vector4(1f, 0f, 0f, 0f);
            }
        }

        effect.Parameters["ImpulseA"]?.SetValue(impulseA);
        effect.Parameters["ImpulseB"]?.SetValue(impulseB);
    }

    private static void SendImpulse(Vector2 position, float reach, float strength, SealImpulseKind kind, int life, int toClient, int ignoreClient)
    {
        ModPacket packet = SealSystem.NewPacket(SealSystem.PacketKind.Impulse);
        packet.WriteVector2(position);
        packet.Write(reach);
        packet.Write(strength);
        packet.Write((byte)kind);
        packet.Write((ushort)life);
        packet.Send(toClient, ignoreClient);
    }

    internal static void ReceiveImpulse(BinaryReader reader, int whoAmI)
    {
        Vector2 position = reader.ReadVector2();
        float reach = reader.ReadSingle();
        float strength = reader.ReadSingle();
        var kind = (SealImpulseKind)reader.ReadByte();
        int life = reader.ReadUInt16();

        if (Main.netMode == NetmodeID.Server)
        {
            SendImpulse(position, reach, strength, kind, life, -1, whoAmI);
            return;
        }

        Strike(position, reach, strength, kind, life, sync: false);
    }
}
