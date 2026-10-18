using System.Collections.Generic;
using Terraria.GameContent.ItemDropRules;
using Terraria.Localization;

namespace TrelamiumIII.Content.Items.Quest;

/// <summary>The glowing mushroom biome's creatures carry <see cref="SporeSample" />s, for Argie's survey.</summary>
public sealed class SporeSampleDrops : GlobalNPC
{
    private static readonly HashSet<int> Carriers =
    [
        NPCID.ZombieMushroom,
        NPCID.ZombieMushroomHat,
        NPCID.AnomuraFungus,
        NPCID.MushiLadybug,
        NPCID.FungiBulb,
        NPCID.GiantFungiBulb,
        NPCID.FungoFish,
        NPCID.SporeBat,
        NPCID.SporeSkeleton
    ];

    public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => Carriers.Contains(entity.type);

    public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
        => npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<SporeSample>(), 2, 1, 2));
}

/// <summary>Drops only for a kill made by a player standing in the glowing mushroom biome.</summary>
public sealed class InGlowingMushroomBiome : IItemDropRuleCondition
{
    public bool CanDrop(DropAttemptInfo info) => info.player?.ZoneGlowshroom == true;

    public bool CanShowItemDropInUI() => true;

    public string GetConditionDescription() => Language.GetTextValue("Mods.TrelamiumIII.Conditions.InGlowingMushroomBiome");
}
