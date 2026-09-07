using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Components.Items;
using TrelamiumIII.Core.Gas;
using TrelamiumIII.Core.Gas.Layers;
using TrelamiumIII.Core.Projectiles;
using TrelamiumIII.Core.Graphics.Cinematics.Camera;
using TrelamiumIII.Utilities;
using Terraria.Audio;

namespace TrelamiumIII.Content.Alchemy.Weapons;

public class PhytoncideFlaskItem : ModItem
{
    public override void SetDefaults()
    {
        Item.damage = 6;
        Item.DamageType = ModContent.GetInstance<ChemicalDamageClass>();
        Item.width = Item.height = 30;
        Item.rare = ItemRarityID.Blue;

        Item.useStyle = ItemUseStyleID.Swing;
        Item.useAnimation = Item.useTime = 23;

        Item.UseSound = SoundID.Item106;
        Item.shoot = ModContent.ProjectileType<PhytoncideFlaskProjectile>();
        Item.shootSpeed = 8f;
        Item.autoReuse = true;
        Item.consumable = true;
        Item.maxStack = Item.CommonMaxStack;
        Item.noUseGraphic = true;
        Item.noMelee = true;

        if (!Item.TryEnable(out ChemicalSourceComponent component))
            return;

        component.ChemicalType = ChemicalDamageType.Caustic;
        component.ChemicalDamage = 2f;
        component.AfflictionRate = 50;
        component.AfflictionDuration = TimeConversion.SetTime(secs: 6);
        component.ReactionChance = 0.002f;
        component.ReactionDamage = 2f;
        component.ReactionHandler = DoPhytoncideBloom;
    }

    /// <summary>Fraction of the reaction's landed damage returned to the thrower as life.</summary>
    private const float LifestealFraction = 0.4f;

    private static void DoPhytoncideBloom(ChemicalReactionEvent e)
    {
        PhytoncideBloomGlobalNPC.BloomWithBurst(e.Target);

        // Lifesteal needs somebody to credit, and only projectile-delivered reactions carry an
        // owner; one rolled by a drifting cloud has none.
        if (e.DeliveryProjectile is not { } proj) return;
        if (proj.owner < 0 || proj.owner >= Main.maxPlayers) return;

        Player owner = Main.player[proj.owner];
        if (!owner.active) return;

        owner.GetModPlayer<PhytoncidePlayer>()
             .HealSelf((int)MathF.Round(e.DamageDealt * LifestealFraction));
    }
}

/// <summary>
///     A flask of phytoncide: barely leaks at all, then opens into a bloom that keeps growing.
/// </summary>
/// <remarks>
///     The only burst in the mod whose arms expand as they travel. Everything else lays a constant
///     radius and relies on the spread of the fan for its shape; this one starts each arm at a third
///     of its size and grows it, so the cloud reads as something blooming outward rather than as
///     something that was already that big and is now moving.
/// </remarks>
public class PhytoncideFlaskProjectile : ChemicalProjectile
{
    public override string Texture => AssetReferences.Content.Alchemy.Weapons.PhytoncideFlaskItem.KEY;

    protected override GasField? Field => GasSystem.GetLayer<PhytoncideSporeLayer>();

    /// <summary>Almost nothing: the spores are sealed until the glass gives.</summary>
    protected override TrailProfile? Trail => TrailProfile.Shed with
    {
        NodeRadius = 5f,
        NodeLife = 8f,
    };

    protected override BurstProfile Burst => BurstProfile.FlaskShatter with
    {
        AirCount = new FloatRange(12f, 16f),
        GroundCount = new FloatRange(7f, 11f),
        AirSpeed = new FloatRange(3f, 8f),
        GroundSpeed = new FloatRange(11.5f, 12f),

        Core = Bloom with { NodeRadius = 28f },
        Body = Bloom with { NodeRadius = 12f },
        Droplet = Bloom with { NodeRadius = 7f },

        DamageShare = 1f,
    };

    /// <summary>
    ///     A spore arm: thrown hard, slowed hard, seeding a tall column of short-lived nodes.
    /// </summary>
    /// <remarks>
    ///     The heavy drag is what makes the bloom. An arm that kept its speed would draw a long thin
    ///     streak; one that sheds most of it in the first half-second deposits nearly all its nodes
    ///     in a small area at the end of a short throw, which is a puff.
    /// </remarks>
    private static EmitterProfile Bloom => EmitterProfile.Default with
    {
        NodeLife = 24.75f,
        NodeScatter = new Vector2(1.5f, 5f),
        Drag = 0.88f,

        // Every arm blooms, not just the core: the rim expanding alongside the middle is what keeps
        // the cloud one shape instead of a growing centre ringed by fixed dots.
        Growth = 0.65f,
    };
}
