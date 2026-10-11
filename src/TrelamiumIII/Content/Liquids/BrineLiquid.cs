using ModLiquidLib.ModLoader;
using ModLiquidLib.Utils.Structs;
using TrelamiumIII.Content.Alchemy.Tiles;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Liquids;
using Terraria.GameContent.Liquid;

namespace TrelamiumIII.Content.Liquids;

public class BrineLiquid : ModLiquid, ILiquidSurface
{
    /// <summary>Thin salt strands drifting through it.</summary>
    public LiquidSurface Surface { get; } = new("Brine")
    {
        ColorA = new Vector3(0.95f, 0.97f, 1f),
        ColorB = new Vector3(0.72f, 0.80f, 0.90f),
        Strength = 0.55f,
    };

    //public override string BlockTexture => "Terraria/Images/Liquid_0";
    //public override string SlopeTexture => "Terraria/Images/Liquid_0";

    public override void SetStaticDefaults()
    {
        LiquidRenderer.WATERFALL_LENGTH[Type] = 3;
        LiquidRenderer.DEFAULT_OPACITY[Type] = 0.75f;
        SlopeOpacity = 0.8f;
        LiquidRenderer.VISCOSITY_MASK[Type] = 200;
        FallDelay = 1;
        SplashDustType = ModContent.DustType<BrineLiquidSplash>();
        SplashSound = SoundID.Splash;
        ChecksForDrowning = true;
        AllowEmitBreathBubbles = false;
        AddMapEntry(new Color(177, 167, 187));
    }

    public override int ChooseWaterfallStyle(int i, int j)
    {
        return ModContent.GetInstance<BrineLiquidfall>().Slot;
    }

    public override bool PreLiquidMerge(int liquidX, int liquidY, int tileX, int tileY, int otherLiquid)
    {
        if (otherLiquid == LiquidID.Water)
        {
            WorldGen.PlaceTile(tileX, tileY, ModContent.TileType<PureSaltTile>());
            return false;
        }
        else
        {
            if (otherLiquid == LiquidID.Honey)
            {
                WorldGen.PlaceTile(tileX, tileY, TileID.HoneyBlock);
                return false;
            }
            else if (otherLiquid == LiquidID.Shimmer)
            {
                WorldGen.PlaceTile(tileX, tileY, TileID.ShimmerBlock);
                return false;
            }
            else if (otherLiquid == LiquidID.Lava)
            {
                WorldGen.PlaceTile(tileX, tileY, TileID.Obsidian);
                return false;
            }
        }

        return base.PreLiquidMerge(liquidX, liquidY, tileX, tileY, otherLiquid);
    }

    public override void RetroDrawEffects(int i, int j, SpriteBatch spriteBatch, ref RetroLiquidDrawInfo drawData, float liquidAmountModified, int liquidGFXQuality)
    {
        drawData.liquidAlphaMultiplier *= 1.5f;
        if (drawData.liquidAlphaMultiplier > 1f)
        {
            drawData.liquidAlphaMultiplier = 1f;
        }
    }
}

public class BrineLiquidSplash : ModDust
{
    public override void OnSpawn(Dust dust)
    {
        dust.velocity *= 0.1f;
        dust.velocity.Y = -0.5f;
        //dust.type = DustID.Water;
    }

    public override bool MidUpdate(Dust dust)
    {
        if (dust.noGravity)
        {
            dust.scale += 0.03f;
            if (dust.scale < 1f)
            {
                dust.velocity.Y += 0.075f;
            }
            dust.velocity.X *= 1.08f;
            if (dust.velocity.X > 0f)
            {
                dust.rotation += 0.01f;
            }
            else
            {
                dust.rotation -= 0.01f;
            }
        }
        else
        {
            if (!Collision.WetCollision(new Vector2(dust.position.X, dust.position.Y - 8f), 4, 4))
            {
                dust.scale = 0f;
            }
            else
            {
                dust.alpha += Main.rand.Next(2);
                if (dust.alpha > 255)
                {
                    dust.scale = 0f;
                }
                dust.velocity.Y = -0.5f;

                dust.alpha++;
                dust.scale -= 0.01f;
                dust.velocity.Y = -0.2f;

                dust.velocity.X += Main.rand.Next(-10, 10) * 0.002f;
                if (dust.velocity.X < -0.25)
                {
                    dust.velocity.X = -0.25f;
                }
                if (dust.velocity.X > 0.25)
                {
                    dust.velocity.X = 0.25f;
                }
            }
        }
        return true;
    }
}

public class BrineLiquidfall : ModWaterfallStyle
{
    public override string Texture => AssetReferences.Content.Liquids.BrineLiquidfall.KEY;
}