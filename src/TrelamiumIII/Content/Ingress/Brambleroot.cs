using Terraria.DataStructures;
using TrelamiumIII.Utilities.Extensions;

namespace TrelamiumIII.Content.Ingress;

public class Brambleroot : ModItem
{
    public override void SetDefaults()
    {
        Item.DefaultToPlaceableTile(ModContent.TileType<BramblerootTile>());
    }
}

public class BramblerootTile : ModTile
{
    public override void SetStaticDefaults()
    {
        Main.tileSolid[Type] = true;
        Main.tileMergeDirt[Type] = true;

        this.Merge(TileID.Mud, TileID.Stone, ModContent.TileType<BramblewoodTile>(), ModContent.TileType<PeatTile>(), Type);
        Main.tileBlockLight[Type] = true;

        DustType = DustID.t_LivingWood;
        MineResist = 1f;
        HitSound = SoundID.Dig;

        // AnimationFrameHeight = 90;
        TileID.Sets.GemsparkFramingTypes[Type] = Type;

        AddMapEntry(new Color(182, 144, 108));
    }

    public override bool TileFrame(int i, int j, ref bool resetFrame, ref bool noBreak)
    {
        Framing.SelfFrame8Way(i, j, Main.tile[i, j], resetFrame);
        return false;
    }

    public override void DrawEffects(int i, int j, SpriteBatch spriteBatch, ref TileDrawInfo drawData)
    {
        base.DrawEffects(i, j, spriteBatch, ref drawData);
    }

    const int frameHeight = 90;
    const int frameWidth = 324;
    public override void AnimateIndividualTile(int type, int i, int j, ref int frameXOffset, ref int frameYOffset)
    {
        var xPos = i % 2;
        var yPos = j % 2;
        frameXOffset = xPos * frameWidth;
        frameYOffset = yPos * frameHeight;
    }
}
