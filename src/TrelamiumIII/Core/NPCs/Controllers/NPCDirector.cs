using TrelamiumIII.Core.NPCs.Actors;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using static TrelamiumIII.Core.NPCs.Controllers.NPCBehavior;
using TrelamiumIII.Core.Components.NPCs;

namespace  TrelamiumIII.Core.NPCs.Controllers;

/// <summary>
/// Manages a queue of <see cref="NPCBehavior"/> instances on any NPC, vanilla
/// or modded. Attach via <c>npc.TryEnable&lt;NPCDirector&gt;</c>, then push
/// behaviors with <see cref="SetBehavior"/> or <see cref="Enqueue"/>.
///
/// Replaces both <c>DialogueNPCBehavior</c> and <c>WorldNPCComponent</c> with
/// a single composable system.
/// </summary>
public class NPCDirector : NPCComponent
{
    private NPCBehavior active;
    private readonly Queue<NPCBehavior> queue = new();

    // Saved vanilla state. restored when the director releases control.
    private float savedAI0, savedAI1;
    private int savedAIStyle;
    private bool hasControl;

    // At the top of the class, after the field declarations:
    private static readonly Dictionary<int, int> HandOutFrames = new()
    {
        [NPCID.Guide] = 17,
    };
    private const int DefaultHandOutFrame = 0;


    /// <summary>The currently running behavior, or null.</summary>
    public NPCBehavior Active => active;

    /// <summary>True when any behavior is active.</summary>
    public bool HasBehavior => active != null;

    /// <summary>Number of behaviors waiting in the queue.</summary>
    public int QueueCount => queue.Count;

    /// <summary>
    /// Immediately sets a behavior, ending the current one and clearing the queue.
    /// </summary>
    public void SetBehavior(NPC npc, NPCBehavior behavior)
    {
        EndCurrent(npc);
        queue.Clear();
        StartBehavior(npc, behavior);
    }

    /// <summary>
    /// Adds a behavior to the end of the queue. If nothing is active, it
    /// starts immediately on the next tick.
    /// </summary>
    public void Enqueue(NPCBehavior behavior)
    {
        queue.Enqueue(behavior);
    }

    /// <summary>
    /// Ends the current behavior and clears the queue. Restores vanilla AI.
    /// </summary>
    public void Clear(NPC npc)
    {
        EndCurrent(npc);
        queue.Clear();

        if (hasControl)
            RestoreVanillaState(npc);
    }

    /// <summary>
    /// Skips the current behavior and advances to the next queued one.
    /// If the queue is empty, restores vanilla AI.
    /// </summary>
    public void Skip(NPC npc)
    {
        EndCurrent(npc);
        TryAdvance(npc);
    }

    // Lifecycle

    public override bool PreAI(NPC npc)
    {
        if (!Enabled) return true;

        // Advance queue if current behavior completed or is null.
        if (active == null || active.IsComplete)
        {
            if (active?.IsComplete == true)
                EndCurrent(npc);

            TryAdvance(npc);
        }

        if (active == null)
            return true; // No behavior, vanilla AI runs.

        // Common visual modifiers.
        if (active.FaceNearestPlayer)
            FaceNearestPlayer(npc);

        active.Update(npc);

        // Guard: behavior or external code may have called SetBehavior/Clear
        // during Update, which would change or null the active reference.
        if (active == null)
            return true;

        // Sync WorldNPC dummy player if applicable.
        if (npc.ModNPC is WorldNPC worldNpc)
        {
            SyncHeldItem(worldNpc);
            worldNpc.SyncDummyToNPC();
            worldNpc.DummyPlayer.PlayerFrame();
        }

        return !active.OverrideVanillaAI;
    }

    public override void FindFrame(NPC npc, int frameHeight)
    {
        if (!Enabled || active == null) return;
        if (npc.ModNPC is WorldNPC) return;

        // If the behavior is holding an item, force the hand-out frame.
        if (active.HeldItem is { ItemType: > 0 })
        {
            int frame = HandOutFrames.GetValueOrDefault(npc.type, DefaultHandOutFrame);
            npc.frame.Y = frame * frameHeight;
            return;
        }

        if (active.HeldItem is { ItemType: > 0 } || active.ForceHandOutFrame)
        {
            int frame = HandOutFrames.GetValueOrDefault(npc.type, DefaultHandOutFrame);
            npc.frame.Y = frame * frameHeight;
            return;
        }

        active.ModifyFrame(npc, frameHeight);
    }

