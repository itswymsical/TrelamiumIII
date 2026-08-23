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

    /// <summary>
    ///     Whether the Eye of Cthulhu's wake has already played in this world.
    /// </summary>
    /// <remarks>
    ///     Tracked separately from <see cref="NPC.downedBoss1" /> rather than reading it, because by
    ///     the time a death hook runs vanilla has generally already set its own flag -- so a check
    ///     against it would report every first kill as a repeat. This is only ever set after the
    ///     cutscene has been offered, which makes "first" mean what it says.
    /// </remarks>
    public static bool SeenEyeOfCthulhuWake;

    public override void ClearWorld()
    {
        DownedFungore = false;
        SeenEyeOfCthulhuWake = false;
    }

    public override void SaveWorldData(TagCompound tag)
    {
        // Written only when set. A tag that is absent reads back as false, so there is no reason to
        // spend a key on the common case.
        if (DownedFungore)
            tag[nameof(DownedFungore)] = true;

        if (SeenEyeOfCthulhuWake)
            tag[nameof(SeenEyeOfCthulhuWake)] = true;
    }

    public override void LoadWorldData(TagCompound tag)
    {
        DownedFungore = tag.ContainsKey(nameof(DownedFungore));
        SeenEyeOfCthulhuWake = tag.ContainsKey(nameof(SeenEyeOfCthulhuWake));
    }

    public override void NetSend(BinaryWriter writer)
    {
        writer.Write(DownedFungore);
        writer.Write(SeenEyeOfCthulhuWake);
    }

    public override void NetReceive(BinaryReader reader)
    {
        DownedFungore = reader.ReadBoolean();
        SeenEyeOfCthulhuWake = reader.ReadBoolean();
    }
}
