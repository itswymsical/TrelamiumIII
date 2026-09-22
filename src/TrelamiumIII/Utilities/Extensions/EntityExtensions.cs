namespace TrelamiumIII.Utilities.Extensions;

/// <summary>
///     Provides <see cref="Entity"/> extension methods.
/// </summary>
public static class EntityExtensions
{
    /// <summary>
    ///     Whether <paramref name="entity"/> exists in the world. 1.4.5 moved <c>active</c> off <see cref="Entity"/>
    ///     onto each entity type.
    /// </summary>
    public static bool IsActive(this Entity entity) => entity switch
    {
        NPC npc => npc.active,
        Projectile projectile => projectile.active,
        Player player => player.active,
        WorldItem item => item.active,
        _ => false,
    };
}
