using System.Linq;
using TrelamiumIII.Common.Systems;
using TrelamiumIII.Content.Jobs;
using TrelamiumIII.Core.Jobs;

namespace TrelamiumIII.Common.Players;

public class TrelamiumPlayer : ModPlayer
{

    public static float ShieldOpacity { get; private set; }

    private const float ShieldFadeSpeed = 0.04f;

    public override void PostUpdateMiscEffects()
    {
        // Put all the job checks here -- yeah, i know it's ass, i do not care.
        JobChecks_PreHM();
    }

    private void JobChecks_PreHM()
    {
        var mp = Player.GetModPlayer<JobPlayer>();
        var slimeJob = mp.GetOrCreateJob(JobID.CuriousSlime1);

        // Definition check: must still be locked
        if (slimeJob.Status != JobStatus.Locked)
            return;

        // Runtime check: must be inactive
        if (slimeJob.Progress != JobProgress.Inactive)
            return;

        int slimeIndex = NPC.FindFirstNPC(ModContent.NPCType<CuriousSlime>());

        if (slimeIndex == -1)
            return;

        NPC slime = Main.npc[slimeIndex];

        if (Player.Distance(slime.Center) >= 128f)
            return;

        mp.UnlockJob(JobID.CuriousSlime1);


    }

    public override void PreUpdate()
    {
        int tileX = (int)(Player.position.X / 16);
        int tileY = (int)(Player.position.Y / 16);

        if (StructureProtectionSystem.IsProtected(tileX, tileY))
            Player.noBuilding = true;

        // Fade in when holding a pickaxe, fade out when not
        bool holding = Player.HeldItem.pick > 0 || Player.HeldItem.hammer > 0;

        if (holding)
            ShieldOpacity = MathHelper.Clamp(ShieldOpacity + ShieldFadeSpeed, 0f, 1f);
        else
            ShieldOpacity = MathHelper.Clamp(ShieldOpacity - ShieldFadeSpeed, 0f, 1f);
    }

    //public override IEnumerable<Item> AddStartingItems(bool mediumCoreDeath)
    //{
    //    return [new(ModContent.ItemType<StartBagItem>())];
    //}
}
