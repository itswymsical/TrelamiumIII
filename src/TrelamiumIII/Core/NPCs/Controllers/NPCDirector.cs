using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using TrelamiumIII.Core.Components.NPCs;
using TrelamiumIII.Core.NPCs.Actors;

namespace TrelamiumIII.Core.NPCs.Controllers;

/// <summary>
/// An artificial hook into any NPC's AI loop.
///
/// <para>
/// The idea is that an NPC's AI is a box. Normally the game opens that box every
/// tick and whatever is inside runs. The director puts itself between the two: it
/// decides when the box runs, pauses its state while something else plays, and
/// injects behavior of our own on top. The fight stays recognisably itself while
/// doing things it never did.
/// </para>
///
/// <para>
/// It works the same for anything with an AI loop. A <see cref="WorldNPC"/> gets a
/// program that walks, talks and swings items through its <see cref="PlayerPuppet"/>.
/// A town NPC gets one for a scripted moment - a sparring match with the Guide -
/// and drops back to vanilla afterwards. A boss gets one that cuts between vanilla
/// patterns and new ones. Same lever, three uses.
/// </para>
///
/// <para>
/// Attach with <c>npc.TryEnable&lt;NPCDirector&gt;(out var director)</c>, then hand
/// it a module tree with <see cref="Play"/>. <see cref="AIDirectorSystem"/> does
/// both automatically for NPC types registered there.
/// </para>
/// </summary>
public class NPCDirector : NPCComponent
{
    /// <summary>Interrupt stack. The last entry is the module actually running.</summary>
    private readonly List<IBehaviorModule> interrupts = [];

    private BehaviorContext context;
    private IBehaviorModule program;
    private VanillaAIState controlSnapshot;
    private bool holdingControl;

    /// <summary>The module currently running - the topmost interrupt, or the program.</summary>
    public IBehaviorModule Current => interrupts.Count > 0 ? interrupts[^1] : program;

    /// <summary>The innermost running module, past any composites.</summary>
    public IBehaviorModule ActiveLeaf => Current?.ActiveLeaf;

    /// <summary>True when the director is running anything at all.</summary>
    public bool HasProgram => Current is { State: BehaviorState.Active };

    /// <summary>True when an interrupt is playing on top of the main program.</summary>
    public bool Interrupted => interrupts.Count > 0;

    /// <summary>
    /// True when the running module ticks in <see cref="PostAI"/> rather than
    /// <see cref="PreAI"/>. Player-rendered actors check this so their puppet is
    /// posed after the module has had its say, not before.
    /// </summary>
    public bool TicksInPostAI => Current is { State: BehaviorState.Active, VanillaAI: VanillaAIMode.Augment };

    /// <summary>Ticks since the director last took control. Reset when control is released.</summary>
    public int ControlAge { get; private set; }

    // Program control

    /// <summary>
    /// Replaces the program, dropping any interrupts. This is the ordinary way to
    /// tell an NPC what to do.
    /// </summary>
    public void Play(NPC npc, IBehaviorModule module)
    {
        BehaviorContext ctx = ContextFor(npc);

        ClearInterrupts(ctx, canceled: true);
        EndProgram(ctx, canceled: true);

        program = module;

        if (program == null)
        {
            ReleaseControl(npc);
            return;
        }

        TakeControl(npc);
        program.OnEnter(ctx);
        npc.netUpdate = true;
    }

    /// <summary>
    /// Plays <paramref name="module"/> on top of whatever is running. The module
    /// underneath is paused, not canceled, and resumes when the interrupt ends.
    /// Use this for reactions: a threat appears, dialogue starts, a phase triggers.
    /// </summary>
    public void Interrupt(NPC npc, IBehaviorModule module)
    {
        if (module == null)
            return;

        BehaviorContext ctx = ContextFor(npc);

        TakeControl(npc);
        interrupts.Add(module);
        module.OnEnter(ctx);
        npc.netUpdate = true;
    }

    /// <summary>Cancels the topmost interrupt, resuming whatever was underneath.</summary>
    public void Resume(NPC npc)
    {
        if (interrupts.Count == 0)
            return;

        BehaviorContext ctx = ContextFor(npc);
        IBehaviorModule top = interrupts[^1];
        interrupts.RemoveAt(interrupts.Count - 1);
        top.OnExit(ctx, canceled: true);
    }

