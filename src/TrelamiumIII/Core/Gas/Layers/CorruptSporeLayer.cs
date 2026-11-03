using ReLogic.Content;
using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Common.DamageMeter;
using TrelamiumIII.Core.Residues;
using TrelamiumIII.Core.Residues.Types;
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
    public override int ChemicalDamage => 1;
    public override int AfflictionDuration => 90;
    public override float AfflictionRate => 30f;
    public override int ReactionChance => 2;
    public override int ReactionDamage => 4;

    protected override ChemicalResidue? Residue => ResidueSystem.Get<NecroticResidue>();

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
        Shadow = new Vector3(0.66f, 0.5f, 0.8f),
        Body = new Vector3(0.52f, 0.22f, 0.72f),
        Core = new Vector3(0.2667f, 0.2392f, 0.3569f),
        Outline = new Vector3(0.5647f, 0.5059f, 0.7020f),
        OutlineAlpha = 0.8f,
    };

    protected override void ApplyLighting(ref FieldNode node)
    {
        Lighting.AddLight(node.Position, 0.14f * node.Alpha, 0.05f * node.Alpha, 0.20f * node.Alpha);
    }

    protected override int ContactBuff => BuffID.Poisoned;

    protected override int ContactBuffTicks => 30;

    public override ClingBehaviour? ClingBehaviour => SporePlagueCling.Instance;
}

/// <summary>
///     The Vilespore plague: a fuse that ticks necrotic damage and sheds spores. A host that dies with it on bursts and
///     plagues every chaseable NPC within <see cref="SpreadRadius" />; one that outlives the fuse fizzles.
/// </summary>
public sealed class SporePlagueCling : ClingBehaviour
{
    public static readonly SporePlagueCling Instance = new();

    private const int FuseTicks = 4 * 60;
    private const int TickDamage = 2;
    private const float TickRate = 20f;
    private const float SpreadRadius = 160f;
    private const int BurstDamage = 10;

    public override int Duration(float concentration) => FuseTicks;

    public override void OnAttach(NPC host, ref ClingState cling)
        => host.GetGlobalNPC<ChemicalGlobalNPC>().ApplyAffliction(ChemicalDamageType.Necrotic, TickDamage, FuseTicks, TickRate, cling.Owner, cling.Payload);

    public override void Update(NPC host, ref ClingState cling)
    {
        if (cling.Age % 5 != 0 || cling.Medium is not { } layer)
            return;

        Vector2 position = host.Center + Main.rand.NextVector2Circular(host.width * 0.45f, host.height * 0.45f);
        Vector2 velocity = host.velocity * 0.85f + new Vector2(0f, -0.2f) + Main.rand.NextVector2Circular(0.4f, 0.4f);

        layer.SpawnNode(position, velocity, radius: Main.rand.NextFloat(7f, 33f), life: 40f, owner: cling.Owner, payload: cling.Payload);
    }

    public override void OnDetach(NPC host, ref ClingState cling, bool hostKilled)
    {
        if (hostKilled)
            Explode(host, in cling);
        else
            Fizzle(host, in cling);
    }

    /// <summary>Reaction handler: plagues the target with a visible spore pop.</summary>
    public static void InfectWithBurst(ChemicalReactionEvent e)
    {
        if (FieldSystem.GetLayer<VilesporeField>() is not { } layer || !Infect(e.Target, layer, e.Payload, e.Owner))
            return;

        CameraSystem.shake = 3;
        SoundEngine.PlaySound(SoundID.DD2_BetsyWindAttack, e.Target.Center);

        Vector2 impact = e.Target.velocity.SafeNormalize(Vector2.UnitY);
        int count = Main.rand.Next(9, 17);

        for (int i = 0; i < count; i++)
        {
            Vector2 scatter = impact * Main.rand.NextFloat(2f, 9f) + Main.rand.NextVector2Circular(1.5f, 1.5f);

            layer.SpawnNode(e.Target.Center, scatter, radius: Main.rand.NextFloat(13f, 33f), life: 90f, owner: e.Owner, payload: e.Payload?.Derived);
        }
    }

    /// <summary>Starts or refreshes the fuse. False for a target that cannot host it, dying ones included.</summary>
    private static bool Infect(NPC npc, ChemicalGasField layer, ChemicalPayload? payload, int owner)
    {
        if (npc is not { active: true, life: > 0 } || npc.friendly || !npc.CanBeChasedBy())
            return false;

        npc.GetGlobalNPC<ChemicalGlobalNPC>().AttachCling(npc, ChemicalDamageType.Necrotic, Instance, layer, payload, owner);

        return true;
    }

    private static void Explode(NPC host, in ClingState cling)
    {
        CameraSystem.shake = 5;
        SoundEngine.PlaySound(SoundID.NPCDeath9 with { Volume = 0.8f, Pitch = -0.2f }, host.Center);

        if (cling.Medium is not { } layer)
            return;

        // Outlives the cling's scope, which ends as the host dies.
        ChemicalPayload? cloud = cling.Payload?.Derive(null);
        int count = Main.rand.Next(16, 22);

        for (int i = 0; i < count; i++)
        {
            Vector2 velocity = Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 5f);
            velocity.Y -= 0.6f; // upward bias: the cloud should loom over the corpse, not rain off it

            layer.SpawnNode(host.Center, velocity, radius: Main.rand.NextFloat(10f, 16f), life: 180f, owner: cling.Owner, payload: cloud);
        }

        float radiusSq = SpreadRadius * SpreadRadius;

        for (int i = 0; i < Main.maxNPCs; i++)
        {
            NPC other = Main.npc[i];

            if (other.whoAmI == host.whoAmI || Vector2.DistanceSquared(host.Center, other.Center) > radiusSq)
                continue;

            if (!Infect(other, layer, cling.Payload, cling.Owner))
                continue;

            int dealt = other.SimpleStrikeNPC(
                BurstDamage,
                hitDirection: 0,
                crit: false,
                knockBack: 0f,
                damageType: ModContent.GetInstance<ChemicalDamageClass>(),
                noPlayerInteraction: cling.Owner != Main.myPlayer);

            DamageMeterSystem.Credit(cling.Owner, other, dealt, DamageMeterSource.Hit);
        }
    }

    private static void Fizzle(NPC host, in ClingState cling)
    {
        if (cling.Medium is not { } layer)
            return;

        for (int i = 0; i < 4; i++)
        {
            Vector2 velocity = new(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(-0.8f, -0.3f));

            layer.SpawnNode(host.Center + Main.rand.NextVector2Circular(8f, 8f), velocity, radius: 5f, life: 40f, owner: cling.Owner, payload: cling.Payload);
        }
    }
}
