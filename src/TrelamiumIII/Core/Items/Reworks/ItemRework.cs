using System.Collections.Generic;

namespace TrelamiumIII.Core.Items.Reworks;

/// <summary>
/// A skill-gated override of how an item is used. Applies per player, not per item, so the same vanilla
/// item behaves one way for someone who holds <see cref="SkillID"/> and normally for everyone else.
/// </summary>
/// <remarks>
///     A rework never touches item defaults, because defaults are shared by every player on the server.
///     Everything it changes is decided at the point of use, from the holder's skills.
/// </remarks>
public abstract class ItemRework
{
    /// <summary>Id of the skill that turns this rework on. See <see cref="ExperientialSystem.Skills.Skill.ID"/>.</summary>
    public abstract string SkillID { get; }

    /// <summary>Whether this rework covers <paramref name="item"/>. Read item fields, never the world or a player.</summary>
    public abstract bool Applies(Item item);

    /// <summary>
    /// Skips <c>Player.ItemCheck_UseMiningTools</c> for this use, so the tool does not also mine at the
    /// cursor. Takes the player because a rework bound to <see cref="Player.altFunctionUse"/> only
    /// displaces mining on the alt use.
    /// </summary>
    public virtual bool SuppressesMining(Item item, Player player) => false;

    /// <inheritdoc cref="GlobalItem.AltFunctionUse"/>
    public virtual bool AltFunctionUse(Item item, Player player) => false;

    /// <inheritdoc cref="GlobalItem.CanUseItem"/>
    public virtual bool? CanUse(Item item, Player player) => null;

    /// <inheritdoc cref="GlobalItem.UseItem"/>
    /// <remarks>
    ///     Runs on every client, so guard anything that spawns an entity with <see cref="Main.myPlayer"/>.
    ///     It also runs once per <c>useTime</c> rather than once per swing, and vanilla tools set
    ///     <c>useAnimation</c> longer than <c>useTime</c>, so anything that must happen once per swing
    ///     needs its own guard here - <see cref="CanUse"/> is only consulted when the swing starts.
    /// </remarks>
    public virtual bool? Use(Item item, Player player) => null;

    /// <inheritdoc cref="GlobalItem.UseStyle"/>
    public virtual void UseStyle(Item item, Player player, Rectangle heldItemFrame) { }

    /// <summary>Tooltip lines for the holder. Only called while the rework is active for them.</summary>
    public virtual void ModifyTooltips(Item item, List<TooltipLine> tooltips) { }
}
