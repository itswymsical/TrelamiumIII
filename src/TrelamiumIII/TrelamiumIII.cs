using Terraria.Localization;
using TrelamiumIII.Core.Books.Lore;
using TrelamiumIII.Core.Dialogue;

namespace TrelamiumIII;

public sealed partial class TrelamiumIII : Mod
{
    public TrelamiumIII()
    {
        MusicAutoloadingEnabled = false; // tml-build handles this with its generators.
    }
    /// <summary>
    ///     The name of this mod.
    /// </summary>
    public const string NAME = nameof(TrelamiumIII);

    /// <summary>
    ///     The prefix to use for the name of this mod.
    /// </summary>
    public const string NAME_PREFIX = NAME + ": ";

    /// <summary>
    ///     Gets the <see cref="Mod" /> implementation of this mod.
    /// </summary>
    public static TrelamiumIII Instance => ModContent.GetInstance<TrelamiumIII>();

    public override void Load()
    {
        SpeakerRegistry.Load();
        DialogueRegistry.Load();
        LoreBookRegistry.Load();
    }

    public override void Unload()
    {
        LoreBookRegistry.Unload();
        DialogueRegistry.Unload();
        SpeakerRegistry.Unload();
    }
}

/// <summary>
///     Provides <see cref="Mod" /> extension methods.
/// </summary>
public static class ModExtensions
{
    /// <summary>
    ///     Retrieves the text value for a localization key belonging to this piece of content with the
    ///     given <paramref name="suffix" />. The text returned will be for the currently selected
    ///     language.
    /// </summary>
    /// <param name="mod">The mod used as the base for the localization key.</param>
    /// <param name="suffix">The suffix appended to the mod's localization key.</param>
    /// <returns>The localized text corresponding to the constructed key.</returns>
    public static string GetLocalizationValue(this Mod mod, string suffix)
    {
        return Language.GetTextValue(mod.GetLocalizationKey(suffix));
    }

    /// <summary>
    ///     Retrieves the text value for a localization key belonging to this piece of content with the
    ///     given <paramref name="suffix" />. The text returned will be for the currently selected
    ///     language.
    /// </summary>
    /// <param name="mod">The mod used as the base for the localization key.</param>
    /// <param name="suffix">The suffix appended to the mod's localization key.</param>
    /// <param name="args">Objects used to format the localized text.</param>
    /// <returns>The formatted localized text corresponding to the constructed key.</returns>
    public static string GetLocalizationValue(this Mod mod, string suffix, params object[] args)
    {
        return Language.GetTextValue(mod.GetLocalizationKey(suffix), args);
    }
}
