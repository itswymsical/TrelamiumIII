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
        var mp = Player.GetModPlayer<JobPlayer>();
        var slimeJob = mp.GetOrCreateJob(JobID.CuriousSlime1);

        if (mp.UnlockedJobs().Contains(slimeJob))
            return;

        int slimeIndex = NPC.FindFirstNPC(ModContent.NPCType<CuriousSlime>());

        if (slimeIndex != -1)
        {
            NPC slime = Main.npc[slimeIndex];

            if (Player.Distance(slime.Center) < 128f &&
                !mp.UnlockedJobs().Contains(slimeJob))
            {
                mp.UnlockJob(JobID.CuriousSlime1);
            }
        }
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
