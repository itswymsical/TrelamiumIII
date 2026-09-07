using Microsoft.Xna.Framework.Graphics;
using TrelamiumIII.Core.NPCs.Actors;

namespace TrelamiumIII.Core.NPCs.Controllers;

/// <summary>Lifecycle of a single <see cref="IBehaviorModule"/>.</summary>
public enum BehaviorState
{
    /// <summary>Built but not yet entered, or already torn down.</summary>
    Inactive,

    /// <summary>Currently running.</summary>
    Active,

    /// <summary>Ran to completion on its own terms.</summary>
    Finished,

    /// <summary>Cut short from outside - a higher-priority interrupt, a new program, or death.</summary>
    Canceled,
}

/// <summary>
/// How a module wants vanilla AI treated while it runs. This is the lever the
/// director pulls: an NPC's AI is a box, and the module decides whether that box
/// runs, when it runs, and whether it runs at all.
/// </summary>
public enum VanillaAIMode
{
    /// <summary>
    /// Vanilla AI never executes. The module owns movement completely.
    /// </summary>
    Suppress,

    /// <summary>
    /// Vanilla AI runs first, exactly as it normally would, and the module ticks
    /// afterwards on top of the result. Use this to decorate a fight - extra
    /// projectiles, an aura, a telegraph - without touching its movement.
    /// </summary>
    Augment,

    /// <summary>
    /// The module ticks first and decides per-tick whether to run vanilla AI, by
    /// calling <see cref="BehaviorContext.RunVanillaAI"/>. This is how you cut
    /// between a vanilla pattern and a modded one within a single fight.
    /// <para>
    /// Note that driving vanilla AI by hand skips <see cref="ModNPC.AI"/> and
    /// <see cref="ModNPC.AIType"/> remapping, so it is meant for vanilla NPCs.
    /// </para>
    /// </summary>
    Drive,
}

/// <summary>
/// Everything a module is handed each tick: the NPC it drives, the director
/// driving it, and shortcuts to the things modules reach for constantly.
/// One instance per director, reused every tick.
/// </summary>
public sealed class BehaviorContext
{
    internal bool vanillaRequested;

    public BehaviorContext(NPC npc, NPCDirector director)
    {
        NPC = npc;
        Director = director;
    }

    /// <summary>The NPC being directed.</summary>
    public NPC NPC { get; }

    /// <summary>The director running this module tree.</summary>
    public NPCDirector Director { get; }

    /// <summary>The player-rendered actor, when this NPC is one. Null for vanilla NPCs.</summary>
    public WorldNPC Actor => NPC.ModNPC as WorldNPC;

    /// <summary>The item-animation driver, when this NPC renders as a player.</summary>
    public PlayerPuppet Puppet => Actor?.Puppet;

    /// <summary>True when this NPC can pose, hold and swing items like a player.</summary>
    public bool HasPuppet => Puppet != null;

    /// <summary>
    /// Asks the director to run vanilla AI this tick. Only meaningful from a module
    /// whose <see cref="IBehaviorModule.VanillaAI"/> is <see cref="VanillaAIMode.Drive"/>.
    /// </summary>
    public void RunVanillaAI() => vanillaRequested = true;

    // Query helpers. Modules use these constantly; keeping them here stops every
    // module from growing its own slightly different copy.

    /// <summary>Nearest living player within <paramref name="maxRange"/> pixels, or null.</summary>
    public Player NearestPlayer(float maxRange = 1000f)
    {
        Player best = null;
        float bestDistance = maxRange * maxRange;

        foreach (Player player in Main.ActivePlayers)
        {
            if (player.dead || player.ghost)
                continue;

            float distance = Vector2.DistanceSquared(NPC.Center, player.Center);
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            best = player;
        }

        return best;
    }

    /// <summary>Nearest attackable hostile NPC within <paramref name="maxRange"/> pixels, or null.</summary>
    public NPC NearestHostile(float maxRange = 400f)
    {
        NPC best = null;
        float bestDistance = maxRange * maxRange;

        foreach (NPC other in Main.ActiveNPCs)
        {
            if (other.whoAmI == NPC.whoAmI || other.friendly || other.CountsAsACritter)
                continue;

            if (other.dontTakeDamage || other.immortal || !other.CanBeChasedBy())
                continue;

            float distance = Vector2.DistanceSquared(NPC.Center, other.Center);
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            best = other;
        }

        return best;
    }

