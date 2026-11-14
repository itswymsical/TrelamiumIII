using Terraria.Localization;
using TrelamiumIII.Core.Liquids;

namespace TrelamiumIII.Content.Liquids;

/// <summary>
<<<<<<< HEAD
///     Liquids on vanilla lava: molten by default. Like <see cref="WaterLiquids" />, every liquid stays
///     <see cref="LiquidID.Lava" /> to vanilla and other mods, so new kinds of lava cost no liquid IDs.
/// </summary>
public sealed class LavaLiquids : CustomLiquidSet, ILoadable
{
    public static LavaLiquids Instance { get; private set; } = null!;

    public InternalLiquid Molten { get; private set; } = null!;
=======
///     Conditions on vanilla lava: molten by default. Like <see cref="WaterLiquids" />, every condition stays
///     <see cref="LiquidID.Lava" /> to vanilla and other mods, so new kinds of lava cost no liquid IDs.
/// </summary>
public sealed class LavaLiquids : LiquidConditionSet, ILoadable
{
    public static LavaLiquids Instance { get; private set; } = null!;

    public LiquidCondition Molten { get; private set; } = null!;
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb

    public override int LiquidType => LiquidID.Lava;

    public override string FullName => "Terraria/Lava";

    public override string Name => "Lava";

    public void Load(Mod mod)
    {
<<<<<<< HEAD
        Molten = AddLiquid(new InternalLiquid("Molten"));
=======
        Molten = AddCondition(new LiquidCondition("Molten"));
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb

        Register();
        Instance = this;
    }

    public void Unload() => Instance = null!;

<<<<<<< HEAD
    public override string LiquidDisplayName(InternalLiquid liquid)
        => Language.GetOrRegister(TrelamiumIII.Instance.GetLocalizationKey($"Liquids.Lava.Liquids.{liquid.Name}"), () => liquid.Name).Value;
=======
    public override string ConditionDisplayName(LiquidCondition condition)
        => Language.GetOrRegister(TrelamiumIII.Instance.GetLocalizationKey($"Liquids.Lava.Conditions.{condition.Name}"), () => condition.Name).Value;
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb
}
