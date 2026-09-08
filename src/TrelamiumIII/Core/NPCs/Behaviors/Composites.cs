using System.Collections.Generic;

namespace TrelamiumIII.Core.NPCs.Behaviors;

/// <summary>
/// Runs children one after another, finishing when the last one does.
/// The everyday way to script a scene.
/// </summary>
public sealed class Sequence : Behavior
{
    private readonly IBehavior[] children;
    private int index;

    public Sequence(params IBehavior[] children)
    {
        this.children = children ?? [];
    }

    private IBehavior Current => index < children.Length ? children[index] : null;

    public override IBehavior Leaf => Current?.Leaf ?? this;

    protected override void Begin(BehaviorContext ctx)
    {
        index = 0;

        if (children.Length == 0)
            Finish();
        else
            children[0].Enter(ctx);
    }

    protected override void Run(BehaviorContext ctx)
    {
        IBehavior current = Current;
        if (current == null)
        {
            Finish();
            return;
        }

        current.Tick(ctx);

        if (current.State == BehaviorState.Active)
            return;

        current.Exit(ctx, canceled: false);
        index++;

        if (Current == null)
            Finish();
        else
            Current.Enter(ctx);
    }

    protected override void End(BehaviorContext ctx, bool canceled) => Current?.Exit(ctx, canceled);
}

/// <summary>
/// Rebuilds and reruns a behaviour each time it finishes.
/// <para>
/// Takes a factory rather than an instance because behaviours keep state; running the
/// same object twice would resume it half-finished.
/// </para>
/// </summary>
public sealed class Loop : Behavior
{
    private readonly Func<IBehavior> factory;
    private readonly int count;

    private IBehavior current;
    private int completed;

    public Loop(Func<IBehavior> factory, int count = -1)
    {
        this.factory = factory;
        this.count = count;
    }

    public override IBehavior Leaf => current?.Leaf ?? this;

    protected override void Begin(BehaviorContext ctx)
    {
        completed = 0;
        StartIteration(ctx);
    }

    protected override void Run(BehaviorContext ctx)
    {
        if (current == null)
        {
            Finish();
            return;
        }

        current.Tick(ctx);

        if (current.State == BehaviorState.Active)
            return;

        current.Exit(ctx, canceled: false);
        current = null;
        completed++;

        if (count >= 0 && completed >= count)
        {
            Finish();
            return;
        }

        StartIteration(ctx);
    }

    private void StartIteration(BehaviorContext ctx)
    {
        current = factory?.Invoke();

        if (current == null)
            Finish();
        else
            current.Enter(ctx);
    }

    protected override void End(BehaviorContext ctx, bool canceled)
    {
        if (current is { State: BehaviorState.Active })
            current.Exit(ctx, canceled);

        current = null;
    }
}

/// <summary>
/// Runs the first branch whose condition holds, re-checked every tick. When a
/// higher-priority condition becomes true the running branch is cut short and
/// replaced - which is what makes an NPC react rather than finish what it started.
/// </summary>
public sealed class Select : Behavior
{
    private readonly List<(Func<BehaviorContext, bool> When, Func<IBehavior> Build)> branches = [];

    private int activeBranch = -1;
    private IBehavior current;

    public override IBehavior Leaf => current?.Leaf ?? this;

    /// <summary>Adds a branch. Order is priority: earlier wins.</summary>
    public Select When(Func<BehaviorContext, bool> condition, Func<IBehavior> build)
    {
        branches.Add((condition, build));
        return this;
    }

    /// <summary>Adds an always-true fallback. Put it last.</summary>
    public Select Otherwise(Func<IBehavior> build) => When(_ => true, build);

    protected override void Run(BehaviorContext ctx)
    {
        int wanted = branches.FindIndex(branch => branch.When(ctx));

        if (wanted != activeBranch)
        {
            if (current is { State: BehaviorState.Active })
                current.Exit(ctx, canceled: true);

            activeBranch = wanted;
            current = wanted < 0 ? null : branches[wanted].Build();
            current?.Enter(ctx);
        }

        if (current == null)
        {
            Finish();
            return;
        }

        current.Tick(ctx);

        if (current.State == BehaviorState.Active)
            return;

        // The branch ran out but its condition still holds, so restart it next tick.
        current.Exit(ctx, canceled: false);
        current = null;
        activeBranch = -1;
    }

