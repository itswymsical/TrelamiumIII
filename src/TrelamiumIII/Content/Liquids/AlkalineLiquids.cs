using Terraria.GameContent.Liquid;
using TrelamiumIII.Core.Liquids;

namespace TrelamiumIII.Content.Liquids;

/// <summary>
///     Alkali base: lye by default, turned milky by marble or pungent by bone. Alkalis meeting acid neutralise into
///     brine (see <see cref="LiquidMixingGlobalLiquid" />).
/// </summary>
<<<<<<< HEAD
public sealed class AlkalineLiquids : CustomLiquid
{
    public static AlkalineLiquids Instance { get; private set; } = null!;

    public InternalLiquid Lye { get; private set; } = null!;

    public InternalLiquid Limewater { get; private set; } = null!;

    public InternalLiquid SpiritOfHartshorn { get; private set; } = null!;
=======
public sealed class AlkalineLiquids : ConditionalLiquid
{
    public static AlkalineLiquids Instance { get; private set; } = null!;

    public LiquidCondition Lye { get; private set; } = null!;

    public LiquidCondition Limewater { get; private set; } = null!;

    public LiquidCondition SpiritOfHartshorn { get; private set; } = null!;
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb

    public override void Load()
    {
        base.Load();
        Instance = this;
    }

    public override void Unload()
    {
        Instance = null!;
        base.Unload();
    }

<<<<<<< HEAD
    protected override void DefineLiquidTypes()
    {
        Lye = AddInternalLiquid(new InternalLiquid("Lye") { Tint = new Color(205, 195, 255), Strength = 0.6f });
        Limewater = AddInternalLiquid(new InternalLiquid("Limewater") { Tint = new Color(240, 242, 228), Strength = 0.3f });
        SpiritOfHartshorn = AddInternalLiquid(new InternalLiquid("SpiritOfHartshorn") { Tint = new Color(200, 255, 230), Strength = 0.45f });
=======
    protected override void DefineConditions()
    {
        Lye = AddCondition(new LiquidCondition("Lye") { Tint = new Color(205, 195, 255), Strength = 0.6f });
        Limewater = AddCondition(new LiquidCondition("Limewater") { Tint = new Color(240, 242, 228), Strength = 0.3f });
        SpiritOfHartshorn = AddCondition(new LiquidCondition("SpiritOfHartshorn") { Tint = new Color(200, 255, 230), Strength = 0.45f });
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb

        AddReaction(Lye, Limewater, TileID.Marble);
        AddReaction(Lye, SpiritOfHartshorn, TileID.BoneBlock);
    }

    public override void SetStaticDefaults()
    {
        base.SetStaticDefaults();

        LiquidRenderer.DEFAULT_OPACITY[Type] = 0.6f;
        SlopeOpacity = 0.55f;
        SplashDustType = DustID.Water;

        ChecksForDrowning = true;
        AllowEmitBreathBubbles = true;
    }

    public override int LiquidMerge(int i, int j, int otherLiquid) => otherLiquid == LiquidID.Lava ? TileID.Obsidian : TileID.Stone;

<<<<<<< HEAD
    protected override void OnPlayerContact(Player player, InternalLiquid condition)
=======
    protected override void OnPlayerContact(Player player, LiquidCondition condition)
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb
    {
        if (player.whoAmI == Main.myPlayer)
            player.AddBuff(BuffID.Poisoned, (int)(30 + 90 * condition.Strength));
    }

<<<<<<< HEAD
    protected override void OnNPCContact(NPC npc, InternalLiquid condition)
=======
    protected override void OnNPCContact(NPC npc, LiquidCondition condition)
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb
    {
        if (!npc.dontTakeDamage && !npc.immortal)
            npc.AddBuff(BuffID.Poisoned, (int)(30 + 90 * condition.Strength));
    }
}
