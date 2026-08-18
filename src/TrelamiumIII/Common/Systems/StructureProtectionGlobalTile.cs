using ReLogic.Content;
using TrelamiumIII.Common.Players;

namespace TrelamiumIII.Common.Systems;

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
        if (SubstratePlayer.ShieldOpacity <= 0f) return;
        if (!NearAnyProtectedArea(i, j)) return;
        if (!StructureProtectionSystem.IsProtected(i, j)) return;

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
        shader.Parameters["uOpacity"]?.SetValue(0.65f * SubstratePlayer.ShieldOpacity);
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

    private static bool NearAnyProtectedArea(int i, int j)
    {
        const int margin = 5;
        var cabin = StructureProtectionSystem.ArgieLabArea;
        if (cabin != Rectangle.Empty)
        {
            var expanded = new Rectangle(
                cabin.X - margin, cabin.Y - margin,
                cabin.Width + margin * 2, cabin.Height + margin * 2);
            if (expanded.Contains(i, j)) return true;
        }
        return false;
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