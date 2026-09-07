using System.Collections.Generic;
using System.Linq;

namespace TrelamiumIII.Core.NPCs.Controllers.Modules;

/// <summary>
/// Runs children one after another. Finishes when the last child finishes.
/// This is the replacement for the old director's behavior queue.
/// </summary>
public sealed class Sequence : BehaviorModule
{
    private readonly IBehaviorModule[] children;
    private int index;

    public Sequence(params IBehaviorModule[] children)
    {
        this.children = children ?? [];
    }

    private IBehaviorModule Current => index < children.Length ? children[index] : null;

    public override VanillaAIMode VanillaAI => Current?.VanillaAI ?? base.VanillaAI;

    public override IBehaviorModule ActiveLeaf => Current?.ActiveLeaf ?? this;

    protected override void Begin(BehaviorContext ctx)
    {
        index = 0;

        if (children.Length == 0)
            Finish();
        else
            children[0].OnEnter(ctx);
    }

    protected override void Run(BehaviorContext ctx)
    {
        IBehaviorModule current = Current;
        if (current == null)
        {
            Finish();
            return;
        }

        current.Tick(ctx);

        if (current.State == BehaviorState.Active)
            return;

        current.OnExit(ctx, canceled: false);
        index++;

        if (Current == null)
            Finish();
        else
            Current.OnEnter(ctx);
    }

    protected override void End(BehaviorContext ctx, bool canceled)
    {
        Current?.OnExit(ctx, canceled);
    }
}

/// <summary>
/// Runs every child each tick. Finishes when <see cref="RequireAll"/> children have
/// finished - all of them by default, or the first one when set false.
/// </summary>
public sealed class Parallel : BehaviorModule
{
    private readonly IBehaviorModule[] children;

    public Parallel(params IBehaviorModule[] children)
    {
        this.children = children ?? [];
    }

    /// <summary>True (default): finish when every child has. False: finish as soon as one does.</summary>
    public bool RequireAll { get; set; } = true;

    /// <summary>
    /// The child whose vanilla-AI mode and drawing win. Defaults to the first child,
    /// since a parallel branch is usually "one driver plus decorations".
    /// </summary>
    public int PrimaryChild { get; set; }

    private IBehaviorModule Primary =>
        PrimaryChild >= 0 && PrimaryChild < children.Length ? children[PrimaryChild] : null;

    public override VanillaAIMode VanillaAI => Primary?.VanillaAI ?? base.VanillaAI;

    public override IBehaviorModule ActiveLeaf => Primary?.ActiveLeaf ?? this;

    protected override void Begin(BehaviorContext ctx)
    {
        if (children.Length == 0)
        {
            Finish();
            return;
        }

        foreach (IBehaviorModule child in children)
            child.OnEnter(ctx);
    }

    protected override void Run(BehaviorContext ctx)
    {
        bool anyFinished = false;
        bool allFinished = true;

        foreach (IBehaviorModule child in children)
        {
            if (child.State == BehaviorState.Active)
            {
                child.Tick(ctx);

                if (child.State != BehaviorState.Active)
                    child.OnExit(ctx, canceled: false);
            }

            if (child.State == BehaviorState.Active)
                allFinished = false;
            else
                anyFinished = true;
        }

        if (RequireAll ? allFinished : anyFinished)
            Finish();
    }

    protected override void End(BehaviorContext ctx, bool canceled)
    {
        foreach (IBehaviorModule child in children)
        {
            if (child.State == BehaviorState.Active)
                child.OnExit(ctx, canceled);
        }
    }
}

/// <summary>
/// Repeats a child. Restarts it from scratch each time it finishes.
/// Pass <paramref name="count"/> -1 (the default) to loop forever.
/// </summary>
public sealed class Loop : BehaviorModule
{
    private readonly Func<IBehaviorModule> factory;
    private readonly int count;

    private IBehaviorModule current;
    private int completed;

    public Loop(Func<IBehaviorModule> factory, int count = -1)
    {
        this.factory = factory;
        this.count = count;
    }

    /// <summary>
    /// Loops a fixed module tree. Only safe when the children are stateless between
    /// runs; prefer the factory overload when in doubt, since modules keep state.
    /// </summary>
    public Loop(IBehaviorModule child, int count = -1)
        : this(() => child, count) { }

    public override VanillaAIMode VanillaAI => current?.VanillaAI ?? base.VanillaAI;

