using System.Collections.Generic;
using ReLogic.Content;
using TrelamiumIII.Core.Graphics.Fields;

namespace TrelamiumIII.Content.Ingress.HighlandsBiome;

/// <summary>
/// Highland mist as a soft field rather than sprites: metaballs that merge into banks, lit node by node with no light floor
/// so they vanish in the dark like the tiles do. The wind carries it, anything moving through it shoves it aside and it
/// closes back behind, and driven into the ground it breaks up. <see cref="HighlandsWeather"/> raises it off the ground.
/// </summary>
public sealed class HighlandsMistField : FieldLayer
{
    /// <summary> Fraction of the gap to the wind's speed a node closes each tick. </summary>
    private const float WindPull = 0.02f;

    private const float PlayerReach = 34f;
    private const float NpcReachPadding = 16f;
    private const float ProjectileReach = 22f;

    /// <summary> Squared speed below which an NPC or projectile is left out of the shoving, so standing crowds cost nothing. </summary>
    private const float MovingSpeedSquared = 0.5f;

    private const float ShoveStrength = 0.9f;

    /// <summary> Fraction of a mover's own velocity it hands to the mist it passes through. </summary>
    private const float Carry = 0.12f;

    private const float SplashBounce = -0.35f;

    /// <summary> Life a node loses per pixel-per-tick of speed it hits the ground with. </summary>
    private const float SplashCost = 12f;

    /// <summary>
    /// How much world light is amplified on its way into the mist. Fog scatters what light there is, so it reads pale against
    /// the sky where a surface in the same light would look dim; zero light stays zero, so it still vanishes in the dark.
    /// </summary>
    private const float LightGain = 2.2f;

    // The threshold has to stay above half the noise strength: the erosion subtracts up to that much, and below it the shader
    // would draw mist out of the noise alone wherever there are no nodes at all.
    private static readonly GasLook BaseLook = new()
    {
        Noise = static () => Assets.Textures.Noise.Milky_3.Asset,
        WorldLight = Vector3.Zero,
        Threshold = 0.22f,
        DepthRange = 0.8f,
        NoiseScale = 0.12f,
        NoiseStrength = 0.4f,
        NoiseStretch = new Vector2(1f, 0.6f),
        Drift = new Vector2(0.02f, -0.01f),
        FlowStrength = 0.25f,
        Bands = 16f,
        BodyAlpha = 0.3f,
        BodyAlphaFloor = 0.12f,
        OutlineAlpha = 0f,
    };

    /// <summary> Everything moving near the mist this tick, gathered once so each node doesn't sweep every entity. </summary>
    private readonly List<(Vector2 Center, Vector2 Velocity, float Reach)> movers = [];

    public override int LoadOrder => 40;

    protected override Asset<Effect> Shader => Assets.Shaders.GasFbm.Asset;

    protected override string TexturePath => Assets.Textures.VFX.RadialGradient.KEY;

    protected override float DefaultDensity => 0.5f;

    protected override float DefaultViscosity => 0.4f;

    protected override float BuoyancyForce => 0.004f;

    protected override float GravityScale => 0.004f;

    protected override float Drag => 0.96f;

    protected override float LifeDecay => 1f;

    protected override float EnclosedLifePenalty => 4f;

    protected override int BaseMaxNodes => 500;

    /// <summary> Linear, so the banks blur together instead of keeping the pixel-art grid the other gases use. </summary>
    protected internal override SamplerState StampSampler => SamplerState.LinearClamp;

    /// <summary> Tinted by the hour each time it's drawn: grey-blue by day, warm at dusk, azure at night. </summary>
    protected override GasLook Look
    {
        get
        {
            Vector3 tint = HighlandsWeather.MistTint.ToVector3();
            return BaseLook with { Shadow = tint * 0.72f, Body = tint * 0.9f, Core = tint, Outline = tint };
        }
    }

    public override void Update()
    {
        GatherMovers();
        base.Update();
    }

    /// <summary> The usual stamp, with the brightness channel amplified; it holds weight times brightness, so capping it at the weight caps brightness at one. </summary>
    protected override void EncodeNode(in FieldNode node, out Color field, out Color attributes)
    {
        base.EncodeNode(in node, out field, out attributes);
        field.G = (byte)MathF.Min(field.G * LightGain, field.R);
    }

    protected override void UpdateNode(ref FieldNode node, int index)
    {
        node.Velocity.X += (HighlandsWeather.MistWind - node.Velocity.X) * WindPull;

        foreach ((Vector2 center, Vector2 velocity, float reach) in movers)
        {
            Vector2 offset = node.Position - center;
            float range = reach + node.Radius;

            if (offset.LengthSquared() >= range * range)
                continue;

            float push = 1f - offset.Length() / range;
            node.Velocity += offset.SafeNormalize(Vector2.UnitY) * push * ShoveStrength + velocity * push * Carry;
        }

        base.UpdateNode(ref node, index);
    }

    /// <summary> Splashes off the ground: it rebounds, and a hard hit costs it life, so mist shoved into the tiles disperses. </summary>
    protected override void OnTileContact(ref FieldNode node, int tileX, int tileY, Tile tile)
    {
        node.Life -= node.Velocity.Length() * SplashCost;
        node.Velocity *= SplashBounce;
    }

    protected override void OnLiquidContact(ref FieldNode node, int tileX, int tileY, Tile tile) => node.Velocity.Y *= 0.5f;

    private void GatherMovers()
    {
        movers.Clear();

        if (NodeCount == 0)
            return;

        Rectangle view = new((int)Main.screenPosition.X - 200, (int)Main.screenPosition.Y - 200, Main.screenWidth + 400, Main.screenHeight + 400);

        foreach (Player player in Main.ActivePlayers)
        {
            if (!player.dead && view.Contains(player.Center.ToPoint()))
                movers.Add((player.Center, player.velocity, PlayerReach));
        }

        foreach (NPC npc in Main.ActiveNPCs)
        {
            if (npc.velocity.LengthSquared() > MovingSpeedSquared && view.Contains(npc.Center.ToPoint()))
                movers.Add((npc.Center, npc.velocity, MathF.Max(npc.width, npc.height) * 0.5f + NpcReachPadding));
        }

        foreach (Projectile projectile in Main.ActiveProjectiles)
        {
            if (projectile.velocity.LengthSquared() > MovingSpeedSquared && view.Contains(projectile.Center.ToPoint()))
                movers.Add((projectile.Center, projectile.velocity, ProjectileReach));
        }
    }
}
