using Terraria.Localization;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Liquids;

namespace TrelamiumIII.Content.Liquids;

/// <summary>
///     Conditions on vanilla water: fresh by default, or brine. Brine tiles stay <see cref="LiquidID.Water" />, so
///     vanilla and other mods treat them as water (fishing, spawns, sponges, buckets).
/// </summary>
public sealed class WaterLiquids : LiquidConditionSet, ILoadable, IConditionalLiquidSurface
{
    public static WaterLiquids Instance { get; private set; } = null!;

    /// <summary>Recipe condition: standing beside brine, the way vanilla checks for water.</summary>
    public static Condition NearBrine => Instance.Brine.Near;

    public LiquidCondition Fresh { get; private set; } = null!;

    public LiquidCondition Brine { get; private set; } = null!;

    public override int LiquidType => LiquidID.Water;

    public override string FullName => "Terraria/Water";

    public override string Name => "Water";

    /// <summary>Brine's surface: fresh water draws none.</summary>
    public LiquidSurface Surface => Brine.Surface!;

    public LiquidSurface? SurfaceAt(int i, int j) => ConditionAt(i, j).Surface;

    public void Load(Mod mod)
    {
        Fresh = AddCondition(new LiquidCondition("Fresh"));

        // Thin salt strands drifting through brine.
        Brine = AddCondition(new LiquidCondition("Brine")
        {
            Tint = new Color(222, 208, 235),
            Surface = new LiquidSurface("Brine")
            {
                ColorA = new Vector3(0.95f, 0.97f, 1f),
                ColorB = new Vector3(0.72f, 0.80f, 0.90f),
                Strength = 0.55f,
            },
        });

        Register();
        Instance = this;
    }

    public void Unload()
    {
        ClearRegistry();
        Instance = null!;
    }

    public override string ConditionDisplayName(LiquidCondition condition)
        => Language.GetOrRegister(TrelamiumIII.Instance.GetLocalizationKey($"Liquids.Water.Conditions.{condition.Name}"), () => condition.Name).Value;

    /// <remarks>Waterfall styles only load on clients.</remarks>
    public override int? WaterfallStyle(LiquidCondition condition) => condition == Brine ? ModContent.GetInstance<BrineLiquidfall>()?.Slot : null;
}

public class BrineLiquidfall : ModWaterfallStyle
{
    public override string Texture => AssetReferences.Content.Liquids.BrineLiquidfall.KEY;
}
