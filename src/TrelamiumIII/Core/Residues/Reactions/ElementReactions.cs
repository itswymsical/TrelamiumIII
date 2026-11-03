using Terraria.Audio;
using TrelamiumIII.Core.Gas.Layers;
using TrelamiumIII.Core.Graphics.Fields;
using TrelamiumIII.Core.Residues.Types;

namespace TrelamiumIII.Core.Residues.Reactions;

// The reactions between the ground's own elements. See ResidueReaction for how pairs are found, ordered and run.

/// <summary>
///     Gel goes up when heat reaches it, whichever got there first: the gel is spent and the tile burns as
///     <see cref="BurnProfile.Combustion" />, which runs along any gel next to it and eats through the tile.
/// </summary>
public sealed class Combustion : ResidueReaction
{
    /// <summary>Heat that catches. Below this, the crust of cooled magma and the like, nothing happens.</summary>
    private const float IGNITION_HEAT = 0.1f;

    /// <summary>Weakest fire gel starts. It burns hot however thin it was laid; intensity caps a fire, and a low cap would never catch.</summary>
    private const float MIN_FIRE = 0.75f;

    public override ResidueElement First => ResidueElement.Flammable;

    public override ResidueElement Second => ResidueElement.Heat;

    public override bool React(in ReactionPair pair)
    {
        if (pair.Second.Potency < IGNITION_HEAT)
            return false;

        Spend(in pair, pair.First);

        int owner = pair.Second.Instance.Owner != ResidueInstance.NoOwner ? pair.Second.Instance.Owner : pair.First.Instance.Owner;

        BurnedResidue.Ignite(pair.Tile.X, pair.Tile.Y, BurnProfile.Combustion, MathHelper.Clamp(pair.First.Potency, MIN_FIRE, 1f), owner);

        ReactionEffects.Flare(pair.Tile);

        return true;
    }
}

/// <summary>
///     Crude goes up when heat reaches it, whichever got there first: the oil is spent into a fire that runs along any oil next
///     to it, and the tile blows a moment later. See <see cref="Fuses" />.
/// </summary>
public sealed class Detonation : ResidueReaction
{
    /// <summary>Heat that sets it off. Below this it waits.</summary>
    private const float IGNITION_HEAT = 0.1f;

    /// <summary>Weakest fire it starts, for the same reason gel has one. See <see cref="Combustion" />.</summary>
    private const float MIN_FIRE = 0.75f;

    public override ResidueElement First => ResidueElement.Volatile;

    public override ResidueElement Second => ResidueElement.Heat;

    public override bool React(in ReactionPair pair)
    {
        if (pair.Second.Potency < IGNITION_HEAT)
            return false;

        Spend(in pair, pair.First);

        int owner = pair.Second.Instance.Owner != ResidueInstance.NoOwner ? pair.Second.Instance.Owner : pair.First.Instance.Owner;

        BurnedResidue.Ignite(pair.Tile.X, pair.Tile.Y, BurnProfile.Fuse, MathHelper.Clamp(pair.First.Potency, MIN_FIRE, 1f), owner);
        Fuses.Light(pair.Tile, pair.First.Potency, owner, breaksTile: pair.First.Instance.Variant != CrudeOilResidue.SEEPED);

        ReactionEffects.Flare(pair.Tile);

        return true;
    }
}

/// <summary>
///     Water against heat. The stronger of the two wins, with the edge going to whichever was there first: water that wins
///     puts the fire out and is partly boiled off doing it, water that loses boils away and takes some of the fire with it.
/// </summary>
public sealed class Quench : ResidueReaction
{
    /// <summary>How much of the winner a fight costs, against an equal opponent.</summary>
    private const float WINNER_COST = 0.5f;

    public override ResidueElement First => ResidueElement.Water;

    public override ResidueElement Second => ResidueElement.Heat;

    public override bool React(in ReactionPair pair)
    {
        float water = pair.First.Potency;
        float heat = pair.Second.Potency;

        if (FirstWins(in pair))
        {
            Weaken(in pair, pair.Second, 1f);
            Weaken(in pair, pair.First, MathF.Min(heat / water, 1f) * WINNER_COST);
        }
        else
        {
            Spend(in pair, pair.First);
            Weaken(in pair, pair.Second, MathF.Min(water / heat, 1f) * WINNER_COST);
        }

        ReactionEffects.Hiss(pair.Tile);

        return true;
    }
}

