using System.Collections.Generic;
using TrelamiumIII.Core.Components.NPCs;
using TrelamiumIII.Core.NPCs.Actors;
using TrelamiumIII.Core.NPCs.Controllers;
using TrelamiumIII.Utilities.Extensions;

namespace TrelamiumIII.Content.NPCs;

public class BodyguardNPC : WorldNPC
{
    public enum Phase
    {
        Idle = 0,
        Await,
        Follow,
        Attack,
        MagicMirror
    }

    public Phase State
    {
        get => (Phase)(int)NPC.ai[0];
        private set => NPC.ai[0] = (int)value;
    }

    public ref float StateTimer => ref NPC.ai[1];

    private NPCDirector director;

    private readonly int playerIndex;
    private float followDistance = 80f;
    private float teleportThreshold = 2000f;
    private float walkSpeed = 4.5f;
    private float acceleration = 0.1f;

    // Stuck detection mirrors vanilla's localAI[3] = position.X pattern.
    // If the NPC's X hasn't changed after a jump, it's stuck.
    private float lastJumpX;
    private int stuckTicks;
    private const int StuckThreshold = 60;


    #region inherited stuff
    public override List<string> SetNPCNameList() => ["Sir Reginald", "Geoffrey", "Siegfried", "Sammelot"];

    public override bool CanDespawn => true;
    #endregion

    protected override void SetAppearanceDefaults()
    {
        NPC.HitSound = SoundID.FemaleHit;
        NPC.DeathSound = SoundID.PlayerKilled;

        Male = true;
        ClothingStyle = (int)PlayerClothingStyleID.MaleDefault;
        HairType = 116;
        HairColor = new Color(255, 81, 54);

        SkinColor = new Color(255, 175, 135);
        EyeColor = new Color(153, 229, 80);

        ShirtColor = new Color(203, 219, 252);
        UnderShirtColor = new Color(203, 219, 252);
        PantsColor = new Color(203, 219, 252);

        ShoeColor = new Color(60, 50, 40);

        SetAccessory(0, ItemID.SWATHelmet, vanity: true);
        SetBodySlot(ItemID.JimsBreastplate, vanity: true);
        SetLegSlot(ItemID.JimsLeggings, vanity: true);
    }

    public override bool PreAI()
    {
        return true; // disabled base for now so it doesnt actually despawn
    }

    public override string GetChat()
    {
        return Main.rand.Next(2) switch
        {
            0 => "Keep your eyes peeled... you never know what's lurking about.",
            _ => "I got your back, boss."
        };
    }

    public override void AI()
    {
        base.AI();
        // Follows the player around and attacks enemies as they enter distance.

        // We need to design an attack machine and create a rendering tool for item usage/'player' animation

        NPC.TryEnable<NPCDirector>(out director);

        director.SetBehavior(NPC,
            new FollowBehavior(Main.LocalPlayer.whoAmI)
            .WithDistance(2f)
            .WithSpeed(6)
            );
        // TODO: make the follow behavior NPC Instance Aware, and make them form a line when >1
    }

    public void Update(NPC npc)
    {
        Player target = Main.player[playerIndex];
        if (!target.active || target.dead)
            return;

        float distX = Math.Abs(target.Center.X - npc.Center.X);
        float distTotal = Vector2.Distance(npc.Center, target.Center);

        // Teleport if too far
        if (distTotal > teleportThreshold)
        {
            Teleport(target);
            // todo: queue UseMagicMirror state and play an animation.
            // the bodyguard should be using DummyPlayer.HeldItem conventions or the same conventions for player item usage.
            return;
        }

        // Within follow distance, idle in place, face the player.
        if (distX <= followDistance)
        {
            // scan for NPCs of the same type, run a foreach check (?), increment follow distance per entity.
            // but nah. they'd all do it.
            // so maybe they'd have to be instance time aware -- who came first.

            BehaviorMovement.Decelerate(npc);
            npc.direction = target.Center.X > npc.Center.X ? 1 : -1;
            npc.spriteDirection = npc.direction;
            stuckTicks = 0;

            BehaviorMovement.ApplyTerrainCollision(npc);

            return;
        }

        // Walk toward player
        BehaviorMovement.WalkTowardX(npc, target.Center.X, walkSpeed, acceleration, followDistance);

        // Try wall jumps (vanilla pattern)
        bool jumped = BehaviorMovement.TryJump(npc);

        // Try gap jumps (non-vanilla, needed for follow)
        if (!jumped && BehaviorMovement.IsOnGround(npc))
            jumped = BehaviorMovement.TryGapJump(npc);

        // Track position after jumps for stuck detection
        if (jumped)
            lastJumpX = npc.position.X;

        // Stuck detection: if X hasn't changed meaningfully, flip direction
        if (npc.velocity.Y != 0f)
        {
            if (Math.Abs(npc.position.X - lastJumpX) < 2f)
            {
                stuckTicks++;
                if (stuckTicks > StuckThreshold)
                {
                    npc.direction *= -1;
                    npc.spriteDirection = npc.direction;
                    npc.velocity.X = npc.direction * walkSpeed;
                    stuckTicks = 0;
                    npc.netUpdate = true;
                }
            }
            else
            {
                stuckTicks = 0;
                lastJumpX = npc.position.X;
            }
        }
        else
        {
            stuckTicks = 0;
        }

        BehaviorMovement.ApplyTerrainCollision(npc);
    }

    private void Teleport(Player target)
    {
        // Find ground near the player
        Vector2 destination = target.Bottom + new Vector2(followDistance * -target.direction, 0f);

        int tileX = (int)(destination.X / 16f);
        int tileY = (int)(destination.Y / 16f);

        // Scan downward for solid ground
        for (int y = tileY; y < tileY + 12; y++)
        {
            Tile tile = Framing.GetTileSafely(tileX, y);
            if (tile.HasUnactuatedTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType])
            {
                NPC.position.X = tileX * 16f + 8f - NPC.width * 0.5f;
                NPC.position.Y = y * 16f - NPC.height;
                NPC.velocity = Vector2.Zero;
                NPC.netUpdate = true;

                stuckTicks = 0;
                lastJumpX = NPC.position.X;
                return;
            }
        }

        // Fallback: teleport directly to player feet.
        NPC.position = target.Bottom - new Vector2(NPC.width * 0.5f, NPC.height);
        NPC.velocity = Vector2.Zero;
        NPC.netUpdate = true;

        stuckTicks = 0;
        lastJumpX = NPC.position.X;
    }

    private void Attack(NPC target)
    {
        // TODO: bodyguards will have a randomized class -- either they use melee weapons, ranged, magic, or controller.
        // Summoner would be kind of odd, so they use any of these weapon categories, anyways...
        // different classes call for different state machines so clench your ass.

    }
}
