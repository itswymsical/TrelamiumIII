using System.Collections.Generic;
using System.Linq;
using TrelamiumIII.Common.Systems;
using TrelamiumIII.Content.Alchemy;
using TrelamiumIII.Content.Alchemy.Reagents;
using TrelamiumIII.Content.Items.FarmingOverhaul.Seedbeds;
using TrelamiumIII.Content.NPCs.WorldNPCs.Silvian;
using TrelamiumIII.Core.Dialogue;
using TrelamiumIII.Core.Events;
using TrelamiumIII.Core.Jobs;

namespace TrelamiumIII.Content.Jobs;

/// <summary>Silvian's first job: rebuild his trampled seed beds, with magnolias and salt. Ends on the botany offer.</summary>
public sealed class HerbalForager : Job
{
    public HerbalForager() : base(
        JobID.HerbalForager, "HerbalForager.Name", "HerbalForager.Description",
        taskList:
        [
            [
                new GatherAnyItemTask("HerbalForager.Tasks.Set1.obj0", SeedBedItemTypes, 6),
                new GatherItemTask("HerbalForager.Tasks.Set1.obj1", ModContent.ItemType<MagnoliaItem>(), 6),
                new GatherItemTask("HerbalForager.Tasks.Set1.obj2", SoluteID.RockSalt, 6),
                new HandInTask("HerbalForager.Tasks.Set1.obj3", promptKey: "Silvian.HerbalForager_HandIn")
            ]
        ],
        rewards:
        [
            new JobReward(ItemID.HerbBag),
            new JobReward("TrelamiumIII/SeedBedItem_Dirt", 3),
            new JobReward("TrelamiumIII/BotanistGlove")
        ],
        providerNPC: ModContent.NPCType<Silvian>())
    { }

    private static IReadOnlyList<int> SeedBedItemTypes()
        => ModContent.GetContent<ModItem>().OfType<SeedBedItem>().Select(item => item.Type).ToArray();

    public override void OnJobStart() => Context.Play("Silvian.HerbalForager_Greeting", zoomIn: true, style: DialogueStyle.Projected);

    /// <summary>The closing line asks whether the player wants botany lessons, which unlocks <see cref="BotanyBasics" />.</summary>
    public override void OnJobComplete(bool giveRewards = true)
    {
        base.OnJobComplete(giveRewards);
        Context.Play("Silvian.HerbalForager_Complete", style: DialogueStyle.Projected);
    }
}

/// <summary>
///     Silvian's botany lessons: seed beds, then soil preference, then a bloom. Talking to him between stages plays the
///     lesson; the almanac handoff follows completion (<see cref="SilvianAlmanac.HandoffJob" />).
/// </summary>
public sealed class BotanyBasics : Job
{
    private const int SoilLessonSet = 1;
    private const int WrapUpSet = 3;

    public BotanyBasics() : base(
        JobID.BotanyBasics, "BotanyBasics.Name", "BotanyBasics.Description",
        taskList:
        [
            [
                new PlaceSeedBedTask("BotanyBasics.Tasks.Set0.obj0", 3),
                new PlantInSeedBedTask("BotanyBasics.Tasks.Set0.obj1", 3)
            ],
            [new TalkToNPCTask("BotanyBasics.Tasks.Set1.obj0", ModContent.NPCType<Silvian>())],
            [
                new PlantInSeedBedTask("BotanyBasics.Tasks.Set2.obj0", 2, favouredSoilOnly: true),
                new BreakTileTask("BotanyBasics.Tasks.Set2.obj1", TileID.BloomingHerbs)
            ],
            [new TalkToNPCTask("BotanyBasics.Tasks.Set3.obj0", ModContent.NPCType<Silvian>())]
        ],
        rewards:
        [
            new JobReward(ItemID.SilverCoin, 50),
            new JobReward("TrelamiumIII/MagnoliaSeedsItem", 5)
        ],
        providerNPC: ModContent.NPCType<Silvian>())
    { }

    public override void OnJobStart() => Context.Play("Silvian.BotanyBasics_Greeting", zoomIn: true, style: DialogueStyle.Projected);

    public override ChatEncounter GetChatEncounter(NPC npc, Player player) => CurrentTaskSet switch
    {
        SoilLessonSet => ChatEncounter.Play("Silvian.BotanyBasics_Soil").Projected(),
        WrapUpSet => ChatEncounter.Play("Silvian.BotanyBasics_WrapUp").Projected(),
        _ => ChatEncounter.None
    };

    /// <summary>Queued behind the wrap-up line, which the same chat opened.</summary>
    public override void OnJobComplete(bool giveRewards = true)
    {
        base.OnJobComplete(giveRewards);
        SilvianAlmanac.OfferIfDue(Context);
    }
}

/// <summary>Place <paramref name="count" /> seed beds of any soil.</summary>
public sealed class PlaceSeedBedTask(string key, int count) : JobTask(key, count)
{
    public override bool Matches(GameEvent evt)
        => evt.Type == GameEventType.TilePlace && evt.TileType is { } type && FarmingSystem.SeedBedTypes.ContainsKey(type);

    public override int CreditFor(GameEvent evt) => 1;
}

/// <summary>
///     Plant herb seeds on seed beds. With <paramref name="favouredSoilOnly" />, only on a bed whose
///     <see cref="SeedBedTile.PreferredHerbs" /> include that herb.
/// </summary>
public sealed class PlantInSeedBedTask(string key, int count, bool favouredSoilOnly = false) : JobTask(key, count)
{
    public override bool Matches(GameEvent evt)
    {
        if (evt.Type != GameEventType.TilePlace || evt.TileType != TileID.ImmatureHerbs || evt.TileX is not { } i || evt.TileY is not { } j)
            return false;

        Tile below = Framing.GetTileSafely(i, j + 1);

        if (!below.HasTile || !FarmingSystem.SeedBedTypes.TryGetValue(below.TileType, out SeedBedTile? bed))
            return false;

        // Herb tiles store their style in frameX, 18 px per herb, in HerbType order.
        var herb = (HerbType)(Framing.GetTileSafely(i, j).TileFrameX / 18);

        return !favouredSoilOnly || bed.PreferredHerbs.Contains(herb);
    }

    public override int CreditFor(GameEvent evt) => 1;
}