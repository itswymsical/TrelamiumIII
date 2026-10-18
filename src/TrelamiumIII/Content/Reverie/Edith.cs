using TrelamiumIII.Content.Reverie.Library;
using TrelamiumIII.Core.Dialogue;

namespace TrelamiumIII.Content.Reverie;

/// <summary>
/// Reverie's librarian, placed at the front desk when the realm generates. Stationary, invulnerable and never despawns.
/// Talking to her opens a projected dialogue whose choices lead to the <see cref="LibraryCatalogPanel"/> for a shelf,
/// or return borrowed books.
/// </summary>
public sealed class Edith : ModNPC, INarrativeSpeaker
{
    public override string Texture => $"{nameof(TrelamiumIII)}/Content/Ingress/NPCs/Edith/Edith";

    public override void SetStaticDefaults()
    {
        Main.npcFrameCount[Type] = 1;

        NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, new NPCID.Sets.NPCBestiaryDrawModifiers { Hide = true });
    }

    public override void SetDefaults()
    {
        NPC.width = 36;
        NPC.height = 84;
        NPC.aiStyle = -1;
        NPC.damage = 0;
        NPC.defense = 0;
        NPC.lifeMax = 250;
        NPC.friendly = true;
        NPC.dontTakeDamage = true;
        NPC.dontTakeDamageFromHostiles = true;
        NPC.knockBackResist = 0f;
        NPC.HitSound = SoundID.NPCHit1;

        DrawOffsetY = -4f;
    }

    public override bool CheckActive() => false;

    /// <summary> One frame: anything else would sample past the sheet. </summary>
    public override void FindFrame(int frameHeight) => NPC.frame.Y = 0;

    public override bool CanChat() => true;

    public ChatEncounter GetEncounter(Player player) =>
        ChatEncounter.Play(player.GetModPlayer<LibraryPlayer>().Loans.Count > 0 ? "Edith.GreetingLoans" : "Edith.Greeting");

    public override void AI()
    {
        NPC.velocity.X = 0f;

        Player nearest = Main.player[Player.FindClosest(NPC.position, NPC.width, NPC.height)];

        if (nearest.active)
            NPC.spriteDirection = NPC.direction = nearest.Center.X < NPC.Center.X ? -1 : 1;
    }
}
