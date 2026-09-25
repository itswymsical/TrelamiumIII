using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Components.Items;
using TrelamiumIII.Core.Gas;
using TrelamiumIII.Core.Gas.Layers;
using TrelamiumIII.Utilities;
using TrelamiumIII.Core.Graphics.Fields;
using TrelamiumIII.Content.Items.Weapons;

namespace TrelamiumIII.Content.Items.Ammo;

public abstract class HyperboreanCanister : GasCanisterAmmo
{
    public override string Texture => AssetReferences.Content.Items.Weapons.Misc.GasCanister.KEY;

    public override GasField? Field => FieldSystem.GetLayer<IceMistField>();

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
        DamageShare = 0.12f,
    };

    public override void SetDefaults()
    {
        base.SetDefaults();

        Item.rare = ItemRarityID.Blue;
    }
}

public class TimedHyperboreanCanister : HyperboreanCanister
{
    public override CanisterFuse Fuse => CanisterFuse.Timed;
    public override void SetDefaults()
    {
        base.SetDefaults();
        if (!Item.TryEnable(out ChemicalSourceComponent component))
            return;

        Item.damage = 6;
        component.ChemicalType = ChemicalDamageType.Cryonic;
        component.ChemicalDamage = 6f;
        component.debuffRate = 30;
        component.debuffDuration = TimeConversion.SetTime(secs: 6);
        component.ReactionChance = 0.002f;
        component.ReactionDamage = 5f;
    }
}

public class ContactHyperboreanCanister : HyperboreanCanister
{
    public override CanisterFuse Fuse => CanisterFuse.Contact;
    public override void SetDefaults()
    {
        base.SetDefaults();
        if (!Item.TryEnable(out ChemicalSourceComponent component))
            return;

        Item.damage = 6;
        component.ChemicalType = ChemicalDamageType.Cryonic;
        component.ChemicalDamage = 6f;
        component.debuffRate = 30;
        component.debuffDuration = TimeConversion.SetTime(secs: 6);
        component.ReactionChance = 0.002f;
        component.ReactionDamage = 5f;
    }
}