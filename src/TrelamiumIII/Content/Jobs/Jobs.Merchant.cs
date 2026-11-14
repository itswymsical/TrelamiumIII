using System.Linq;
using TrelamiumIII.Content.Items.Quest;
using TrelamiumIII.Core.Events;
using TrelamiumIII.Core.Jobs;

namespace TrelamiumIII.Content.Jobs;

/// <summary>
///     The Merchant decides copper is the market of the future. The player digs it up, smelts it, then runs back and forth
///     putting it in whatever he thinks of next, until he's holding a copper bunny and has to admit nobody will buy it.
/// </summary>
/// <remarks>
///     The bars are only shown to him, since the trials are made from them. Each trial's samples are handed over, and the
///     line that opens the next set is his review of them and his next idea.
/// </remarks>
public sealed class CopperStandard : Job
{
    /// <summary>Until the bars are made, tin is a mistake he can still catch.</summary>
    private const int SmeltSet = 1;

    public const int ArmoryTrial = 2;
    public const int HouseholdTrial = 3;
    public const int BunnyTrial = 4;

    /// <summary>Tin is all he nags about, and once a minute is plenty.</summary>
    protected override int SpeechCooldown => 60 * 60;

    public CopperStandard() : base(
        JobID.CopperStandard, "CopperStandard.Name", "CopperStandard.Description",
        taskList:
        [
            [new GatherItemTask("CopperStandard.Tasks.Set0.obj0", ItemID.CopperOre, 75, consume: false)],
            [
                new GatherItemTask("CopperStandard.Tasks.Set1.obj0", ItemID.CopperBar, 25, consume: false),
                new HandInTask("CopperStandard.Tasks.Set1.obj1", promptKey: "Merchant.CopperStandard_Set1HandIn")
            ],
            [
                new GatherItemTask("CopperStandard.Tasks.Set2.obj0", CopperArrow()),
                new GatherItemTask("CopperStandard.Tasks.Set2.obj1", ModContent.ItemType<CopperBullet>()),
                new GatherItemTask("CopperStandard.Tasks.Set2.obj2", ModContent.ItemType<CopperBoomerang>()),
                new HandInTask("CopperStandard.Tasks.Set2.obj3", promptKey: "Merchant.CopperStandard_Set2HandIn")
            ],
            [
                new GatherItemTask("CopperStandard.Tasks.Set3.obj0", ModContent.ItemType<CopperTorch>()),
                new GatherItemTask("CopperStandard.Tasks.Set3.obj1", ModContent.ItemType<CopperRope>()),
                new GatherItemTask("CopperStandard.Tasks.Set3.obj2", ModContent.ItemType<CopperHealingPotion>()),
                new HandInTask("CopperStandard.Tasks.Set3.obj3", promptKey: "Merchant.CopperStandard_Set3HandIn")
            ],
            [
                new GatherItemTask("CopperStandard.Tasks.Set4.obj0", ModContent.ItemType<CopperBunny>()),
                new HandInTask("CopperStandard.Tasks.Set4.obj1", promptKey: "Merchant.CopperStandard_Appraisal")
            ]
        ],
        rewards: [new JobReward("TrelamiumIII/StalagMagMKI")],
        providerNPC: NPCID.Merchant)
    { }

    /// <summary>
    ///     Autoloaded once per metal, so there's no class to name. None on the instance built during load, which only ever
    ///     reports its name.
    /// </summary>
    private static int CopperArrow()
        => ModContent.TryFind("TrelamiumIII/CopperTippedArrow", out ModItem arrow) ? arrow.Type : ItemID.None;

    public override JobCondition UnlockCondition => JobCondition.WorldDay(3);

<<<<<<< HEAD
    public override void OnJobStart() => Context.Play("Merchant.CopperStandard_Greeting", zoomIn: true);

    public override void OnSetAdvanced(int previousSet, int newSet) => Context.Play($"Merchant.CopperStandard_Set{newSet}");

    /// <summary>Queued behind the appraisal, whose answer is what finished the job.</summary>
    public override void OnJobComplete(bool giveRewards = true)
    {
        base.OnJobComplete(giveRewards);
        Context.Play("Merchant.CopperStandard_Complete");
    }
=======
    public override void OnJobStart()
        => Context.Play("Merchant.GoldMine_Greeting");
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb

    public override void OnEvent(GameEvent evt)
    {
        if (CurrentTaskSet <= SmeltSet
            && evt.Type is GameEventType.ItemPickup or GameEventType.ItemCraft
            && evt.ItemType is ItemID.TinOre or ItemID.TinBar)
        {
            Speak("Merchant.CopperStandard_Tin");
        }
    }

    /// <summary>
    ///     Keeps a trial's goods out of the crafting menu until the Merchant has thought of them. They stay craftable once the
    ///     job is done.
    /// </summary>
    public static Condition AskedFor(int trial) => new(
        TrelamiumIII.Instance.GetLocalization("Conditions.CopperTrial"),
        () => HasReached(trial));

    private static bool HasReached(int trial)
    {
        var job = Main.LocalPlayer.GetModPlayer<JobPlayer>().GetAllJobs().FirstOrDefault(job => job.ID == JobID.CopperStandard);

        return job?.State == JobState.Completed || job?.State == JobState.Active && job.CurrentTaskSet >= trial;
    }
}
