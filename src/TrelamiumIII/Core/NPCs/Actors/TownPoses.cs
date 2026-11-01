using TrelamiumIII.Core.NPCs.Behaviors;

namespace TrelamiumIII.Core.NPCs.Actors;

/// <summary>
/// Poses for a <see cref="TerrarianModel"/> following vanilla's town AI (aiStyle 7), whose own frames say these
/// things in sprite sheets a player model doesn't have.
///
/// <para>
/// Walking, jumping and falling need nothing: <see cref="PlayerPuppet.SyncTransform"/> copies the NPC's velocity and
/// <see cref="Player.PlayerFrame"/> animates from it. What's left is sitting, the rock-paper-scissors arm and the attack
/// states, read from <c>ai[]</c> the way vanilla writes them.
/// </para>
/// </summary>
public static class TownPoses
{
    /// <summary>Vanilla's <c>ai[0]</c> values for the states posed here.</summary>
    private const int Sitting = 5;
    private const int Throwing = 10;
    private const int Shooting = 12;
    private const int Casting = 14;
    private const int Swinging = 15;
    private const int GameLead = 16;
    private const int GameListen = 17;

    /// <summary>Ticks in one rock-paper-scissors match. Vanilla's games last a whole number of these.</summary>
    public const int RockPaperScissorsCycle = 420;

    /// <summary>
    /// Front arm during rock-paper-scissors: pumped for 40 ticks before each of the three reveals at 40, 100 and 160,
    /// held out for 20 after. <paramref name="beat"/> is the tick within the match. Null once the rounds are over.
    /// </summary>
    public static float? RockPaperScissorsArm(int beat)
    {
        if (beat >= 180)
            return null;

        int round = beat % 60;
        float lift = round < 40 ? 0.45f + 0.4f * MathF.Abs(MathF.Sin(round * MathHelper.Pi / 10f)) : 0.15f;

        return -(MathHelper.PiOver2 + lift);
    }

    /// <summary>
    /// Poses <paramref name="model"/> from its NPC's vanilla town state. Attacks play <paramref name="weapon"/> as
    /// animation only - vanilla fires the real projectile - and start on the first tick of the state
    /// (<paramref name="stateTicks"/> 0). While the director drives the NPC its <c>ai[]</c> stands still, so only sitting
    /// carries on, and only while the NPC is still on the seat.
    /// </summary>
    public static void ApplyVanilla(TerrarianModel model, int stateTicks, int weapon)
    {
        NPC npc = model.NPC;
        PlayerPuppet puppet = model.Puppet;
        int state = (int)npc.ai[0];
        bool directed = npc.TryGetGlobalNPC(out NPCDirector director) && director.Enabled && director.IsBusy;

        Sit(model.Player, state == Sitting && IsOnSeat(npc));

        puppet.FrontArmRotation = !directed && state is GameLead or GameListen
            ? RockPaperScissorsArm(BeatFromCountdown(npc.ai[1]))
            : null;

        if (directed || stateTicks != 0 || weapon <= ItemID.None || state is not (Throwing or Shooting or Casting or Swinging))
            return;

        // Shooting keeps the aim's vertical part in ai[2]; the rest face straight ahead.
        float rise = state == Shooting ? MathHelper.Clamp(npc.ai[2], -0.5f, 0.5f) : 0f;
        Vector2 aim = new(npc.direction * MathF.Sqrt(1f - rise * rise), rise);

        puppet.BeginUse(weapon, npc.Center + aim * 160f);
    }

    /// <summary>
    /// The tick within the current match. Vanilla counts <c>ai[1]</c> down from a whole number of
    /// <see cref="RockPaperScissorsCycle"/>s, which keeps a client that joined mid-game on the same beat.
    /// </summary>
    private static int BeatFromCountdown(float ticksLeft)
        => (RockPaperScissorsCycle - (int)ticksLeft % RockPaperScissorsCycle) % RockPaperScissorsCycle;

    /// <summary>Vanilla's own seat test: the tile just above the NPC's feet can be sat on.</summary>
    private static bool IsOnSeat(NPC npc)
    {
        Point seat = (npc.Bottom - Vector2.UnitY * 2f).ToTileCoordinates();
        return WorldGen.InWorld(seat.X, seat.Y) && TileID.Sets.CanBeSatOnForNPCs[Main.tile[seat.X, seat.Y].TileType];
    }

    /// <summary>
    /// The seated body and leg frames. Vanilla already put the NPC where it sits, so there is no seat offset to add;
    /// index 0 keeps the stacked-seat shift at nothing.
    /// </summary>
    private static void Sit(Player player, bool sitting)
    {
        if (player.sitting.isSitting == sitting)
            return;

        player.sitting.isSitting = sitting;
        player.sitting.offsetForSeat = Vector2.Zero;
        player.sitting.sittingIndex = sitting ? 0 : -1;
    }
}