    public override IBehaviorModule ActiveLeaf => current?.ActiveLeaf ?? this;

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

        current.OnExit(ctx, canceled: false);
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
        current = factory();

        if (current == null)
            Finish();
        else
            current.OnEnter(ctx);
    }

    protected override void End(BehaviorContext ctx, bool canceled)
    {
        if (current?.State == BehaviorState.Active)
            current.OnExit(ctx, canceled);
    }
}

/// <summary>
/// A named state machine. Each state is a module plus a set of guarded transitions;
/// the first transition whose predicate passes wins, and the state's own completion
/// falls through to <see cref="StateEntry.Then"/>.
///
/// <para>
/// This is the shape most useful for actors that need to react - an NPC that idles,
/// notices a threat, closes distance, swings, and drops back to idle.
/// </para>
/// </summary>
public sealed class StateMachine : BehaviorModule
{
    /// <summary>One state: how to build it, where it goes next, and what can interrupt it.</summary>
    public sealed class StateEntry
    {
        internal readonly List<(string To, Func<BehaviorContext, bool> When)> transitions = [];

        public StateEntry(string name, Func<BehaviorContext, IBehaviorModule> factory)
        {
            Name = name;
            Factory = factory;
        }

        public string Name { get; }

        public Func<BehaviorContext, IBehaviorModule> Factory { get; }

        /// <summary>State entered when this one finishes on its own. Null ends the machine.</summary>
        public string Then { get; set; }

        /// <summary>
        /// Jumps to <paramref name="state"/> the moment <paramref name="when"/> is true,
        /// cutting the current module short. Checked before the module ticks.
        /// </summary>
        public StateEntry On(string state, Func<BehaviorContext, bool> when)
        {
            transitions.Add((state, when));
            return this;
        }

        /// <summary>Sets the state to fall into when this one completes normally.</summary>
        public StateEntry GoesTo(string state)
        {
            Then = state;
            return this;
        }
    }

    private readonly Dictionary<string, StateEntry> states = [];
    private readonly string initial;

    private StateEntry currentEntry;
    private IBehaviorModule current;
    private string pendingState;

    public StateMachine(string initialState)
    {
        initial = initialState;
    }

    /// <summary>The state currently running, or null before the machine starts.</summary>
    public string CurrentState => currentEntry?.Name;

    /// <summary>Ticks spent in the current state.</summary>
    public int StateAge { get; private set; }

    public override VanillaAIMode VanillaAI => current?.VanillaAI ?? base.VanillaAI;

    public override IBehaviorModule ActiveLeaf => current?.ActiveLeaf ?? this;

    /// <summary>Declares a state. The factory is called fresh on every entry.</summary>
    public StateEntry AddState(string name, Func<BehaviorContext, IBehaviorModule> factory)
    {
        var entry = new StateEntry(name, factory);
        states[name] = entry;
        return entry;
    }

    /// <summary>Declares a state whose module needs no context.</summary>
    public StateEntry AddState(string name, Func<IBehaviorModule> factory)
        => AddState(name, _ => factory());

    /// <summary>Forces a transition from outside the machine, taking effect next tick.</summary>
    public void GoTo(string state) => pendingState = state;

    protected override void Begin(BehaviorContext ctx)
    {
        Enter(ctx, initial, canceled: false);
    }

    protected override void Run(BehaviorContext ctx)
    {
        if (pendingState != null)
        {
            string next = pendingState;
            pendingState = null;
            Enter(ctx, next, canceled: true);
        }

        if (currentEntry == null)
        {
            Finish();
            return;
        }

        // Guards are checked before the tick so a reaction lands on the same frame
        // the condition became true, not one frame late.
        foreach ((string to, Func<BehaviorContext, bool> when) in currentEntry.transitions)
        {
            if (!when(ctx))
                continue;

            Enter(ctx, to, canceled: true);

            if (currentEntry == null)
            {
                Finish();
                return;
            }

            break;
        }

        StateAge++;
        current?.Tick(ctx);

        if (current == null || current.State == BehaviorState.Active)
            return;

        current.OnExit(ctx, canceled: false);

        string fallthrough = currentEntry.Then;
        Enter(ctx, fallthrough, canceled: false);

        if (currentEntry == null)
            Finish();
    }

