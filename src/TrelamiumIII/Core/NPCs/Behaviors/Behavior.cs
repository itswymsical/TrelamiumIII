using System.Collections.Generic;
using TrelamiumIII.Core.NPCs.Actors;

namespace TrelamiumIII.Core.NPCs.Behaviors;

/// <summary>Lifecycle of a single <see cref="IBehavior"/>.</summary>
public enum BehaviorState
{
    /// <summary>Built but not yet entered, or already torn down.</summary>
    Inactive,

    /// <summary>Currently running.</summary>
    Active,

    /// <summary>Ran to completion on its own terms.</summary>
    Finished,

    /// <summary>Cut short from outside - an interrupt, a new script, or death.</summary>
    Canceled,
}

/// <summary>
/// Something an NPC does for a while, which eventually finishes.
///
/// <para>
/// Behaviours compose. A leaf walks somewhere or says something; a composite owns
/// children and decides which is running. The <see cref="NPCDirector"/> only ever
/// talks to the outermost one.
/// </para>
/// </summary>
public interface IBehavior
{
    BehaviorState State { get; }

    /// <summary>
    /// The innermost behaviour actually running. Leaves return themselves, composites
    /// recurse. Used to find who wants to override the NPC's animation frame.
    /// </summary>
    IBehavior Leaf { get; }

    void Enter(BehaviorContext ctx);

    void Tick(BehaviorContext ctx);

    /// <summary><paramref name="canceled"/> separates "was interrupted" from "ran out".</summary>
    void Exit(BehaviorContext ctx, bool canceled);
}

/// <summary>Implemented by behaviours that pose a sprite-based NPC while they run.</summary>
public interface IFramedBehavior
{
    /// <summary>Return true if the frame was set. Not called for player-rendered actors.</summary>
    bool ModifyFrame(BehaviorContext ctx, int frameHeight);
}

/// <summary>
/// What a behaviour is handed each tick: the NPC, the director running it, and the
/// handful of lookups every behaviour reaches for. One instance per director.
/// </summary>
public sealed class BehaviorContext
{
    public BehaviorContext(NPC npc, NPCDirector director)
    {
        NPC = npc;
        Director = director;
    }

    public NPC NPC { get; }

    public NPCDirector Director { get; }

    /// <summary>The world actor, when this NPC is one. Null for plain vanilla NPCs.</summary>
    public WorldNPC Actor => NPC.ModNPC as WorldNPC;

    /// <summary>The player-model driver, when this NPC renders as a Terrarian.</summary>
    public PlayerPuppet Puppet => TerrarianModel.Of(NPC)?.Puppet;