    protected override void End(BehaviorContext ctx, bool canceled)
    {
        if (current is { State: BehaviorState.Active })
            current.Exit(ctx, canceled);

        current = null;
        activeBranch = -1;
    }
}

/// <summary>Base for the one-child wrappers built by <see cref="BehaviorExtensions"/>.</summary>
public abstract class Decorator : Behavior
{
    protected Decorator(IBehavior child)
    {
        Child = child;
    }

    protected IBehavior Child { get; }

    public override IBehavior Leaf => Child?.Leaf ?? this;

    protected override void Begin(BehaviorContext ctx) => Child?.Enter(ctx);

    protected override void Run(BehaviorContext ctx)
    {
        if (Child == null)
        {
            Finish();
            return;
        }

        if (ShouldStop(ctx))
        {
            Child.Exit(ctx, canceled: true);
            Finish();
            return;
        }

        Child.Tick(ctx);
        Alongside(ctx);

        if (Child.State == BehaviorState.Active)
            return;

        Child.Exit(ctx, canceled: false);
        Finish();
    }

    /// <summary>Checked before the child ticks. True cancels it and finishes.</summary>
    protected virtual bool ShouldStop(BehaviorContext ctx) => false;

    /// <summary>Runs every tick alongside the child.</summary>
    protected virtual void Alongside(BehaviorContext ctx) { }

    protected override void End(BehaviorContext ctx, bool canceled)
    {
        if (Child is { State: BehaviorState.Active })
            Child.Exit(ctx, canceled);
    }
}

/// <summary>Cancels the child after a number of ticks, whichever comes first.</summary>
public sealed class TimeLimited(IBehavior child, int ticks) : Decorator(child)
{
    protected override bool ShouldStop(BehaviorContext ctx) => Age >= ticks;
}

/// <summary>Cancels the child the moment a condition stops holding.</summary>
public sealed class Guarded(IBehavior child, Func<BehaviorContext, bool> condition) : Decorator(child)
{
    protected override bool ShouldStop(BehaviorContext ctx) => !condition(ctx);
}

/// <summary>Faces the nearest player each tick while the child runs.</summary>
public sealed class Facing(IBehavior child, float range) : Decorator(child)
{
    protected override void Alongside(BehaviorContext ctx)
    {
        Player player = ctx.NearestPlayer(range);

        if (player != null)
            ctx.Face(player.Center);
    }
}

/// <summary>Chaining helpers. These wrap, so they read left to right off any behaviour.</summary>
public static class BehaviorExtensions
{
    /// <summary>Gives up after <paramref name="ticks"/>, so a behaviour that might never finish can't hang the script.</summary>
    public static IBehavior For(this IBehavior behavior, int ticks) => new TimeLimited(behavior, ticks);

    /// <summary>Runs only while <paramref name="condition"/> holds.</summary>
    public static IBehavior While(this IBehavior behavior, Func<BehaviorContext, bool> condition)
        => new Guarded(behavior, condition);

    /// <summary>Keeps the NPC turned toward the nearest player throughout.</summary>
    public static IBehavior FacingNearestPlayer(this IBehavior behavior, float range = 400f)
        => new Facing(behavior, range);

    /// <summary>Repeats a freshly built copy <paramref name="count"/> times.</summary>
    public static IBehavior Repeat(this Func<IBehavior> factory, int count) => new Loop(factory, count);

    /// <summary>Repeats a freshly built copy forever.</summary>
    public static IBehavior Forever(this Func<IBehavior> factory) => new Loop(factory);

    /// <summary>Continues into <paramref name="next"/> once this finishes.</summary>
    public static IBehavior Then(this IBehavior behavior, IBehavior next) => new Sequence(behavior, next);
}
