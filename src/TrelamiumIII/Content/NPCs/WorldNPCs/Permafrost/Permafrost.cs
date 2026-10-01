using TrelamiumIII.Core;
using TrelamiumIII.Core.Dialogue;
using TrelamiumIII.Core.NPCs;

namespace TrelamiumIII.Content.NPCs.WorldNPCs.Permafrost;

public class PermafrostNPC : WorldNPC, INarrativeSpeaker
{
    public override string Texture => AssetReferences.Content.NPCs.WorldNPCs.Permafrost.PermafrostNPC.KEY;

    public override bool CanDespawn => false;

    protected override void SetWorldNPCDefaults()
    {
        NPC.HitSound = SoundID.NPCHit1;
        NPC.DeathSound = SoundID.PlayerKilled;
        NPC.width = 130;
        NPC.height = 410;
    }

    public ChatEncounter GetEncounter(Player player)
        => ChatEncounter.Play("Permafrost.Greeting").Projected();
}
