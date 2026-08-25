using ReLogic.Content;
using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core.Graphics.Cinematics.Camera;
using Terraria.Audio;

namespace TrelamiumIII.Core.Gas.Layers;

public class PhytoncideSporeLayer : ChemicalGasLayer
{
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
    public override float ChemicalDamage => 0.8f;
    public override int AfflictionDuration => 90;
    public override float AfflictionRate => 30f;
    public override float ReactionChance => 0.02f;
    public override float ReactionDamage => 4f;

    private Asset<Texture2D> noiseTex;

    public override void Load()
    {
        base.Load();
        noiseTex = Assets.Textures.Noise.LiquidNoise.Asset;
    }

    protected override void ApplyShaderParameters(Effect effect)
    {
        effect.Parameters["Time"]?.SetValue((float)Main.timeForVisualEffects / 60f);
        effect.Parameters["uNoiseTex"]?.SetValue(noiseTex.Value);

        int stride = Math.Max(1, gasNodes.Count / 16);

        Vector3 light = Vector3.Zero;
        int samples = 0;

        for (int i = 0; i < gasNodes.Count; i += stride)
        {
            light += Lighting.GetSubLight(gasNodes[i].Position);
            samples++;
        }

        light /= samples;

        effect.Parameters["WorldLight"]?.SetValue(light);

        effect.Parameters["FieldTexel"]?.SetValue(new Vector2(
            2f / Main.screenWidth, 2f / Main.screenHeight));
        effect.Parameters["WorldOffset"]?.SetValue(Main.screenPosition / Main.screenHeight);
        effect.Parameters["Aspect"]?.SetValue(Main.screenWidth / (float)Main.screenHeight);

        effect.Parameters["Threshold"]?.SetValue(0.16f);
        effect.Parameters["NoiseScale"]?.SetValue(1.25f);
        effect.Parameters["NoiseStrength"]?.SetValue(0.22f);
        effect.Parameters["NoiseDrift"]?.SetValue(new Vector2(-0.075f, 0.09f));

        effect.Parameters["Bands"]?.SetValue(8f);
        effect.Parameters["BodyAlpha"]?.SetValue(0.88f);

        effect.Parameters["ShadowColor"]?.SetValue(new Vector3(0.12f, 0.35f, 0.09f));
        effect.Parameters["BodyColor"]?.SetValue(new Vector3(0.42f, 0.62f, 0.1f));
        effect.Parameters["CoreColor"]?.SetValue(new Vector3(0.42f, 0.65f, 0.1f));
        effect.Parameters["OutlineColor"]?.SetValue(new Vector3(0.22f, 0.74f, 0.22f));
        effect.Parameters["OutlineAlpha"]?.SetValue(0.8f);
    }

    protected override void ApplyLighting(ref GasNode node)
    {
        Lighting.AddLight(node.Position, 0.14f * node.Alpha, 0.05f * node.Alpha, 0.20f * node.Alpha);
    }

    protected override void OnNPCContact(ref GasNode node, NPC npc)
    {
        base.OnNPCContact(ref node, npc);
    }
    protected override void OnPlayerContact(ref GasNode node, Player player)
    {
        base.OnPlayerContact(ref node, player);
        player.GetModPlayer<PhytoncidePlayer>().TouchGas();
    }

    public override void OnChemicalReaction(in ChemicalReactionEvent e)
    {
        base.OnChemicalReaction(e);

        PhytoncideBloomGlobalNPC.Cling(e.Target);
    }
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

public class PhytoncideBloomGlobalNPC : GlobalNPC
{
    public override bool InstancePerEntity => true;

    private const int ClingTicks = 6 * 60;    // how long the cloud sticks to a host
    private const float TickDamage = 1.5f;
    private const float TickRate = 20f;       // one affliction tick every 1/3 s
    private const float BurstRadius = 160f;   // 10 tiles; who the reaction burst catches
    private const int ReseedInterval = 5;     // ticks between attached nodes

    public int BloomTicks { get; private set; }
    public bool IsBlooming => BloomTicks > 0;

    /// <summary>
    /// Reaction entry point for the flask: pop a visible burst of gas at the target, then stick
    /// the lingering cloud onto everything the burst reached (the target included).
    /// </summary>
    public static void BloomWithBurst(NPC target)
    {
        if (target is not { active: true }) return;

        CameraSystem.shake = 3;
        SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.9f, Pitch = -0.4f }, target.Center);

        var layer = GasSystem.GetLayer<PhytoncideSporeLayer>();
        if (layer != null)
        {
            int count = Main.rand.Next(14, 22);
            for (int i = 0; i < count; i++)
            {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 5f);
                vel.Y -= 0.5f;
                layer.SpawnNode(target.Center, vel, radius: Main.rand.NextFloat(12f, 28f), life: 150f);
            }
        }

        float radiusSq = BurstRadius * BurstRadius;
        for (int i = 0; i < Main.maxNPCs; i++)
        {
            NPC other = Main.npc[i];
            if (!other.active) continue;
            if (Vector2.DistanceSquared(target.Center, other.Center) > radiusSq) continue;
            Cling(other);
        }
    }

    public static bool Cling(NPC npc)
    {
        if (npc is not { active: true } || npc.friendly || !npc.CanBeChasedBy()) return false;

        npc.GetGlobalNPC<PhytoncideBloomGlobalNPC>().BloomTicks = ClingTicks;
        npc.GetGlobalNPC<ChemicalGlobalNPC>()
           .ApplyAffliction(TickDamage, ClingTicks, TickRate, ChemicalDamageType.Caustic);
        return true;
    }

    public override void PostAI(NPC npc)
    {
        if (BloomTicks <= 0) return;
        if (--BloomTicks <= 0) return;
        if (BloomTicks % ReseedInterval != 0) return;

        var layer = GasSystem.GetLayer<PhytoncideSporeLayer>();
        if (layer == null) return;

        Vector2 pos = npc.Center + Main.rand.NextVector2Circular(npc.width * 0.45f, npc.height * 0.45f);
        Vector2 vel = npc.velocity * 0.85f + new Vector2(0f, -0.2f) + Main.rand.NextVector2Circular(0.4f, 0.4f);
        layer.SpawnNode(pos, vel, radius: Main.rand.NextFloat(8f, 20f), life: 45f);
    }
}