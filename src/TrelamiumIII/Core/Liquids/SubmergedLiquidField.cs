using ReLogic.Content;
using TrelamiumIII.Core.Graphics.Fields;

namespace TrelamiumIII.Core.Liquids;

/// <summary>
///     Draws every <see cref="SubmergedPocket" /> as a placeholder pink checkered blob. Rebuilt from the pockets each
///     tick, so the nodes are pure decoration: a few pinned lobes per pocket that wobble around its position.
/// </summary>
public sealed class SubmergedLiquidField : FieldLayer
{
    private const int Lobes = 3;

    /// <summary>Pixels past the screen edge a pocket may sit and still be drawn.</summary>
    private const float ScreenMargin = 64f;

    public override int LoadOrder => 60;

    protected override Asset<Effect> Shader => Assets.Shaders.CheckerField.Asset;

    protected override string TexturePath => Assets.Textures.VFX.RadialGradient.KEY;

    protected override float LifeDecay => 0f;

    public override void Update()
    {
        ClearNodes();

        if (Main.dedServ || SubmergedLiquidSystem.Pockets.Count == 0)
        {
            base.Update();
            return;
        }

        Rectangle view = new Rectangle((int)Main.screenPosition.X, (int)Main.screenPosition.Y, Main.screenWidth, Main.screenHeight);
        view.Inflate((int)ScreenMargin, (int)ScreenMargin);

        float time = (float)Main.timeForVisualEffects / 60f;

        foreach (SubmergedPocket pocket in SubmergedLiquidSystem.Pockets)
        {
            if (!view.Contains(pocket.Position.ToPoint()))
                continue;

            float radius = pocket.Radius;
            float seed = pocket.Position.X * 0.37f + pocket.Position.Y * 0.11f;
            Vector2 centre = InsideHost(pocket.Position, radius);

            SpawnNode(centre, Vector2.Zero, radius * 1.3f, 1f, pinned: true);

            for (int k = 0; k < Lobes; k++)
            {
                float angle = seed + k * MathHelper.TwoPi / Lobes + time * 0.6f;
                Vector2 offset = angle.ToRotationVector2() * radius * (0.45f + 0.15f * MathF.Sin(time * 1.7f + seed + k));
                SpawnNode(centre + offset, Vector2.Zero, radius, 1f, pinned: true);
            }
        }

        base.Update();
    }

    /// <summary>
    ///     Moves the drawn centre down from the liquid's surface and up from the floor, so the blob reads as held in
    ///     the liquid. Splits the difference where the host is too shallow for both.
    /// </summary>
    private static Vector2 InsideHost(Vector2 position, float radius)
    {
        int x = (int)(position.X / 16f);
        int y = (int)(position.Y / 16f);

        int top = y;
        while (top > y - 8 && WorldGen.InWorld(x, top - 1) && Main.tile[x, top - 1].LiquidAmount > 0 && !WorldGen.SolidTile(x, top - 1))
            top--;

        int bottom = y;
        while (bottom < y + 8 && WorldGen.InWorld(x, bottom + 1) && !WorldGen.SolidTile(x, bottom + 1))
            bottom++;

        float surface = top * 16f + (byte.MaxValue - Main.tile[x, top].LiquidAmount) / 255f * 16f;
        float floor = (bottom + 1) * 16f;

        float highest = surface + radius * 0.8f;
        float lowest = floor - radius * 0.6f;

        position.Y = highest <= lowest
            ? MathHelper.Clamp(position.Y, highest, lowest)
            : (surface + floor) / 2f;

        return position;
    }

    protected override void ApplyShaderParameters(Effect effect)
    {
        effect.Parameters["Threshold"]?.SetValue(0.3f);
        effect.Parameters["CheckerScale"]?.SetValue(Main.screenHeight / 6f);
        effect.Parameters["ColorA"]?.SetValue(new Vector3(1f, 0.25f, 0.85f));
        effect.Parameters["ColorB"]?.SetValue(new Vector3(0.35f, 0.02f, 0.3f));
        effect.Parameters["OutlineColor"]?.SetValue(new Vector3(1f, 0.75f, 0.95f));
        effect.Parameters["BodyAlpha"]?.SetValue(0.85f);
    }
}
