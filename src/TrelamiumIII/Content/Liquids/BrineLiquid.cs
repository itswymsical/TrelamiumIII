using ModLiquidLib.ModLoader;
using ModLiquidLib.Utils.Structs;
using TrelamiumIII.Core;
using Terraria.GameContent.Liquid;

namespace TrelamiumIII.Content.Liquids;

public class BrineLiquid : ModLiquid
{
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
            WorldGen.PlaceTile(tileX, tileY, ModContent.TileType<SaltBlockTile>());
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

    /// <summary>
    /// Scrolling salt overlay over the vanilla water draw, world-anchored so it's continuous across tiles. Slices straddling
    /// the noise texture's edge are split, avoiding a SpriteBatch restart for a wrapping sampler.
    /// </summary>
    public override void PostDraw(int i, int j, LiquidRenderer.LiquidDrawCache liquidDrawCache, Vector2 drawOffset, bool isBackgroundDraw)
    {
        if (isBackgroundDraw || !liquidDrawCache.IsVisible)
            return;

        Rectangle sr = liquidDrawCache.SourceRectangle;
        if (sr.Width <= 0 || sr.Height <= 0)
            return;

        Texture2D noise = Assets.Textures.Noise.Melt_02.Asset.Value;
        if (noise == null)
            return;

        Vector2 destPos = new Vector2(i * 16f, j * 16f) - Main.screenPosition + drawOffset + liquidDrawCache.LiquidOffset;

        float t = Main.GlobalTimeWrappedHourly;
        int scrollX = (int)(t * 6f);
        int scrollY = -(int)(t * 3f);
        int worldU = i * 16 + (int)liquidDrawCache.LiquidOffset.X + scrollX;
        int worldV = j * 16 + (int)liquidDrawCache.LiquidOffset.Y + scrollY;
        int srcU = ((worldU % noise.Width) + noise.Width) % noise.Width;
        int srcV = ((worldV % noise.Height) + noise.Height) % noise.Height;

        Color light = Lighting.GetColor(i, j);
        Color salt = new(
            (byte)(220 * light.R / 255),
            (byte)(225 * light.G / 255),
            (byte)(235 * light.B / 255));
        salt *= 0.18f * liquidDrawCache.Opacity;

        DrawWrapped(Main.spriteBatch, noise, destPos, srcU, srcV, sr.Width, sr.Height, salt);
    }

    private static void DrawWrapped(SpriteBatch sb, Texture2D tex, Vector2 destPos, int srcU, int srcV, int w, int h, Color color)
    {
        int firstW = Math.Min(w, tex.Width - srcU);
        int firstH = Math.Min(h, tex.Height - srcV);

        sb.Draw(tex, destPos, new Rectangle(srcU, srcV, firstW, firstH), color);
        if (firstW < w)
            sb.Draw(tex, destPos + new Vector2(firstW, 0f), new Rectangle(0, srcV, w - firstW, firstH), color);
        if (firstH < h)
            sb.Draw(tex, destPos + new Vector2(0f, firstH), new Rectangle(srcU, 0, firstW, h - firstH), color);
        if (firstW < w && firstH < h)
            sb.Draw(tex, destPos + new Vector2(firstW, firstH), new Rectangle(0, 0, w - firstW, h - firstH), color);
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