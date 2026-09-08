using TrelamiumIII.Core;
using TrelamiumIII.Core.Dialogue;
using TrelamiumIII.Core.NPCs.Actors;

namespace TrelamiumIII.Content.NPCs.HugeKraken;

[AutoloadHead]
public class KrakenNPC : WorldNPC, INarrativeSpeaker
{
    protected override bool UsePlayerRenderer => false;
    public override string Texture => AssetReferences.Content.NPCs.HugeKraken.Kraken.KEY;
    public override bool CanDespawn => false;

    protected override void SetWorldNPCDefaults()
    {
        NPC.HitSound = SoundID.NPCHit1;
        NPC.DeathSound = SoundID.PlayerKilled;
    }

    protected override void SetAppearanceDefaults() { }

    /// <summary>
    /// Purely narrative: there is no shop and no flavour text behind it, so every
    /// conversation is the dialogue box rather than the vanilla chat window.
    /// </summary>
    public ChatEncounter GetEncounter(Player player)
        => ChatEncounter.Play("Kraken.Greeting");
}
