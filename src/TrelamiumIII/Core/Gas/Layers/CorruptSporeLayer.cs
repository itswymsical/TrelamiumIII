using ReLogic.Content;
using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core.Tiles.Conditions;
using TrelamiumIII.Core.Tiles.Conditions.Types;
using TrelamiumIII.Core.Graphics.Cinematics.Camera;
using Terraria.Audio;
using TrelamiumIII.Core.Graphics.Fields;

namespace TrelamiumIII.Core.Gas.Layers;

public class VilesporeField : ChemicalGasField
{
    public override int LoadOrder => 50;

    protected override Asset<Effect> Shader => Assets.Shaders.GasFbm.Asset;
    protected override string TexturePath => Assets.Textures.VFX.BloomFlare.KEY;
    protected override int GasType => GasTypeID.Gas;

    protected override float DefaultDensity => 1.10f;
    protected override float DefaultViscosity => 0.58f;
    protected override float EnclosedLifePenalty => 2f;
    protected override float GravityScale => 0.0f;
    protected override float BuoyancyForce => 0.68f;
    protected override float Drag => 0.9998f;
    protected override float LifeDecay => 0.75f;

    public override ChemicalDamageType ChemicalType => ChemicalDamageType.Necrotic;
    public override float ChemicalDamage => 0.4f;
    public override int AfflictionDuration => 90;
    public override float AfflictionRate => 30f;
    public override float ReactionChance => 0.02f;
    public override float ReactionDamage => 4f;

    protected override ChemicalReactionTileCondition? Residue => TileConditionSystem.Get<NecroticResidueCondition>();

    private Asset<Texture2D> noiseTex;

    public override void Load()
    {
        base.Load();
        noiseTex = Assets.Textures.Noise.LiquidNoise.Asset;
    }

    protected override void ApplyShaderParameters(Effect effect)
    {
        effect.Parameters["uNoiseTex"]?.SetValue(noiseTex.Value);


        Vector3 light = new Vector3(0.24f);

        effect.Parameters["WorldLight"]?.SetValue(light);


        effect.Parameters["Threshold"]?.SetValue(0.16f);
        effect.Parameters["NoiseScale"]?.SetValue(1.25f);
        effect.Parameters["NoiseStrength"]?.SetValue(0.22f);
        effect.Parameters["NoiseDrift"]?.SetValue(new Vector2(-0.075f, 0.09f));

        effect.Parameters["FlowStrength"]?.SetValue(0.14f);

        effect.Parameters["Bands"]?.SetValue(8f);
        effect.Parameters["BodyAlpha"]?.SetValue(0.88f);

        effect.Parameters["ShadowColor"]?.SetValue(new Vector3(0.66f, 0.5f, 0.8f));
        effect.Parameters["BodyColor"]?.SetValue(new Vector3(0.52f, 0.22f, 0.72f));
        effect.Parameters["CoreColor"]?.SetValue(new Vector3(0.2667f, 0.2392f, 0.3569f));
        effect.Parameters["OutlineColor"]?.SetValue(new Vector3(0.5647f, 0.5059f, 0.7020f));
        effect.Parameters["OutlineAlpha"]?.SetValue(0.8f);
    }

    protected override void ApplyLighting(ref FieldNode node)
    {
        Lighting.AddLight(node.Position, 0.14f * node.Alpha, 0.05f * node.Alpha, 0.20f * node.Alpha);
    }

    protected override void OnNPCContact(ref FieldNode node, NPC npc)
    {
        base.OnNPCContact(ref node, npc);
        npc.AddBuff(BuffID.Poisoned, 30);
    }
    public override void OnChemicalReaction(in ChemicalReactionEvent e)
    {
        base.OnChemicalReaction(e);
        SporePlagueGlobalNPC.InfectWithBurst(e.Target);
    }
}

public class SporePlagueGlobalNPC : GlobalNPC
{
    public override bool InstancePerEntity => true;

    private const int FuseTicks = 4 * 60;        // the kill window
    private const float TickDamage = 2f;         // affliction damage per tick while plagued
    private const float TickRate = 20f;          // one affliction tick every 1/3 s
    private const float SpreadRadius = 160f;     // 10 tiles; explosion infects chaseable NPCs inside
    private const int BurstDamage = 10;          // necrotic strike on everything the burst infects

    public int PlagueTicks { get; private set; }
    public bool IsPlagued => PlagueTicks > 0;

