using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace TrelamiumIII.Common.Configs;

public sealed class AuthoringConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ClientSide;

    public static AuthoringConfig Instance => ModContent.GetInstance<AuthoringConfig>();

    [Header("Authoring")]
    [Label("Mod source directory")]
    [Tooltip("Absolute path to src/TrelamiumIII. Leave empty to export to the Terraria save folder instead.\n"
             + "Example: C:/Users/you/Documents/My Games/Terraria/tModLoader/ModSources/TrelamiumIII/src/TrelamiumIII")]
    [DefaultValue("")]
    public string SourceDirectory { get; set; } = string.Empty;

    /// <summary>Whether exports also land in the source tree rather than only the save folder.</summary>
    public bool HasSourceDirectory => !string.IsNullOrWhiteSpace(SourceDirectory);
}
