using ReLogic.Content;
using Terraria.GameContent.Drawing;
using Terraria.GameContent.Metadata;
using TrelamiumIII.Core.Graphics.Particles;

namespace TrelamiumIII.Content.Ingress;

/// <summary>
/// Hangs from Ingress grass. Drawn by vanilla's swaying vine pass; the glow mask is supplied through
/// <see cref="TileDrawing.GetTileDrawData"/> so it sways with the strip. Particles come from <see cref="OusiaParticleEmitter"/>.
/// </summary>
public class OusiaVineTile : ModTile
{
    /// <summary> Alpha 0 so the glow adds light inside the alpha-blended vine batch. </summary>
    private static readonly Color GlowColor = new Color(117, 218, 199, 0) * 0.6f;

    private static Asset<Texture2D>? glowMask;
    private static int vineType = -1;

    public override void Load() => On_TileDrawing.GetTileDrawData += AddGlowMask;

    public override void SetStaticDefaults()
    {
        vineType = Type;

        if (!Main.dedServ)
            glowMask = ModContent.Request<Texture2D>($"{Texture}_Glow");

        TileID.Sets.VineThreads[Type] = true;
        TileID.Sets.IsVine[Type] = true;
        TileID.Sets.SwaysInWindBasic[Type] = true;

        Main.tileCut[Type] = true;
        Main.tileLavaDeath[Type] = true;
        Main.tileBlockLight[Type] = true;
        Main.tileNoFail[Type] = true;
        Main.tileNoAttach[Type] = true;
        Main.tileLighted[Type] = true;
        HitSound = SoundID.Grass;
        DustType = DustID.JunglePlants;

        TileMaterials.SetForTileId(Type, TileMaterials._materialsByName["Plant"]);
        AddMapEntry(new Color(23, 122, 124));
    }
    public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
    {
        r = 0.15f;
        g = 0.2f;
        b = 0.24f;
    }
    public override void KillTile(int i, int j, ref bool fail, ref bool effectOnly, ref bool noItem)
    {
        var tile = Framing.GetTileSafely(i, j + 1);
        if (tile.HasTile && tile.TileType == Type)
        {
            WorldGen.KillTile(i, j + 1);
        }
    }
    public override bool TileFrame(int i, int j, ref bool resetFrame, ref bool noBreak)
    {
        var tile = Framing.GetTileSafely(i, j - 1);
        var type = -1;
        if (tile.HasTile && !tile.BottomSlope)
            type = tile.TileType;

        if (type == ModContent.TileType<IngressGrass>() || type == Type) {
            return true;
        }

        WorldGen.KillTile(i, j);
        return true;
    }
    public override void RandomUpdate(int i, int j)
    {
        var tile = Framing.GetTileSafely(i, j + 1);
        if (WorldGen.genRand.NextBool(10) && !tile.HasTile && !(tile.LiquidType == LiquidID.Lava))
        {
            var placed = false;
            var Test = j;
            while (Test > j - 10)
            {
                var testTile = Framing.GetTileSafely(i, Test);
                if (testTile.BottomSlope)
                {
                    break;
                }
                else if (!testTile.HasTile || testTile.TileType != ModContent.TileType<IngressGrass>())
                {
                    Test--;
                    continue;
                }
                placed = true;
                break;
            }
            if (placed && CanGrowMoreVines(i, j + 1))  // Add this condition
            {
                tile.TileType = Type;
                tile.HasTile = true;
                WorldGen.SquareTileFrame(i, j + 1, true);
                if (Main.netMode == NetmodeID.Server)
                {
                    NetMessage.SendTileSquare(-1, i, j + 1, 3, TileChangeType.None);
                }
            }
        }
    }
    private static bool CanGrowMoreVines(int centerX, int centerY)
    {
        const int WorldBoundary = 30;
        if (!WorldGen.InWorld(centerX, centerY, WorldBoundary))
            return false;

        const int HorizontalSearchRange = 4;
        const int VerticalSearchRangeUp = 6;
        const int VerticalSearchRangeDown = 10;
        const int MaxVineCount = 60;
        var SpecialVineType = ModContent.TileType<OusiaVineTile>();

        var vineCount = 0;
        var adjustedMaxVineCount = Main.tile[centerX, centerY].TileType == SpecialVineType
            ? MaxVineCount / 5
            : MaxVineCount;

        for (var x = centerX - HorizontalSearchRange; x <= centerX + HorizontalSearchRange; x++)
        {
            for (var y = centerY - VerticalSearchRangeUp; y <= centerY + VerticalSearchRangeDown; y++)
            {
                if (TileID.Sets.IsVine[Main.tile[x, y].TileType])
                {
                    vineCount++;

                    if (y > centerY && CanDrawLineBetweenPoints(centerX, centerY, x, y))
                    {
                        var verticalDistance = y - centerY;
                        vineCount += Main.tile[x, y].TileType != SpecialVineType
                            ? verticalDistance * 2
                            : verticalDistance * 20;
                    }

                    if (vineCount > adjustedMaxVineCount)
                        return false;
                }
            }
        }
        return true;
    }
    private static bool CanDrawLineBetweenPoints(int x1, int y1, int x2, int y2)
    {
        return Collision.CanHitLine(
            new Vector2(x1 * 16, y1 * 16), 1, 1,
            new Vector2(x2 * 16, y2 * 16), 1, 1
        );
    }

