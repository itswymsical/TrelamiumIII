using System.Collections.Generic;
using Terraria.GameContent;
using TrelamiumIII.Common.Systems;
using TrelamiumIII.Content.Dryad;
using TrelamiumIII.Core.Graphics.Cinematics;
using TrelamiumIII.Core.Graphics.Fields.Layers;
using TrelamiumIII.Core.Graphics.Particles;

namespace TrelamiumIII.Content.EyeOfCthulhu;

/// <summary>Watches for the Eye of Cthulhu dying and hands off to <see cref="EyeOfCthulhuWakeSystem" />.</summary>
public sealed class EyeOfCthulhuWake : GlobalNPC
{
    public override void HitEffect(NPC npc, NPC.HitInfo hit)
    {
        if (npc.type != NPCID.EyeofCthulhu || npc.life > 0 || Main.dedServ)
            return;

        // Read before OnKill sets it, so this means "not seen yet" rather than "not dead yet".
        EyeOfCthulhuWakeSystem.Arm(npc.Center, firstKill: true);
    }

    public override void OnKill(NPC npc)
    {
        if (npc.type != NPCID.EyeofCthulhu)
            return;

        // Also syncs: mod flags ride the same world-data packet.
        NPC.SetEventFlagCleared(ref DownedBossSystem.SeenEyeOfCthulhuWake, -1);

        DryadArrivalSystem.OnEyeOfCthulhuKilled();
    }
}

/// <summary>Stages the wake: adopts the gore, sheds the first ink, and starts the cutscene a beat later.</summary>
public sealed class EyeOfCthulhuWakeSystem : ModSystem
{
    /// <summary>Ticks between the death and the cutscene opening.</summary>
    private const int QUEUE_DELAY = 45;

    /// <summary>The Eye of Cthulhu's own gore, and how many of each the wake sheds.</summary>
    private static readonly WakeGore[] EyeGore =
    [
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

    /// <summary>True from the death until the cutscene has been handed off, so other scenes can wait for it.</summary>
    public static bool Active => countdown >= 0;

    /// <summary>Arms the sequence. Safe to call more than once per death; the first call wins.</summary>
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

    private static void Prune()
    {
        for (int i = pieces.Count - 1; i >= 0; i--)
        {
            if (!pieces[i].Active)
                pieces.RemoveAt(i);
        }
    }

    /// <summary>Converts the base game's gore into our pieces and throws the first billow of ink.</summary>
    private static void Stage()
    {
        pieces.Clear();

        foreach (var entry in EyeGore)
        {
            for (int i = 0; i < entry.Count; i++)
                Shed(entry);
        }
        InikiField.Bloom(center, radius: 120f);
    }

    /// <summary>Spawns one piece wearing a vanilla gore sprite.</summary>
    private static void Shed(WakeGore entry)
    {
        if (entry.GoreType <= 0 || entry.GoreType >= TextureAssets.Gore.Length)
            return;

        if (TextureAssets.Gore[entry.GoreType] is not { } texture)
            return;

        Vector2 velocity = Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.4f, 5f);

        // Long-lived: the vortex kills pieces when it swallows them, not a timer.
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

/// <param name="GoreType">Index into <see cref="TextureAssets.Gore" />.</param>
/// <param name="Scale">Base scale, before the per-piece jitter.</param>
/// <param name="Source">Cell within the sprite, or null for the whole texture.</param>
internal readonly record struct WakeGore(int GoreType, int Count = 1, float Scale = 1f, Rectangle? Source = null);

