using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent.Liquid;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Liquids;

namespace TrelamiumIII.Content.Liquids;

public class OilLiquids : ConditionalLiquid, ILiquidSurface
{
    public static OilLiquids Instance { get; private set; } = null!;

    public LiquidSurface Surface => new("Acid")
    {
        ColorA = Rgb(255, 255, 104),
        ColorB = Rgb(254, 237, 154),
    };
    public Color SurfaceTint(int i, int j)
        => ConditionAt(i, j).SurfaceTint;

    public LiquidCondition Sunflower { get; private set; } = null!;

    public override string TextureBase => AssetReferences.Content.Liquids.BaseLiquid.KEY;

    public override void Load()
    {
        base.Load();
        Instance = this;
    }
    public override void Unload()
    {
        Instance = null!;
        base.Unload();
    }

    protected override void DefineConditions()
    {
        Sunflower = AddCondition(new LiquidCondition("Sunflower") { Tint = new Color(252, 228, 137) });
    }

    public override void SetStaticDefaults()
    {
        base.SetStaticDefaults();

        LiquidRenderer.WATERFALL_LENGTH[Type] = 3;
        LiquidRenderer.DEFAULT_OPACITY[Type] = 0.95f;
        SlopeOpacity = 0.9f;
        LiquidRenderer.VISCOSITY_MASK[Type] = 200;
        FallDelay = 3;
        SplashDustType = ModContent.DustType<OilLiquidSplash>();

        SplashSound = SoundID.Splash with { Pitch = -.98f };
        ChecksForDrowning = true;
        AllowEmitBreathBubbles = false;
    }

    protected override Color MapColor(LiquidCondition condition) => Multiply(new Color(252, 228, 137), condition.Tint);


    protected override void OnPlayerContact(Player player, LiquidCondition condition)
    {
        player.moveSpeed *= 0.1f;
        player.velocity.Y = Math.Max(player.velocity.Y, -2f);
    }

    public override bool PreLiquidMerge(int liquidX, int liquidY, int tileX, int tileY, int otherLiquid)
    {
        if (otherLiquid == LiquidID.Lava)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {

                int y = liquidY;
                Tile tile = Main.tile[liquidX, liquidY];

                if (tile.HasTile && tile.IsHalfBlock)
                {
                    int yPos = y - 1;
                    if (yPos >= 0)
                    {
                        tile = Main.tile[liquidX, yPos];
                        if (!WorldGen.SolidOrSlopedTile(tile))
                        {
                            y--;
                        }
                    }
                }

                DelegateMethods.v2_1 = new Vector2(liquidX, y);
                DelegateMethods.f_1 = 3.5f;
                Utils.PlotTileArea(liquidX, y, DelegateMethods.SpreadDry);
            }


            if (Main.netMode == NetmodeID.SinglePlayer)
                ExplosionEffect(liquidX, liquidY);


            if (Main.netMode == NetmodeID.Server)
            {
                ModPacket packet = ModContent.GetInstance<TrelamiumIII>().GetPacket();
                packet.Write((byte)TrelamiumIII.MessageType.LiquidMergeExplosion);
                packet.Write(liquidX);
                packet.Write(liquidY);
                packet.Send();
            }
        }
        return false;
    }

    public static void ExplosionEffect(int x, int y)
    {
        Vector2 position = new Vector2(x * 16, y * 16);
        SoundEngine.PlaySound(SoundID.Item14, position);
        int dustType = DustID.Smoke;

        for (int i = 0; i < 30; i++)
        {
            Dust dust = Dust.NewDustDirect(position, 22, 22, dustType, 0f, 0f, 100, Color.Transparent, 1.5f);
            dust.velocity *= 1.4f;
        }

        for (int i = 0; i < 80; i++)
        {
            Dust dust = Dust.NewDustDirect(position, 22, 22, dustType, 0f, 0f, 100, Color.Transparent, 1.2f);
            dust.velocity *= 7f;
            dust = Dust.NewDustDirect(position, 22, 22, dustType, 0f, 0f, 100, Color.Transparent, 0.3f);
            dust.velocity *= 4f;
        }

        for (int i = 1; i <= 2; i++)
        {
            for (int j = -1; j <= 1; j += 2)
            {
                for (int k = -1; k <= 1; k += 2)
                {
                    Gore gore = Gore.NewGoreDirect(new EntitySource_Misc("Liquid Explosion"), position, Vector2.Zero, Main.rand.Next(GoreID.Smoke1, GoreID.Smoke3 + 1));
                    gore.velocity *= ((i == 1) ? 0.4f : 0.8f);
                    gore.velocity += new Vector2(j, k);
                }
            }
        }
    }
}