    /// <summary>Stops everything and hands the NPC back to its own AI.</summary>
    public void Stop(NPC npc)
    {
        BehaviorContext ctx = ContextFor(npc);

        ClearInterrupts(ctx, canceled: true);
        EndProgram(ctx, canceled: true);
        ReleaseControl(npc);
    }

    // Lifecycle

    public override bool PreAI(NPC npc)
    {
        if (!Enabled)
            return true;

        IBehaviorModule current = Current;
        if (current == null)
            return true;

        // Augment: vanilla runs untouched and we tick afterwards, in PostAI.
        if (current.VanillaAI == VanillaAIMode.Augment)
            return true;

        BehaviorContext ctx = ContextFor(npc);
        VanillaAIMode mode = current.VanillaAI;

        ctx.vanillaRequested = false;
        Advance(ctx);

        // Drive: the module asked for the box to run, so run it by hand. Doing this
        // here rather than by returning true keeps the ordering explicit - our
        // module has already had its say this tick.
        if (mode == VanillaAIMode.Drive && ctx.vanillaRequested && npc.active)
            npc.VanillaAI();

        return false;
    }

    public override void PostAI(NPC npc)
    {
        if (!Enabled || !TicksInPostAI)
            return;

        Advance(ContextFor(npc));

        // ModNPC.PostAI already ran, so the actor skipped its own puppet update and
        // left it to us - see WorldNPC.PostAI.
        if (npc.ModNPC is WorldNPC actor)
            actor.Puppet?.Update();
    }

    /// <summary>
    /// Runs one tick of the active module and advances the stack when it ends.
    /// </summary>
    private void Advance(BehaviorContext ctx)
    {
        NPC npc = ctx.NPC;
        IBehaviorModule current = Current;

        if (current == null)
            return;

        ControlAge++;

        current.Tick(ctx);

        // The module may have called Play or Stop on us mid-tick, which swaps the
        // stack out from under this frame. Bail rather than tearing down a module
        // that was already replaced.
        if (!ReferenceEquals(Current, current))
            return;

        if (current.State == BehaviorState.Active)
            return;

        current.OnExit(ctx, canceled: false);

        if (interrupts.Count > 0 && ReferenceEquals(interrupts[^1], current))
        {
            interrupts.RemoveAt(interrupts.Count - 1);
            return;
        }

        program = null;
        ReleaseControl(npc);
    }

    public override void FindFrame(NPC npc, int frameHeight)
    {
        if (!Enabled)
            return;

        // Player-rendered actors get their frames from Player.PlayerFrame instead.
        if (npc.ModNPC is WorldNPC)
            return;

        if (ActiveLeaf is IFramedModule framed)
            framed.ModifyFrame(ContextFor(npc), frameHeight);
    }

    public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        if (!Enabled)
            return;

        if (ActiveLeaf is IDrawnModule drawn)
            drawn.Draw(ContextFor(npc), spriteBatch, screenPos, drawColor);
    }

    // Control bookkeeping

    /// <summary>
    /// Snapshots the vanilla AI pattern the first time the director takes over, so
    /// <see cref="ReleaseControl"/> can put it back.
    /// </summary>
    private void TakeControl(NPC npc)
    {
        if (holdingControl)
            return;

        controlSnapshot = VanillaAIState.Capture(npc);
        holdingControl = true;
        ControlAge = 0;
    }

    private void ReleaseControl(NPC npc)
    {
        if (!holdingControl)
            return;

        controlSnapshot.RestoreAI(npc);
        holdingControl = false;
        ControlAge = 0;
    }

    private void EndProgram(BehaviorContext ctx, bool canceled)
    {
        if (program is { State: BehaviorState.Active })
            program.OnExit(ctx, canceled);

        program = null;
    }

    private void ClearInterrupts(BehaviorContext ctx, bool canceled)
    {
        for (int i = interrupts.Count - 1; i >= 0; i--)
        {
            if (interrupts[i].State == BehaviorState.Active)
                interrupts[i].OnExit(ctx, canceled);
        }

        interrupts.Clear();
    }

    /// <summary>
    /// The per-NPC context. Built lazily because GlobalNPC instances exist before
    /// their NPC does, and reused thereafter so modules can rely on identity.
    /// </summary>
    private BehaviorContext ContextFor(NPC npc)
        => context ??= new BehaviorContext(npc, this);

    public override string ToString()
        => Current == null ? "NPCDirector (idle)" : $"NPCDirector -> {Current} / {ActiveLeaf}";
}
