using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Content.Misc;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Components.Items;
using TrelamiumIII.Core.Gas;
using TrelamiumIII.Core.Gas.Layers;
using TrelamiumIII.Utilities;
using TrelamiumIII.Core.Graphics.Fields;

namespace TrelamiumIII.Content.Ammo;

public abstract class VilesporeCanister : GasCanisterAmmo
{
    public override string Texture => AssetReferences.Content.Misc.GasCanister.KEY;

    public override GasField? Field => FieldSystem.GetLayer<VilesporeField>();

    public override BurstProfile Burst => BurstProfile.Vent with
    {
        AirSpeed = new FloatRange(1.5f, 5f),
        GroundSpeed = new FloatRange(1f, 3f),
        Core = EmitterProfile.Ballistic with
        {
            NodeRadius = 22f,
            Lifetime = 22,
            Drag = 0.2f,
        },
        DamageShare = 1f,
    };

    public override void SetDefaults()
    {
        base.SetDefaults();

        Item.rare = ItemRarityID.Blue;
    }
}

public class TimedVilesporeCanister : VilesporeCanister
{
    public override CanisterFuse Fuse => CanisterFuse.Timed;
    public override void SetDefaults()
    {
        base.SetDefaults();
        if (!Item.TryEnable(out ChemicalSourceComponent component))
            return;

        Item.damage = 7;
        component.ChemicalType = ChemicalDamageType.Necrotic;
        component.ChemicalDamage = 2f;
        component.AfflictionRate = 30;
        component.AfflictionDuration = TimeConversion.SetTime(secs: 6);
        component.ReactionChance = 0.002f;
        component.ReactionDamage = 5f;
    }
}

public class ContactVilesporeCanister : VilesporeCanister
{
    public override CanisterFuse Fuse => CanisterFuse.Contact;
    public override void SetDefaults()
    {
        base.SetDefaults();
        if (!Item.TryEnable(out ChemicalSourceComponent component))
            return;

        Item.damage = 2;
        component.ChemicalType = ChemicalDamageType.Necrotic;
        component.ChemicalDamage = 2f;
        component.AfflictionRate = 30;
        component.AfflictionDuration = TimeConversion.SetTime(secs: 6);
        component.ReactionChance = 0.002f;
        component.ReactionDamage = 5f;
    }
}