using System.Collections.Generic;
using System.Linq;
using TrelamiumIII.Core.ExperientialSystem.Skills;

namespace TrelamiumIII.Core.Items.Reworks;

/// <summary>
/// Finds every <see cref="ItemRework"/> at load and answers which one, if any, is live for a given item
/// and holder.
/// </summary>
public sealed class ItemReworkRegistry : ModSystem
{
    private static readonly List<ItemRework> All = [];

    public static IReadOnlyList<ItemRework> Reworks => All;

    public override void Load()
    {
        All.Clear();

        foreach (Type type in Mod.Code.GetTypes().Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(ItemRework))))
        {
            try
            {
                if (Activator.CreateInstance(type) is ItemRework rework)
                    All.Add(rework);
            }
            catch (Exception ex)
            {
                TrelamiumIII.Instance.Logger.Error($"Failed to register item rework {type.Name}: {ex.Message}");
            }
        }

        TrelamiumIII.Instance.Logger.Info($"Registered {All.Count} item reworks");
    }

    public override void Unload() => All.Clear();

    /// <summary>
    /// Validates that every rework names a skill that exists. Runs after all registries are loaded,
    /// because a rework and its skill are found by two separate reflection passes.
    /// </summary>
    public override void PostSetupContent()
    {
        foreach (ItemRework rework in All)
        {
            if (SkillRegistry.Get(rework.SkillID) is null)
            {
                TrelamiumIII.Instance.Logger.Error(
                    $"Item rework {rework.GetType().Name} is gated on skill '{rework.SkillID}', which no skill claims; it can never turn on.");
            }
        }
    }

    /// <summary>
    /// The rework covering <paramref name="item"/> for <paramref name="player"/>, or null. Called from
    /// per-tick item hooks, so it stays a scan over a handful of reworks with no allocation.
    /// </summary>
    public static ItemRework? Active(Item item, Player? player)
    {
        if (All.Count == 0 || player is null || item.IsAir)
            return null;

        SkillPlayer? skills = null;

        foreach (ItemRework rework in All)
        {
            if (!rework.Applies(item))
                continue;

            skills ??= player.GetModPlayer<SkillPlayer>();

            if (skills.Has(rework.SkillID))
                return rework;
        }

        return null;
    }
}
