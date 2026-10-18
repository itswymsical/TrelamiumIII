using TrelamiumIII.Content.NPCs.WorldNPCs.Argie;
using TrelamiumIII.Core.Graphics.Particles;
using TrelamiumIII.Core.NPCs.Housing;

namespace TrelamiumIII.Content.Items.Quest;

/// <summary>
///     The scent trail <see cref="SilviansLetter" /> shows while held. Points at Argie's home anchor (see <see cref="WorldNPCHousing"/>). client-side local, and visual only.
/// </summary>
public static class LetterScent
{
    private const float MAX_LENGTH = 32f * 16f;

    private const float ARRIVE_DISTANCE = 6f * 16f;

    private const float SWAY = 22f;

    private const int MOTES_PER_TICK = 3;

    private const int LIGHT_POINTS = 6;

    private static readonly Color SporeColor = new(110, 170, 255);

    public static bool TryFindTarget(out Vector2 target)
    {
        if (WorldNPCHousing.TryGetHome(ModContent.NPCType<ArgieNPC>(), out NPCHome? home))
        {
            target = home!.Anchor.ToWorldCoordinates();

            return true;
        }

        int index = NPC.FindFirstNPC(ModContent.NPCType<ArgieNPC>());
        target = index >= 0 ? Main.npc[index].Center : default;

        return index >= 0;
    }

    public static void Emit(Player player)
    {
        if (Main.dedServ || player.whoAmI != Main.myPlayer || !TryFindTarget(out Vector2 target))
            return;

        Vector2 start = player.Center + new Vector2(player.direction * 10f, -6f);
        Vector2 toTarget = target - start;
        float distance = toTarget.Length();

        if (distance < ARRIVE_DISTANCE)
            return;

        Vector2 direction = toTarget / distance;
        Vector2 across = direction.RotatedBy(MathHelper.PiOver2);
        float length = Math.Min(distance - ARRIVE_DISTANCE * 0.5f, MAX_LENGTH);
        float time = Main.GlobalTimeWrappedHourly;

        for (int i = 0; i < MOTES_PER_TICK; i++)
        {
            // Biased toward the near end, so the trail is dense at the hand and thins out ahead.
            float along = MathF.Pow(Main.rand.NextFloat(), 1.25f);
            Vector2 position = PointOn(start, direction, across, length, along, time);
            Vector2 ahead = PointOn(start, direction, across, length, Math.Min(along + 0.02f, 1f), time);

            ParticleSystem.Spawn<ScentMoteParticle>(
                position + Main.rand.NextVector2Circular(4f, 4f),
                (ahead - position).SafeNormalize(direction) * Main.rand.NextFloat(0.8f, 1.8f),
                timeLeft: Main.rand.Next(50, 85),
                scale: Main.rand.NextFloat(0.8f, 1.25f) * (1f - along * 0.4f),
                rotation: Main.rand.NextFloat(MathHelper.TwoPi));
        }

        for (int i = 0; i < LIGHT_POINTS; i++)
            Lighting.AddLight(PointOn(start, direction, across, length, i / (float)LIGHT_POINTS, time), SporeColor.ToVector3() * (0.3f * (1f - i / (float)LIGHT_POINTS)));
    }

    /// <summary>A point <paramref name="along" /> (0 to 1) the swaying line, which widens and drifts with time.</summary>
    private static Vector2 PointOn(Vector2 start, Vector2 direction, Vector2 across, float length, float along, float time)
    {
        float sway = MathF.Sin(along * 9f - time * 2.2f) * SWAY * along + MathF.Sin(along * 3.1f + time * 0.9f) * SWAY * 0.5f * along;

        return start + direction * (along * length) + across * sway;
    }
}
