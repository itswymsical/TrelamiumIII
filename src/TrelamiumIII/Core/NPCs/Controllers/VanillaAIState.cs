namespace TrelamiumIII.Core.NPCs.Controllers;

/// <summary>
/// A snapshot of the fields a vanilla AI keeps its pattern in.
///
/// <para>
/// Vanilla NPC AI is a state machine with no state of its own: everything lives in
/// <see cref="NPC.ai"/>, <see cref="NPC.localAI"/> and a handful of framing fields.
/// Taking control means overwriting those, so handing control back means putting
/// them the way we found them - otherwise King Slime wakes up mid-teleport with a
/// jump timer that came from a module that has nothing to do with him.
/// </para>
///
/// <para>
/// Deliberately a struct with flat fields rather than copied arrays: a director
/// captures one of these every time it takes control, and that should not allocate.
/// </para>
/// </summary>
public struct VanillaAIState
{
    private float ai0, ai1, ai2, ai3;
    private float localAI0, localAI1, localAI2, localAI3;

    private int aiStyle;
    private int target;
    private int direction;
    private int directionY;
    private int spriteDirection;
    private int timeLeft;

    private double frameCounter;
    private Rectangle frame;
    private float rotation;

    private Vector2 velocity;
    private Vector2 position;

    /// <summary>False for a default-constructed snapshot that was never filled in.</summary>
    public bool Valid { get; private set; }

    /// <summary>Reads the NPC's current AI state into a snapshot.</summary>
    public static VanillaAIState Capture(NPC npc)
    {
        return new VanillaAIState
        {
            ai0 = npc.ai[0],
            ai1 = npc.ai[1],
            ai2 = npc.ai[2],
            ai3 = npc.ai[3],

            localAI0 = npc.localAI[0],
            localAI1 = npc.localAI[1],
            localAI2 = npc.localAI[2],
            localAI3 = npc.localAI[3],

            aiStyle = npc.aiStyle,
            target = npc.target,
            direction = npc.direction,
            directionY = npc.directionY,
            spriteDirection = npc.spriteDirection,
            timeLeft = npc.timeLeft,

            frameCounter = npc.frameCounter,
            frame = npc.frame,
            rotation = npc.rotation,

            velocity = npc.velocity,
            position = npc.position,

            Valid = true,
        };
    }

    /// <summary>
    /// Restores the AI pattern - slots, style, target and framing - but leaves the
    /// NPC where it currently is. This is what you want after a modded interlude:
    /// the boss resumes its attack from where it paused, at its new position.
    /// </summary>
    public void RestoreAI(NPC npc)
    {
        if (!Valid)
            return;

        npc.ai[0] = ai0;
        npc.ai[1] = ai1;
        npc.ai[2] = ai2;
        npc.ai[3] = ai3;

        npc.localAI[0] = localAI0;
        npc.localAI[1] = localAI1;
        npc.localAI[2] = localAI2;
        npc.localAI[3] = localAI3;

        npc.aiStyle = aiStyle;
        npc.target = target;
        npc.timeLeft = timeLeft;

        npc.frameCounter = frameCounter;
        npc.frame = frame;

        npc.netUpdate = true;
    }

    /// <summary>
    /// Restores everything, position and velocity included. Only appropriate when
    /// the module was not supposed to move the NPC at all - a scripted pose, a
    /// dialogue beat - since it will teleport an NPC that has since walked away.
    /// </summary>
    public void RestoreFull(NPC npc)
    {
        if (!Valid)
            return;

        RestoreAI(npc);

        npc.direction = direction;
        npc.directionY = directionY;
        npc.spriteDirection = spriteDirection;
        npc.rotation = rotation;
        npc.velocity = velocity;
        npc.position = position;
    }
}