    /// <summary>
    /// Reaction entry point shared by the flask's ReactionHandler and the gas layer:
    /// pin the plague on the target with a visible spore pop.
    /// </summary>
    public static void InfectWithBurst(NPC target)
    {
        if (!Infect(target)) return;

        var layer = FieldSystem.GetLayer<VilesporeField>();
        if (layer == null) return;

        CameraSystem.shake = 3;
        SoundEngine.PlaySound(SoundID.DD2_BetsyWindAttack, target.Center);

        int count = Main.rand.Next(9, 17);
        for (int i = 0; i < count; i++)
        {
            //Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(target.oldVelocity.X, 6.5f);
            //layer.SpawnNode(target.Center, vel, radius: Main.rand.NextFloat(33f, 73f), life: 90f);

            Vector2 impactDir = target.velocity.SafeNormalize(Vector2.UnitY);

            Vector2 scatter = impactDir * Main.rand.NextFloat(2f, 9f)
                            + Main.rand.NextVector2Circular(1.5f, 1.5f);

            layer.SpawnNode(target.Center, scatter, radius: Main.rand.NextFloat(13f, 33f), life: 90f);

        }
    }

    /// <summary>Start (or refresh) the plague fuse. False if the target can't host it.</summary>
    public static bool Infect(NPC npc)
    {
        if (npc is not { active: true } || npc.friendly || !npc.CanBeChasedBy()) return false;

        var plague = npc.GetGlobalNPC<SporePlagueGlobalNPC>();
        plague.PlagueTicks = FuseTicks;

        // Tick damage rides the chemical affliction system for exactly the fuse window,
        // so composition matchups and resistances apply like any other necrotic source.
        npc.GetGlobalNPC<ChemicalGlobalNPC>()
           .ApplyAffliction(TickDamage, FuseTicks, TickRate, ChemicalDamageType.Necrotic);
        return true;
    }

    public override void PostAI(NPC npc)
    {
        if (PlagueTicks <= 0) return;

        if (--PlagueTicks <= 0)
        {
            Fizzle(npc);
            return;
        }

        if (PlagueTicks % 5 == 0)
        {
            var layer = FieldSystem.GetLayer<VilesporeField>();
            if (layer == null) return;

            Vector2 pos = npc.Center + Main.rand.NextVector2Circular(npc.width * 0.45f, npc.height * 0.45f);
            Vector2 vel = npc.velocity * 0.85f + new Vector2(0f, -0.2f) + Main.rand.NextVector2Circular(0.4f, 0.4f);
            layer.SpawnNode(pos, vel, radius: Main.rand.NextFloat(7f, 33f), life: 40f);
        }
    }

    public override void OnKill(NPC npc)
    {
        if (PlagueTicks <= 0) return;
        PlagueTicks = 0;
        Explode(npc);
    }

    private static void Explode(NPC npc)
    {
        CameraSystem.shake = 5;
        SoundEngine.PlaySound(SoundID.NPCDeath9 with { Volume = 0.8f, Pitch = -0.2f }, npc.Center);

        var layer = FieldSystem.GetLayer<VilesporeField>();
        if (layer != null)
        {
            int count = Main.rand.Next(16, 22);
            for (int i = 0; i < count; i++)
            {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 5f);
                vel.Y -= 0.6f;   // upward bias: the cloud should loom over the corpse, not rain off it
                layer.SpawnNode(npc.Center, vel, radius: Main.rand.NextFloat(10f, 16f), life: 180f);
            }
        }

        // Direct spread is what makes the chain reliable: every chaseable NPC in the burst
        // radius takes a necrotic hit and starts its own fresh fuse. The lingering cloud's
        // contact chemistry stays as a slower secondary infection channel.
        float radiusSq = SpreadRadius * SpreadRadius;
        for (int i = 0; i < Main.maxNPCs; i++)
        {
            NPC other = Main.npc[i];
            if (!other.active || other.whoAmI == npc.whoAmI) continue;
            if (Vector2.DistanceSquared(npc.Center, other.Center) > radiusSq) continue;
            if (!Infect(other)) continue;

            other.SimpleStrikeNPC(
                BurstDamage,
                hitDirection: 0,
                crit: false,
                knockBack: 0f,
                damageType: ModContent.GetInstance<ChemicalDamageClass>(),
                noPlayerInteraction: true);
        }
    }

    private static void Fizzle(NPC npc)
    {
        var layer = FieldSystem.GetLayer<VilesporeField>();
        if (layer == null) return;

        for (int i = 0; i < 4; i++)
        {
            Vector2 vel = new(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(-0.8f, -0.3f));
            layer.SpawnNode(npc.Center + Main.rand.NextVector2Circular(8f, 8f), vel,
                radius: 5f, life: 40f);
        }
    }
}
