namespace TrelamiumIII.Core.Tiles.Seals;

/// <summary>Refuses breaking and building on sealed tiles.</summary>
public sealed class SealGlobalTile : GlobalTile
{
    public override bool CanKillTile(int i, int j, int type, ref bool blockDamaged) => !SealSystem.PreventsBreaking(i, j);

    public override bool CanExplode(int i, int j, int type) => !SealSystem.PreventsBreaking(i, j);

    public override bool CanReplace(int i, int j, int type, int tileTypeBeingPlaced) => !SealSystem.PreventsBreaking(i, j);

    public override bool CanPlace(int i, int j, int type) => !SealSystem.PreventsBuilding(i, j);
}

/// <inheritdoc cref="SealGlobalTile" />
public sealed class SealGlobalWall : GlobalWall
{
    public override bool CanExplode(int i, int j, int type) => !SealSystem.PreventsBreaking(i, j);

    public override bool CanPlace(int i, int j, int type) => !SealSystem.PreventsBuilding(i, j);

    public override void KillWall(int i, int j, int type, ref bool fail)
    {
        if (SealSystem.PreventsBreaking(i, j))
            fail = true;
    }
}

/// <summary>Stops building inside seals, and ripples the seal under every player's feet as they walk and land.</summary>
public sealed class SealPlayer : ModPlayer
{
    /// <summary>World pixels walked between footstep ripples.</summary>
    private const float Stride = 40f;

    /// <summary>Fall speed a landing has to beat to ripple.</summary>
    private const float LandingSpeed = 4f;

    private float strideDistance;

    /// <summary>Fastest fall since leaving the ground. The last airborne tick is clipped to the gap left, so it can't be used.</summary>
    private float peakFallSpeed;

    public override void PreUpdate()
    {
        if (SealSystem.PreventsBuilding((int)(Player.Center.X / 16f), (int)(Player.Center.Y / 16f)))
            Player.noBuilding = true;
    }

    public override void PostUpdate()
    {
        if (Main.dedServ || !SealSystem.Any)
            return;

        if (Player.velocity.Y != 0f)
        {
            peakFallSpeed = MathF.Max(peakFallSpeed, Player.velocity.Y * Player.gravDir);
            return;
        }

        float landing = peakFallSpeed;
        peakFallSpeed = 0f;

        if (FeetOnSeal(out Vector2 feet))
        {
            if (landing > LandingSpeed)
            {
                float force = MathHelper.Clamp((landing - LandingSpeed) / 8f + 0.35f, 0.35f, 1f);

                SealField.Strike(feet, (2.5f + 3f * force) * 16f, force, SealImpulseKind.Ripple, 40, sync: false);
                strideDistance = 0f;
            }
            else if ((strideDistance += Math.Abs(Player.velocity.X)) >= Stride)
            {
                float force = 0.4f + 0.3f * MathF.Min(Math.Abs(Player.velocity.X) / 8f, 1f);

                SealField.Strike(feet, 2.2f * 16f, force, SealImpulseKind.Ripple, 30, sync: false);
                strideDistance = 0f;
            }
        }
    }

    /// <summary>Whether the tile under the player's feet is sealed, and the point on its surface they stand on.</summary>
    private bool FeetOnSeal(out Vector2 feet)
    {
        float surface = Player.gravDir > 0f ? Player.Bottom.Y : Player.Top.Y;
        int i = (int)(Player.Center.X / 16f);
        int j = (int)((surface + 4f * Player.gravDir) / 16f);

        feet = new Vector2(Player.Center.X, surface);

        return SealSystem.TypeAt(i, j) is not null && Main.tile[i, j].HasTile;
    }
}

/// <summary>Ripples seals struck by a pickaxe, and sends a shockwave through those caught in an explosion.</summary>
public sealed class SealImpactSystem : ModSystem
{
    /// <summary>Furthest a shockwave travels, in tiles, however large the blast.</summary>
    private const float MaxBlastReach = 30f;

    public override void Load()
    {
        On_Player.PickTile += StrikeOnPick;
        On_Projectile.ExplodeTiles += StrikeOnExplosion;
    }

    public override void Unload()
    {
        On_Player.PickTile -= StrikeOnPick;
        On_Projectile.ExplodeTiles -= StrikeOnExplosion;
    }

    private static void StrikeOnPick(On_Player.orig_PickTile orig, Player self, int x, int y, int pickPower)
    {
        if (SealSystem.PreventsBreaking(x, y) && Main.tile[x, y].HasTile)
        {
            float force = 0.45f + 0.45f * MathHelper.Clamp(pickPower / 200f, 0f, 1f);

            SealField.Strike(new Vector2(x * 16f + 8f, y * 16f + 8f), 4.5f * 16f, force, SealImpulseKind.Ripple, 45);
        }

        orig(self, x, y, pickPower);
    }

    private static void StrikeOnExplosion(On_Projectile.orig_ExplodeTiles orig, Projectile self, Vector2 compareSpot, int radius, int minI, int maxI, int minJ, int maxJ, bool wallSplode)
    {
        if (SealSystem.Any && TouchesSeal(compareSpot, radius, minI, maxI, minJ, maxJ))
        {
            // Some callers pass a token radius and bound the blast by the box instead.
            float blast = MathF.Min(radius, MathF.Max(maxI - minI, maxJ - minJ) / 2f + 0.5f);
            float reach = MathF.Min(blast * 1.3f + 1.5f, MaxBlastReach) * 16f;

            SealField.Strike(compareSpot, reach, 1f, SealImpulseKind.Shockwave, 50);
        }

        orig(self, compareSpot, radius, minI, maxI, minJ, maxJ, wallSplode);
    }

    private static bool TouchesSeal(Vector2 centre, int radius, int minI, int maxI, int minJ, int maxJ)
    {
        for (int i = Math.Max(minI, 0); i <= maxI && i < Main.maxTilesX; i++)
        {
            for (int j = Math.Max(minJ, 0); j <= maxJ && j < Main.maxTilesY; j++)
            {
                if (Vector2.Distance(new Vector2(i, j), centre / 16f) < radius && SealSystem.PreventsBreaking(i, j))
                    return true;
            }
        }

        return false;
    }
}
