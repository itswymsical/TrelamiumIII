using System.Collections.Generic;
using TrelamiumIII.Core.Components.NPCs;
using TrelamiumIII.Core.NPCs.Actors;

namespace TrelamiumIII.Core.NPCs.Behaviors;

/// <summary>
/// Runs one behaviour on an NPC, and suppresses that NPC's own AI while it does.
///
/// <para>
/// There are two things you can hand it. A <em>script</em> is what the NPC is
/// normally doing, set with <see cref="Play"/>. An <em>interrupt</em> plays on top of
/// the script and the script resumes underneath when it ends - that's how a
/// conversation, a cutscene beat or a reaction gets in without the caller having to
/// remember and restore what was running.
/// </para>
///
/// <para>
/// Attach with <c>npc.TryEnable&lt;NPCDirector&gt;(out var director)</c>. It works on
/// any NPC, modded or vanilla, which is what lets a scene root the Guide mid-sentence
/// and give him back afterwards.
/// </para>
/// </summary>
public class NPCDirector : NPCComponent
{
    /// <summary>Interrupt stack. The last entry is what is actually running.</summary>
    private readonly List<IBehavior> interrupts = [];

    private BehaviorContext context;
    private IBehavior script;

    /// <summary>What is running: the topmost interrupt, or the script.</summary>
    public IBehavior Current => interrupts.Count > 0 ? interrupts[^1] : script;

    /// <summary>The innermost running behaviour, past any composites.</summary>
    public IBehavior Leaf => Current?.Leaf;

    /// <summary>True when the director is running anything at all.</summary>
    public bool IsBusy => Current is { State: BehaviorState.Active };

    /// <summary>True when something is playing on top of the script.</summary>
    public bool IsInterrupted => interrupts.Count > 0;

    /// <summary>Ticks the current behaviour has been running for.</summary>
    public int Age { get; private set; }

    /// <summary>
    /// Replaces the script, dropping any interrupts. The ordinary way to tell an NPC
    /// what it does.
    /// </summary>
    public void Play(NPC npc, IBehavior behavior)
    {
        BehaviorContext ctx = ContextFor(npc);

        ClearInterrupts(ctx);
        EndScript(ctx);

        script = behavior;
        Age = 0;

        script?.Enter(ctx);
        npc.netUpdate = true;
    }

    /// <summary>
    /// Plays <paramref name="behavior"/> on top of whatever is running. What's
    /// underneath is paused, not canceled, and picks up again when this ends.
    /// </summary>
    public void Interrupt(NPC npc, IBehavior behavior)
    {
        if (behavior == null)
            return;

        BehaviorContext ctx = ContextFor(npc);

        interrupts.Add(behavior);
        Age = 0;
        behavior.Enter(ctx);
        npc.netUpdate = true;
    }

    /// <summary>Cancels the topmost interrupt, resuming what was underneath.</summary>
    public void Resume(NPC npc)
    {
        if (interrupts.Count == 0)
            return;

        IBehavior top = interrupts[^1];
        interrupts.RemoveAt(interrupts.Count - 1);
        top.Exit(ContextFor(npc), canceled: true);
    }

    /// <summary>Stops everything and hands the NPC back to its own AI.</summary>
    public void Stop(NPC npc)
    {
        BehaviorContext ctx = ContextFor(npc);

        ClearInterrupts(ctx);
        EndScript(ctx);
    }

    public override bool PreAI(NPC npc)
    {
        if (!Enabled)
            return true;

        IBehavior current = Current;
        if (current == null)
            return true;

        BehaviorContext ctx = ContextFor(npc);
        Age++;

        current.Tick(ctx);

        // The behaviour may have called Play or Stop on us mid-tick, swapping the
        // stack out from under this frame. Bail rather than tearing down something
        // that was already replaced.
        if (!ReferenceEquals(Current, current))
            return false;

        if (current.State == BehaviorState.Active)
            return false;

        current.Exit(ctx, canceled: false);

        if (interrupts.Count > 0 && ReferenceEquals(interrupts[^1], current))
            interrupts.RemoveAt(interrupts.Count - 1);
        else
            script = null;

        Age = 0;
        return false;
    }

    public override void FindFrame(NPC npc, int frameHeight)
    {
        if (!Enabled)
            return;

        // Player-rendered actors get their frames from Player.PlayerFrame instead.
        if (npc.ModNPC is TerrarianNPC)
            return;

        if (Leaf is IFramedBehavior framed)
            framed.ModifyFrame(ContextFor(npc), frameHeight);
    }

    private void EndScript(BehaviorContext ctx)
    {
        if (script is { State: BehaviorState.Active })
            script.Exit(ctx, canceled: true);

        script = null;
    }

    private void ClearInterrupts(BehaviorContext ctx)
    {
        for (int i = interrupts.Count - 1; i >= 0; i--)
        {
            if (interrupts[i].State == BehaviorState.Active)
                interrupts[i].Exit(ctx, canceled: true);
        }

        interrupts.Clear();
    }

    /// <summary>
    /// The per-NPC context. Built lazily because a GlobalNPC instance exists before its
    /// NPC does, and reused after so behaviours can rely on identity.
    /// </summary>
    private BehaviorContext ContextFor(NPC npc) => context ??= new BehaviorContext(npc, this);

    public override string ToString() => Current == null ? "NPCDirector (idle)" : $"NPCDirector -> {Current}";
}
