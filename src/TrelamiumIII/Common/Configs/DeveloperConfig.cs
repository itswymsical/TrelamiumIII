using System.ComponentModel;
<<<<<<< HEAD
using System.IO;
using System.Linq;
=======
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb
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
<<<<<<< HEAD

    /// <summary>The configured source tree, else the one under ModSources that holds <c>build.txt</c>.</summary>
    public static string? SourceRoot()
    {
        DeveloperConfig config = Instance;

        if (config.HasSourceDirectory && Directory.Exists(config.SourceDirectory))
            return config.SourceDirectory;

        string sources = Path.Combine(Main.SavePath, "ModSources", "TrelamiumIII");
        string[] candidates = [Path.Combine(sources, "src", "TrelamiumIII"), sources];

        return candidates.FirstOrDefault(path => File.Exists(Path.Combine(path, "build.txt")));
    }
=======
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb
}
