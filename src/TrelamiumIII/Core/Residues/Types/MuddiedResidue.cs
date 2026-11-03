using TrelamiumIII.Common.ChemicalClass;

namespace TrelamiumIII.Core.Residues.Types;

/// <summary>A coat of mud over the terrain. Drags whatever walks through it and leaves NPCs <see cref="Mired" />.</summary>
public sealed class MuddiedResidue : FilmResidue
{
    private const float PLAYER_DRAG = 0.92f;

    private const float NPC_DRAG = 0.9f;

    private const int MIRED_TICKS = 45;

    protected override FilmLook Look { get; } = new()
    {
        Threshold = 0.22f,
        NoiseScale = 0.07f,
        NoiseStrength = 0.16f,
        NoiseDrift = new Vector2(0.004f, 0.012f),
        Bands = 4f,
        ShadowColor = new Vector3(0.2f, 0.13f, 0.07f),
        BodyColor = new Vector3(0.32f, 0.21f, 0.12f),
        CoreColor = new Vector3(0.42f, 0.29f, 0.17f),
        BodyAlpha = 0.88f,
        OutlineColor = new Vector3(0.13f, 0.09f, 0.05f),
        OutlineAlpha = 0.92f,
        HighlightColor = new Vector3(0.55f, 0.43f, 0.3f),
        HighlightAlpha = 0.6f,
        SurfaceLip = 2f,
    };

    public override float Duration => 10f * 60f;

    public override int MaxTiles => 900;

    public override int FadeInTicks => 6;

    public override int FadeOutTicks => 18;

    public override void OnPlayerContact(ref ResidueInstance instance, Player player)
    {
        if (instance.IsOwnedBy(player) || !FirstContact(player))
            return;

        player.velocity.X *= MathHelper.Lerp(1f, PLAYER_DRAG, instance.Strength);

        if (!Main.dedServ && player.velocity.X != 0f && Main.rand.NextBool(12))
            MudDust(player.Bottom);
    }

    /// <summary>Drags on every machine; the debuff comes from the server, which syncs it.</summary>
    public override void OnNPCContact(ref ResidueInstance instance, NPC npc)
    {
        if (npc.noTileCollide || npc.friendly || !FirstContact(npc))
            return;

        npc.velocity.X *= MathHelper.Lerp(1f, NPC_DRAG, instance.Strength);

        if (Main.netMode != NetmodeID.MultiplayerClient)
            ChemicalControlGlobalNPC.Refresh<Mired>(npc, MIRED_TICKS);
    }

    public override void OnLiquidContact(ref ResidueInstance instance, int liquidType, int amount)
    {
        // Lava bakes it off; water washes it away.
        if (liquidType == LiquidID.Lava)
        {
            instance.Life = 0f;
            return;
        }

        instance.Life -= 8f * (amount / 255f);
    }

    private static void MudDust(Vector2 position)
    {
        Dust dust = Dust.NewDustPerfect(
            position + Main.rand.NextVector2Circular(6f, 2f),
            DustID.Mud,
            new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(0.4f, 1.2f)),
            0,
            default,
            Main.rand.NextFloat(0.7f, 1.1f));

        dust.noGravity = false;
    }
}
