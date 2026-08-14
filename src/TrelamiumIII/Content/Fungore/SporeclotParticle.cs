using TrelamiumIII.Core;
using TrelamiumIII.Core.Graphics.Particles;
using TrelamiumIII.Core.Graphics.Metaballs.Layers;

namespace TrelamiumIII.Content.Fungore;

internal sealed class SporeclotParticle : Particle
{
    public const int HUSK = 0;

    public const int CHUNK = 1;

    public const int SHARD = 2;

    public override string[] Textures =>
    [
        AssetReferences.Content.Fungore.Sporeclot_Gore_3.KEY,
        AssetReferences.Content.Fungore.Sporeclot_Gore_1.KEY,
        AssetReferences.Content.Fungore.Sporeclot_Gore_2.KEY,
    ];

    // Footprints are kept well under the sprites: the art is a ragged silhouette, and a box drawn
    // to its full extent leaves chunks resting on empty pixels a few px off the ground.
    public override Vector2 Size => Variant switch
    {
        HUSK => new Vector2(16f),
        CHUNK => new Vector2(12f),
        _ => new Vector2(8f),
    };

    public override float Bounce => Variant switch
    {
        HUSK => 0.15f,
        CHUNK => 0.25f,
        _ => 0.35f,
    };

    public override float Gravity => 0.26f;

    public override float GroundFriction => 0.78f;

    public override int FadeOutTicks => 25;

    /// <summary>
    ///     Long-lived enough to still be lying there once the fight moves on, which is the whole
    ///     reason it is worth being able to kick them.
    /// </summary>
    public override bool PlayerCollide => true;

    private int spawnedSpores;

    public override void OnSpawn()
    {
        RotationVelocity = Main.rand.NextFloat(-0.35f, 0.35f);
        Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
    }

    public override void Update()
    {
        // A trail of spores while it is still airborne and wet, thinning out as it dries. Capped,
        // so a big burst cannot flood the metaball field on its own.
        if (OnGround || InLiquid || spawnedSpores >= 4)
            return;

        if (Progress > 0.5f || !Main.rand.NextBool(9))
            return;

        spawnedSpores++;
        SporeLayer.Trail(Position, Velocity * 0.2f, Variant == HUSK ? 9f : 6f);
    }

    public override void OnTileCollide(Vector2 oldVelocity)
    {
        // Only a real impact puffs; the little settling hops at the end of a roll should not.
        if (oldVelocity.Length() < 3.5f)
            return;

        SporeLayer.TileSplat(Position, oldVelocity);
    }

    public override void OnPlayerCollide(Player player)
    {
        // Kicking a husk that still has something in it shakes a little loose.
        if (Progress < 0.6f && Main.rand.NextBool(2))
            SporeLayer.Trail(Position, Velocity * 0.25f, 7f);
    }

    /// <summary>Water washes the spores out; the husk sinks and goes quiet.</summary>
    public override void OnEnterLiquid(int liquid)
    {
        spawnedSpores = int.MaxValue;

        if (liquid == LiquidID.Lava)
            Kill();
    }
}
