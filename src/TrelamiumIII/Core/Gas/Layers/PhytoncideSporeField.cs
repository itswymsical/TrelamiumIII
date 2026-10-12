using ReLogic.Content;
using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core.Tiles.Conditions;
using TrelamiumIII.Core.Tiles.Conditions.Types;
using TrelamiumIII.Core.Graphics.Cinematics.Camera;
using Terraria.Audio;
using TrelamiumIII.Core.Graphics.Fields;

namespace TrelamiumIII.Core.Gas.Layers;

public class PhytoncideSporeField : ChemicalGasField
{
    /// <summary>Dense gas, alongside the other spore clouds.</summary>
    public override int LoadOrder => 55;

    protected override Asset<Effect> Shader => Assets.Shaders.GasFbm.Asset;
    protected override string TexturePath => Assets.Textures.VFX.InkMote.KEY;
    protected override int GasType => GasTypeID.Gas;

    protected override float DefaultDensity => 1.00f;
    protected override float DefaultViscosity => 0.58f;
    protected override float EnclosedLifePenalty => 1.5f;
    protected override float GravityScale => 0.0f;
    protected override float BuoyancyForce => 0.68f;
    protected override float Drag => 0.9998f;
    protected override float LifeDecay => 0.35f;

    public override ChemicalDamageType ChemicalType => ChemicalDamageType.Caustic;
    public override int ChemicalDamage => 1;
    public override int AfflictionDuration => 90;
    public override float AfflictionRate => 30f;
    public override int ReactionChance => 2;
    public override int ReactionDamage => 4;

    /// <summary>Spores that settle leave a caustic film on the surface.</summary>
    protected override ChemicalReactionTileCondition? Residue => TileConditionSystem.Get<CausticResidueCondition>();

    protected override GasLook Look { get; } = new()
    {
        Noise = static () => Assets.Textures.Noise.LiquidNoise.Asset,
        WorldLight = new Vector3(0.24f),
        Threshold = 0.16f,
        NoiseScale = 1.25f,
        NoiseStrength = 0.22f,
        Drift = new Vector2(-0.075f, 0.09f),
        FlowStrength = 0.14f,
        Bands = 8f,
        BodyAlpha = 0.88f,
        Shadow = new Vector3(0.12f, 0.35f, 0.09f),
        Body = new Vector3(0.42f, 0.62f, 0.1f),
        Core = new Vector3(0.42f, 0.65f, 0.1f),
        Outline = new Vector3(0.22f, 0.74f, 0.22f),
        OutlineAlpha = 0.8f,
    };

    protected override void ApplyLighting(ref FieldNode node)
    {
        Lighting.AddLight(node.Position, 0.14f * node.Alpha, 0.05f * node.Alpha, 0.20f * node.Alpha);
    }

    protected override int ContactBuff => BuffID.Poisoned;

    protected override int ContactBuffTicks => 30;

    protected override void OnPlayerContact(ref FieldNode node, Player player)
    {
        base.OnPlayerContact(ref node, player);
        player.GetModPlayer<PhytoncidePlayer>().TouchGas();
    }

    public override ClingBehaviour? ClingBehaviour => PhytoncideBloomCling.Instance;
}

public class PhytoncidePlayer : ModPlayer
{
    public const int RegenDuration = 5 * 60;

    public const int RegenBonus = 12;

    private const int TouchHealInterval = 30;
    private const int TouchHealAmount = 1;

    public int RegenTicks;

    private int _touchCooldown;

    public void TouchGas()
    {
        RegenTicks = RegenDuration;

        if (_touchCooldown > 0) return;
        _touchCooldown = TouchHealInterval;
        HealSelf(TouchHealAmount);
    }

    public void HealSelf(int amount)
    {
        if (Player.whoAmI != Main.myPlayer) return;
        if (amount <= 0 || Player.dead || Player.statLife >= Player.statLifeMax2) return;

        Player.statLife = Math.Min(Player.statLife + amount, Player.statLifeMax2);
        Player.HealEffect(amount);
    }

    public override void PostUpdate()
    {
        if (_touchCooldown > 0) _touchCooldown--;
        if (RegenTicks > 0) RegenTicks--;
    }

    public override void UpdateLifeRegen()
    {
        if (RegenTicks > 0) Player.lifeRegen += RegenBonus;
    }

    public override void UpdateDead()
    {
        RegenTicks = 0;
        _touchCooldown = 0;
    }
}

/// <summary>A phytoncide bloom: a cloud that sticks to its host, ticking caustic damage and reseeding gas around it.</summary>
public sealed class PhytoncideBloomCling : ClingBehaviour
{
    public static readonly PhytoncideBloomCling Instance = new();

    private const int BloomTicks = 6 * 60;
    private const int TickDamage = 2;
    private const float TickRate = 20f;
    private const float BurstRadius = 160f;
    private const int ReseedInterval = 5;

    public override int Duration(float concentration) => BloomTicks;

    public override void OnAttach(NPC host, ref ClingState cling)
        => host.GetGlobalNPC<ChemicalGlobalNPC>().ApplyAffliction(ChemicalDamageType.Caustic, TickDamage, BloomTicks, TickRate, cling.Owner, cling.Payload);

    public override void Update(NPC host, ref ClingState cling)
    {
        if (cling.Age % ReseedInterval != 0 || cling.Medium is not { } layer)
            return;

        Vector2 position = host.Center + Main.rand.NextVector2Circular(host.width * 0.45f, host.height * 0.45f);
        Vector2 velocity = host.velocity * 0.85f + new Vector2(0f, -0.2f) + Main.rand.NextVector2Circular(0.4f, 0.4f);

        layer.SpawnNode(position, velocity, radius: Main.rand.NextFloat(8f, 20f), life: 45f, owner: cling.Owner, payload: cling.Payload);
    }

    /// <summary>
    /// Reaction handler: pops a burst of gas at the target, then blooms on everything within
    /// <see cref="BurstRadius" />, the target included.
    /// </summary>
    public static void BloomWithBurst(ChemicalReactionEvent e)
    {
        NPC target = e.Target;

        if (target is not { active: true } || FieldSystem.GetLayer<PhytoncideSporeField>() is not { } layer)
            return;

        SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.9f, Pitch = -0.4f }, target.Center);

        int count = Main.rand.Next(14, 22);

        for (int i = 0; i < count; i++)
        {
            Vector2 velocity = Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 5f);
            velocity.Y -= 0.5f;

            layer.SpawnNode(target.Center, velocity, radius: Main.rand.NextFloat(12f, 28f), life: 150f, owner: e.Owner, payload: e.Payload?.Derived);
        }

        float radiusSq = BurstRadius * BurstRadius;

        for (int i = 0; i < Main.maxNPCs; i++)
        {
            NPC other = Main.npc[i];

            if (other is not { active: true, life: > 0 } || other.friendly || !other.CanBeChasedBy())
                continue;

            if (Vector2.DistanceSquared(target.Center, other.Center) > radiusSq)
                continue;

            other.GetGlobalNPC<ChemicalGlobalNPC>().AttachCling(other, ChemicalDamageType.Caustic, Instance, layer, e.Payload, e.Owner);
        }
    }
}