    private void Enter(BehaviorContext ctx, string state, bool canceled)
    {
        if (current is { State: BehaviorState.Active })
            current.OnExit(ctx, canceled);

        current = null;
        currentEntry = null;
        StateAge = 0;

        if (state == null)
            return;

        if (!states.TryGetValue(state, out StateEntry entry))
        {
            TrelamiumIII.Instance?.Logger.Warn($"[StateMachine] No state named '{state}'. Machine will end.");
            return;
        }

        currentEntry = entry;
        current = entry.Factory(ctx);
        current?.OnEnter(ctx);
    }

    protected override void End(BehaviorContext ctx, bool canceled)
    {
        if (current is { State: BehaviorState.Active })
            current.OnExit(ctx, canceled);

        current = null;
        currentEntry = null;
    }
}

/// <summary>
/// Runs the first child whose condition passes, re-evaluating every tick. When a
/// higher-priority condition becomes true, the running child is canceled and
/// replaced. Finishes only when no branch applies.
/// </summary>
public sealed class Selector : BehaviorModule
{
    private readonly List<(Func<BehaviorContext, bool> When, Func<IBehaviorModule> Factory)> branches = [];

    private int activeBranch = -1;
    private IBehaviorModule current;

    public override VanillaAIMode VanillaAI => current?.VanillaAI ?? base.VanillaAI;

    public override IBehaviorModule ActiveLeaf => current?.ActiveLeaf ?? this;

    /// <summary>Adds a branch. Order is priority: earlier branches win.</summary>
    public Selector When(Func<BehaviorContext, bool> condition, Func<IBehaviorModule> factory)
    {
        branches.Add((condition, factory));
        return this;
    }

    /// <summary>Adds an always-true fallback branch. Put it last.</summary>
    public Selector Otherwise(Func<IBehaviorModule> factory)
        => When(_ => true, factory);

    protected override void Run(BehaviorContext ctx)
    {
        int wanted = branches.FindIndex(branch => branch.When(ctx));

        if (wanted != activeBranch)
        {
            if (current is { State: BehaviorState.Active })
                current.OnExit(ctx, canceled: true);

            activeBranch = wanted;
            current = wanted < 0 ? null : branches[wanted].Factory();
            current?.OnEnter(ctx);
        }

        if (current == null)
        {
            Finish();
            return;
        }

        current.Tick(ctx);

        if (current.State == BehaviorState.Active)
            return;

        // The branch ran out but its condition still holds; restart it next tick.
        current.OnExit(ctx, canceled: false);
        current = null;
        activeBranch = -1;
    }

    protected override void End(BehaviorContext ctx, bool canceled)
    {
        if (current is { State: BehaviorState.Active })
            current.OnExit(ctx, canceled);

        current = null;
        activeBranch = -1;
    }
}

/// <summary>Does nothing for a fixed number of ticks. The glue between real modules.</summary>
public sealed class Wait : BehaviorModule
{
    private readonly int ticks;

    public Wait(int ticks)
    {
        this.ticks = ticks;
    }

    protected override void Run(BehaviorContext ctx)
    {
        if (Age + 1 >= ticks)
            Finish();
    }
}

/// <summary>Runs a callback once and finishes. For one-shot effects inside a sequence.</summary>
public sealed class Do : BehaviorModule
{
    private readonly Action<BehaviorContext> action;

    public Do(Action<BehaviorContext> action)
    {
        this.action = action;
    }

    protected override void Run(BehaviorContext ctx)
    {
        action?.Invoke(ctx);
        Finish();
    }
}

/// <summary>Ticks a callback until it returns true. The escape hatch for one-off logic.</summary>
public sealed class Until : BehaviorModule
{
    private readonly Func<BehaviorContext, bool> tick;

    public Until(Func<BehaviorContext, bool> tick)
    {
        this.tick = tick;
    }

    protected override void Run(BehaviorContext ctx)
    {
        if (tick == null || tick(ctx))
            Finish();
    }
}

/// <summary>
/// Wraps a child and cancels it after <paramref name="ticks"/> ticks, whichever
/// comes first. Keeps a module that might never finish from hanging the program.
/// </summary>
public sealed class Timeout : BehaviorModule
{
    private readonly IBehaviorModule child;
    private readonly int ticks;

    public Timeout(IBehaviorModule child, int ticks)
    {
        this.child = child;
        this.ticks = ticks;
    }

    public override VanillaAIMode VanillaAI => child?.VanillaAI ?? base.VanillaAI;

    public override IBehaviorModule ActiveLeaf => child?.ActiveLeaf ?? this;

