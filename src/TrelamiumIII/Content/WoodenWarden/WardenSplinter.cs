using System.Collections.Generic;
using ReLogic.Content;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Graphics.Particles;

namespace TrelamiumIII.Content.WoodenWarden;

/// <summary>A square cut out of a Warden sprite, thrown off when the body or an arm breaks apart.</summary>
public sealed class WardenSplinter : Particle
{
    /// <summary>Cells under this share of opaque pixels are skipped, so the burst has no empty squares.</summary>
    private const float MIN_COVERAGE = 0.35f;

    /// <summary>Opaque cells per sprite, frame and cell size. Built once each; reading texture data stalls the GPU.</summary>
    private static readonly Dictionary<(Texture2D, Rectangle, int), List<Point>> cells = [];

    /// <summary>Stand-in; every spawn wears the sprite it was cut from.</summary>
    public override string[] Textures => [AssetReferences.Content.WoodenWarden.WoodenWardenBoss.KEY];

    public override Vector2 Size => new(8f);

    public override float Gravity => 0.26f;

    public override float Bounce => 0.3f;

    public override int FadeOutTicks => 40;

    public Rectangle SourceRect { get; set; }

    public SpriteEffects Effects { get; set; }

    public override bool PreDraw(SpriteBatch spriteBatch, Texture2D texture, Vector2 drawPosition, Color lightColor)
    {
        spriteBatch.Draw(texture, drawPosition, SourceRect, lightColor, Rotation, SourceRect.Size() * 0.5f, Scale, Effects, 0f);

        return false;
    }

    /// <summary>
    ///     Cuts <paramref name="frame" /> of <paramref name="sprite" />, drawn centred on <paramref name="centre" />, into
    ///     <paramref name="cell" />-pixel squares and throws them outward at up to <paramref name="force" />.
    /// </summary>
    public static void Burst(Asset<Texture2D> sprite, Rectangle frame, Vector2 centre, float rotation, SpriteEffects effects, int cell, float force)
    {
        if (Main.dedServ || sprite.Value is not { } texture)
            return;

        bool flipped = effects.HasFlag(SpriteEffects.FlipHorizontally);
        var middle = new Vector2(frame.Width, frame.Height) * 0.5f;

        foreach (Point corner in OpaqueCells(texture, frame, cell))
        {
            var local = new Vector2(corner.X + cell * 0.5f, corner.Y + cell * 0.5f) - middle;

            if (flipped)
                local.X = -local.X;

            local = local.RotatedBy(rotation);

            Vector2 velocity = local.SafeNormalize(Main.rand.NextVector2Unit()) * Main.rand.NextFloat(0.35f, 1f) * force
                               + new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-4f, -1f));

            WardenSplinter? splinter = ParticleSystem.Spawn<WardenSplinter>(
                centre + local,
                velocity,
                Main.rand.Next(90, 150),
                rotation: rotation,
                rotationVelocity: Main.rand.NextFloat(-0.3f, 0.3f)
            );

            if (splinter is null)
                continue;

            splinter.TextureOverride = sprite;
            splinter.SourceRect = new Rectangle(frame.X + corner.X, frame.Y + corner.Y, cell, cell);
            splinter.Effects = effects;
        }
    }

    /// <summary>Top-left corners, relative to <paramref name="frame" />, of the cells worth throwing.</summary>
    private static List<Point> OpaqueCells(Texture2D texture, Rectangle frame, int cell)
    {
        if (cells.TryGetValue((texture, frame, cell), out List<Point>? cached))
            return cached;

        var pixels = new Color[frame.Width * frame.Height];

        texture.GetData(0, frame, pixels, 0, pixels.Length);

        var found = new List<Point>();

        for (int y = 0; y + cell <= frame.Height; y += cell)
        {
            for (int x = 0; x + cell <= frame.Width; x += cell)
            {
                int opaque = 0;

                for (int j = 0; j < cell; j++)
                {
                    for (int i = 0; i < cell; i++)
                    {
                        if (pixels[(y + j) * frame.Width + x + i].A > 0)
                            opaque++;
                    }
                }

                if (opaque >= cell * cell * MIN_COVERAGE)
                    found.Add(new Point(x, y));
            }
        }

        cells[(texture, frame, cell)] = found;

        return found;
    }

    /// <summary>Drops the cell cache with the textures it is keyed on.</summary>
    public static void ClearCache() => cells.Clear();
}
