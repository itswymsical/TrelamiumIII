using Terraria.GameContent.ItemDropRules;

namespace TrelamiumIII.Common.Systems;

public class GlobalLootSystem : GlobalNPC
{
    public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
    {
        base.ModifyNPCLoot(npc, npcLoot);

        if (npc.type == NPCID.Zombie)
            npcLoot.Add(ItemDropRule.Common(ItemID.Leather, 3, 1, 3));
        
    }
}