    public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        if (!Enabled || active == null) return;
        if (active.HeldItem is not { ItemType: > 0 } config) return;

        if (npc.ModNPC is WorldNPC worldNpc)
            DrawHeldItemWorldNPC(worldNpc, spriteBatch, screenPos, drawColor, config);
        else
            DrawHeldItem(npc, spriteBatch, screenPos, drawColor, config);
    }

    private static void DrawHeldItemWorldNPC(WorldNPC worldNpc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor, HeldItemConfig config)
    {
        Texture2D texture = TextureAssets.Item[config.ItemType].Value;

        // itemLocation is set by PlayerFrame when itemAnimation > 0.
        // It's the world-space position of the player's hand this frame.

        Vector2 offset = config.Offset;
        if (worldNpc.NPC.spriteDirection == -1)
            offset.X *= -1;

        Vector2 drawPos = worldNpc.DummyPlayer.Center - screenPos + offset;

        spriteBatch.Draw(
            texture,
            drawPos,
            null,
            drawColor,
            config.rotation,
            texture.Size() * 0.5f,
            config.Scale,
            worldNpc.DummyPlayer.direction == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally,
            0f
        );
    }
    // Internal behavior

    private void StartBehavior(NPC npc, NPCBehavior behavior)
    {
        active = behavior;

        if (behavior.OverrideVanillaAI && !hasControl)
            SaveVanillaState(npc);

        behavior.OnStart(npc);
        npc.netUpdate = true;
    }

    private void EndCurrent(NPC npc)
    {
        if (active == null) return;

        active.OnEnd(npc);

        // Clear held item from WorldNPC when behavior ends.
        if (active.HeldItem is { ItemType: > 0 } && npc.ModNPC is WorldNPC worldNpc)
            worldNpc.SetHeldItem(0);

        active = null;
    }

    private void TryAdvance(NPC npc)
    {
        if (queue.Count > 0)
        {
            StartBehavior(npc, queue.Dequeue());
        }
        else if (hasControl)
        {
            RestoreVanillaState(npc);
        }
    }

    private void SaveVanillaState(NPC npc)
    {
        savedAI0 = npc.ai[0];
        savedAI1 = npc.ai[1];
        savedAIStyle = npc.aiStyle;
        hasControl = true;
    }

    private void RestoreVanillaState(NPC npc)
    {
        if (!hasControl) return;

        npc.ai[0] = savedAI0;
        npc.ai[1] = savedAI1;
        npc.aiStyle = savedAIStyle;
        hasControl = false;
        npc.netUpdate = true;
    }

    // Visual helpers

    private void SyncHeldItem(WorldNPC worldNpc)
    {
        int desired = active?.HeldItem?.ItemType ?? 0;
        var slot = worldNpc.DummyPlayer.inventory[worldNpc.DummyPlayer.selectedItem];

        if (slot.type != desired)
            slot.SetDefaults(desired);

        // Sustain itemAnimation so PlayerFrame plays the arm-holding pose.
        // holdStyle == 0 items only draw their arm during active animation,
        // so we keep it alive here, after PlayerFrame has already run this tick.
        if (desired > ItemID.None && slot.holdStyle == 0)
        {
            worldNpc.DummyPlayer.itemAnimation = 1;
            worldNpc.DummyPlayer.itemAnimationMax = 1;
        }
    }

    private static void FaceNearestPlayer(NPC npc)
    {
        float closest = float.MaxValue;
        int dir = npc.direction;

        for (int i = 0; i < Main.maxPlayers; i++)
        {
            Player p = Main.player[i];
            if (!p.active || p.dead || p.ghost) continue;

            float dist = Vector2.DistanceSquared(npc.Center, p.Center);
            if (dist < closest && dist < 400f * 400f)
            {
                closest = dist;
                dir = p.Center.X < npc.Center.X ? -1 : 1;
            }
        }

        npc.direction = dir;
        npc.spriteDirection = dir;
    }

    private static void DrawHeldItem(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor, HeldItemConfig config)
    {
        Texture2D texture = TextureAssets.Item[config.ItemType].Value;

        Vector2 offset = config.Offset;
        if (npc.spriteDirection == -1)
            offset.X *= -1;

        Vector2 drawPos = npc.Center - screenPos + offset;

        spriteBatch.Draw(
            texture,
            drawPos,
            null,
            drawColor,
            config.rotation,
            texture.Size() * 0.5f,
            config.Scale,
            npc.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally,
            0f
        );
    }
}