    /// <summary>Nearest living player within range, or null.</summary>
    public Player NearestPlayer(float range = 1000f)
    {
        Player best = null;
        float bestDistance = range * range;

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

    /// <summary>Nearest attackable hostile NPC within range, or null.</summary>
    public NPC NearestHostile(float range = 400f)
    {
        NPC best = null;
        float bestDistance = range * range;

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
/// Base class for behaviours, and the entry point for building them.
///
/// <para>
/// Subclasses override <see cref="Begin"/>, <see cref="Run"/> and <see cref="End"/>
/// and call <see cref="Finish"/> when done; the base owns the state transitions so
/// they can't be got wrong. The static members are the fluent vocabulary - everything
/// an NPC can be told to do is reachable from <c>Behavior.</c>, and the decorator
/// extensions in <see cref="BehaviorExtensions"/> chain off whatever they return.
/// </para>
///
/// <example>
/// <code>
/// director.Play(npc, Behavior.Sequence(
///     Behavior.WalkTo(doorway),
///     Behavior.Wait(30),
///     Behavior.Talk("Guide.Intro"),
///     Behavior.Stay(home)).FacingNearestPlayer());
/// </code>
/// </example>
/// </summary>
public abstract class Behavior : IBehavior
{
    public BehaviorState State { get; private set; } = BehaviorState.Inactive;

    /// <summary>Ticks elapsed since this behaviour was entered.</summary>
    public int Age { get; private set; }

    /// <summary>Optional label, shown in director debug output.</summary>
    public string Name { get; set; }

    public virtual IBehavior Leaf => this;

    public void Enter(BehaviorContext ctx)
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

    public void Exit(BehaviorContext ctx, bool canceled)
    {
        if (State == BehaviorState.Inactive)
            return;

        if (State == BehaviorState.Active)
            State = canceled ? BehaviorState.Canceled : BehaviorState.Finished;

        End(ctx, canceled);
    }

    /// <summary>Called once as the behaviour starts. Seed timers and targets here.</summary>
    protected virtual void Begin(BehaviorContext ctx) { }

    /// <summary>Called every tick while active.</summary>
    protected abstract void Run(BehaviorContext ctx);

    /// <summary>Called once as it stops, whether it finished or was cut short.</summary>
    protected virtual void End(BehaviorContext ctx, bool canceled) { }

    /// <summary>Marks this behaviour complete. The director advances next tick.</summary>
    protected void Finish() => State = BehaviorState.Finished;

    public Behavior Named(string name)
    {
        Name = name;
        return this;
    }

    public override string ToString() => Name ?? GetType().Name;

    // The fluent vocabulary. Everything below builds behaviours; nothing runs here.

    /// <summary>Runs children one after another, finishing with the last.</summary>
    public static Sequence Sequence(params IBehavior[] children) => new(children);

    /// <summary>Runs children one after another, skipping nulls.</summary>
    public static Sequence Sequence(IEnumerable<IBehavior> children)
    {
        List<IBehavior> kept = [];

        foreach (IBehavior child in children)
        {
            if (child != null)
                kept.Add(child);
        }

        return new Sequence([.. kept]);
    }

    /// <summary>Rebuilds and reruns a behaviour. -1 repeats forever.</summary>
    public static Loop Loop(Func<IBehavior> factory, int count = -1) => new(factory, count);

    /// <summary>Priority branches, re-evaluated every tick. Build it with <c>When</c>/<c>Otherwise</c>.</summary>
    public static Select Select() => new();

    /// <summary>Stands still for a number of ticks.</summary>
    public static Wait Wait(int ticks) => new(ticks);

    /// <summary>Runs a callback once, then finishes.</summary>
    public static Do Do(Action<BehaviorContext> action) => new(action);

    /// <summary>Ticks a callback until it returns true. The escape hatch.</summary>
    public static Until Until(Func<BehaviorContext, bool> tick) => new(tick);

    /// <summary>Stands in place, optionally emoting. Runs forever unless given a duration.</summary>
    public static Idle Idle(int ticks = -1) => new() { Duration = ticks };

    /// <summary>Holds a spot, walking back when pushed off it. Runs forever.</summary>
    public static Stay Stay(Point tile) => new Stay().At(tile);

    /// <inheritdoc cref="Stay(Point)"/>
    public static Stay Stay(Vector2 worldPosition) => new Stay().At(worldPosition);

    /// <summary>Faces whoever has the NPC's chat window open, finishing once it closes.</summary>
    public static Listen Listen() => new();

    /// <summary>Walks to a world position and finishes on arrival.</summary>
    public static WalkTo WalkTo(Vector2 worldPosition) => new(worldPosition);

    /// <inheritdoc cref="WalkTo(Vector2)"/>
    public static WalkTo WalkTo(Point tile) => new(tile.ToWorldCoordinates());

    /// <summary>Trails an entity, re-resolved each tick.</summary>
    public static Follow Follow(Func<BehaviorContext, Entity> target) => new(target);

    /// <summary>Trails whichever player is closest.</summary>
    public static Follow FollowNearestPlayer(float searchRange = 2000f)
        => new(ctx => ctx.NearestPlayer(searchRange));

    /// <summary>Turns to face the nearest player, and nothing else.</summary>
    public static FacePlayer FacePlayer(float range = 400f) => new() { Range = range };

    /// <summary>Snaps to a position and finishes.</summary>
    public static Teleport Teleport(Vector2 worldPosition) => new(worldPosition);

    /// <summary>
    /// Roots the NPC in a talking loop until <paramref name="stillTalking"/> goes false.
    /// Pass null to talk until something else takes over.
    /// </summary>
    public static Talk Talk(Func<BehaviorContext, bool> stillTalking = null) => new(stillTalking);
}
