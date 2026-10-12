using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Content.Items.Weapons;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Components.Items;
using TrelamiumIII.Core.Gas;
using TrelamiumIII.Core.Gas.Layers;
using TrelamiumIII.Core.Graphics.Fields;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Content.Items.Ammo;

/// <summary>
///     Canister Cannon ammo, one instance per gas and <see cref="CanisterFuse" />. <see cref="GasCanisterProj" /> reads the
///     gas layer, fuse and burst from the item its <c>ai[0]</c> names.
/// </summary>
public sealed class GasCanisterAmmo : ModItem, ICustomAutoload
{
    /// <summary>Ticks from launch to burst for a <see cref="CanisterFuse.Timed" /> canister.</summary>
    public const int FuseTicks = 90;

    /// <param name="TimedDamage">Direct-hit damage of the timed canister; <paramref name="ContactDamage" /> likewise.</param>
    private sealed record Gas(
        string Name,
        Func<GasField?> Field,
        BurstProfile Burst,
        ChemicalDamageType Element,
        int ChemicalDamage,
        int TimedDamage,
        int ContactDamage);

    [CloneByReference]
    private readonly Gas gas;

    private GasCanisterAmmo(Gas gas, CanisterFuse fuse)
    {
        this.gas = gas;
        Fuse = fuse;
    }

    public static int TimedPoison { get; private set; }

    public static int ContactPoison { get; private set; }

    public static int TimedHyperborean { get; private set; }

    public static int ContactHyperborean { get; private set; }

    public static int TimedVilespore { get; private set; }

    public static int ContactVilespore { get; private set; }

    public CanisterFuse Fuse { get; }

    public GasField? Field => gas.Field();

    public BurstProfile Burst => gas.Burst;

    protected override bool CloneNewInstances => true;

    public override string Name => $"{Fuse}{gas.Name}Canister";

    public override string Texture => AssetReferences.Content.Items.Weapons.Misc.GasCanister.KEY;

    public static void Autoload(Mod mod)
    {
        var poison = new Gas("Poison", static () => FieldSystem.GetLayer<PoisonGasField>(),
            BurstProfile.Vent with
            {
                AirSpeed = new FloatRange(1.5f, 5f),
                GroundSpeed = new FloatRange(1f, 3f),
                Core = EmitterProfile.Default with { NodeRadius = 14f, Lifetime = 22, Drag = 0.9f, Inheritance = 1 },
                DamageShare = 1f,
            },
            ChemicalDamageType.Caustic, ChemicalDamage: 6, TimedDamage: 6, ContactDamage: 6);

        var hyperborean = new Gas("Hyperborean", static () => FieldSystem.GetLayer<IceMistField>(),
            BurstProfile.Vent with
            {
                AirSpeed = new FloatRange(1.5f, 5f),
                GroundSpeed = new FloatRange(1f, 3f),
                Core = EmitterProfile.Ballistic with { NodeRadius = 22f, Lifetime = 22, Drag = 0.2f },
                DamageShare = 0.12f,
            },
            ChemicalDamageType.Cryonic, ChemicalDamage: 6, TimedDamage: 6, ContactDamage: 6);

        var vilespore = new Gas("Vilespore", static () => FieldSystem.GetLayer<VilesporeField>(),
            BurstProfile.Vent with
            {
                AirSpeed = new FloatRange(1.5f, 5f),
                GroundSpeed = new FloatRange(1f, 3f),
                Core = EmitterProfile.Ballistic with { NodeRadius = 22f, Lifetime = 22, Drag = 0.2f },
                DamageShare = 1f,
            },
            ChemicalDamageType.Necrotic, ChemicalDamage: 2, TimedDamage: 7, ContactDamage: 2);

        TimedPoison = Add(mod, poison, CanisterFuse.Timed);
        ContactPoison = Add(mod, poison, CanisterFuse.Contact);
        TimedHyperborean = Add(mod, hyperborean, CanisterFuse.Timed);
        ContactHyperborean = Add(mod, hyperborean, CanisterFuse.Contact);
        TimedVilespore = Add(mod, vilespore, CanisterFuse.Timed);
        ContactVilespore = Add(mod, vilespore, CanisterFuse.Contact);
    }

    private static int Add(Mod mod, Gas gas, CanisterFuse fuse)
    {
        var item = new GasCanisterAmmo(gas, fuse);
        mod.AddContent(item);

        return item.Type;
    }

    public override void SetDefaults()
    {
        Item.DamageType = ModContent.GetInstance<ChemicalDamageClass>();
        Item.damage = Fuse == CanisterFuse.Timed ? gas.TimedDamage : gas.ContactDamage;
        Item.width = 24;
        Item.height = 16;
        Item.maxStack = Item.CommonMaxStack;
        Item.consumable = true;
        Item.ammo = ModContent.ItemType<CanisterCannon>();
        Item.shoot = ModContent.ProjectileType<GasCanisterProj>();
        Item.rare = ItemRarityID.Blue;

        if (!Item.TryEnable(out ChemicalSourceComponent component))
            return;

        component.ChemicalType = gas.Element;
        component.ChemicalDamage = gas.ChemicalDamage;
        component.debuffRate = 30;
        component.debuffDuration = TimeConversion.SetTime(secs: 6);
        component.ReactionChance = 2;
        component.ReactionDamage = 5;
    }
}
