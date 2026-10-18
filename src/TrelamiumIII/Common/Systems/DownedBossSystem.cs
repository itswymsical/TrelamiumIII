using System.IO;
using Terraria.ModLoader.IO;

namespace TrelamiumIII.Common.Systems;

/// <summary>
///     Per-world progression flags. Set them through <see cref="NPC.SetEventFlagCleared" />, which announces the
///     kill and syncs clients; <see cref="NetSend" /> brings late joiners up to date.
/// </summary>
public sealed class DownedBossSystem : ModSystem, Subworlds.ISharedWorldData
{
    public static bool DownedFungore;

    public static bool DownedWoodenWarden;

    public static bool SeenEyeOfCthulhuWake;

    public override void ClearWorld()
    {
        DownedFungore = false;
        DownedWoodenWarden = false;
        SeenEyeOfCthulhuWake = false;
    }

    public override void SaveWorldData(TagCompound tag)
    {
        // Absent tags read back as false, so only set flags are written.
        if (DownedFungore)
            tag[nameof(DownedFungore)] = true;

        if (DownedWoodenWarden)
            tag[nameof(DownedWoodenWarden)] = true;

        if (SeenEyeOfCthulhuWake)
            tag[nameof(SeenEyeOfCthulhuWake)] = true;
    }

    public override void LoadWorldData(TagCompound tag)
    {
        DownedFungore = tag.ContainsKey(nameof(DownedFungore));
        DownedWoodenWarden = tag.ContainsKey(nameof(DownedWoodenWarden));
        SeenEyeOfCthulhuWake = tag.ContainsKey(nameof(SeenEyeOfCthulhuWake));
    }

    public void MergeWorldData(TagCompound tag)
    {
        DownedFungore |= tag.ContainsKey(nameof(DownedFungore));
        DownedWoodenWarden |= tag.ContainsKey(nameof(DownedWoodenWarden));
        SeenEyeOfCthulhuWake |= tag.ContainsKey(nameof(SeenEyeOfCthulhuWake));
    }

    public override void NetSend(BinaryWriter writer)
    {
        writer.Write(DownedFungore);
        writer.Write(DownedWoodenWarden);
        writer.Write(SeenEyeOfCthulhuWake);
    }

    public override void NetReceive(BinaryReader reader)
    {
        DownedFungore = reader.ReadBoolean();
        DownedWoodenWarden = reader.ReadBoolean();
        SeenEyeOfCthulhuWake = reader.ReadBoolean();
    }
}
