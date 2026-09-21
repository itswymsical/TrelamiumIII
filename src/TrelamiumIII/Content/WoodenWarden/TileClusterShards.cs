using System.Collections.Generic;
using TrelamiumIII.Core;

namespace TrelamiumIII.Content.WoodenWarden;

/// <summary>
///     Client-side tile debris dissolved through <c>TileDissolve.fx</c>. A shattered <see cref="TileCluster" /> flies as
///     two rigid halves for <see cref="SPLIT_TICKS" />, then falls apart tile by tile.
/// </summary>
internal sealed class TileClusterShards : ModSystem
{
    private const float GRAVITY = 0.24f;
    private const float DRAG = 0.992f;
    private const float BOUNCE = 0.35f;
    private const float GROUND_FRICTION = 0.82f;

    /// <summary>Below this a bounce is indistinguishable from jitter, so the piece is treated as landed.</summary>
    private const float MIN_BOUNCE_SPEED = 1.6f;

    private const int SPLIT_TICKS = 12;

    /// <summary>Speed the two halves separate at, along the split normal.</summary>
    private const float SPLIT_SPEED = 2.4f;

    private const float SPLIT_SPIN = 0.07f;

    private const int DISSOLVE_TICKS = 26;

    /// <summary>Collision box of one piece, smaller than the sprite so pieces bed into the ground.</summary>
    private const int PIECE_SIZE = 10;

    private const int MAX_PIECES = 480;

    /// <summary>Noise texels advanced per sprite texel.</summary>
    private const float NOISE_TEXELS = 6f;

    /// <summary>Width of the dissolve band, in noise units.</summary>
    private const float EDGE = 0.12f;

    public static readonly Color EdgeColor = new(165, 230, 37);

    private sealed class Piece
    {
        public ushort Type;
        public Point Frame;

        // Rigid motion of the half this piece belongs to, used until Age reaches SplitTicks.
        public Vector2 HalfCentre;
        public Vector2 HalfVelocity;
        public float HalfSpin;
        public Vector2 Local;
        public float BaseRotation;
        public int SplitTicks;

        public Vector2 Position;
        public Vector2 Velocity;
        public float Rotation;
        public float Spin;

        public int Age;
        public int DissolveAt;
        public Vector2 NoiseOffset;

        public bool Loose => Age >= SplitTicks;
    }

    private static readonly List<Piece> pieces = [];

    private static WrapperShaderData<Assets.Shaders.TileDissolve.Parameters>? shader;

    private static bool loaded;

    /// <summary>
    ///     Breaks <paramref name="cluster" />, drawn at <paramref name="centre" /> turned by <paramref name="rotation" />,
    ///     across the line along <paramref name="velocity" />; the halves separate sideways from it.
    /// </summary>
    public static void Shatter(TileCluster cluster, Vector2 centre, float rotation, Vector2 velocity)
    {
        if (Main.dedServ || !loaded || cluster.Count == 0)
            return;

        Vector2 normal = velocity.LengthSquared() > 0.01f
            ? velocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2)
            : Vector2.UnitX;

        (float sin, float cos) = MathF.SinCos(rotation);

        Span<Vector2> sums = stackalloc Vector2[2];
        Span<int> counts = stackalloc int[2];

        for (int y = 0; y < cluster.Height; y++)
        {
            for (int x = 0; x < cluster.Width; x++)
            {
                if (!cluster.Filled(x, y))
                    continue;

                Vector2 turned = Turn(cluster.Offset(x, y), sin, cos);
                int half = Vector2.Dot(turned, normal) >= 0f ? 0 : 1;

                sums[half] += turned;
                counts[half]++;
            }
        }

        Vector2 rebound = -velocity * 0.12f + new Vector2(0f, -2.2f);

        for (int y = 0; y < cluster.Height; y++)
        {
            for (int x = 0; x < cluster.Width; x++)
            {
                if (!cluster.Filled(x, y))
                    continue;

                Vector2 turned = Turn(cluster.Offset(x, y), sin, cos);
                int half = Vector2.Dot(turned, normal) >= 0f ? 0 : 1;
                float sign = half == 0 ? 1f : -1f;

                Vector2 halfCentre = centre + sums[half] / Math.Max(counts[half], 1);

                pieces.Add(new Piece
                {
                    Type = cluster.TypeAt(x, y),
                    Frame = cluster.FrameAt(x, y),
                    HalfCentre = halfCentre,
                    HalfVelocity = rebound + normal * (SPLIT_SPEED * sign),
                    HalfSpin = SPLIT_SPIN * sign,
                    Local = centre + turned - halfCentre,
                    BaseRotation = rotation,
                    SplitTicks = SPLIT_TICKS,
                    DissolveAt = SPLIT_TICKS + Main.rand.Next(8, 34),
                    NoiseOffset = Main.rand.NextVector2Square(0f, 1f),
                });
            }
        }

