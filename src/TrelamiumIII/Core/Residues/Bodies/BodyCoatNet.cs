using System.IO;
using TrelamiumIII.Common.ChemicalClass;

namespace TrelamiumIII.Core.Residues.Bodies;

/// <summary>
///     Tells the other machines what's on a body. Only ever about how it looks: whatever deals the damage stays with the
///     machine it's on.
/// </summary>
/// <remarks>
///     NPCs need no packet for what they touch on the ground: every machine sees the same marks now and runs their contacts, so
///     each works out the same thing. A player's contacts only run on its own machine, so those go out now and then, and a hit
///     is only ever known to whoever landed it.
/// </remarks>
internal static class BodyCoatNet
{
    private const byte KIND_PLAYER = 0;

    private const byte KIND_NPC = 1;

    /// <summary>Who a server says it's from. No player occupies that slot.</summary>
    private const byte FROM_SERVER = 255;

    /// <summary>Ticks between telling the other machines a player's still touching something.</summary>
    private const uint TOUCH_SHARE_TICKS = 12;

    /// <summary>The share of the body's height a mark on the ground touches, around where it's touching.</summary>
    private const float TILE_RADIUS = 0.2f;

    private enum Kind : byte
    {
        Lay,
        Touch,
    }

    private static BodyCoats? CoatsOf(Entity body) => body switch
    {
        Player player => player.GetModPlayer<BodyCoatPlayer>().Coats,
        NPC npc => npc.GetGlobalNPC<BodyCoatNPC>().Coats,
        _ => null,
    };

    /// <summary>Holds a coat on <paramref name="body" /> here and on every other machine.</summary>
    public static void Lay(Entity body, ChemicalDamageType type, float fill, int ticks)
    {
        CoatsOf(body)?.Lay(type, fill, ticks, Main.myPlayer);
        Send(body, type, fill, ticks);
    }

    /// <summary>Holds a coat on <paramref name="body" /> on every other machine.</summary>
    public static void Send(Entity body, ChemicalDamageType type, float fill, int ticks)
    {
        if (Main.netMode == NetmodeID.SinglePlayer || body is not (Player or NPC))
            return;

        byte from = Main.netMode == NetmodeID.Server ? FROM_SERVER : (byte)Main.myPlayer;

        WriteLay(body is Player ? KIND_PLAYER : KIND_NPC, (byte)body.whoAmI, (byte)type, (byte)(MathHelper.Clamp(fill, 0f, 1f) * 255f), (ushort)ticks, from, -1);
    }

    /// <summary>Something landed on <paramref name="body" /> at a point in the world, here and on every other machine.</summary>
    /// <param name="radius">As a share of the body's height.</param>
    /// <param name="amount">What the middle of it gets, 0..1.</param>
    public static void Splash(Entity body, IResidue residue, Vector2 world, float radius, float amount, Color tint = default)
    {
        if (Main.dedServ)
            return;

        (float u, float v) = BodyBox.Of(body).ToGrid(world);

        CoatsOf(body)?.Touch(residue, u, v, radius, amount, tint);
        SendTouch(body, residue, u, v, radius, amount, tint);
    }

    /// <summary>A body is touching a mark on the ground.</summary>
    public static void TouchTile(Entity body, IResidue residue, Point tile, float amount, Color tint)
    {
        if (CoatsOf(body) is not { } coats)
            return;

        (float u, float v) = BodyBox.Of(body).ToGrid(tile.ToWorldCoordinates());
        BodyCoat coat = coats.Touch(residue, u, v, TILE_RADIUS, amount, tint);

        if (body is not Player || Main.GameUpdateCount < coat.NextShare)
            return;

        coat.NextShare = Main.GameUpdateCount + TOUCH_SHARE_TICKS;

        // Standing in it for that long is worth that much more at once.
        SendTouch(body, residue, u, v, TILE_RADIUS, MathF.Min(amount * TOUCH_SHARE_TICKS, 1f), tint);
    }

    private static void SendTouch(Entity body, IResidue residue, float u, float v, float radius, float amount, Color tint)
    {
        if (Main.netMode == NetmodeID.SinglePlayer)
            return;

        WriteTouch(
            body is Player ? KIND_PLAYER : KIND_NPC,
            (byte)body.whoAmI,
            (byte)ResidueSystem.IdOf(residue),
            (byte)(u * 255f),
            (byte)(v * 255f),
            (byte)(MathHelper.Clamp(radius, 0f, 1f) * 255f),
            (byte)(MathHelper.Clamp(amount, 0f, 1f) * 255f),
            tint.PackedValue,
            -1);
    }

    private static ModPacket Begin(Kind kind)
    {
        ModPacket packet = ModContent.GetInstance<TrelamiumIII>().GetPacket();

        packet.Write((byte)TrelamiumIII.MessageType.BodyCoat);
        packet.Write((byte)kind);

        return packet;
    }

    private static void WriteLay(byte kind, byte index, byte type, byte fill, ushort ticks, byte from, int ignoreClient)
    {
        ModPacket packet = Begin(Kind.Lay);

        packet.Write(kind);
        packet.Write(index);
        packet.Write(type);
        packet.Write(fill);
        packet.Write(ticks);
        packet.Write(from);
        packet.Send(-1, ignoreClient);
    }

    private static void WriteTouch(byte kind, byte index, byte residue, byte u, byte v, byte radius, byte amount, uint tint, int ignoreClient)
    {
        ModPacket packet = Begin(Kind.Touch);

        packet.Write(kind);
        packet.Write(index);
        packet.Write(residue);
        packet.Write(u);
        packet.Write(v);
        packet.Write(radius);
        packet.Write(amount);
        packet.Write(tint);
        packet.Send(-1, ignoreClient);
    }

    private static Entity? Target(byte kind, byte index)
    {
        Entity? body = kind == KIND_PLAYER
            ? index < Main.maxPlayers ? Main.player[index] : null
            : index < Main.maxNPCs ? Main.npc[index] : null;

        return body is { active: true } ? body : null;
    }

    internal static void Receive(BinaryReader reader, int whoAmI)
    {
        var message = (Kind)reader.ReadByte();
        byte kind = reader.ReadByte();
        byte index = reader.ReadByte();

        if (message == Kind.Lay)
        {
            byte type = reader.ReadByte();
            byte fill = reader.ReadByte();
            ushort ticks = reader.ReadUInt16();
            byte from = reader.ReadByte();

            // A server draws nothing, so it only passes this along to everyone but who sent it.
            if (Main.netMode == NetmodeID.Server)
                WriteLay(kind, index, type, fill, ticks, (byte)whoAmI, whoAmI);
            else if (Target(kind, index) is { } body)
                CoatsOf(body)?.Lay((ChemicalDamageType)type, fill / 255f, ticks, from);

            return;
        }

        byte residue = reader.ReadByte();
        byte u = reader.ReadByte();
        byte v = reader.ReadByte();
        byte radius = reader.ReadByte();
        byte amount = reader.ReadByte();
        uint tint = reader.ReadUInt32();

        if (Main.netMode == NetmodeID.Server)
            WriteTouch(kind, index, residue, u, v, radius, amount, tint, whoAmI);
        else if (Target(kind, index) is { } touched && ResidueSystem.ById(residue) is { } laid)
            CoatsOf(touched)?.Touch(laid, u / 255f, v / 255f, radius / 255f, amount / 255f, new Color { PackedValue = tint });
    }
}
