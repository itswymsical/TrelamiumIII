using Terraria.Audio;
using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Components.Items;
using TrelamiumIII.Core.Gas;
using TrelamiumIII.Core.Gas.Layers;
using TrelamiumIII.Core.Projectiles;
using TrelamiumIII.Utilities;
using TrelamiumIII.Core.Graphics.Fields;

namespace TrelamiumIII.Content.Items.Weapons;

public class VilesporeFlaskItem : ModItem
{
    public override void SetDefaults()
    {
        Item.damage = 7;
        Item.DamageType = ModContent.GetInstance<ChemicalDamageClass>();
        Item.width = Item.height = 30;
        Item.rare = ItemRarityID.Blue;

        Item.useStyle = ItemUseStyleID.Swing;
        Item.useAnimation = Item.useTime = 23;

        Item.UseSound = SoundID.Item106;
        Item.shoot = ModContent.ProjectileType<VilesporeFlaskProjectile>();
        Item.shootSpeed = 7f;
        Item.autoReuse = true;
        Item.consumable = true;
        Item.maxStack = Item.CommonMaxStack;
        Item.noUseGraphic = true;
        Item.noMelee = true;

        if (!Item.TryEnable(out ChemicalSourceComponent component))
            return;

        component.ChemicalType = ChemicalDamageType.Necrotic;
        component.ChemicalDamage = 2.2f;
        component.AfflictionRate = 50;
        component.AfflictionDuration = TimeConversion.SetTime(secs: 6);
        component.ReactionChance = 0.007f;
        component.ReactionDamage = 8f;
        component.ReactionHandler = DoCorruptSporeSpread;
    }

    private static void DoCorruptSporeSpread(ChemicalReactionEvent e)
        => SporePlagueGlobalNPC.InfectWithBurst(e.Target);
}

/// <summary>A flask of corrupt spores: a slow dribble in flight, a wide fan on breaking.</summary>
public class VilesporeFlaskProjectile : ChemicalProjectile
{
    public override string Texture => AssetReferences.Content.Items.Weapons.VilesporeFlaskItem.KEY;

    protected override GasField? Field => FieldSystem.GetLayer<VilesporeField>();

    protected override BurstProfile Burst => BurstProfile.FlaskShatter with
    {
        AirCount = new FloatRange(16f, 24f),
        GroundCount = new FloatRange(11f, 19f),
        AirSpeed = new FloatRange(3.5f, 5.5f),

        // Faster at the middle than the rim on purpose: light spores lose their momentum spreading outward.
        GroundSpeed = new FloatRange(7.5f, 6f),

        CoreCut = 0.5f,

        Core = EmitterProfile.Default with { NodeRadius = 14f },
        Body = EmitterProfile.Default with { NodeRadius = 12f },
        Droplet = EmitterProfile.Default with { NodeRadius = 7f },

        DamageShare = 1f,
    };
}
