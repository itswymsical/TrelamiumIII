using Terraria.GameContent.Liquid;
using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Content.Alchemy.Tiles;
using TrelamiumIII.Core.Alchemy;
using TrelamiumIII.Core.Graphics.Fields;
using TrelamiumIII.Core.Liquids;

namespace TrelamiumIII.Content.Liquids;

/// <summary>
///     Acid base. Plain acid is the world's; the vitriols and named acids come from it by contact with ore, sulfur and
///     salt. Every acid dissolves water and honey and neutralises alkalis into brine (see
///     <see cref="LiquidMixingGlobalLiquid" />). Dropped gold ore dissolves into aqua regia as gold solution, one
///     potency at a time.
/// </summary>
public sealed class AcidicLiquids : ConditionalLiquid, ILiquidSurface
{
    /// <summary>Gold ore one potency step dissolves; every step, 150 ore in all, makes a fully potent solution.</summary>
    private const int GoldOrePerPotency = 30;

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

    /// <summary>Aqua regia holding dissolved gold, by potency: index 0 is potency 1.</summary>
    public LiquidCondition[] GoldSolution { get; } = new LiquidCondition[SolutePotency.MAX];
    public static AcidicLiquids Instance { get; private set; } = null!;
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
        Acid = AddCondition(new LiquidCondition("Acid") { Tint = new Color(170, 255, 70), Light = ElementProfile.Caustic.EmittedLight, Strength = 0.5f });
        OilOfVitriol = AddCondition(new LiquidCondition("OilOfVitriol") { Tint = new Color(255, 225, 140), Strength = 0.75f });
        BlueVitriol = AddCondition(new LiquidCondition("BlueVitriol") { Tint = new Color(70, 160, 255), Strength = 0.35f });
        GreenVitriol = AddCondition(new LiquidCondition("GreenVitriol") { Tint = new Color(120, 235, 160), Strength = 0.35f });
        SpiritOfSalt = AddCondition(new LiquidCondition("SpiritOfSalt") { Tint = new Color(235, 250, 190), Strength = 0.7f });
        AquaFortis = AddCondition(new LiquidCondition("AquaFortis") { Tint = new Color(255, 221, 149), Strength = 0.85f });
        AquaRegia = AddCondition(new LiquidCondition("AquaRegia") { Tint = new Color(255, 130, 50), Strength = 1f });

        for (int level = 0; level < GoldSolution.Length; level++)
        {
            float potency = (level + 1f) / GoldSolution.Length;

            GoldSolution[level] = AddCondition(new LiquidCondition($"GoldSolution{level + 1}")
            {
                Tint = Color.Lerp(AquaRegia.Tint, new Color(255, 215, 70), potency),
                Light = new Vector3(0.45f, 0.35f, 0.08f) * potency,
                Strength = AquaRegia.Strength,
            });
        }

        AddReaction(Acid, BlueVitriol, TileID.Copper, TileID.Tin);
        AddReaction(Acid, GreenVitriol, TileID.Iron, TileID.Lead);
        AddReaction(Acid, OilOfVitriol, type => type == ModContent.TileType<SulfurTile>());
        AddReaction(Acid, SpiritOfSalt, type => type == ModContent.TileType<RockSaltTile>());

        AddCorrosion(AquaFortis, type => type is TileID.Silver or TileID.Tungsten);
        AddCorrosion(AquaRegia, type => type is TileID.Gold or TileID.Platinum);

        AddMix(AquaFortis, SpiritOfSalt, AquaRegia);
    }

    protected override void DefineItemReactions()
    {
        LiquidCondition from = AquaRegia;

        foreach (LiquidCondition level in GoldSolution)
        {
            AddItemReaction(from, new ItemReaction(ItemID.GoldOre)
            {
                From = from,
                Amount = GoldOrePerPotency,
                Ticks = 120,
                Spent = SubmergedLiquidSystem.BottleVolume,
                Becomes = level,
            });

            from = level;
        }
    }

    public override void SetStaticDefaults()
    {
        base.SetStaticDefaults();

        LiquidRenderer.DEFAULT_OPACITY[Type] = 0.75f;
        SlopeOpacity = 0.7f; 
        SplashDustType = DustID.CursedTorch;

        SplashSound = SoundID.Splash with { Pitch = -.07f };

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
        int ticks = (int)(60 + 120 * condition.Strength);

        if (!npc.dontTakeDamage && !npc.immortal)
        {
            ChemicalPayload.Contact(npc, ElementProfile.Caustic, null, FieldNode.NoOwner, null, condition.Strength);

            npc.AddBuff(condition.Strength >= 0.85f ? BuffID.Venom : BuffID.Poisoned, ticks);

            if (condition.Strength >= 0.6f)
                npc.AddBuff(BuffID.Ichor, ticks);
        }
    }
}