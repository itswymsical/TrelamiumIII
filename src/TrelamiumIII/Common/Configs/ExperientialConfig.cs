using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace TrelamiumIII.Common.Configs;

/// <summary>
/// How much the Experiential System explains itself. Quiet by default: Exp gains are shown, and the rest is
/// left for the player to discover.
/// </summary>
public class ExperientialConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ClientSide;

    public static ExperientialConfig Instance => ModContent.GetInstance<ExperientialConfig>();

    /// <summary>Null-safe: the instance does not exist during the earliest part of load.</summary>
    public static bool MilestoneTracker => Instance is { ShowMilestoneTracker: true };

    public static bool ExpPopups => Instance is null || Instance.ShowExpPopups;

    public static bool ThresholdEffect => Instance is null || Instance.ShowThresholdEffect;

    /// <summary>The act tracker panel: act names, categories and tier progress bars.</summary>
    [Header("Feedback")]
    [DefaultValue(false)]
    public bool ShowMilestoneTracker { get; set; } = false;

    /// <summary>Floating "+N Exp" text when Exp is earned.</summary>
    [DefaultValue(true)]
    public bool ShowExpPopups { get; set; } = true;

    /// <summary>The light that gathers into the player on Exp gain: large on a Threshold, small on an act tier.</summary>
    [DefaultValue(true)]
    public bool ShowThresholdEffect { get; set; } = true;
}
