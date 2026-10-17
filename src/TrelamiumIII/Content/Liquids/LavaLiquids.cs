using Terraria.Localization;
using TrelamiumIII.Core.Liquids;

namespace TrelamiumIII.Content.Liquids;

/// <summary>
///     Conditions on vanilla lava: molten by default. Like <see cref="WaterLiquids" />, every condition stays
///     <see cref="LiquidID.Lava" /> to vanilla and other mods, so new kinds of lava cost no liquid IDs.
/// </summary>
public sealed class LavaLiquids : LiquidConditionSet, ILoadable
{
    public static LavaLiquids Instance { get; private set; } = null!;

    public LiquidCondition Molten { get; private set; } = null!;

    public override int LiquidType => LiquidID.Lava;

    public override string FullName => "Terraria/Lava";

    public override string Name => "Lava";

    public void Load(Mod mod)
    {
        Molten = AddCondition(new LiquidCondition("Molten"));

        Register();
        Instance = this;
    }

    public void Unload() => Instance = null!;

    public override string ConditionDisplayName(LiquidCondition condition)
        => Language.GetOrRegister(TrelamiumIII.Instance.GetLocalizationKey($"Liquids.Lava.Conditions.{condition.Name}"), () => condition.Name).Value;
}
