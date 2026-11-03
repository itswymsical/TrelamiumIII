using TrelamiumIII.Common.ChemicalClass;

namespace TrelamiumIII.Core.Residues.Reactions;

/// <summary>
///     What a blast does to whatever's near it. <see cref="Fuses" /> spawns one on the server, the same way vanilla's explosive
///     tile does, so it hits NPCs there and each player on their own machine. It draws nothing: the look is
///     <see cref="ReactionEffects.Blast" />.
/// </summary>
public sealed class ResidueBlast : ModProjectile
{
    private const int LIFE = 3;

    private const int NPC_HIT_COOLDOWN = 15;

    private const int PLAYER_FIRE_TICKS = 3 * 60;

    public override string Texture => Assets.Textures.VFX.RadialGradient.KEY;

    /// <summary>In pixels, from whoever spawned it.</summary>
    private float Radius => Projectile.ai[0];

    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 16;
        Projectile.friendly = true;
        Projectile.hostile = true;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.penetrate = -1;
        Projectile.timeLeft = LIFE;

        // A chain of these is one explosion to anything standing in it.
        Projectile.usesIDStaticNPCImmunity = true;
        Projectile.idStaticNPCHitCooldown = NPC_HIT_COOLDOWN;
    }

    public override void AI()
    {
        if (Projectile.localAI[0] != 0f)
            return;

        Projectile.localAI[0] = 1f;
        Projectile.Resize((int)(Radius * 2f), (int)(Radius * 2f));
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        => Vector2.Distance(Projectile.Center, targetHitbox.ClosestPointInRect(Projectile.Center)) <= Radius;

    /// <summary>A blast is a thermal hit like any other, so it afflicts, can set off a reaction, and shows on the body.</summary>
    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        => ChemicalPayload.Create(ElementProfile.Thermal)?.Hit(target);

    public override void OnHitPlayer(Player target, Player.HurtInfo info)
        => target.AddBuff(BuffID.OnFire, PLAYER_FIRE_TICKS);

    public override bool PreDraw(ref Color lightColor) => false;
}
