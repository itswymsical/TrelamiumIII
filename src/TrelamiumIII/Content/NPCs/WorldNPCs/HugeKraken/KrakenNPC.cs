using TrelamiumIII.Core;
using TrelamiumIII.Core.Dialogue;
using TrelamiumIII.Core.NPCs;

namespace TrelamiumIII.Content.NPCs.WorldNPCs.HugeKraken;

[AutoloadHead]
public class KrakenNPC : WorldNPC, INarrativeSpeaker
{
    public override string Texture => AssetReferences.Content.NPCs.WorldNPCs.HugeKraken.Kraken.KEY;

    public override bool CanDespawn => false;

    protected override void SetWorldNPCDefaults()
    {
        NPC.HitSound = SoundID.NPCHit1;
        NPC.DeathSound = SoundID.PlayerKilled;
    }

    public ChatEncounter GetEncounter(Player player)
        => ChatEncounter.Play("Kraken.Greeting");
}