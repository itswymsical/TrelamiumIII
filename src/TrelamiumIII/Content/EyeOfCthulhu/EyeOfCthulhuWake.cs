using System.Collections.Generic;
using Terraria.GameContent;
using TrelamiumIII.Common.Systems;
using TrelamiumIII.Core.Graphics.Cinematics;
using TrelamiumIII.Core.Graphics.Metaballs.Layers;
using TrelamiumIII.Core.Graphics.Particles;

namespace TrelamiumIII.Content.EyeOfCthulhu;

/// <summary>
///     Watches for the Eye of Cthulhu dying and hands the moment over to
///     <see cref="EyeOfCthulhuWakeSystem" />.
/// </summary>
/// <remarks>
///     Split across two hooks because they run in different places.
///     <list type="bullet">
///         <item>
///             <b><see cref="HitEffect" /></b> is the client-side one. It fires wherever the death is
///             seen, including on every client in a multiplayer fight, which is what the visual side
///             needs -- and it is also the hook vanilla spawns its own gore from, so it is the moment
///             those pieces come into existence.
///         </item>
///         <item>
///             <b><see cref="OnKill" /></b> runs on the server (or the single player), and is
///             therefore the only correct place to write a world flag.
///         </item>
///     </list>
/// </remarks>
public sealed class EyeOfCthulhuWake : GlobalNPC
{
    public override void HitEffect(NPC npc, NPC.HitInfo hit)
    {
        if (npc.type != NPCID.EyeofCthulhu || npc.life > 0 || Main.dedServ)
            return;

        // Read before OnKill has a chance to set it, which is what makes this mean "the player has
        // not seen this yet" rather than "the boss is not dead yet".
        EyeOfCthulhuWakeSystem.Arm(npc.Center, firstKill: true);
    }

    public override void OnKill(NPC npc)
    {
        if (npc.type != NPCID.EyeofCthulhu)
            return;

        // Sets the flag and pushes it to every client. Mod flags ride along in the same world-data
        // packet, so there is nothing further to send.
        NPC.SetEventFlagCleared(ref DownedBossSystem.SeenEyeOfCthulhuWake, -1);
    }
}

/// <summary>
///     Stages the Eye of Cthulhu's wake: adopts the gore, sheds the first ink, and starts the
///     cutscene a beat later.
/// </summary>
/// <remarks>
///     <para>
///         The work is deferred a tick rather than done inside the death hook, for two reasons. The
///         gore has to already exist to be adopted, and whether a <see cref="GlobalNPC" /> hook runs
///         before or after the vanilla body that creates it is not something worth depending on. And
///         the cutscene should not open on the same frame the killing blow lands -- the player needs
///         a moment to read the death first, which is why the scene is queued rather than cut to.
///     </para>
///     <para>
///         Entirely client-side. The pieces are decoration and the cutscene is a camera, so nothing
///         here is synced; each client stages its own, the same way <c>BossIntroSystem</c> does.
///     </para>
/// </remarks>
public sealed class EyeOfCthulhuWakeSystem : ModSystem
{
    /// <summary>Ticks between the death and the cutscene opening.</summary>
    private const int QUEUE_DELAY = 45;

    /// <summary>
    ///     The Eye of Cthulhu's own gore, and how many of each the wake sheds.
    /// </summary>
    /// <remarks>
    ///     Explicit rather than discovered. An earlier version scanned <see cref="Main.gore" /> around
    ///     the corpse and adopted whatever it found, which fails for a reason that only shows up in a
    ///     forest: ambient gore -- leaves, petals shed by the grass -- is inside any sensible radius
    ///     constantly, so the wake shed foliage instead of the boss. Nothing about a live
    ///     <see cref="Gore" /> says which NPC it came from, so there is no filter that fixes that;
    ///     the sprites have to be named.
    ///     <para>
    ///         Add a row to shed a new kind of piece. A sheeted sprite MUST carry its cell rectangle
    ///         in <see cref="WakeGore.Source" /> -- see that type for what happens otherwise.
    ///     </para>
    /// </remarks>
    private static readonly WakeGore[] EyeGore =
    [
        // Gore_6 through Gore_10. These are the FILE suffixes and the array indices both: Terraria
        // fills TextureAssets.Gore[i] from "Images/Gore_" + i, and Gore_0 exists, so there is no
        // off-by-one to correct for -- indexing 5..9 would shift the whole set one sprite earlier.
        //
        // Counts are uniform to start. Tune per row once it is clear on screen which sprite is which;
        // a piece that reads as a big shell chunk wants fewer than one that reads as a scrap.
        new WakeGore(GoreType: 6, Count: 2),
        new WakeGore(GoreType: 7, Count: 2),
        new WakeGore(GoreType: 8, Count: 2),
        new WakeGore(GoreType: 9, Count: 2),
        new WakeGore(GoreType: 10, Count: 2),
    ];
    /// <summary>Live pieces, for the cutscene to drive. Pruned as they expire.</summary>
    private static readonly List<EyeGoreParticle> pieces = [];

