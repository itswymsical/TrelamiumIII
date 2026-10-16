using Terraria.Localization;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Liquids;

namespace TrelamiumIII.Content.Liquids;

/// <summary>
///     Conditions on vanilla water: fresh by default, or brine. Brine tiles stay <see cref="LiquidID.Water" />, so
///     vanilla and other mods treat them as water (fishing, spawns, sponges, buckets).
/// </summary>
public sealed class WaterLiquids : LiquidConditionSet, ILoadable, ILiquidSurface
{
    public static WaterLiquids Instance { get; private set; } = null!;

    /// <summary>Recipe condition: standing beside brine, the way vanilla checks for water.</summary>
    public static Condition NearBrine { get; private set; } = null!;

    public LiquidCondition Fresh { get; private set; } = null!;

    public LiquidCondition Brine { get; private set; } = null!;

    public override int LiquidType => LiquidID.Water;

    public override string FullName => "Terraria/Water";

    public override string Name => "Water";

    /// <summary>Thin salt strands drifting through brine.</summary>
    public LiquidSurface Surface { get; } = new("Brine")
    {
        ColorA = new Vector3(0.95f, 0.97f, 1f),
        ColorB = new Vector3(0.72f, 0.80f, 0.90f),
        Strength = 0.55f,
    };

    public bool HasSurface(int i, int j) => ConditionAt(i, j) == Brine;

    public void Load(Mod mod)
    {
        Fresh = AddCondition(new LiquidCondition("Fresh"));
        Brine = AddCondition(new LiquidCondition("Brine") { Tint = new Color(222, 208, 235) });
        Register();

        Instance = this;
        NearBrine = new Condition(mod.GetLocalization("Conditions.NearBrine", () => "Near Brine"), () => IsNear(Main.LocalPlayer, Brine));
    }

    public void Unload()
    {
        ClearRegistry();
        Instance = null!;
        NearBrine = null!;
    }

    public override string ConditionDisplayName(LiquidCondition condition)
        => Language.GetOrRegister(TrelamiumIII.Instance.GetLocalizationKey($"Liquids.Water.Conditions.{condition.Name}"), () => condition.Name).Value;

    /// <remarks>Waterfall styles only load on clients.</remarks>
    public override int? WaterfallStyle(LiquidCondition condition) => condition == Brine ? ModContent.GetInstance<BrineLiquidfall>()?.Slot : null;

    /// <summary>Whether a near-full water tile in <paramref name="condition" /> is in the area vanilla checks for adjacent water.</summary>
    public static bool IsNear(Player player, LiquidCondition condition)
    {
        int centerX = (int)((player.position.X + player.width * 0.5f) / 16f);
        int feetY = (int)((player.position.Y + player.height) / 16f);

        for (int x = centerX - 4; x <= centerX + 4; x++)
        {
            for (int y = feetY - 3; y < feetY + 3; y++)
            {
                if (!WorldGen.InWorld(x, y))
                    continue;

                Tile tile = Main.tile[x, y];
                if (tile.LiquidAmount > 200 && tile.LiquidType == LiquidID.Water && tile.LiquidCondition() == condition.Index)
                    return true;
            }
        }

        return false;
    }
}

public class BrineLiquidfall : ModWaterfallStyle
{
    public override string Texture => AssetReferences.Content.Liquids.BrineLiquidfall.KEY;
}