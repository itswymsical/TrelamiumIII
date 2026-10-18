using System.Collections.Generic;
using System.Linq;
using Terraria.ModLoader.IO;

namespace TrelamiumIII.Core.NPCs;

/// <summary>
/// Which <see cref="WorldNPC.HiddenOnMapUntilMet"/> NPCs this player has found, per world. Being within
/// <see cref="REVEAL_DISTANCE"/> of one, or talking to it, puts it on their map for good.
/// </summary>
/// <remarks>
/// Kept per player and keyed by the world's unique id, since the same character stands somewhere else in
/// another world. Client side: only the local player's set is read, by <see cref="WorldNPCMapSystem"/>.
/// </remarks>
public sealed class WorldNPCDiscovery : ModPlayer
{
    private const float REVEAL_DISTANCE = 25f * 16f;

    private const int CHECK_INTERVAL = 10;

    /// <summary>Entries are <c>worldId/Mod/NPCName</c>.</summary>
    private readonly HashSet<string> found = [];

    public static bool IsRevealed(WorldNPC worldNPC) =>
        !worldNPC.HiddenOnMapUntilMet || Main.LocalPlayer.GetModPlayer<WorldNPCDiscovery>().found.Contains(Key(worldNPC));

    public void Reveal(WorldNPC worldNPC) => found.Add(Key(worldNPC));

    /// <summary>Forgets every NPC of <paramref name="npcType"/> in the current world, for dev commands.</summary>
    public void Forget(int npcType)
    {
        if (NPCLoader.GetNPC(npcType) is WorldNPC worldNPC)
            found.Remove(Key(worldNPC));
    }

    private static string Key(WorldNPC worldNPC) => $"{Main.ActiveWorldFileData?.UniqueId}/{worldNPC.FullName}";

    public override void PostUpdate()
    {
        if (Player.whoAmI != Main.myPlayer || Main.dedServ || Main.GameUpdateCount % CHECK_INTERVAL != 0)
            return;

        foreach (NPC npc in Main.ActiveNPCs)
        {
            if (npc.ModNPC is not WorldNPC { HiddenOnMapUntilMet: true } worldNPC)
                continue;

            if (Player.talkNPC == npc.whoAmI || Player.DistanceSQ(npc.Center) <= REVEAL_DISTANCE * REVEAL_DISTANCE)
                Reveal(worldNPC);
        }
    }

    public override void SaveData(TagCompound tag)
    {
        if (found.Count > 0)
            tag["found"] = found.ToList();
    }

    public override void LoadData(TagCompound tag)
    {
        found.Clear();
        found.UnionWith(tag.GetList<string>("found"));
    }
}
