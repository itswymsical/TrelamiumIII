using TrelamiumIII.Core.Graphics.Fields;

namespace TrelamiumIII.Core.Graphics.Vfx.Modules;

/// <summary> Carried along by the wind, closing part of the gap to its speed each tick. </summary>
internal sealed class WindForce : FieldModule
{
    /// <summary> Fraction of the gap to the wind's speed closed each tick. </summary>
    [ModuleRange(0f, 0.2f)]
    public float Pull = 0.02f;

    /// <summary> How much of the wind this field feels. </summary>
    [ModuleRange(0f, 4f)]
    public float Scale = 1f;

    public override string Tip => "Carried along by the wind";

    public override void Apply(ref FieldNode node, IFieldSpace space)
        => node.Velocity.X += (space.Wind.X * Scale - node.Velocity.X) * Pull;
}

/// <summary> Anything moving through the field shoves it aside, and it closes back behind. </summary>
internal sealed class MoverShove : FieldModule
{
    [ModuleRange(0f, 3f)]
    public float Strength = 0.9f;

    /// <summary> Fraction of a mover's own velocity it hands to what it passes through. </summary>
    [ModuleRange(0f, 1f)]
    public float Carry = 0.12f;

    public override string Tip => "Pushed aside by players, NPCs and projectiles";

    public override void Apply(ref FieldNode node, IFieldSpace space)
    {
        foreach ((Vector2 center, Vector2 velocity, float reach) in space.Movers)
        {
            Vector2 offset = node.Position - center;
            float range = reach + node.Radius;

            if (offset.LengthSquared() >= range * range)
                continue;

            float push = 1f - offset.Length() / range;
            node.Velocity += offset.SafeNormalize(Vector2.UnitY) * push * Strength + velocity * push * Carry;
        }
    }
}

/// <summary> Swirls, by flowing along the curl of a smooth pattern, which moves things around without bunching them up. </summary>
internal sealed class CurlFlow : FieldModule
{
    [ModuleRange(0f, 0.6f)]
    public float Strength = 0.08f;

    /// <summary> Size of the swirls, in pixels. </summary>
    [ModuleRange(16f, 800f)]
    public float Size = 180f;

    /// <summary> How fast the pattern churns. </summary>
    [ModuleRange(0f, 4f)]
    public float Speed = 0.4f;

    public override string Tip => "Swirls, without bunching up";

    public override void Apply(ref FieldNode node, IFieldSpace space)
    {
        float size = MathF.Max(Size, 1f);
        float x = node.Position.X / size;
        float y = node.Position.Y / size;
        float t = space.Time * Speed;

        // The partial derivatives of sin(x + t) * cos(y - t) + sin(x * 0.7 - t) * sin(y * 1.3 + t), turned a right angle.
        float dx = MathF.Cos(x + t) * MathF.Cos(y - t) + 0.7f * MathF.Cos(x * 0.7f - t) * MathF.Sin(y * 1.3f + t);
        float dy = -MathF.Sin(x + t) * MathF.Sin(y - t) + 1.3f * MathF.Sin(x * 0.7f - t) * MathF.Cos(y * 1.3f + t);

        node.Velocity += new Vector2(dy, -dx) * Strength;
    }
}

/// <summary> Drawn toward the nearest player. </summary>
internal sealed class AttractPlayer : FieldModule
{
    /// <summary> Negative pushes away. </summary>
    [ModuleRange(-1f, 1f)]
    public float Strength = 0.1f;

    [ModuleRange(16f, 1200f)]
    public float Range = 320f;

    public override string Tip => "Drawn toward the player, or away if negative";

    public override void Apply(ref FieldNode node, IFieldSpace space)
    {
        if (space.Focus is { } focus)
            Attraction.Pull(ref node, focus, Strength, Range);
    }
}

/// <summary> Drawn toward the pointer, so a field can be steered around by hand. </summary>
internal sealed class AttractPointer : FieldModule
{
    /// <summary> Negative pushes away. </summary>
    [ModuleRange(-1f, 1f)]
    public float Strength = 0.2f;

    [ModuleRange(16f, 1200f)]
    public float Range = 480f;

    public override string Tip => "Follows the mouse, or flees it if negative";

    public override void Apply(ref FieldNode node, IFieldSpace space)
    {
        if (space.Pointer is { } pointer)
            Attraction.Pull(ref node, pointer, Strength, Range);
    }
}

internal static class Attraction
{
    /// <summary> Fades out to nothing at the edge of its range, so nothing is caught suddenly. </summary>
    public static void Pull(ref FieldNode node, Vector2 toward, float strength, float range)
    {
        Vector2 offset = toward - node.Position;
        float distance = offset.Length();

        if (distance > range || distance < 0.001f)
            return;

        node.Velocity += offset / distance * strength * (1f - distance / range);
    }
}

/// <summary> Temperature drifting to the air's, so a hot release cools as it spreads. Reaches colour through a palette's heat blend. </summary>
internal sealed class Cooling : FieldModule
{
    [ModuleRange(0f, 1200f)]
    public float Ambient = 20f;

    /// <summary> Fraction of the gap to ambient closed each tick. </summary>
    [ModuleRange(0f, 0.2f)]
    public float Rate = 0.01f;

    public override string Tip => "Cools toward the air's temperature";

    public override void Apply(ref FieldNode node, IFieldSpace space)
        => node.Temperature += (Ambient - node.Temperature) * Rate;
}

/// <summary> Rebounds off what it hits, and a hard hit costs it life, so a field driven into the ground breaks up. </summary>
internal sealed class GroundSplash : FieldModule
{
    /// <summary> Negative rebounds. </summary>
    [ModuleRange(-1f, 1f)]
    public float Bounce = -0.35f;

    /// <summary> Life lost per pixel-per-tick of speed it hits with. </summary>
    [ModuleRange(0f, 60f)]
    public float LifeCost = 12f;

    public override string Tip => "Rebounds off terrain, and hard hits break it up";

    public override void OnTileContact(ref FieldNode node)
    {
        node.Life -= node.Velocity.Length() * LifeCost;
        node.Velocity *= Bounce;
    }
}
