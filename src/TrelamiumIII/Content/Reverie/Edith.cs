namespace TrelamiumIII.Content.Reverie;

/// <summary>
/// Reverie's librarian, placed at the front desk when the realm generates. Stationary, invulnerable and never despawns;
/// talking to her only greets for now, until book checkout exists.
/// </summary>
public sealed class Edith : ModNPC
{
    private static readonly string[] ChatKeys = ["Welcome", "Shelves", "Checkout"];

    public override string Texture => $"{nameof(TrelamiumIII)}/Content/Ingress/NPCs/Edith/Edith";

    public override void SetStaticDefaults()
    {
        Main.npcFrameCount[Type] = 1;

        NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, new NPCID.Sets.NPCBestiaryDrawModifiers { Hide = true });

        foreach (string key in ChatKeys)
            _ = this.GetLocalization($"Chat.{key}");
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

    public override bool CanChat() => true;

    public override string GetChat() => this.GetLocalization($"Chat.{Main.rand.Next(ChatKeys)}").Value;

    public override void AI()
    {
        NPC.velocity.X = 0f;

        Player nearest = Main.player[Player.FindClosest(NPC.position, NPC.width, NPC.height)];

        if (nearest.active)
            NPC.spriteDirection = NPC.direction = nearest.Center.X < NPC.Center.X ? -1 : 1;
    }
}
