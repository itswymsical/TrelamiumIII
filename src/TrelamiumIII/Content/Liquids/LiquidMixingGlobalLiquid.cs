using ModLiquidLib.ModLoader;
using TrelamiumIII.Core.Liquids;

namespace TrelamiumIII.Content.Liquids;

/// <summary>
///     Liquid contacts that need both sides. ModLiquidLib asks only one liquid's hook per contact, so a rule kept on
///     a ModLiquid fires or not depending on which tile updated; here (liquidX, liquidY) always holds
///     <c>type</c> and a neighbour holds <c>otherLiquid</c>.
///     Brews and solvents submerge in anything they meet. Acids dissolve water and honey, neutralise alkalis into
///     brine, turn lava to obsidian through <see cref="AcidicLiquids.LiquidMerge" />, and sit beside everything else.
///     Alkalis likewise only merge with lava.
/// </summary>
public sealed class LiquidMixingGlobalLiquid : GlobalLiquid
{
    /// <summary>Acid spent per unit of water or honey it dissolves, or of alkali it neutralises.</summary>
    private const float AcidSpentPerUnit = 0.5f;

    private static readonly Point[] Neighbours = [new(-1, 0), new(1, 0), new(0, -1), new(0, 1)];

    public override bool PreLiquidMerge(int liquidX, int liquidY, int tileX, int tileY, int type, int otherLiquid)
    {
        int acid = LiquidLoader.LiquidType<AcidicLiquids>();
        int alkali = LiquidLoader.LiquidType<AlkalineLiquids>();

        foreach (int pocketed in (ReadOnlySpan<int>)[LiquidLoader.LiquidType<MuddledBrewLiquid>(), LiquidLoader.LiquidType<SolventLiquid>()])
        {
            if (type != pocketed && otherLiquid != pocketed)
                continue;

            if (type == pocketed)
                Submerge(liquidX, liquidY, pocketed, otherLiquid);
            else
                ForNeighbours(liquidX, liquidY, pocketed, (i, j) => Submerge(i, j, pocketed, type));

            return false;
        }

        if (type == acid || otherLiquid == acid)
        {
            int other = type == acid ? otherLiquid : type;

            if (other == alkali)
            {
                if (type == acid)
                    ForNeighbours(liquidX, liquidY, alkali, (i, j) => Neutralise(i, j, liquidX, liquidY));
                else
                    ForNeighbours(liquidX, liquidY, acid, (i, j) => Neutralise(liquidX, liquidY, i, j));

                return false;
            }

            if (other is LiquidID.Water or LiquidID.Honey)
            {
                if (type == acid)
                    ForNeighbours(liquidX, liquidY, other, (i, j) => Dissolve(i, j, liquidX, liquidY));
                else
                    ForNeighbours(liquidX, liquidY, acid, (i, j) => Dissolve(liquidX, liquidY, i, j));

                return false;
            }

            return other == LiquidID.Lava;
        }

        if (type == alkali || otherLiquid == alkali)
            return (type == alkali ? otherLiquid : type) == LiquidID.Lava;

        return true;
    }

    private static void ForNeighbours(int x, int y, int liquidType, Action<int, int> action)
    {
        foreach (Point offset in Neighbours)
        {
            int i = x + offset.X;
            int j = y + offset.Y;
            Tile tile = Main.tile[i, j];

            if (tile.LiquidAmount > 0 && tile.LiquidType == liquidType)
                action(i, j);
        }
    }

    /// <summary>
    ///     Turns the brew or solvent at (i, j) into <paramref name="hostType" /> of the same volume, holding it as a
    ///     pocket inside it. The host keeps the condition of its neighbouring liquid.
    /// </summary>
    private static void Submerge(int i, int j, int pocketedType, int hostType)
    {
        Tile tile = Main.tile[i, j];

        if (tile.LiquidAmount == 0 || tile.LiquidType != pocketedType || CustomLiquidSet.ByType(pocketedType) is not { } pocketed)
            return;

        InternalLiquid liquid = pocketed.Get(tile.InternalLiquid());
        byte hostLiquid = 0;

        foreach (Point offset in Neighbours)
        {
            Tile next = Main.tile[i + offset.X, j + offset.Y];
            if (next.LiquidAmount > 0 && next.LiquidType == hostType)
            {
                hostLiquid = next.InternalLiquid();
                break;
            }
        }

        int volume = tile.LiquidAmount;
        tile.LiquidType = hostType;
        LiquidInternalSystem.Set(i, j, hostLiquid);
        SubmergedLiquidSystem.Submerge(liquid, new Vector2(i * 16 + 8, j * 16 + 8), volume);

        Liquid.AddWater(i, j);

        if (Main.netMode == NetmodeID.Server)
            NetMessage.sendWater(i, j);
    }

    /// <summary>Turns the alkali at (x, y) into brine of the same volume, spending acid at (acidX, acidY) in proportion.</summary>
    private static void Neutralise(int x, int y, int acidX, int acidY)
    {
        Tile alkali = Main.tile[x, y];
        Tile acid = Main.tile[acidX, acidY];

        int spent = (int)MathF.Ceiling(alkali.LiquidAmount * AcidSpentPerUnit);
        alkali.LiquidType = LiquidID.Water;
        LiquidInternalSystem.Set(x, y, WaterLiquids.Instance.Brine.Index);
        acid.LiquidAmount = (byte)Math.Max(acid.LiquidAmount - spent, 0);

        Liquid.AddWater(x, y);
        Liquid.AddWater(acidX, acidY);

        if (Main.netMode == NetmodeID.Server)
        {
            NetMessage.sendWater(x, y);
            NetMessage.sendWater(acidX, acidY);
        }

        LiquidInternalSystem.Fizz(x, y);
    }

    /// <summary>Empties the water or honey at (x, y), spending acid at (acidX, acidY) in proportion.</summary>
    private static void Dissolve(int x, int y, int acidX, int acidY)
    {
        Tile dissolved = Main.tile[x, y];
        Tile acid = Main.tile[acidX, acidY];

        int spent = (int)MathF.Ceiling(dissolved.LiquidAmount * AcidSpentPerUnit);
        dissolved.LiquidAmount = 0;
        acid.LiquidAmount = (byte)Math.Max(acid.LiquidAmount - spent, 0);

        Liquid.AddWater(x, y);
        Liquid.AddWater(acidX, acidY);

        if (Main.netMode == NetmodeID.Server)
        {
            NetMessage.sendWater(x, y);
            NetMessage.sendWater(acidX, acidY);
        }

        LiquidInternalSystem.Fizz(x, y);
    }
}
