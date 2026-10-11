using Terraria.GameContent.Liquid;
using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Content.Alchemy.Tiles;
using TrelamiumIII.Core.Graphics.Fields;
using TrelamiumIII.Core.Liquids;

namespace TrelamiumIII.Content.Liquids;

/// <summary>
///     Acid base. Plain acid is the world's; the vitriols and named acids come from it by contact with ore, sulfur and
///     salt. Every acid dissolves water and honey (see <see cref="LiquidMixingGlobalLiquid" />).
/// </summary>
public sealed class AcidLiquid : ConditionalLiquid, ILiquidSurface
{
    /// <summary>A caustic web with bubbles rising through it, in the condition's colour.</summary>
    public LiquidSurface Surface { get; } = new("Acid") { Strength = 0.8f };

    public Color SurfaceTint(int i, int j) => ConditionAt(i, j).Tint;

    public LiquidCondition Acid { get; private set; } = null!;

    public LiquidCondition OilOfVitriol { get; private set; } = null!;

    public LiquidCondition BlueVitriol { get; private set; } = null!;

    public LiquidCondition GreenVitriol { get; private set; } = null!;

    public LiquidCondition SpiritOfSalt { get; private set; } = null!;

    public LiquidCondition AquaFortis { get; private set; } = null!;

    public LiquidCondition AquaRegia { get; private set; } = null!;

    protected override void DefineConditions()
    {
        Acid = AddCondition(new LiquidCondition("Acid") { Tint = new Color(170, 255, 70), Light = ElementProfile.Caustic.EmittedLight, Strength = 0.5f });
        OilOfVitriol = AddCondition(new LiquidCondition("OilOfVitriol") { Tint = new Color(255, 225, 140), Strength = 0.75f });
        BlueVitriol = AddCondition(new LiquidCondition("BlueVitriol") { Tint = new Color(70, 160, 255), Strength = 0.35f });
        GreenVitriol = AddCondition(new LiquidCondition("GreenVitriol") { Tint = new Color(120, 235, 160), Strength = 0.35f });
        SpiritOfSalt = AddCondition(new LiquidCondition("SpiritOfSalt") { Tint = new Color(235, 250, 190), Strength = 0.7f });
        AquaFortis = AddCondition(new LiquidCondition("AquaFortis") { Tint = new Color(255, 200, 80), Strength = 0.85f });
        AquaRegia = AddCondition(new LiquidCondition("AquaRegia") { Tint = new Color(255, 130, 50), Strength = 1f });

        AddReaction(Acid, BlueVitriol, TileID.Copper, TileID.Tin);
        AddReaction(Acid, GreenVitriol, TileID.Iron, TileID.Lead);
        AddReaction(Acid, OilOfVitriol, type => type == ModContent.TileType<SulfurTile>());
        AddReaction(Acid, SpiritOfSalt, type => type == ModContent.TileType<RockSaltTile>());

        AddCorrosion(AquaFortis, type => type is TileID.Silver or TileID.Tungsten);
        AddCorrosion(AquaRegia, type => type is TileID.Gold or TileID.Platinum);

        AddMix(AquaFortis, SpiritOfSalt, AquaRegia);
    }

    public override void SetStaticDefaults()
    {
        base.SetStaticDefaults();

        LiquidRenderer.DEFAULT_OPACITY[Type] = 0.75f;
        SlopeOpacity = 0.7f;
        ChecksForDrowning = true;
        AllowEmitBreathBubbles = false;
    }

    public override int LiquidMerge(int i, int j, int otherLiquid) => otherLiquid == LiquidID.Lava ? TileID.Obsidian : TileID.Stone;

    protected override void OnPlayerContact(Player player, LiquidCondition condition)
    {
        if (player.whoAmI != Main.myPlayer)
            return;

        int ticks = (int)(60 + 120 * condition.Strength);

        player.AddBuff(condition.Strength >= 0.85f ? BuffID.Venom : BuffID.Poisoned, ticks);

        if (condition.Strength >= 0.6f)
            player.AddBuff(BuffID.Ichor, ticks);
    }

    protected override void OnNPCContact(NPC npc, LiquidCondition condition)
    {
        if (!npc.friendly && !npc.dontTakeDamage && !npc.immortal)
            ChemicalPayload.Contact(npc, ElementProfile.Caustic, null, FieldNode.NoOwner, null, condition.Strength);
    }
}
