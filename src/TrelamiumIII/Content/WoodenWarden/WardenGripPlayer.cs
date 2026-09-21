using Terraria.DataStructures;

namespace TrelamiumIII.Content.WoodenWarden;

/// <summary>Squashes a player's sprite while a <see cref="WardenArm" /> holds them, scaled by <see cref="WardenArm.SqueezeOn" />.</summary>
internal sealed class WardenGripPlayer : ModPlayer
{
    /// <summary>Height lost at full squeeze, as a fraction.</summary>
    private const float SQUASH = 0.55f;

    /// <summary>Width gained at full squeeze, as a fraction.</summary>
    private const float SPREAD = 0.4f;

    private const float EASE = 0.18f;

    private float squeeze;

    public override void PostUpdate()
    {
        squeeze = MathHelper.Lerp(squeeze, WardenArm.SqueezeOn(Player.whoAmI), EASE);

        if (squeeze < 0.002f)
            squeeze = 0f;
    }

    /// <summary>Scales every cached draw about the player's centre, after vanilla has applied rotation and scale.</summary>
    public override void TransformDrawData(ref PlayerDrawSet drawInfo)
    {
        if (squeeze <= 0f)
            return;

        var scale = new Vector2(1f + squeeze * SPREAD, 1f - squeeze * SQUASH);
        Vector2 pivot = drawInfo.Position + drawInfo.drawPlayer.Size * 0.5f - Main.screenPosition;

        for (int i = 0; i < drawInfo.DrawDataCache.Count; i++)
        {
            DrawData data = drawInfo.DrawDataCache[i];

            data.position = pivot + (data.position - pivot) * scale;
            data.scale *= scale;

            drawInfo.DrawDataCache[i] = data;
        }
    }
}
