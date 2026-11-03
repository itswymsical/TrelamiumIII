namespace TrelamiumIII.Core.Residues.Bodies;

/// <summary>
///     A hardcoded fire on the local player's lower half, to prove <see cref="BodyCanvas" /> draws: it deals no damage and
///     nothing lights it but <c>/bodyfire</c>. Goes away once bodies carry real residues.
/// </summary>
internal static class BodyFireTest
{
    private const float GROWTH_PER_TICK = 0.02f;

    /// <summary>Pixels below the body's centre where the fire starts, which the kernel softens up to about the waist.</summary>
    private const float FIRE_TOP = 5f;

    /// <summary>Pixels below the body's centre where it's at its thickest, level with the feet.</summary>
    private const float FIRE_BOTTOM = 26f;

    /// <summary>What's left at the top, of the fuel and heat it has at the feet. So it's yellow and solid low down, and breaks up into red tongues higher.</summary>
    private const float TOP_FUEL = 0.2f;

    private const float TOP_HEAT = 0.4f;

    private static bool lit;

    /// <summary>0 to 1, so it grows in and dies down rather than popping.</summary>
    public static float Strength { get; private set; }

    public static bool IsBurning => Strength > 0f;

    public static bool Toggle() => lit = !lit;

    public static void Stop()
    {
        lit = false;
        Strength = 0f;
    }

    public static float CoverageAt(Vector2 fromCentre)
        => fromCentre.Y >= FIRE_TOP ? MathHelper.Lerp(TOP_FUEL, 1f, Depth(fromCentre)) * Strength : 0f;

    public static float HeatAt(Vector2 fromCentre) => MathHelper.Lerp(TOP_HEAT, 1f, Depth(fromCentre)) * Strength;

    private static float Depth(Vector2 fromCentre)
        => MathHelper.Clamp((fromCentre.Y - FIRE_TOP) / (FIRE_BOTTOM - FIRE_TOP), 0f, 1f);

    /// <summary>
    ///     The ground's look is scaled for fire many tiles wide. A body is three tall, so its flames are finer, taller and
    ///     faster. Strength and threshold are both high so that fuel decides where fire shows: this noise runs dark, and with
    ///     less the whole shape fills in up to a flat line where the stamps end.
    /// </summary>
    public static void ApplyLook(Effect effect, Player player)
    {
        effect.Parameters["FireThreshold"]?.SetValue(1.9f);
        effect.Parameters["FireNoiseScale"]?.SetValue(0.55f);
        effect.Parameters["FireStretch"]?.SetValue(3f);
        effect.Parameters["FireNoiseStrength"]?.SetValue(5f);
        effect.Parameters["FireDrift"]?.SetValue(new Vector2(0.02f, 0.7f * player.gravDir));
        effect.Parameters["FireEdgeWidth"]?.SetValue(0.5f);
        effect.Parameters["FireBodyAlpha"]?.SetValue(0.92f);
        effect.Parameters["FireEdgeAlpha"]?.SetValue(1f);
    }

    public static void Update()
    {
        if (Main.dedServ || Main.gameMenu)
            return;

        Strength = MathHelper.Clamp(Strength + (lit ? GROWTH_PER_TICK : -GROWTH_PER_TICK), 0f, 1f);

        if (!IsBurning)
            return;

        Player player = Main.LocalPlayer;
        Vector2 flames = player.Center + new Vector2(0f, player.height * 0.25f * player.gravDir);

        Lighting.AddLight(flames, 0.42f * Strength, 0.17f * Strength, 0.03f * Strength);

        if (!Main.rand.NextBool(5))
            return;

        Dust ember = Dust.NewDustPerfect(
            flames + new Vector2(Main.rand.NextFloat(-player.width * 0.5f, player.width * 0.5f), Main.rand.NextFloat(-6f, player.height * 0.25f) * player.gravDir),
            DustID.Torch,
            player.velocity * 0.4f + new Vector2(Main.rand.NextFloat(-0.25f, 0.25f), Main.rand.NextFloat(-1.8f, -0.6f) * player.gravDir),
            0,
            default,
            Main.rand.NextFloat(0.8f, 1.3f));

        ember.noGravity = true;
    }
}
