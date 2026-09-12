using System.IO;
using Terraria.ModLoader.IO;

namespace TrelamiumIII.Common.Systems;

/// <summary>
///     Per-world progression flags. Set them through <see cref="NPC.SetEventFlagCleared" />, which announces the
///     kill and syncs clients; <see cref="NetSend" /> brings late joiners up to date.
/// </summary>
public sealed class DownedBossSystem : ModSystem
{
    public static bool DownedFungore;

    public static bool SeenEyeOfCthulhuWake;

    public override void ClearWorld()
    {
        DownedFungore = false;
        SeenEyeOfCthulhuWake = false;
    }

    public override void SaveWorldData(TagCompound tag)
    {
        // Absent tags read back as false, so only set flags are written.
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
