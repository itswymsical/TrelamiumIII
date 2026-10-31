using System.Collections.Generic;
using ReLogic.Content;

namespace TrelamiumIII.Core.Tiles.Conditions;

/// <summary>
///     Base for every <see cref="ITileCondition" />: a default for each member of the contract, plus
///     the handful of drawing and sampling helpers a condition normally wants.
/// </summary>
/// <remarks>
///     <para>
///         Derived types are discovered and registered automatically by
///         <see cref="TileConditionSystem" />, so a new condition is one file and nothing else. It
///         needs a parameterless constructor and it must not keep per-tile state: see
///         <see cref="ITileCondition" /> for why.
///     </para>
///     <para>
///         A coat that is only a palette and some rules for what touches it -- slime, mud, a stain --
///         should extend <see cref="FilmCondition" /> instead, which does the drawing.
///     </para>
/// </remarks>
public abstract class TileCondition : ITileCondition
{
    private readonly HashSet<Entity> touchedThisTick = [];

    /// <summary>
    ///     True the first time <paramref name="entity" /> touches this condition this tick, false
    ///     after.
    /// </summary>
    /// <remarks>
    ///     Contact hooks run once per marked tile under the hitbox, so a player standing across three
    ///     slimed tiles is slowed three times over. Anything that should happen once per entity --
    ///     drag, a debuff, a sound -- belongs behind this.
    /// </remarks>
    protected bool FirstContact(Entity entity) => touchedThisTick.Add(entity);

    /// <summary>Forgets this tick's contacts. Called by the system before <see cref="UpdateGlobal" />.</summary>
    internal void ResetContacts() => touchedThisTick.Clear();

    public virtual string Name => GetType().Name;

    public virtual float Duration => 8f * 60f;

    public virtual int MaxTiles => 1024;

    public virtual void Load() { }

    public virtual void Unload() { }

    public virtual bool CanApplyTo(int i, int j)
    {
        Tile tile = Framing.GetTileSafely(i, j);

        return tile.HasTile && Main.tileSolid[tile.TileType];
    }

    public virtual void OnApplied(ref TileConditionInstance instance) { }

    public virtual void OnExpired(in TileConditionInstance instance) { }

    public virtual bool CanRemain(in TileConditionInstance instance) => instance.Tile.HasTile;

    public virtual void UpdateTile(ref TileConditionInstance instance) { }

    public virtual void UpdateGlobal() { }

    public virtual void OnLiquidContact(ref TileConditionInstance instance, int liquidType, int amount) { }

    public virtual void OnPlayerContact(ref TileConditionInstance instance, Player player) { }

    public virtual void OnNPCContact(ref TileConditionInstance instance, NPC npc) { }

    public virtual void OnProjectileContact(ref TileConditionInstance instance, Projectile projectile) { }

    public virtual TileConditionRenderMode RenderMode => TileConditionRenderMode.None;

    public virtual BlendState BlendState => BlendState.AlphaBlend;

    public virtual int CanvasPixelScale => 2;

    public virtual int FadeInTicks => 8;

    public virtual int FadeOutTicks => 12;

    public virtual Asset<Effect>? Shader => null;

    public virtual TileCanvasSample EncodeCanvas(in TileConditionInstance instance) => new(instance.Strength);

    public virtual void ApplyShaderParameters(Effect effect) { }

    /// <summary>Whether the tile is close enough to the camera to be worth spending anything on.</summary>
    /// <remarks>
    ///     Per-tile update runs for the whole batch, on and off screen alike, because the simulation
    ///     has to stay consistent. Dust, light and sound dont, and a thousand tiles all rolling for
    ///     an ember two screens away is pure cost.
    /// </remarks>
    protected static bool OnScreen(in TileConditionInstance instance, float padding = 64f)
    {
        Vector2 relative = instance.WorldCenter - Main.screenPosition;

        return relative.X >= -padding
               && relative.Y >= -padding
               && relative.X <= Main.screenWidth + padding
               && relative.Y <= Main.screenHeight + padding;
    }
}
