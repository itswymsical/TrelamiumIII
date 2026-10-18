namespace TrelamiumIII.Core.NPCs.Combat;

/// <summary>Distances (pixels) and cadence (ticks) the <see cref="WorldNPC"/> fight state uses with one weapon.</summary>
/// <param name="DetectRange">How close a threat must be to start a fight.</param>
/// <param name="PreferredRange">Distance the NPC walks to before holding position.</param>
/// <param name="StrikeRange">Distance within which the weapon is used.</param>
/// <param name="RetreatRange">Backs away from targets closer than this. 0 holds ground.</param>
/// <param name="Cooldown">Ticks between uses, after the use animation.</param>
/// <param name="CooldownVariance">Random extra ticks, 0 to this exclusive, added to each cooldown.</param>
/// <param name="ArcHeight">Aim raised by this at <paramref name="StrikeRange" />, scaled by distance, for projectile gravity.</param>
/// <param name="LeashRange">Furthest the NPC walks from its anchor in a fight; threats beyond it plus the strike range are ignored. 0 is unlimited.</param>
public readonly record struct CombatStyle(
    float DetectRange,
    float PreferredRange,
    float StrikeRange,
    float RetreatRange,
    int Cooldown,
    int CooldownVariance,
    float ArcHeight,
    float LeashRange)
{
    public static readonly CombatStyle Melee = new(240f, 30f, 54f, 0f, 14, 16, 0f, 320f);

    public static readonly CombatStyle Thrown = new(360f, 130f, 260f, 64f, 30, 30, 24f, 320f);

    public static readonly CombatStyle Ranged = new(420f, 190f, 360f, 96f, 36, 30, 0f, 320f);

    /// <summary>Melee for swung weapons, thrown for consumables and throwing-class items, ranged otherwise.</summary>
    public static CombatStyle For(int itemType)
    {
        Item item = ContentSamples.ItemsByType[itemType];

        if (!item.noMelee)
            return Melee;

        if (item.consumable || item.CountsAsClass(DamageClass.Throwing))
            return Thrown;

        return item.useAmmo == AmmoID.Arrow ? Ranged with { ArcHeight = 32f } : Ranged;
    }
}

/// <summary>Vanilla aiStyle 7's danger test, shared by the <see cref="WorldNPC"/> base AI.</summary>
public static class Threats
{
    /// <summary>
    /// Hostile, deals contact damage, chaseable, within <paramref name="range" /> of <paramref name="npc" />, in line of
    /// sight unless it ignores tiles, and able to hit <paramref name="npc" />.
    /// </summary>
    public static bool IsThreat(NPC npc, NPC other, float range)
    {
        if (!other.active || other.whoAmI == npc.whoAmI || other.friendly || other.damage <= 0 || !other.CanBeChasedBy(npc))
            return false;

        if (Vector2.DistanceSquared(npc.Center, other.Center) >= range * range)
            return false;

        if (!other.noTileCollide && !Collision.CanHit(npc.Center, 0, 0, other.Center, 0, 0))
            return false;

        return NPCLoader.CanHitNPC(other, npc);
    }

    /// <summary>The closest NPC passing <see cref="IsThreat" /> and <paramref name="filter" />, or null.</summary>
    public static NPC? Nearest(NPC npc, float range, Func<NPC, bool>? filter = null)
    {
        NPC? best = null;
        float bestDistance = float.MaxValue;

        foreach (NPC other in Main.ActiveNPCs)
        {
            float distance = Vector2.DistanceSquared(npc.Center, other.Center);
            if (distance >= bestDistance || !IsThreat(npc, other, range) || (filter != null && !filter(other)))
                continue;

            best = other;
            bestDistance = distance;
        }

        return best;
    }
}
