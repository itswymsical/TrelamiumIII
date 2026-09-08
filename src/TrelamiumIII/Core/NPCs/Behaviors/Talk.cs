using System.Collections.Generic;
using Terraria.GameContent.UI;

namespace TrelamiumIII.Core.NPCs.Behaviors;

/// <summary>Which frames a sprite-based NPC cycles through while it talks.</summary>
public class TalkFrames
{
    public int[] Frames { get; set; } = [0, 1];

    public int FrameSpeed { get; set; } = 8;
}

/// <summary>
/// Roots the NPC in place, faces the nearest player and animates a talking loop.
///
/// <para>
/// Runs until its predicate goes false - normally the dialogue system closing the
/// conversation - or until <see cref="Duration"/> ticks elapse. The NPC takes no
/// damage meanwhile, so a stray arrow can't end a scene.
/// </para>
/// </summary>
public sealed class Talk : Behavior, IFramedBehavior
{
    private static readonly Dictionary<int, TalkFrames> FramesByType = new()
    {
        [NPCID.Guide] = new TalkFrames { Frames = [19, 0], FrameSpeed = 10 },
    };

    private static readonly TalkFrames Default = new();

    private static readonly int[] DefaultEmotes =
    [
        EmoteID.EmoteNote,
        EmoteID.EmoteConfused,
        EmoteID.EmoteHappiness,
        EmoteID.WeatherSunny,
    ];

    private readonly Func<BehaviorContext, bool> stillTalking;

    private int frameCounter;
    private int frameIndex;
    private int emoteTimer;
    private int nextEmote;
    private bool restoreDamage;

    /// <summary>
    /// Talks until <paramref name="stillTalking"/> returns false. Null talks until the
    /// director is told otherwise.
    /// </summary>
    public Talk(Func<BehaviorContext, bool> stillTalking = null)
    {
        this.stillTalking = stillTalking;
    }

    /// <summary>Hard cap in ticks. -1 means no cap.</summary>
    public int Duration { get; set; } = -1;

    /// <summary>Average ticks between emote bubbles. 0 disables them.</summary>
    public int EmoteInterval { get; set; } = 200;

    public int[] EmotePool { get; set; }

    /// <summary>Make the NPC invulnerable for the conversation. On by default.</summary>
    public bool Invulnerable { get; set; } = true;

    /// <summary>Registers a talking frame loop for an NPC type. Call during load.</summary>
    public static void RegisterFrames(int npcType, TalkFrames frames) => FramesByType[npcType] = frames;

    protected override void Begin(BehaviorContext ctx)
    {
        frameCounter = 0;
        frameIndex = 0;
        emoteTimer = 0;
        nextEmote = EmoteInterval > 0 ? Main.rand.Next(EmoteInterval, (int)(EmoteInterval * 1.5f)) : 0;

        if (Invulnerable && !ctx.NPC.dontTakeDamage)
        {
            ctx.NPC.dontTakeDamage = true;
            restoreDamage = true;
        }
    }

    protected override void Run(BehaviorContext ctx)
    {
        NPC npc = ctx.NPC;

        BehaviorMovement.Decelerate(npc);
        BehaviorMovement.PinTownIdle(npc);

        Player listener = ctx.NearestPlayer();
        if (listener != null)
            ctx.Face(listener.Center);

        TickEmotes(npc);
        AdvanceFrames(npc);

        if (stillTalking != null && !stillTalking(ctx))
        {
            Finish();
            return;
        }

        if (Duration >= 0 && Age + 1 >= Duration)
            Finish();
    }

    protected override void End(BehaviorContext ctx, bool canceled)
    {
        if (restoreDamage)
        {
            ctx.NPC.dontTakeDamage = false;
            restoreDamage = false;
        }

        // Nudge vanilla town AI out of its locked idle so it resumes smoothly.
        ctx.NPC.localAI[3] = 30f;
    }

    public bool ModifyFrame(BehaviorContext ctx, int frameHeight)
    {
        int[] frames = FramesFor(ctx.NPC.type).Frames;
        if (frames.Length == 0)
            return false;

        ctx.NPC.frame.Y = frames[Math.Clamp(frameIndex, 0, frames.Length - 1)] * frameHeight;
        return true;
    }

    private void AdvanceFrames(NPC npc)
    {
        TalkFrames frames = FramesFor(npc.type);
        if (frames.Frames.Length == 0 || ++frameCounter < frames.FrameSpeed)
            return;

        frameCounter = 0;
        frameIndex = (frameIndex + 1) % frames.Frames.Length;
    }

    private void TickEmotes(NPC npc)
    {
        if (EmoteInterval <= 0 || ++emoteTimer < nextEmote)
            return;

        EmoteBubble.NewBubble(Main.rand.Next(EmotePool ?? DefaultEmotes), new WorldUIAnchor(npc), 120);
        emoteTimer = 0;
        nextEmote = Main.rand.Next(EmoteInterval, (int)(EmoteInterval * 1.5f));
    }

    private static TalkFrames FramesFor(int npcType)
        => FramesByType.TryGetValue(npcType, out TalkFrames frames) ? frames : Default;

    public Talk For(int ticks) { Duration = ticks; return this; }
    public Talk WithEmotes(int interval, int[] pool = null) { EmoteInterval = interval; EmotePool = pool; return this; }
    public Talk WithoutEmotes() { EmoteInterval = 0; return this; }
}
