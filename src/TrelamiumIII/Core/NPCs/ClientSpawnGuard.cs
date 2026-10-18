namespace TrelamiumIII.Core.NPCs;

/// <summary>Removes NPCs spawned locally on a multiplayer client, which the server never syncs.</summary>
internal static class ClientSpawnGuard
{
    /// <summary>
    ///     Call first in OnSpawn. OnSpawn only runs on a multiplayer client for its own NPC.NewNPC call (DragonLens's spawner
    ///     does this beside its server request); synced NPCs never run it there.
    /// </summary>
    /// <returns>True if <paramref name="npc" /> was a client-only copy and has been deactivated.</returns>
    public static bool Reject(NPC npc)
    {
        if (Main.netMode != NetmodeID.MultiplayerClient)
            return false;

        npc.active = false;
        return true;
    }
}
