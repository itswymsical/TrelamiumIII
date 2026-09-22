using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Content.Misc;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Components.Items;
using TrelamiumIII.Core.Gas;
using TrelamiumIII.Core.Gas.Layers;
using TrelamiumIII.Utilities;
using TrelamiumIII.Core.Graphics.Fields;

namespace TrelamiumIII.Content.Items.Ammo;

/// <summary>Canister Cannon ammo that bursts into <see cref="PoisonGasField" />.</summary>
public abstract class PoisonCanister : GasCanisterAmmo
{
    public override string Texture => AssetReferences.Content.Items.Weapons.Misc.GasCanister.KEY;

    public override GasField? Field => FieldSystem.GetLayer<PoisonGasField>();

    /// <summary>Slow, growing arms so the burst settles into one cloud around the canister.</summary>
    public override BurstProfile Burst => BurstProfile.Vent with
    {
        AirSpeed = new FloatRange(1.5f, 5f),
        GroundSpeed = new FloatRange(1f, 3f),
        Core = EmitterProfile.Default with
        {
            NodeRadius = 14f,
            Lifetime = 22,
            Drag = 0.9f,
            Inheritance = 1,
        },
        DamageShare = 1f,
    };

    public override void SetDefaults()
    {
        base.SetDefaults();

        Item.rare = ItemRarityID.Blue;
    }
}

public class TimedPoisonCanister : PoisonCanister
{
    public override CanisterFuse Fuse => CanisterFuse.Timed;
    public override void SetDefaults()
    {
        base.SetDefaults();
        if (!Item.TryEnable(out ChemicalSourceComponent component))
            return;

        Item.damage = 6;
        component.ChemicalType = ChemicalDamageType.Caustic;
        component.ChemicalDamage = 6f;
        component.AfflictionRate = 30;
        component.AfflictionDuration = TimeConversion.SetTime(secs: 6);
        component.ReactionChance = 0.002f;
        component.ReactionDamage = 5f;
    }
}

public class ContactPoisonCanister : PoisonCanister
{
    public override CanisterFuse Fuse => CanisterFuse.Contact;
    public override void SetDefaults()
    {
        base.SetDefaults();
        if (!Item.TryEnable(out ChemicalSourceComponent component))
            return;

        Item.damage = 6;
        component.ChemicalType = ChemicalDamageType.Caustic;
        component.ChemicalDamage = 6f;
        component.AfflictionRate = 30;
        component.AfflictionDuration = TimeConversion.SetTime(secs: 6);
        component.ReactionChance = 0.002f;
        component.ReactionDamage = 5f;
    }
}