    private static int countdown = -1;
    private static bool staged;
    private static bool playCutscene;
    private static Vector2 center;

    /// <summary>The remains still on screen, in the order they were shed.</summary>
    public static IReadOnlyList<EyeGoreParticle> Pieces => pieces;

    /// <summary>Where the Eye died. The cutscene frames and vortexes on this.</summary>
    public static Vector2 Center => center;

    /// <summary>
    ///     Arms the sequence. Safe to call more than once for one death; the first call wins.
    /// </summary>
    public static void Arm(Vector2 deathCenter, bool firstKill)
    {
        if (Main.dedServ || countdown >= 0)
            return;

        center = deathCenter;
        countdown = 0;
        staged = false;
        playCutscene = firstKill;
    }

    public override void ClearWorld() => Reset();

    public override void OnWorldUnload() => Reset();

    private static void Reset()
    {
        pieces.Clear();
        countdown = -1;
        staged = false;
        playCutscene = false;
    }

    public override void PostUpdateEverything()
    {
        if (Main.dedServ || Main.gameMenu)
            return;

        Prune();

        if (countdown < 0)
            return;

        // One tick after the death, so vanilla's gore is certainly on the ground by now.
        if (!staged)
        {
            Stage();
            staged = true;
        }

        countdown++;

        if (countdown < QUEUE_DELAY)
            return;

        countdown = -1;

        if (playCutscene)
            CutsceneSystem.PlayCutscene<EyeOfCthulhuWakeCutscene>();

        playCutscene = false;
    }

    /// <summary>Drops references to pieces that have expired, so the list cannot grow forever.</summary>
    private static void Prune()
    {
        for (int i = pieces.Count - 1; i >= 0; i--)
        {
            if (!pieces[i].Active)
                pieces.RemoveAt(i);
        }
    }

    /// <summary>
    ///     Turns the base game's gore into our own pieces, and throws the first billow of ink.
    /// </summary>
    private static void Stage()
    {
        pieces.Clear();

        foreach (var entry in EyeGore)
        {
            for (int i = 0; i < entry.Count; i++)
                Shed(entry);
        }

        InkLayer.Bloom(center, radius: 90f);
    }

    /// <summary>
    ///     Spawns one piece wearing a vanilla gore sprite.
    /// </summary>
    /// <remarks>
    ///     Reads <see cref="TextureAssets" /> directly rather than creating a <see cref="Gore" /> and
    ///     taking its texture. That is what makes the pieces independent of the gore setting: nothing
    ///     here is gore as far as the game is concerned, so a player who has it disabled still gets
    ///     the cutscene -- which is the distinction Particle was written around in the first place.
    /// </remarks>
    private static void Shed(WakeGore entry)
    {
        if (entry.GoreType <= 0 || entry.GoreType >= TextureAssets.Gore.Length)
            return;

        if (TextureAssets.Gore[entry.GoreType] is not { } texture)
            return;

        Vector2 velocity = Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.4f, 5f);

        // Long-lived: these have to survive the whole cutscene, and the vortex kills them off itself
        // when it swallows them rather than waiting for a timer.
        var piece = ParticleSystem.Spawn<EyeGoreParticle>(
            center + Main.rand.NextVector2Circular(30f, 30f),
            velocity,
            timeLeft: 60 * 22,
            scale: entry.Scale * Main.rand.NextFloat(0.9f, 1.15f),
            rotation: Main.rand.NextFloat(MathHelper.TwoPi),
            rotationVelocity: Main.rand.NextFloat(-0.06f, 0.06f)
        );

        if (piece is null)
            return;

        piece.TextureOverride = texture;
        piece.SourceRect = entry.Source;

        pieces.Add(piece);
    }
}

/// <summary>
///     One kind of debris a wake sheds: which sprite it wears, how many of it, and how it is cut out
///     of that sprite.
/// </summary>
/// <remarks>
///     <paramref name="Source" /> is null for a sprite that is a single image, and a cell rectangle
///     for one that is a sheet. It is not optional in the second case:
///     <see cref="Particle" /> models frames as a vertical strip, so a gore laid out as a GRID drawn
///     without a source rect renders as a lattice of every sprite on it.
/// </remarks>
/// <param name="GoreType">Index into <see cref="TextureAssets.Gore" />.</param>
/// <param name="Count">How many of this piece the wake sheds.</param>
/// <param name="Scale">Base scale, before the per-piece jitter.</param>
/// <param name="Source">Cell within the sprite, or null for the whole texture.</param>
internal readonly record struct WakeGore(int GoreType, int Count = 1, float Scale = 1f, Rectangle? Source = null);

