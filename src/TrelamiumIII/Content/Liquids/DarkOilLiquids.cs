using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent.Liquid;
using TrelamiumIII.Content.Alchemy.Reagents;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Liquids;

namespace TrelamiumIII.Content.Liquids;

/// <summary>Oil base: crude by default. Pitch is the heavy residue, slower to flow and harder to wade.</summary>
public class DarkOilLiquids : ConditionalLiquid, ILiquidSurface
{
    public static DarkOilLiquids Instance { get; private set; } = null!;

    private const int PitchFlowInterval = 4;

    public LiquidSurface Surface => new("Oil")
    {
        ColorA = Rgb(43, 33, 56),
        ColorB = Rgb(5, 5, 5),
        Scale = 1f,
        Speed = 0.7f,
    };

    public LiquidSurface? SurfaceAt(int i, int j)
    {
        LiquidCondition condition = ConditionAt(i, j);

        // Conditions without their own surface inherit the default style.
        return condition.Surface ?? Default.Surface;
    }

    public Color SurfaceTint(int i, int j)
        => ConditionAt(i, j).SurfaceTint;

    public LiquidCondition Crude { get; private set; } = null!;

    public LiquidCondition Pitch { get; private set; } = null!;
    public override string TextureBase => AssetReferences.Content.Liquids.CrudeOilLiquid.KEY;

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
        Crude = AddCondition(new LiquidCondition("Crude"));

        Pitch = AddCondition(new LiquidCondition("Pitch")
        {
            Tint = new Color(77, 45, 79),
            Strength = 0.88f,
        });
    }

    /// <summary>The station's Bottled Oil + 4 Cobweb = 2 Pitch, with a bottle's worth of oil spent from the pool.</summary>
    protected override void DefineItemReactions()
        => AddItemReaction(Crude, new ItemReaction(ItemID.Cobweb)
        {
            Amount = 4,
            Ticks = 60,
            Spent = SubmergedLiquidSystem.BottleVolume,
            Product = SoluteID.Pitch,
            ProductStack = 2,
        });

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

    protected override Color MapColor(LiquidCondition condition) => Multiply(new Color(24, 22, 20), condition.Tint);

    public override bool UpdateLiquid(int i, int j, Liquid liquid)
    {
        if (Liquid.quickSettle || WorldGen.gen || ConditionAt(i, j) != Pitch)
            return true;

        return (Main.GameUpdateCount + (uint)(i * 7 + j * 13)) % PitchFlowInterval == 0;
    }
    protected override void OnPlayerContact(Player player, LiquidCondition condition) 
    {
        if (condition != Pitch)
        {
            player.moveSpeed *= 0.25f;
            return;
        }

        player.moveSpeed *= 0.1f;
        player.velocity.Y = Math.Max(player.velocity.Y, -2f);
    }

    public override bool PreLiquidMerge(int liquidX, int liquidY, int tileX, int tileY, int otherLiquid)
    {
        //TODO: let oil sit on top of water. Explode on contact with lava.
        if (otherLiquid == LiquidID.Lava)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {

                int y = liquidY;
                Tile tile = Main.tile[liquidX, liquidY];

                // We grab the tile above to explode at by checking if the tile is not solid or sloped
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

                // Same liquid removal dry bombs use.
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

public class OilLiquidSplash : ModDust
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