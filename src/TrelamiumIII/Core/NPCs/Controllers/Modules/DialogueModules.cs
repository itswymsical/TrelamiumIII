using System.Collections.Generic;
using Terraria.GameContent.UI;

namespace TrelamiumIII.Core.NPCs.Controllers.Modules;

/// <summary>Which frames a sprite-based NPC cycles through while it talks.</summary>
public class DialogueFrameConfig
{
    public int[] TalkingFrames { get; set; } = [0, 1];

    public int TalkingFrameSpeed { get; set; } = 8;
}

/// <summary>
/// Roots the NPC in place, faces the nearest player and animates a talking loop.
///
/// <para>
/// Runs until something cancels it - normally the dialogue system closing the
/// conversation - or until <see cref="Duration"/> ticks elapse. While it runs the
/// NPC takes no damage, so a conversation can't be ended by a stray arrow.
/// </para>
/// </summary>
public sealed class Talk : BehaviorModule, IFramedModule
{
    private static readonly Dictionary<int, DialogueFrameConfig> FrameConfigs = new()
    {
        [NPCID.Guide] = new DialogueFrameConfig
        {
            TalkingFrames = [19, 0],
            TalkingFrameSpeed = 10,
        },
    };

    private static readonly DialogueFrameConfig Default = new();

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
    /// Talks until <paramref name="stillTalking"/> returns false. Pass null to talk
    /// until the director is told otherwise.
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
    public static void RegisterFrameConfig(int npcType, DialogueFrameConfig config)
        => FrameConfigs[npcType] = config;

    protected override void Begin(BehaviorContext ctx)
    {
        frameCounter = 0;
        frameIndex = 0;
        emoteTimer = 0;
        nextEmote = EmoteInterval > 0
            ? Main.rand.Next(EmoteInterval, (int)(EmoteInterval * 1.5f))
            : 0;

        if (Invulnerable && !ctx.NPC.dontTakeDamage)
        {
            ctx.NPC.dontTakeDamage = true;
            restoreDamage = true;
        }
    }

    protected override void Run(BehaviorContext ctx)
    {
        NPC npc = ctx.NPC;

        npc.velocity.X *= 0.8f;
        if (Math.Abs(npc.velocity.X) < 0.1f)
            npc.velocity.X = 0f;

        npc.ai[0] = 0f;
        npc.ai[1] = 300f;

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
        int[] frames = ConfigFor(ctx.NPC.type).TalkingFrames;
        if (frames.Length == 0)
            return false;

        ctx.NPC.frame.Y = frames[Math.Clamp(frameIndex, 0, frames.Length - 1)] * frameHeight;
        return true;
    }

    private void AdvanceFrames(NPC npc)
    {
        DialogueFrameConfig config = ConfigFor(npc.type);
        if (config.TalkingFrames.Length == 0)
            return;

        if (++frameCounter < config.TalkingFrameSpeed)
            return;

        frameCounter = 0;
        frameIndex = (frameIndex + 1) % config.TalkingFrames.Length;
    }

    private void TickEmotes(NPC npc)
    {
        if (EmoteInterval <= 0)
            return;

        if (++emoteTimer < nextEmote)
            return;

        EmoteBubble.NewBubble(Main.rand.Next(EmotePool ?? DefaultEmotes), new WorldUIAnchor(npc), 120);
        emoteTimer = 0;
        nextEmote = Main.rand.Next(EmoteInterval, (int)(EmoteInterval * 1.5f));
    }

    private static DialogueFrameConfig ConfigFor(int npcType)
        => FrameConfigs.TryGetValue(npcType, out DialogueFrameConfig config) ? config : Default;

    public Talk For(int ticks) { Duration = ticks; return this; }
    public Talk WithEmotes(int interval, int[] pool = null) { EmoteInterval = interval; EmotePool = pool; return this; }
    public Talk WithoutEmotes() { EmoteInterval = 0; return this; }
}
