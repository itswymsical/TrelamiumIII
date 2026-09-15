using TrelamiumIII.Content.Misc;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Gas;
using TrelamiumIII.Core.Gas.Layers;

namespace TrelamiumIII.Content.Ammo;

/// <summary>Canister Cannon ammo that bursts into <see cref="PoisonGasLayer" />.</summary>
public abstract class PoisonCanister : GasCanisterAmmo
{
    public override string Texture => AssetReferences.Content.Misc.GasCanister.KEY;

    public override GasField? Field => GasSystem.GetLayer<PoisonGasLayer>();

    /// <summary>Slow, growing arms so the burst settles into one cloud around the canister.</summary>
    public override BurstProfile Burst => BurstProfile.Vent with
    {
        AirSpeed = new FloatRange(2f, 6f),
        GroundSpeed = new FloatRange(2f, 6f),
        Core = EmitterProfile.Default with
        {
            NodeRadius = 18f,
            NodeLife = 60f,
            Drag = 0.9f,
            Growth = 0.5f,
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
}

public class ContactPoisonCanister : PoisonCanister
{
    public override CanisterFuse Fuse => CanisterFuse.Contact;
}