    /// <summary>Points the NPC (and its puppet, if any) at a world position.</summary>
    public void Face(Vector2 worldPosition)
    {
        int direction = worldPosition.X < NPC.Center.X ? -1 : 1;
        NPC.direction = direction;
        NPC.spriteDirection = direction;

        if (Puppet != null)
            Puppet.Player.direction = direction;
    }
}

/// <summary>
/// A thing that can run for some number of ticks and eventually finishes.
///
/// <para>
/// Modules compose: a module may be a leaf that walks somewhere, or a composite
/// that owns children and decides which of them is running. The director only ever
/// talks to the root; everything below is the root's business.
/// </para>
/// </summary>
public interface IBehaviorModule
{
    /// <summary>Where this module is in its lifecycle.</summary>
    BehaviorState State { get; }

    /// <summary>
    /// How vanilla AI should be treated right now. Composites forward this from
    /// whichever child is currently running.
    /// </summary>
    VanillaAIMode VanillaAI { get; }

    /// <summary>
    /// The innermost module actually running. Leaves return themselves; composites
    /// recurse. The director uses this to find who wants to draw or set frames.
    /// </summary>
    IBehaviorModule ActiveLeaf { get; }

    /// <summary>Called once when this module becomes the running module.</summary>
    void OnEnter(BehaviorContext ctx);

    /// <summary>Called every tick while <see cref="State"/> is <see cref="BehaviorState.Active"/>.</summary>
    void Tick(BehaviorContext ctx);

    /// <summary>
    /// Called once when the module stops running.
    /// <paramref name="canceled"/> distinguishes "was interrupted" from "ran out".
    /// </summary>
    void OnExit(BehaviorContext ctx, bool canceled);
}

/// <summary>Implemented by modules that want to override a vanilla NPC's animation frame.</summary>
public interface IFramedModule
{
    /// <summary>Return true if the frame was set. Not called for player-rendered actors.</summary>
    bool ModifyFrame(BehaviorContext ctx, int frameHeight);
}

/// <summary>Implemented by modules that draw something extra on top of the NPC.</summary>
public interface IDrawnModule
{
    void Draw(BehaviorContext ctx, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor);
}

/// <summary>
/// Base class for behavior modules. Owns the state transitions so subclasses can't
/// get them wrong: override <see cref="Begin"/>, <see cref="Run"/> and
/// <see cref="End"/>, and call <see cref="Finish"/> when the work is done.
/// </summary>
public abstract class BehaviorModule : IBehaviorModule
{
    private VanillaAIMode mode = VanillaAIMode.Suppress;

    public BehaviorState State { get; private set; } = BehaviorState.Inactive;

    /// <summary>Ticks elapsed since this module was entered.</summary>
    public int Age { get; private set; }

    /// <summary>Optional label. Shows up in director debug output.</summary>
    public string Name { get; set; }

    public virtual VanillaAIMode VanillaAI => mode;

    public virtual IBehaviorModule ActiveLeaf => this;

    public void OnEnter(BehaviorContext ctx)
    {
        State = BehaviorState.Active;
        Age = 0;
        Begin(ctx);
    }

    public void Tick(BehaviorContext ctx)
    {
        if (State != BehaviorState.Active)
            return;

        Run(ctx);
        Age++;
    }

    public void OnExit(BehaviorContext ctx, bool canceled)
    {
        if (State == BehaviorState.Inactive)
            return;

        if (State == BehaviorState.Active)
            State = canceled ? BehaviorState.Canceled : BehaviorState.Finished;

        End(ctx, canceled);
    }

    /// <summary>Called once as the module starts. Seed timers and targets here.</summary>
    protected virtual void Begin(BehaviorContext ctx) { }

    /// <summary>Called every tick while active.</summary>
    protected abstract void Run(BehaviorContext ctx);

    /// <summary>Called once as the module stops, whether it finished or was cut short.</summary>
    protected virtual void End(BehaviorContext ctx, bool canceled) { }

    /// <summary>Marks this module complete. The director advances on the next tick.</summary>
    protected void Finish() => State = BehaviorState.Finished;

    /// <summary>Sets how vanilla AI is treated while this module runs.</summary>
    public BehaviorModule WithVanillaAI(VanillaAIMode value)
    {
        mode = value;
        return this;
    }

    /// <summary>Names the module for debugging.</summary>
    public BehaviorModule Named(string name)
    {
        Name = name;
        return this;
    }

    public override string ToString() => Name ?? GetType().Name;
}
