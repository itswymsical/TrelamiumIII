using ReLogic.Content;

namespace TrelamiumIII.Common.Systems;

/// <summary>Blocks building inside protected structures, and fades the shield overlay while the local player holds a pick or hammer.</summary>
public sealed class StructureProtectionPlayer : ModPlayer
{
    private const float ShieldFadeSpeed = 0.04f;

    /// <summary>Shield overlay opacity, driven by the local player's held tool.</summary>
    public static float ShieldOpacity { get; private set; }

    public override void PreUpdate()
    {
        if (StructureProtectionSystem.IsProtected((int)(Player.position.X / 16), (int)(Player.position.Y / 16)))
            Player.noBuilding = true;

        if (Player.whoAmI != Main.myPlayer)
            return;

        bool holdingTool = Player.HeldItem.pick > 0 || Player.HeldItem.hammer > 0;
        ShieldOpacity = MathHelper.Clamp(ShieldOpacity + (holdingTool ? ShieldFadeSpeed : -ShieldFadeSpeed), 0f, 1f);
    }
}

public class StructureProtectionGlobalTile : GlobalTile
{
    public override bool CanKillTile(int i, int j, int type, ref bool blockDamaged)
        => !StructureProtectionSystem.IsProtected(i, j);

    public override bool CanExplode(int i, int j, int type)
        => !StructureProtectionSystem.IsProtected(i, j);

    public override bool CanReplace(int i, int j, int type, int tileTypeBeingPlaced)
        => !StructureProtectionSystem.IsProtected(i, j);

    public override bool CanPlace(int i, int j, int type)
        => !StructureProtectionSystem.IsProtected(i, j);

    private static Asset<Effect> ShieldEffect => Assets.Shaders.StructureShield.Asset;

    private static Asset<Texture2D> NoiseTexture => Assets.Textures.Noise.Turbulence_05.Asset;

    public override void PostDraw(int i, int j, int type, SpriteBatch spriteBatch)
    {
        if (StructureProtectionPlayer.ShieldOpacity <= 0f) return;
        if (!StructureProtectionSystem.IsShielded(i, j)) return;

        Effect shader = ShieldEffect.Value;
        if (shader == null) return;

        int offX = (int)(i * 16 - Main.screenPosition.X);
        int offY = (int)(j * 16 - Main.screenPosition.Y);

        if (!Main.drawToScreen)
        {
            offX += Main.offScreenRange;
            offY += Main.offScreenRange;
        }

        shader.Parameters["uColor"]?.SetValue(new Vector3(0.3f, 0.8f, 1.0f));
        shader.Parameters["uSecondaryColor"]?.SetValue(new Vector3(0.6f, 0.4f, 1.0f));
        shader.Parameters["uOpacity"]?.SetValue(0.65f * StructureProtectionPlayer.ShieldOpacity);
        shader.Parameters["uTime"]?.SetValue(Main.GameUpdateCount * 0.02f);
        shader.Parameters["uScale"]?.SetValue(0.05f);

        spriteBatch.End();
        spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive,
            default, default, default, shader);

        Main.graphics.GraphicsDevice.Textures[1] = NoiseTexture.Value;

        spriteBatch.Draw(
            Terraria.GameContent.TextureAssets.MagicPixel.Value,
            new Rectangle(offX, offY, 16, 16),
            Color.White
        );

        spriteBatch.End();
        spriteBatch.Begin(default, default, default, default, default, default);
    }
}

public class StructureProtectionGlobalWall : GlobalWall
{
    public override bool CanExplode(int i, int j, int type)
        => !StructureProtectionSystem.IsProtected(i, j);
    public override bool CanPlace(int i, int j, int type)
        => !StructureProtectionSystem.IsProtected(i, j);

    public override void KillWall(int i, int j, int type, ref bool fail)
    {
        if (StructureProtectionSystem.IsProtected(i, j))
            fail = true;
    }
}