    protected override void Begin(BehaviorContext ctx) => child?.OnEnter(ctx);

    protected override void Run(BehaviorContext ctx)
    {
        if (child == null)
        {
            Finish();
            return;
        }

        child.Tick(ctx);

        if (child.State != BehaviorState.Active)
        {
            child.OnExit(ctx, canceled: false);
            Finish();
            return;
        }

        if (Age + 1 >= ticks)
        {
            child.OnExit(ctx, canceled: true);
            Finish();
        }
    }

    protected override void End(BehaviorContext ctx, bool canceled)
    {
        if (child is { State: BehaviorState.Active })
            child.OnExit(ctx, canceled);
    }
}

/// <summary>
/// Hands the NPC back to its own AI for a while.
///
/// <para>
/// This is the "let the boss be the boss" module: it runs in
/// <see cref="VanillaAIMode.Drive"/>, asking the director to execute the vanilla AI
/// box each tick, and finishes after a duration or when a condition is met. Put one
/// of these in a <see cref="Loop"/> alongside your own modules and you have a fight
/// that alternates between its original identity and something new.
/// </para>
/// </summary>
public sealed class RunVanilla : BehaviorModule
{
    private readonly int duration;
    private readonly Func<BehaviorContext, bool> until;

    public RunVanilla(int duration = -1, Func<BehaviorContext, bool> until = null)
    {
        this.duration = duration;
        this.until = until;
        WithVanillaAI(VanillaAIMode.Drive);
    }

    protected override void Run(BehaviorContext ctx)
    {
        ctx.RunVanillaAI();

        if (until != null && until(ctx))
        {
            Finish();
            return;
        }

        if (duration >= 0 && Age + 1 >= duration)
            Finish();
    }
}

/// <summary>
/// Wraps a child so the NPC's vanilla AI slots survive it untouched.
///
/// <para>
/// Vanilla bosses keep their whole pattern in <see cref="NPC.ai"/> and
/// <see cref="NPC.localAI"/>. Cut away to a modded attack without this and the boss
/// resumes from whatever garbage your module happened to leave in those slots; wrap
/// the module and it picks up its pattern exactly where it left off.
/// </para>
/// </summary>
public sealed class PreserveAI : BehaviorModule
{
    private readonly IBehaviorModule child;
    private VanillaAIState saved;

    public PreserveAI(IBehaviorModule child)
    {
        this.child = child;
    }

    public override VanillaAIMode VanillaAI => child?.VanillaAI ?? base.VanillaAI;

    public override IBehaviorModule ActiveLeaf => child?.ActiveLeaf ?? this;

    protected override void Begin(BehaviorContext ctx)
    {
        saved = VanillaAIState.Capture(ctx.NPC);
        child?.OnEnter(ctx);
    }

    protected override void Run(BehaviorContext ctx)
    {
        if (child == null)
        {
            Finish();
            return;
        }

        child.Tick(ctx);

        if (child.State == BehaviorState.Active)
            return;

        child.OnExit(ctx, canceled: false);
        Finish();
    }

    protected override void End(BehaviorContext ctx, bool canceled)
    {
        if (child is { State: BehaviorState.Active })
            child.OnExit(ctx, canceled);

        saved.RestoreAI(ctx.NPC);
    }
}

/// <summary>Fluent helpers for building module trees inline.</summary>
public static class Behavior
{
    public static Sequence Seq(params IBehaviorModule[] children) => new(children);

    public static Parallel All(params IBehaviorModule[] children) => new(children);

    public static Loop Forever(Func<IBehaviorModule> factory) => new(factory);

    public static Loop Repeat(int count, Func<IBehaviorModule> factory) => new(factory, count);

    public static Wait Pause(int ticks) => new(ticks);

    public static Do Once(Action<BehaviorContext> action) => new(action);

    public static Until While(Func<BehaviorContext, bool> tick) => new(tick);

    public static Timeout Capped(IBehaviorModule child, int ticks) => new(child, ticks);

    public static PreserveAI Preserving(IBehaviorModule child) => new(child);

    public static RunVanilla Vanilla(int duration = -1, Func<BehaviorContext, bool> until = null)
        => new(duration, until);

    /// <summary>Builds a sequence from an enumerable, skipping nulls.</summary>
    public static Sequence Seq(IEnumerable<IBehaviorModule> children)
        => new(children.Where(child => child != null).ToArray());
}
