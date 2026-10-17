using Terraria.GameContent.Liquid;
using TrelamiumIII.Core.Liquids;

namespace TrelamiumIII.Content.Liquids;

/// <summary>
///     Alkali base: lye by default, turned milky by marble or pungent by bone. Alkalis meeting acid neutralise into
///     brine (see <see cref="LiquidMixingGlobalLiquid" />).
/// </summary>
public sealed class AlkalineLiquids : ConditionalLiquid
{
    public static AlkalineLiquids Instance { get; private set; } = null!;

    public LiquidCondition Lye { get; private set; } = null!;

    public LiquidCondition Limewater { get; private set; } = null!;

    public LiquidCondition SpiritOfHartshorn { get; private set; } = null!;

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

    protected override void DefineConditions()
    {
        Lye = AddCondition(new LiquidCondition("Lye") { Tint = new Color(205, 195, 255), Strength = 0.6f });
        Limewater = AddCondition(new LiquidCondition("Limewater") { Tint = new Color(240, 242, 228), Strength = 0.3f });
        SpiritOfHartshorn = AddCondition(new LiquidCondition("SpiritOfHartshorn") { Tint = new Color(200, 255, 230), Strength = 0.45f });

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

    protected override void OnPlayerContact(Player player, LiquidCondition condition)
    {
        if (player.whoAmI == Main.myPlayer)
            player.AddBuff(BuffID.Poisoned, (int)(30 + 90 * condition.Strength));
    }

    protected override void OnNPCContact(NPC npc, LiquidCondition condition)
    {
        if (!npc.dontTakeDamage && !npc.immortal)
            npc.AddBuff(BuffID.Poisoned, (int)(30 + 90 * condition.Strength));
    }
}
