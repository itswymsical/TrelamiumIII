using TrelamiumIII.Core;
using TrelamiumIII.Core.Dialogue;
using TrelamiumIII.Core.NPCs;

namespace TrelamiumIII.Content.NPCs.Sophie;

[AutoloadHead]
public class SophieNPC : WorldNPC, INarrativeSpeaker
{
    public override string Texture => AssetReferences.Content.NPCs.Sophie.Sophie_NPCs.KEY;

    public override bool CanDespawn => false;

    protected override void SetWorldNPCDefaults()
    {
        NPC.HitSound = SoundID.NPCHit1;
        NPC.DeathSound = SoundID.PlayerKilled;
    }

    public ChatEncounter GetEncounter(Player player)
        => ChatEncounter.Play("Sophie.Greeting").Adaptive();
}
