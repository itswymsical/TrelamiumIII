using MonoMod.Cil;

namespace TrelamiumIII.Common.Systems;

/// <summary>
/// Makes <see cref="Liquid.DelWater"/> liquid-type aware. Vanilla assumes touching liquids of different types have
/// already merged; without this, a non-merging liquid resting on a partial tile of another type never leaves
/// <see cref="Main.liquid"/>, and settle loops run to their iteration caps.
/// </summary>
public class LiquidSettleSystem : ModSystem
{
    public override void Load()
    {
        IL_Liquid.DelWater += MakeDelWaterTypeAware;
    }

    public override void Unload()
    {
        IL_Liquid.DelWater -= MakeDelWaterTypeAware;
    }

    private void MakeDelWaterTypeAware(ILContext il)
    {
        ILCursor c = new(il);
        ILLabel? skipKeepAlive = null;

        if (!c.TryGotoNext(MoveType.After,
            i => i.MatchLdsfld<Liquid>(nameof(Liquid.stuck)),
            i => i.MatchBrtrue(out skipKeepAlive)))
        {
            Mod.Logger.Warn("Could not patch Liquid.DelWater, failed to match the \"!stuck\" check before kill is reset.");

            throw new ILPatchFailureException(Mod, il, null);
        }

        c.EmitLdarg0();
        c.EmitDelegate<Func<int, bool>>(BelowAcceptsLiquid);
        c.EmitBrfalse(skipKeepAlive!);

        c.Index = 0;
        int replaced = 0;

        while (c.TryGotoNext(MoveType.Before, i => i.MatchCall<Liquid>(nameof(Liquid.AddWater))))
        {
            c.EmitLdarg0();
            c.Remove();
            c.EmitDelegate<Action<int, int, int>>(AddWaterIfCanFlowIn);
            replaced++;
        }

        if (replaced == 0)
        {
            Mod.Logger.Warn("Could not patch Liquid.DelWater, failed to match any Liquid.AddWater call.");

            throw new ILPatchFailureException(Mod, il, null);
        }
    }

    private static bool BelowAcceptsLiquid(int liquidIndex)
    {
        Liquid liquid = Main.liquid[liquidIndex];
        Tile below = Main.tile[liquid.x, liquid.y + 1];

        return below.LiquidAmount == 0 || below.LiquidType == Main.tile[liquid.x, liquid.y].LiquidType;
    }

    /// <summary> Queues the neighbour at (x, y) only if the tile being removed is empty or holds the same liquid type. </summary>
    private static void AddWaterIfCanFlowIn(int x, int y, int liquidIndex)
    {
        Liquid liquid = Main.liquid[liquidIndex];
        Tile current = Main.tile[liquid.x, liquid.y];

        if (current.LiquidAmount == 0 || Main.tile[x, y].LiquidType == current.LiquidType)
            Liquid.AddWater(x, y);
    }
}
