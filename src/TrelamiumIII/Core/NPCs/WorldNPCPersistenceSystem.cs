namespace TrelamiumIII.Core.NPCs;

/// <summary>
/// Applies <see cref="WorldNPC.Persistent"/>. tModLoader saves and restores every town NPC, so every WorldNPC is
/// written with the world; this removes the non-persistent ones once they have been restored.
/// </summary>
public class WorldNPCPersistenceSystem : ModSystem
{
    // Runs after tModLoader has restored modded NPCs and loaded system data (housing reservations included).
    public override void PostWorldLoad()
    {
        foreach (NPC npc in Main.ActiveNPCs)
        {
            if (npc.ModNPC is WorldNPC { Persistent: false, HasActiveJob: false, HasHome: false } worldNPC)
                worldNPC.Despawn();
        }
    }
}
