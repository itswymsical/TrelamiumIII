using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Components.Items;
using TrelamiumIII.Core.Gas;
using TrelamiumIII.Core.Gas.Layers;
using TrelamiumIII.Core.Projectiles;
using TrelamiumIII.Core.Graphics.Cinematics.Camera;
using TrelamiumIII.Utilities;
using Terraria.Audio;
using TrelamiumIII.Core.Graphics.Fields;

namespace TrelamiumIII.Content.Items.Weapons;

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
        component.ChemicalDamage = 2;
        component.debuffRate = 50;
        component.debuffDuration = TimeConversion.SetTime(secs: 6);
        component.ReactionChance = 2;
        component.ReactionDamage = 2;
        component.ReactionHandler = DoPhytoncideBloom;
    }

    /// <summary>Fraction of the reaction's landed damage returned to the thrower as life.</summary>
    private const float LifestealFraction = 0.4f;

    private static void DoPhytoncideBloom(ChemicalReactionEvent e)
    {
        PhytoncideBloomGlobalNPC.BloomWithBurst(e.Target);

        // Ambient reactions have no payload, so nobody to heal.
        if (e.Payload is not { Owner: >= 0 and < Main.maxPlayers } payload) return;

        Player owner = Main.player[payload.Owner];
        if (!owner.active) return;

        owner.GetModPlayer<PhytoncidePlayer>()
             .HealSelf((int)MathF.Round(e.DamageDealt * LifestealFraction));
    }
}

/// <summary>A flask of phytoncide: barely leaks, then opens into a bloom whose arms grow as they travel.</summary>
public class PhytoncideFlaskProjectile : ChemicalProjectile
{
    public override string Texture => AssetReferences.Content.Items.Weapons.PhytoncideFlaskItem.KEY;

    protected override GasField? Field => FieldSystem.GetLayer<PhytoncideSporeField>();

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

    /// <summary>A spore arm: thrown hard and slowed hard, so its nodes pile up at the end of a short throw as a puff.</summary>
    private static EmitterProfile Bloom => EmitterProfile.Default with
    {
        NodeLife = 24.75f,
        NodeScatter = new Vector2(1.5f, 5f),
        Drag = 0.88f,

        // Every arm grows, not just the core, so the cloud stays one shape.
        Growth = 0.65f,
    };
}
