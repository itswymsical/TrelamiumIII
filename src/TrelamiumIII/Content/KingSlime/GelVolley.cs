namespace TrelamiumIII.Content.KingSlime;

/// <summary>
///     Launch layouts for the King Slime's gel sprays. Each shot lays about 21 trail nodes while it flies and 9 more when
///     it lands, so the volley sizes here keep the gel layer well under <see cref="NODE_CEILING" />.
/// </summary>
internal static class GelVolley
{
    /// <summary>Live gel nodes past which hostile shots stop adding more. The layer's own cap is 900 at default settings.</summary>
    public const int NODE_CEILING = 700;

    /// <summary>Launch elevation of the innermost and outermost lobs, radians above horizontal.</summary>
    private const float INNER_ELEVATION = 1.2f;

    private const float OUTER_ELEVATION = 0.9f;

    /// <summary>
    ///     Lobs <paramref name="perSide" /> shots to each side of the slime, landing at evenly spaced distances from
    ///     <paramref name="near" /> to <paramref name="far" /> pixels off its edge.
    /// </summary>
    /// <param name="phase">Shifts every landing by this fraction of the spacing, so consecutive slams can fill each other's gaps.</param>
    public static void Fan(NPC npc, int perSide, float near, float far, float phase, int damage, float knockback)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        float spacing = perSide > 1 ? (far - near) / (perSide - 1) : 0f;
        int type = ModContent.ProjectileType<GelBallProjectile>();

        for (int i = 0; i < perSide; i++)
        {
            float progress = perSide > 1 ? i / (float)(perSide - 1) : 0f;
            float elevation = MathHelper.Lerp(INNER_ELEVATION, OUTER_ELEVATION, progress);

            for (int side = -1; side <= 1; side += 2)
            {
                float reach = (near + spacing * (i + phase)) * Main.rand.NextFloat(0.95f, 1.05f);

                Vector2 velocity = GelShotProjectile.LobVelocity(reach, elevation) * new Vector2(side, 1f);
                Vector2 origin = new(npc.Center.X + side * npc.width * 0.35f, npc.Bottom.Y - 12f);

                Projectile.NewProjectile(npc.GetSource_FromAI(), origin, velocity, type, damage, knockback);
            }
        }
    }

    /// <summary>Throws <paramref name="count" /> harmless shots across the upper half circle, for the finale.</summary>
    public static void Radial(NPC npc, int count, float minSpeed, float maxSpeed)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        int type = ModContent.ProjectileType<GelBallProjectile>();

        for (int i = 0; i < count; i++)
        {
            float angle = MathHelper.Lerp(-MathHelper.Pi + 0.3f, -0.3f, (i + 0.5f) / count);
            Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(minSpeed, maxSpeed);

            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, velocity, type, 0, 0f);
        }
    }
}
