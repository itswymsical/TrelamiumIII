using System.Collections.Generic;
using TrelamiumIII.Core.Graphics.Fields;

namespace TrelamiumIII.Core.Graphics.Vfx;

/// <summary> What a slider should offer for a module's field. Discovered with the field, so a new module needs no UI work. </summary>
[AttributeUsage(AttributeTargets.Field)]
internal sealed class ModuleRangeAttribute(float min, float max) : Attribute
{
    public float Min { get; } = min;

    public float Max { get; } = max;
}

/// <summary>
///     Where a field lives, as modules see it. They never read the world directly: tiles and the camera are one space a field can live
///     in, and the rectangles of an inventory's slots will be another.
/// </summary>
internal interface IFieldSpace
{
    /// <summary> What the air is doing, in pixels per tick. </summary>
    Vector2 Wind { get; }

    /// <summary> Seconds on this space's clock. </summary>
    float Time { get; }

    /// <summary> Where whoever's nearest stands, or null if nobody is there. </summary>
    Vector2? Focus { get; }

    /// <summary> Where the pointer is, or null if it isn't in this space. </summary>
    Vector2? Pointer { get; }

    /// <summary> Everything moving through the space that could push a field aside: where it is, how fast, and how far it reaches. </summary>
    IReadOnlyList<(Vector2 Center, Vector2 Velocity, float Reach)> Movers { get; }
}

/// <summary> The game world as a place for a field to be. </summary>
internal sealed class WorldFieldSpace : IFieldSpace
{
    private const float NpcPadding = 16f;
    private const float PlayerReach = 34f;
    private const float ProjectileReach = 22f;
    private const float MovingSpeedSquared = 0.5f;

    private readonly List<(Vector2 Center, Vector2 Velocity, float Reach)> movers = [];
    private uint gatheredOn = uint.MaxValue;

    public static WorldFieldSpace Instance { get; } = new();

    /// <summary> Vanilla's wind speed reads as a fraction of something around twenty pixels a tick at full gale. </summary>
    public Vector2 Wind => new(Main.windSpeedCurrent * 2f, 0f);

    /// <summary> Set while something else owns the clock, as the lab's can pause and the game's can't. </summary>
    public float? ClockOverride { get; set; }

    public float Time => ClockOverride ?? (float)Main.timeForVisualEffects / 60f;

    public Vector2? Focus => Main.dedServ ? null : Main.LocalPlayer.Center;

    public Vector2? Pointer => Main.dedServ ? null : Main.MouseWorld;

    public IReadOnlyList<(Vector2 Center, Vector2 Velocity, float Reach)> Movers
    {
        get
        {
            if (gatheredOn != Main.GameUpdateCount)
                Gather();

            return movers;
        }
    }

    /// <summary> Once a tick, however many fields ask, so each node doesn't sweep every entity. </summary>
    private void Gather()
    {
        gatheredOn = Main.GameUpdateCount;
        movers.Clear();

        foreach (Player player in Main.ActivePlayers)
        {
            if (!player.dead)
                movers.Add((player.Center, player.velocity, PlayerReach));
        }

        foreach (NPC npc in Main.ActiveNPCs)
        {
            if (npc.velocity.LengthSquared() > MovingSpeedSquared)
                movers.Add((npc.Center, npc.velocity, MathF.Max(npc.width, npc.height) * 0.5f + NpcPadding));
        }

        foreach (Projectile projectile in Main.ActiveProjectiles)
        {
            if (projectile.velocity.LengthSquared() > MovingSpeedSquared)
                movers.Add((projectile.Center, projectile.velocity, ProjectileReach));
        }
    }
}

/// <summary>
///     One piece of behaviour a field can have, on top of its physics. Its public float fields are its settings: the inspector reads them
///     by reflection, recipes save them by name, and an export writes them as an object initialiser. A new module is one class.
/// </summary>
internal abstract class FieldModule
{
    public virtual string Tip => string.Empty;

    /// <summary> Once a tick, before any node, while the field holds any. </summary>
    public virtual void BeginTick(IFieldSpace space) { }

    /// <summary> Each node, each tick, before the physics moves it. </summary>
    public virtual void Apply(ref FieldNode node, IFieldSpace space) { }

    public virtual void OnTileContact(ref FieldNode node) { }
}
