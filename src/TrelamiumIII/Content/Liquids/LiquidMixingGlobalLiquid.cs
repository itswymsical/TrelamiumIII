using ModLiquidLib.ModLoader;
using TrelamiumIII.Core.Liquids;

namespace TrelamiumIII.Content.Liquids;

/// <summary>
///     Liquid contacts that need both sides. ModLiquidLib asks only one liquid's hook per contact, so a rule kept on
///     a ModLiquid fires or not depending on which tile updated; here (liquidX, liquidY) always holds
///     <c>type</c> and a neighbour holds <c>otherLiquid</c>.
///     Brews submerge in anything they meet. Acids dissolve water and honey, turn lava to obsidian through
///     <see cref="AcidLiquids.LiquidMerge" />, and sit beside everything else.
/// </summary>
public sealed class LiquidMixingGlobalLiquid : GlobalLiquid
{
    /// <summary>Acid spent per unit of water or honey it dissolves.</summary>
    private const float AcidSpentPerUnit = 0.5f;

    private static readonly Point[] Neighbours = [new(-1, 0), new(1, 0), new(0, -1), new(0, 1)];

    public override bool PreLiquidMerge(int liquidX, int liquidY, int tileX, int tileY, int type, int otherLiquid)
    {
        int brew = LiquidLoader.LiquidType<BrewLiquid>();
        int acid = LiquidLoader.LiquidType<AcidLiquids>();

        if (type == brew || otherLiquid == brew)
        {
            if (type == brew)
                Submerge(liquidX, liquidY, otherLiquid);
            else
                ForNeighbours(liquidX, liquidY, brew, (i, j) => Submerge(i, j, type));

            return false;
        }

        if (type == acid || otherLiquid == acid)
        {
            int other = type == acid ? otherLiquid : type;

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
    ///     Turns the brew at (i, j) into <paramref name="hostType" /> of the same volume, holding the brew as a pocket
    ///     inside it. The host keeps the condition of its neighbouring liquid.
    /// </summary>
    private static void Submerge(int i, int j, int hostType)
    {
        var brew = ModContent.GetInstance<BrewLiquid>();
        Tile tile = Main.tile[i, j];

        if (tile.LiquidAmount == 0 || tile.LiquidType != brew.Type)
            return;

        LiquidCondition condition = brew.Get(tile.LiquidCondition());
        byte hostCondition = 0;

        foreach (Point offset in Neighbours)
        {
            Tile next = Main.tile[i + offset.X, j + offset.Y];
            if (next.LiquidAmount > 0 && next.LiquidType == hostType)
            {
                hostCondition = next.LiquidCondition();
                break;
            }
        }

        int volume = tile.LiquidAmount;
        tile.LiquidType = hostType;
        LiquidConditionSystem.Set(i, j, hostCondition);
        SubmergedLiquidSystem.Submerge(condition, new Vector2(i * 16 + 8, j * 16 + 8), volume);

        Liquid.AddWater(i, j);

        if (Main.netMode == NetmodeID.Server)
            NetMessage.sendWater(i, j);
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

        LiquidConditionSystem.Fizz(x, y);
    }
}