/// <summary>
///     Heat melts frost into real water and leaves the tile soaked, whichever got there first. The soak takes the frost's
///     place in the tile's order and <see cref="Quench" /> then settles it against the fire, so frost that was there first
///     usually puts out what lit it, and frost blown onto a raging fire only adds to the steam.
/// </summary>
public sealed class Melt : ResidueReaction
{
    /// <summary>Heat that melts. Below this it waits, so a smoulder that takes hold still gets there.</summary>
    private const float MELTING_HEAT = 0.1f;

    /// <summary>Liquid a full-strength tile of frost melts into, out of 255 in a cell.</summary>
    private const int MELT_WATER = 56;

    public override ResidueElement First => ResidueElement.Cold;

    public override ResidueElement Second => ResidueElement.Heat;

    public override bool React(in ReactionPair pair)
    {
        if (pair.Second.Potency < MELTING_HEAT)
            return false;

        float frost = pair.First.Potency;

        Spend(in pair, pair.First);

        LiquidResidue.Soak(pair.Tile.X, pair.Tile.Y, frost);
        ResidueSystem.TakePlace<LiquidResidue>(pair.Tile.X, pair.Tile.Y, pair.First.Instance.Arrival);

        ReactionEffects.AddWater(pair.Tile, (int)(MELT_WATER * frost));
        ReactionEffects.Hiss(pair.Tile);

        return true;
    }
}

/// <summary>What a server tells clients to show, as reactions only run there.</summary>
internal enum ReactionEffect : byte
{
    Hiss,
    Flare,
    Blast,
}

/// <summary>What reactions look and sound like, and the one thing they do to the world's liquid.</summary>
internal static class ReactionEffects
{
    public static void Play(ReactionEffect effect, Point tile)
    {
        switch (effect)
        {
            case ReactionEffect.Hiss:
                Hiss(tile);
                break;
            case ReactionEffect.Flare:
                Flare(tile);
                break;
            case ReactionEffect.Blast:
                Blast(tile);
                break;
        }
    }

    /// <summary>Ticks between hisses, or painting a patch would stack one per tile.</summary>
    private const uint SOUND_GAP = 18;

    private const int STEAM_PUFFS = 4;

    private const int FLARE_SPARKS = 6;

    /// <summary>Ticks between blast sounds, shorter than for a hiss, and kept apart from it so the hiss of a fuse can't drown them out.</summary>
    private const uint BLAST_SOUND_GAP = 10;

    private const int BLAST_FLAMES = 16;

    private const int BLAST_SMOKE = 14;

    private const int BLAST_SPARKS = 10;

    private static uint lastSound;

    private static uint lastBlastSound;

    public static void Hiss(Point tile)
    {
        if (ResidueSystem.ShareEffect(ReactionEffect.Hiss, tile))
            return;

        Vector2 center = tile.ToWorldCoordinates();

        if (!Visible(center))
            return;

        Steam(center, STEAM_PUFFS);
        PlayOnce(SoundID.LiquidsWaterLava, center);
    }

    public static void Flare(Point tile)
    {
        if (ResidueSystem.ShareEffect(ReactionEffect.Flare, tile))
            return;

        Vector2 center = tile.ToWorldCoordinates();

        if (!Visible(center))
            return;

        for (int i = 0; i < FLARE_SPARKS; i++)
        {
            Dust spark = Dust.NewDustPerfect(
                center + Main.rand.NextVector2Circular(7f, 7f),
                DustID.Torch,
                new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-2.6f, -0.8f)),
                0,
                default,
                Main.rand.NextFloat(1f, 1.7f)
            );

