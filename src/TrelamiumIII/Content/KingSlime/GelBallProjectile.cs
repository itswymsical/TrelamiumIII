using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Graphics.Fields;

namespace TrelamiumIII.Content.KingSlime;

/// <summary>
///     The King Slime's gel blob: the blaster's shot, hostile. Its gel belongs to no player, so the coat it leaves
///     slows everyone and its nodes never afflict NPCs.
/// </summary>
public class GelBallProjectile : GelShotProjectile
{
    public override string Texture => AssetReferences.Content.KingSlime.GelBallProj.KEY;

    protected override int NodeOwner => FieldNode.NoOwner;

    protected override ChemicalPayload? Payload => null;

    protected override int NodeCeiling => GelVolley.NODE_CEILING;

    public override void SetDefaults()
    {
        base.SetDefaults();

        Projectile.hostile = true;
    }

    /// <summary>Shots spawned with no damage are scenery.</summary>
    public override bool CanHitPlayer(Player target) => Projectile.damage > 0;

    public override void OnHitPlayer(Player target, Player.HurtInfo info)
    {
        target.AddBuff(BuffID.Slow, 5 * 60);
        target.AddBuff(BuffID.Slimed, 5 * 60);

        BurstOnHit();

        // Vanilla does not spend a hostile projectile's penetration on a player hit.
        Projectile.Kill();
    }
}
