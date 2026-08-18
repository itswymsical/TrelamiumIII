using System.Collections.Generic;
using Terraria.ModLoader.IO;

namespace  TrelamiumIII.Core.NPCs.Actors;

/// <summary>
/// Saves and restores <see cref="WorldNPC"/>. Can be disabled by setting <see cref="WorldNPC.Persistent"/> to false.
/// </summary>
public class WorldNPCPersistenceSystem : ModSystem
{
    private const string SaveKey = "persistentWorldNPCs";

    public override void SaveWorldData(TagCompound tag)
    {
        var entries = new List<TagCompound>();

        for (int i = 0; i < Main.maxNPCs; i++)
        {
            NPC npc = Main.npc[i];

            if (!npc.active || npc.ModNPC is not WorldNPC worldNpc)
                continue;

            if (!worldNpc.Persistent)
                continue;

            var entry = new TagCompound
            {
                ["mod"] = worldNpc.Mod.Name,
                ["name"] = worldNpc.Name,
                ["x"] = npc.position.X,
                ["y"] = npc.position.Y,
            };

            // Let the NPC save its own state (anchor tile, etc.).
            worldNpc.SaveData(entry);

            entries.Add(entry);
        }

        tag[SaveKey] = entries;
    }

    public override void LoadWorldData(TagCompound tag)
    {
        if (!tag.ContainsKey(SaveKey))
            return;

        var entries = tag.GetList<TagCompound>(SaveKey);

        foreach (var entry in entries)
        {
            string modName = entry.GetString("mod");
            string npcName = entry.GetString("name");
            float x = entry.Get<float>("x");
            float y = entry.Get<float>("y");

            // Resolve the NPC type from the mod+name identity.
            if (!ModLoader.TryGetMod(modName, out Mod mod))
                continue;

            if (!mod.TryFind<ModNPC>(npcName, out ModNPC template))
                continue;

            int idx = NPC.NewNPC(
                NPC.GetSource_None(),
                (int)x,
                (int)y,
                template.Type
            );

            if (idx >= Main.maxNPCs)
                continue;

            // Restore anchor tile and any derived-class state.
            if (Main.npc[idx].ModNPC is WorldNPC spawned)
                spawned.LoadData(entry);
        }
    }
}