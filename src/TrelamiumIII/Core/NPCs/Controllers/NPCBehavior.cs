using Terraria;

namespace TrelamiumIII.Core.NPCs.Controllers;

/// <summary>
/// A discrete unit of NPC control. The <see cref="NPCDirector"/> activates one
/// behavior at a time and advances through a queue when the active behavior
/// signals completion. Behaviors own their own state and are single-use;
/// create a new instance to repeat one.
/// </summary>
public abstract class NPCBehavior
{
    /// <summary>True once the behavior has finished its work.</summary>
    public bool IsComplete { get; private set; }

    /// <summary>
    /// When true (default), the director suppresses vanilla AI while this
    /// behavior is active. Set to false for behaviors that augment vanilla AI
    /// rather than replacing it (e.g. attack delegation).
    /// </summary>
    public virtual bool OverrideVanillaAI { get; set; } = true;

    // Any behavior can set these. The director handles rendering.

    /// <summary>Item ID to render in the NPC's hand. 0 = nothing.</summary>
    public HeldItemConfig HeldItem { get; set; }
    public bool ForceHandOutFrame { get; set; }

    /// <summary>When true, the NPC faces the nearest player each tick.</summary>
    public bool FaceNearestPlayer { get; set; }


    /// <summary>Called once when this behavior becomes the active behavior.</summary>
    public virtual void OnStart(NPC npc) { }

    /// <summary>
    /// Called every tick while active. Drives movement, timers, and state.
    /// The director handles AI suppression based on <see cref="OverrideVanillaAI"/>.
    /// </summary>
    public abstract void Update(NPC npc);

    /// <summary>Called once when this behavior is removed or interrupted.</summary>
    public virtual void OnEnd(NPC npc) { }

    /// <summary>
    /// frame override for vanilla NPC animation.
    /// Return true if you set the frame yourself.
    /// Not called for WorldNPCs (they use PlayerFrame).
    /// </summary>
    public virtual bool ModifyFrame(NPC npc, int frameHeight) => false;

    public NPCBehavior WithHeldItem(int itemType, Vector2? offset = null, float scale = 0.95f, float rotation = 0f)
    {
        HeldItem = new HeldItemConfig(itemType, offset ?? new Vector2(12, -8), scale, rotation);
        return this;
    }
    public NPCBehavior WithHandOut(bool force = true) { ForceHandOutFrame = force; return this; }

    public NPCBehavior WithFacePlayer(bool face = true) { FaceNearestPlayer = face; return this; }
    public NPCBehavior WithVanillaAI(bool allow = true) { OverrideVanillaAI = !allow; return this; }
    public record HeldItemConfig(int ItemType, Vector2 Offset = default, float Scale = 0.95f, float rotation = 0f);


    /// <summary>Signals the director to advance to the next queued behavior.</summary>
    protected void Complete() => IsComplete = true;
}