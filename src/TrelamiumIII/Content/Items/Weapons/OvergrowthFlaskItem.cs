using Terraria.DataStructures;
using Terraria.Localization;
using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Components.Items;
using TrelamiumIII.Core.Gas;
using TrelamiumIII.Core.Gas.Layers;
using TrelamiumIII.Core.Graphics.Fields;
using TrelamiumIII.Core.Projectiles;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Content.Items.Weapons;

/// <summary>Registers one <see cref="OvergrowthFlaskItem" /> per <see cref="VineStrain" />.</summary>
public sealed class OvergrowthFlaskID : ICustomAutoload
{
    private static int[] types = [];

    public static void Autoload(Mod mod)
    {
        types = new int[VineStrain.All.Length];

        foreach (VineStrain strain in VineStrain.All)
        {
            var item = new OvergrowthFlaskItem(strain);

            mod.AddContent(item);
            types[strain.Id] = item.Type;
        }
    }

    /// <summary>The flask item that grows <paramref name="strain" />.</summary>
    public static int Of(VineStrain strain) => types[strain.Id];
}

/// <summary>A flask of mud and seed. One instance per <see cref="VineStrain" />; the strain decides the vines' element.</summary>
public sealed class OvergrowthFlaskItem : ModItem
{
    /// <summary>Pixels of vine a reaction grows under its target.</summary>
    private const float REACTION_VOLUME = 96f;

    [CloneByReference]
    private readonly VineStrain strain;

    public OvergrowthFlaskItem(VineStrain strain) => this.strain = strain;

    /// <summary>Clones the loaded instance, since there is no parameterless constructor.</summary>
    protected override bool CloneNewInstances => true;

    public override string Name => $"{strain.Key}OvergrowthFlaskItem";

    public override string Texture => Assets.Textures.PlaceholderBrew.KEY;

    public override LocalizedText DisplayName => strain.IsDefault
        ? Mod.GetLocalization("Items.OvergrowthFlask.DisplayName")
        : Mod.GetLocalization("Items.OvergrowthFlask.StrainDisplayName").WithFormatArgs(Lang.GetItemName(strain.Herb));

    public override LocalizedText Tooltip => Mod.GetLocalization("Items.OvergrowthFlask.Tooltip");

    public override void SetDefaults()
    {
        Item.damage = 8;
        Item.DamageType = ModContent.GetInstance<ChemicalDamageClass>();
        Item.width = Item.height = 30;
        Item.rare = ItemRarityID.Blue;
        Item.value = Item.sellPrice(copper: 40);

        Item.useStyle = ItemUseStyleID.Swing;
        Item.useAnimation = Item.useTime = 26;

        Item.UseSound = SoundID.Item106;
        Item.shoot = ModContent.ProjectileType<OvergrowthFlaskProjectile>();
        Item.shootSpeed = 7.5f;
        Item.autoReuse = true;
        Item.consumable = true;
        Item.maxStack = Item.CommonMaxStack;
        Item.noUseGraphic = true;
        Item.noMelee = true;

        if (!Item.TryEnable(out ChemicalSourceComponent component))
            return;

        component.ChemicalType = strain.Element;
        component.ChemicalDamage = 2;
        component.debuffRate = 30;
        component.debuffDuration = TimeConversion.SetTime(secs: 3);
        component.Modifiers = [TemperamentModifier.Alkaline];

        if (strain.IsDefault)
            return;

        VineStrain grown = strain;

        component.ReactionChance = 4;
        component.ReactionDamage = 3;
        component.ReactionHandler = e => SproutUnder(e, grown);
    }

    /// <summary>Carries the strain in <c>ai[2]</c>, which the thrown AI leaves alone and which syncs.</summary>
    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
    {
        Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI, ai2: strain.Id);

        return false;
    }

    private static void SproutUnder(ChemicalReactionEvent e, VineStrain strain)
        => FieldSystem.GetLayer<VineField>()?.Sprout(
            e.Target.Center,
            e.Target.velocity * 0.2f,
            REACTION_VOLUME,
            strain,
            e.Owner,
            payload: e.Payload?.Derived,
            lash: false);
}

/// <summary>Breaks into a small splash of mud and a vine pod that grows along the terrain and lashes the nearest enemy.</summary>
public sealed class OvergrowthFlaskProjectile : ChemicalProjectile
{
    /// <summary>Pixels of vine one flask grows, before the payload's burst size.</summary>
    private const float VINE_VOLUME = 352f;

    public override string Texture => Assets.Textures.PlaceholderBrew.KEY;

    protected override GasField? Field => FieldSystem.GetLayer<VineField>();

    protected override TrailProfile? Trail => null;

    private VineStrain Strain => VineStrain.Get((byte)Projectile.ai[2]);

    /// <summary>The mud carries no payload: it is plain Solute whatever the strain, and only the vines take the element.</summary>
    private static BurstProfile MudSplash => BurstProfile.FlaskShatter with
    {
        AirCount = new FloatRange(6f, 9f),
        GroundCount = new FloatRange(5f, 8f),
        AirSpeed = new FloatRange(1.5f, 3f),
        GroundSpeed = new FloatRange(3f, 4f),
        Arc = MathHelper.Pi * 0.7f,
        UpwardBias = 0.5f,
        Core = Glob with { NodeRadius = 12f },
        Body = Glob with { NodeRadius = 9f },
        Droplet = Glob with { NodeRadius = 6f, NodeLife = 70f },
    };

    private static EmitterProfile Glob => EmitterProfile.Default with
    {
        Lifetime = 7,
        NodeLife = 150f,
        NodeScatter = new Vector2(1f, 1f),
        Inheritance = 0.6f,
        Drag = 0.9f,
    };

    public override void PostAI()
    {
        base.PostAI();

        if (Main.dedServ || !Main.rand.NextBool(4))
            return;

        Dust leaf = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, Strain.Dust, 0f, 0f, 0, default, 0.7f);
        leaf.velocity = -Projectile.velocity * 0.15f;
        leaf.noGravity = true;
    }

    protected override void Shatter()
    {
        FieldSystem.GetLayer<MudField>()?.SpawnBurst(MudSplash, Projectile.Center, BurstDirection(), Grounded, Projectile.owner);

        if (Field is not VineField vines)
            return;

        Vector2 momentum = Grounded && ImpactNormal.Y <= 0f ? -ImpactNormal * 2f : Vector2.Zero;
        momentum.X += Projectile.oldVelocity.X * 0.2f;

        float volume = VINE_VOLUME * (Payload?.Stats.BurstCountMultiplier ?? 1f);

        vines.Sprout(Projectile.Center, momentum, volume, Strain, Projectile.owner, Projectile.damage, Projectile.knockBack, Payload);
    }
}
