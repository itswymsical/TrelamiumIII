using System.IO;
using Terraria.ModLoader.IO;
using TrelamiumIII.Core.Jobs;

namespace TrelamiumIII.Common.Systems;

/// <summary>Counts world days, the reading behind <see cref="JobCondition.WorldDay"/>.</summary>
/// <remarks>
/// The server owns the count and syncs it in world data. Job state is saved per player on each client,
/// so a rising count only marks each client's own player as worth re-evaluating; the unlocking itself
/// is <see cref="JobPlayer"/>'s.
/// </remarks>
public class WorldTimeSystem : ModSystem, Subworlds.ISharedWorldData
{
    public static int TotalDaysElapsed;

    public override void PreUpdateWorld()
    {
        if (!Main.dayTime || Main.time != 0.0)
            return;

        TotalDaysElapsed++;

        // Clients re-read their own gates when the new count reaches them in NetReceive.
        if (Main.netMode == NetmodeID.Server)
            NetMessage.SendData(MessageID.WorldData);
        else
            InvalidateLocalUnlocks();
    }

    // Static, so without this a new world starts on the last world's day count.
    public override void ClearWorld() => TotalDaysElapsed = 0;

    public override void SaveWorldData(TagCompound tag) => tag["TotalDaysElapsed"] = TotalDaysElapsed;

    public override void LoadWorldData(TagCompound tag) => TotalDaysElapsed = tag.GetInt("TotalDaysElapsed");

    public override void NetSend(BinaryWriter writer) => writer.Write(TotalDaysElapsed);

    public override void NetReceive(BinaryReader reader)
    {
        int previous = TotalDaysElapsed;
        TotalDaysElapsed = reader.ReadInt32();

        // While joining (gameMenu), JobPlayer.OnEnterWorld catches the player up instead.
        if (TotalDaysElapsed > previous && !Main.gameMenu)
            InvalidateLocalUnlocks();
    }

    private static void InvalidateLocalUnlocks()
        => Main.LocalPlayer?.GetModPlayer<JobPlayer>().InvalidateUnlocks();
}