        Trim();
    }

    /// <summary>One loose block of <paramref name="type" />, thrown along <paramref name="velocity" />.</summary>
    public static void Debris(ushort type, Vector2 position, Vector2 velocity)
    {
        if (Main.dedServ || !loaded)
            return;

        pieces.Add(new Piece
        {
            Type = type,
            Frame = TileCluster.LooseFrame(Main.rand.Next(3)),
            Position = position,
            Velocity = velocity,
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
            Spin = Main.rand.NextFloat(-0.25f, 0.25f),
            DissolveAt = Main.rand.Next(18, 40),
            NoiseOffset = Main.rand.NextVector2Square(0f, 1f),
        });

        Trim();
    }

    public override void OnModLoad()
    {
        if (Main.dedServ)
            return;

        loaded = true;

        On_Main.DrawDust += static (orig, self) =>
        {
            orig(self);

            if (loaded)
                DrawPieces();
        };
    }

    public override void OnModUnload()
    {
        loaded = false;
        shader = null;
        pieces.Clear();
    }

    public override void OnWorldUnload() => pieces.Clear();

    public override void PostUpdateEverything()
    {
        if (Main.dedServ)
            return;

        for (int i = pieces.Count - 1; i >= 0; i--)
        {
            Piece piece = pieces[i];

            piece.Age++;

            if (piece.Age == piece.SplitTicks)
                Loosen(piece);
            else if (piece.Loose)
                Step(piece);

            if (piece.Age >= piece.DissolveAt + DISSOLVE_TICKS)
                pieces.RemoveAt(i);
        }
    }

    /// <summary>Hands a piece its half's velocity at the moment of the break, spin included, and lets it fall on its own.</summary>
    private static void Loosen(Piece piece)
    {
        Vector2 position = RigidPosition(piece, out float rotation);
        Vector2 arm = piece.Local.RotatedBy(piece.HalfSpin * piece.Age);

        piece.Position = position;
        piece.Rotation = rotation;
        piece.Velocity = piece.HalfVelocity + new Vector2(0f, GRAVITY * piece.Age)
                         + new Vector2(-arm.Y, arm.X) * piece.HalfSpin
                         + new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), Main.rand.NextFloat(-1.5f, 0f));
        piece.Spin = piece.HalfSpin + Main.rand.NextFloat(-0.1f, 0.1f);
    }

    private static void Step(Piece piece)
    {
        piece.Velocity.Y += GRAVITY;
        piece.Velocity *= DRAG;

        Vector2 old = piece.Velocity;

        piece.Velocity = Collision.TileCollision(piece.Position - new Vector2(PIECE_SIZE * 0.5f), piece.Velocity, PIECE_SIZE, PIECE_SIZE);

        if (piece.Velocity.X != old.X)
        {
            piece.Velocity.X = -old.X * BOUNCE;
            piece.Spin *= -BOUNCE;
        }

        if (piece.Velocity.Y != old.Y)
        {
            piece.Velocity.Y = Math.Abs(old.Y) > MIN_BOUNCE_SPEED ? -old.Y * BOUNCE : 0f;
            piece.Velocity.X *= GROUND_FRICTION;

            // Rolling rather than sliding once it is down.
            piece.Spin = piece.Velocity.X * 0.06f;
        }

        piece.Position += piece.Velocity;
        piece.Rotation += piece.Spin;
    }

    /// <summary>Where a piece sits while it is still part of a rigid half, <see cref="GRAVITY" /> included.</summary>
    private static Vector2 RigidPosition(Piece piece, out float rotation)
    {
        float t = piece.Age;
        float turn = piece.HalfSpin * t;

        rotation = piece.BaseRotation + turn;

        return piece.HalfCentre + piece.HalfVelocity * t + new Vector2(0f, 0.5f * GRAVITY * t * t) + piece.Local.RotatedBy(turn);
    }

    private static void DrawPieces()
    {
        if (pieces.Count == 0 || Main.gameMenu)
            return;

        Texture2D noise = Assets.Textures.Noise.FbmNoise2.Asset.Value;

        shader ??= Assets.Shaders.TileDissolve.CreateP0();

        var parameters = shader.Parameters;

        parameters.uNoiseTex = noise;
        parameters.NoiseSampler = new HlslSampler2D { Texture = noise, Sampler = SamplerState.PointWrap };
        parameters.uNoiseScale = NOISE_TEXELS / noise.Width;
        parameters.uEdge = EDGE;
        parameters.uEdgeColor = EdgeColor.ToVector3();

        SpriteBatch spriteBatch = Main.spriteBatch;

        // Immediate, so each piece's progress and noise offset are applied to its own draw.
        spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.AlphaBlend,
            SamplerState.PointClamp,
            DepthStencilState.None,
            Main.Rasterizer,
            null,
            Main.GameViewMatrix.TransformationMatrix
        );

        foreach (Piece piece in pieces)
        {
            if (!TileCluster.TryGetTexture(piece.Type, out Texture2D texture))
                continue;

            float rotation = piece.Rotation;
            Vector2 position = piece.Loose ? piece.Position : RigidPosition(piece, out rotation);

            parameters.uTexelSize = new Vector2(1f / texture.Width, 1f / texture.Height);
            parameters.uNoiseOffset = piece.NoiseOffset;
            parameters.uProgress = Math.Clamp((piece.Age - piece.DissolveAt) / (float)DISSOLVE_TICKS, 0f, 1f);

            shader.Apply();

            spriteBatch.Draw(
                texture,
                position - Main.screenPosition,
                new Rectangle(piece.Frame.X, piece.Frame.Y, TileCluster.TILE, TileCluster.TILE),
                TileCluster.LightAt(position),
                rotation,
                new Vector2(TileCluster.TILE * 0.5f),
                1f,
                SpriteEffects.None,
                0f
            );
        }

        spriteBatch.End();
    }

    private static Vector2 Turn(Vector2 offset, float sin, float cos) => new(offset.X * cos - offset.Y * sin, offset.X * sin + offset.Y * cos);

    /// <summary>Drops the oldest pieces past the budget.</summary>
    private static void Trim()
    {
        if (pieces.Count > MAX_PIECES)
            pieces.RemoveRange(0, pieces.Count - MAX_PIECES);
    }
}
