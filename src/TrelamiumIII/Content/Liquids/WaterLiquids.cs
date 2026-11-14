using Terraria.Localization;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Liquids;

namespace TrelamiumIII.Content.Liquids;

/// <summary>
///     Conditions on vanilla water: fresh by default, or brine. Brine tiles stay <see cref="LiquidID.Water" />, so
///     vanilla and other mods treat them as water (fishing, spawns, sponges, buckets).
/// </summary>
<<<<<<< HEAD
public sealed class WaterLiquids : CustomLiquidSet, ILoadable, IInternalLiquidSurface
=======
public sealed class WaterLiquids : LiquidConditionSet, ILoadable, IConditionalLiquidSurface
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb
{
    public static WaterLiquids Instance { get; private set; } = null!;

    /// <summary>Recipe condition: standing beside brine, the way vanilla checks for water.</summary>
    public static Condition NearBrine => Instance.Brine.Near;

<<<<<<< HEAD
    public InternalLiquid Fresh { get; private set; } = null!;

    public InternalLiquid Brine { get; private set; } = null!;
=======
    public LiquidCondition Fresh { get; private set; } = null!;

    public LiquidCondition Brine { get; private set; } = null!;
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb

    public override int LiquidType => LiquidID.Water;

    public override string FullName => "Terraria/Water";

    public override string Name => "Water";

    /// <summary>Brine's surface: fresh water draws none.</summary>
    public LiquidSurface Surface => Brine.Surface!;

<<<<<<< HEAD
    public LiquidSurface? SurfaceAt(int i, int j) => InternalLiqAt(i, j).Surface;

    public void Load(Mod mod)
    {
        Fresh = AddLiquid(new InternalLiquid("Fresh"));

        // Thin salt strands drifting through brine.
        Brine = AddLiquid(new InternalLiquid("Brine")
=======
    public LiquidSurface? SurfaceAt(int i, int j) => ConditionAt(i, j).Surface;

    public void Load(Mod mod)
    {
        Fresh = AddCondition(new LiquidCondition("Fresh"));

        // Thin salt strands drifting through brine.
        Brine = AddCondition(new LiquidCondition("Brine")
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb
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

<<<<<<< HEAD
    public override string LiquidDisplayName(InternalLiquid condition)
        => Language.GetOrRegister(TrelamiumIII.Instance.GetLocalizationKey($"Liquids.Water.Conditions.{condition.Name}"), () => condition.Name).Value;

    /// <remarks>Waterfall styles only load on clients.</remarks>
    public override int? WaterfallStyle(InternalLiquid condition) => condition == Brine ? ModContent.GetInstance<BrineLiquidfall>()?.Slot : null;
=======
    public override string ConditionDisplayName(LiquidCondition condition)
        => Language.GetOrRegister(TrelamiumIII.Instance.GetLocalizationKey($"Liquids.Water.Conditions.{condition.Name}"), () => condition.Name).Value;

    /// <remarks>Waterfall styles only load on clients.</remarks>
    public override int? WaterfallStyle(LiquidCondition condition) => condition == Brine ? ModContent.GetInstance<BrineLiquidfall>()?.Slot : null;
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb
}

public class BrineLiquidfall : ModWaterfallStyle
{
    public override string Texture => AssetReferences.Content.Liquids.BrineLiquidfall.KEY;
}
