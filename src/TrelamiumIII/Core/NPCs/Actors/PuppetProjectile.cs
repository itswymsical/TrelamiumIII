using Terraria.DataStructures;

namespace TrelamiumIII.Core.NPCs.Actors;

/// <summary>
/// Records which NPC fired a projectile.
///
/// <para>
/// <see cref="Projectile.owner"/> is a player index and always will be - hundreds of
/// vanilla AIs index <c>Main.player[owner]</c> with it - so a puppet's shots are still
/// owned by a player slot for the engine's purposes. What they are *attributed* to
/// lives here instead: who fired it, what side it fights for, and whether the kill
/// belongs to anybody.
/// </para>
///
/// <para>
/// Ownership is captured for everything created inside <see cref="PlayerPuppet"/>'s
/// firing scope, not just the first projectile, and is inherited down spawn chains -
/// so a weapon whose own <c>Shoot</c> hook spawns three projectiles, one of which
/// splits later, ends up with all of them attributed to the NPC that swung it.
/// </para>
/// </summary>
public sealed class PuppetProjectile : GlobalProjectile
{
    /// <summary>
    /// The NPC being credited for projectiles spawned right now, or -1.
    /// Set by <see cref="PlayerPuppet"/> for the duration of a single shot.
    /// </summary>
    internal static int FiringNPC = -1;

    /// <summary>Allegiance of the NPC currently firing. Decides friendly/hostile flags.</summary>
    internal static PuppetAllegiance FiringAllegiance = PuppetAllegiance.Harmless;

    public override bool InstancePerEntity => true;

    /// <summary>Index into <see cref="Main.npc"/> of the NPC that fired this, or -1.</summary>
    public int OwnerNPC { get; private set; } = -1;

    /// <summary>Which side this projectile was fired for.</summary>
    public PuppetAllegiance Allegiance { get; private set; } = PuppetAllegiance.Harmless;

    /// <summary>True when an NPC fired this and that NPC is still alive.</summary>
    public bool HasLivingOwner => OwnerNPC >= 0 && OwnerNPC < Main.maxNPCs && Main.npc[OwnerNPC].active;

    /// <summary>The NPC that fired this, or null.</summary>
    public NPC Owner => HasLivingOwner ? Main.npc[OwnerNPC] : null;

    public override void OnSpawn(Projectile projectile, IEntitySource source)
    {
        if (FiringNPC >= 0)
        {
            Adopt(projectile, FiringNPC, FiringAllegiance);
            return;
        }

        // Inherit down spawn chains: a projectile spawned by a puppet's projectile
        // belongs to the same NPC.
        if (source is EntitySource_Parent { Entity: Projectile parent }
            && parent.TryGetGlobalProjectile(out PuppetProjectile parentOwnership)
            && parentOwnership.OwnerNPC >= 0)
        {
            Adopt(projectile, parentOwnership.OwnerNPC, parentOwnership.Allegiance);
        }
    }

    private void Adopt(Projectile projectile, int npcIndex, PuppetAllegiance allegiance)
    {
        OwnerNPC = npcIndex;
        Allegiance = allegiance;

        // npcProj is vanilla's own marker for "a town NPC fired this". It keeps the
        // shot off the owning player's damage stats and out of loot attribution.
        projectile.npcProj = true;
        projectile.noDropItem = true;

        switch (allegiance)
        {
            case PuppetAllegiance.Ally:
                projectile.friendly = true;
                projectile.hostile = false;
                break;

            case PuppetAllegiance.Enemy:
                projectile.friendly = false;
                projectile.hostile = true;
                break;

            default:
                projectile.friendly = false;
                projectile.hostile = false;
                break;
        }
    }

    /// <summary>An NPC's own shots should not hit itself.</summary>
    public override bool? CanHitNPC(Projectile projectile, NPC target)
    {
        if (HasLivingOwner && target.whoAmI == OwnerNPC)
            return false;

        return null;
    }

    public override void PostDraw(Projectile projectile, Color lightColor)
    {
        if (PuppetDebug.ShowHitboxes && OwnerNPC >= 0)
            PuppetDebug.DrawHitbox(Main.spriteBatch, projectile.Hitbox, Color.Orange);
    }
}
