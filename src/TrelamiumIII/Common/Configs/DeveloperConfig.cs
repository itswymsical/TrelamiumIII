using System.ComponentModel;
using Newtonsoft.Json;
using Terraria.ModLoader.Config;

namespace TrelamiumIII.Common.Configs;

/// <summary>Client-side settings for debugging and the authoring tools.</summary>
public sealed class DeveloperConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ClientSide;

    public static DeveloperConfig Instance => ModContent.GetInstance<DeveloperConfig>();

    [Header("Debug")]
    [DefaultValue(false)]
    public bool DebugMode { get; set; }

    /// <summary>Absolute path to <c>src/TrelamiumIII</c>; empty exports to the save folder only.</summary>
    [Header("Authoring")]
    [DefaultValue("")]
    public string SourceDirectory { get; set; } = string.Empty;

    /// <summary>Whether authoring exports also land in the source tree.</summary>
    [JsonIgnore]
    public bool HasSourceDirectory => !string.IsNullOrWhiteSpace(SourceDirectory);
}
