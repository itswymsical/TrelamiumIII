using System.IO;
using Terraria.ModLoader.IO;

namespace TrelamiumIII.Common.Systems;

/// <summary>
///     Per-world progression flags.
/// </summary>
/// <remarks>
///     Set through <see cref="NPC.SetEventFlagCleared" /> rather than by assignment, which is what
///     announces the kill and pushes the flag to every client. <see cref="NetSend" /> covers the
///     other direction: a player joining after the fact needs the world's history, not just its
///     future.
/// </remarks>
public sealed class DownedBossSystem : ModSystem
{
    public static bool DownedFungore;

    public override void ClearWorld()
    {
        DownedFungore = false;
    }

    public override void SaveWorldData(TagCompound tag)
    {
        // Written only when set. A tag that is absent reads back as false, so there is no reason to
        // spend a key on the common case.
        if (DownedFungore)
            tag[nameof(DownedFungore)] = true;
    }

    public override void LoadWorldData(TagCompound tag)
    {
        DownedFungore = tag.ContainsKey(nameof(DownedFungore));
    }

    public override void NetSend(BinaryWriter writer)
    {
        writer.Write(DownedFungore);
    }

    public override void NetReceive(BinaryReader reader)
    {
        DownedFungore = reader.ReadBoolean();
    }
}