    /// <summary> Registers the strip with vanilla's vine pass instead of drawing here; the crawl skips strips already registered. </summary>
    public override bool PreDraw(int i, int j, SpriteBatch spriteBatch)
    {
        Main.instance.TilesRenderer.CrawlToTopOfVineAndAddSpecialPoint(j, i);
        return false;
    }

    private static void AddGlowMask(On_TileDrawing.orig_GetTileDrawData orig, TileDrawing self, int x, int y, Tile tileCache, ushort typeCache,
        ref short tileFrameX, ref short tileFrameY, out int tileWidth, out int tileHeight, out int tileTop, out int halfBrickHeight, out int addFrX,
        out int addFrY, out SpriteEffects tileSpriteEffect, out Texture2D glowTexture, out Rectangle glowSourceRect, out Color glowColor)
    {
        orig(self, x, y, tileCache, typeCache, ref tileFrameX, ref tileFrameY, out tileWidth, out tileHeight, out tileTop, out halfBrickHeight,
            out addFrX, out addFrY, out tileSpriteEffect, out glowTexture, out glowSourceRect, out glowColor);

        if (typeCache != vineType || glowMask is not { IsLoaded: true })
            return;

        glowTexture = glowMask.Value;
        glowSourceRect = new Rectangle(tileFrameX + addFrX, tileFrameY + addFrY, tileWidth, tileHeight - halfBrickHeight);
        glowColor = GlowColor;
    }
}

/// <summary> A faint blue orb that rises slowly and weaves sideways. Self-lit, drawn additively, and adds a little light. </summary>
public class OusiaParticle : Particle
{
    private const int LifetimeMin = 180;
    private const int LifetimeMax = 360;
    private const float ScaleMin = 0.05f;
    private const float ScaleMax = 0.09f;

    private const float RiseAcceleration = 0.004f;
    private const float WeaveAcceleration = 0.012f;
    private const float WeaveFrequency = 0.035f;
    private const float PulseFrequency = 0.07f;
    private const float FadeInFraction = 0.2f;

    private const float Intensity = 0.6f;
    private const float CoreScale = 0.35f;
    private const float LightStrength = 0.25f;

    private static readonly Color Deep = new(40, 110, 255);
    private static readonly Color Pale = new(120, 210, 255);
    private static readonly Color CoreColor = new(210, 240, 255);

    private float phase;

    public override string[] Textures => [Assets.Textures.VFX.RadialGradient.KEY];
    public override float Gravity => 0f;
    public override float Drag => 0.98f;
    public override bool TileCollide => false;
    public override bool LiquidCollide => false;
    public override bool UseLighting => false;
    public override int FadeOutTicks => 60;
    public override bool HasGlow => true;

    /// <summary> Fade-in times a slow pulse; <see cref="Particle.Opacity"/> covers the fade-out. </summary>
    private float Brightness => MathF.Min(Progress / FadeInFraction, 1f) * (0.75f + 0.25f * MathF.Sin((MaxTimeLeft - TimeLeft) * PulseFrequency + phase));

    /// <summary> An orb at <paramref name="position"/> in world pixels, with random drift, size, colour and lifetime. </summary>
    public static void Spawn(Vector2 position)
    {
        ParticleSystem.Spawn<OusiaParticle>(
            position,
            new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.05f, 0.25f)),
            Main.rand.Next(LifetimeMin, LifetimeMax + 1),
            scale: Main.rand.NextFloat(ScaleMin, ScaleMax),
            color: Color.Lerp(Deep, Pale, Main.rand.NextFloat()));
    }

    public override void OnSpawn() => phase = Main.rand.NextFloat(MathHelper.TwoPi);

    public override void Update()
    {
        Velocity.Y -= RiseAcceleration;
        Velocity.X += MathF.Sin((MaxTimeLeft - TimeLeft) * WeaveFrequency + phase) * WeaveAcceleration;

        Lighting.AddLight(Position, Color.ToVector3() * (LightStrength * Brightness * Opacity));
    }

    /// <summary> Additive only; see <see cref="DrawGlow"/>. </summary>
    public override bool PreDraw(SpriteBatch spriteBatch, Texture2D texture, Vector2 drawPosition, Color lightColor) => false;

    public override void DrawGlow(SpriteBatch spriteBatch, Texture2D texture, Vector2 drawPosition, float opacity)
    {
        float alpha = Brightness * opacity * Intensity;

        if (alpha <= 0f)
            return;

        Vector2 origin = texture.Size() * 0.5f;

        spriteBatch.Draw(texture, drawPosition, null, Color * alpha, 0f, origin, Scale, SpriteEffects.None, 0f);
        spriteBatch.Draw(texture, drawPosition, null, CoreColor * alpha, 0f, origin, Scale * CoreScale, SpriteEffects.None, 0f);
    }
}
