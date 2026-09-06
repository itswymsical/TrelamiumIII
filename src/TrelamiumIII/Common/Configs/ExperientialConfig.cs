using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace TrelamiumIII.Common.Configs;

/// <summary>
/// How much the Experiential System explains itself.
///
/// <para>
/// The defaults are deliberately quiet. Exp arriving is shown, because a resource you cannot see arrive
/// is not a resource; everything else - the act ladders, the tier names, the progress bars - is left for
/// the player to discover in the world rather than read off a panel.
/// </para>
/// </summary>
public class ExperientialConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ClientSide;

    public static ExperientialConfig Instance => ModContent.GetInstance<ExperientialConfig>();

    /// <summary>Null-safe: the instance does not exist during the earliest part of load.</summary>
    public static bool MilestoneTracker => Instance is { ShowMilestoneTracker: true };

    public static bool ExpPopups => Instance is null || Instance.ShowExpPopups;

    public static bool ThresholdEffect => Instance is null || Instance.ShowThresholdEffect;

    /// <summary>
    /// The act tracker panel - act names, categories, tier progress bars.
    ///
    /// <para>
    /// Off by default. It is a good panel and a bad fit: it narrates a system meant to be inferred, and
    /// it does so in the corner of the screen while you are trying to look at the game.
    /// </para>
    /// </summary>
    [Header("Feedback")]
    [DefaultValue(false)]
    public bool ShowMilestoneTracker { get; set; } = false;

    /// <summary>Floating "+N Exp" text when Exp is earned. The one piece of feedback that is on by default.</summary>
    [DefaultValue(true)]
    public bool ShowExpPopups { get; set; } = true;

    /// <summary>
    /// The light that gathers back into the player when Exp is earned - large on a Threshold, small on an
    /// act tier. Separate from the popups because someone who has silenced the numbers may still want the
    /// moment, and because it is the feedback that survives with every panel switched off.
    /// </summary>
    [DefaultValue(true)]
    public bool ShowThresholdEffect { get; set; } = true;
}