            spark.noGravity = true;
        }

        PlayOnce(SoundID.Item20, center);
    }

    public static void Blast(Point tile)
    {
        if (ResidueSystem.ShareEffect(ReactionEffect.Blast, tile))
            return;

        Vector2 center = tile.ToWorldCoordinates();

        if (!Visible(center))
            return;

        // The fire's own field, with no owner, so it's only for show and lays nothing where it lands.
        if (FieldSystem.GetLayer<ThermiteFlaskField>() is { } fire)
        {
            for (int i = 0; i < BLAST_FLAMES; i++)
            {
                Vector2 velocity = Main.rand.NextVector2Circular(5.5f, 5.5f) - Vector2.UnitY * 1.6f;

                fire.SpawnClingNode(center + velocity * 2f, velocity, Main.rand.NextFloat(10f, 18f), Main.rand.NextFloat(18f, 32f));
            }
        }

        for (int i = 0; i < BLAST_SMOKE; i++)
        {
            Dust smoke = Dust.NewDustPerfect(center + Main.rand.NextVector2Circular(10f, 10f), DustID.Smoke, Main.rand.NextVector2Circular(2.6f, 2.6f) - Vector2.UnitY, 100, default, Main.rand.NextFloat(1.1f, 1.8f));

            smoke.noGravity = true;
        }

        for (int i = 0; i < BLAST_SPARKS; i++)
        {
            Dust spark = Dust.NewDustPerfect(center + Main.rand.NextVector2Circular(8f, 8f), DustID.Torch, Main.rand.NextVector2Circular(5f, 5f) - Vector2.UnitY * 2f, 0, default, Main.rand.NextFloat(1.2f, 2f));

            spark.noGravity = true;
        }

        Lighting.AddLight(center, 1.4f, 0.75f, 0.2f);

        if (Main.GameUpdateCount - lastBlastSound < BLAST_SOUND_GAP)
            return;

        lastBlastSound = Main.GameUpdateCount;
        SoundEngine.PlaySound(SoundID.Item14, center);
    }

    public static void Steam(Vector2 position, int count)
    {
        for (int i = 0; i < count; i++)
        {
            Dust steam = Dust.NewDustPerfect(
                position + Main.rand.NextVector2Circular(7f, 7f),
                DustID.Cloud,
                new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(-1.4f, -0.6f)),
                120,
                Color.White,
                Main.rand.NextFloat(0.7f, 1.2f)
            );

            steam.noGravity = true;
        }
    }

    /// <summary>
    ///     Puts water in the nearest open cell around a solid tile, above it first. A shallow film at most, and only where
    ///     the cell is dry or already water, so repeated melting can't flood anything or mix with lava.
    /// </summary>
    public static void AddWater(Point tile, int amount)
    {
        const int FULLEST = 96;

        if (amount <= 0 || Main.netMode == NetmodeID.MultiplayerClient)
            return;

        foreach (Point cell in (ReadOnlySpan<Point>)[new(tile.X, tile.Y - 1), new(tile.X - 1, tile.Y), new(tile.X + 1, tile.Y), new(tile.X, tile.Y + 1)])
        {
            if (!WorldGen.InWorld(cell.X, cell.Y, 1) || WorldGen.SolidTile(cell.X, cell.Y))
                continue;

            Tile open = Main.tile[cell.X, cell.Y];

            if (open.LiquidAmount >= FULLEST || (open.LiquidAmount > 0 && open.LiquidType != LiquidID.Water))
                continue;

            open.LiquidType = LiquidID.Water;
            open.LiquidAmount += (byte)amount;

            WorldGen.SquareTileFrame(cell.X, cell.Y);

            if (Main.netMode == NetmodeID.Server)
                NetMessage.sendWater(cell.X, cell.Y);

            return;
        }
    }

    private static void PlayOnce(in SoundStyle style, Vector2 position)
    {
        if (Main.GameUpdateCount - lastSound < SOUND_GAP)
            return;

        lastSound = Main.GameUpdateCount;
        SoundEngine.PlaySound(in style, position);
    }

    private static bool Visible(Vector2 world)
    {
        if (Main.dedServ)
            return false;

        Vector2 relative = world - Main.screenPosition;

        return relative.X >= -64f && relative.Y >= -64f && relative.X <= Main.screenWidth + 64f && relative.Y <= Main.screenHeight + 64f;
    }
}
