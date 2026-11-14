using Terraria.Graphics.Effects;
using Terraria.Localization;
<<<<<<< HEAD
using TrelamiumIII.Core.Books;
=======
using TrelamiumIII.Content.Ingress;
using TrelamiumIII.Core.Books.Lore;
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb
using TrelamiumIII.Core.Dialogue;
using TrelamiumIII.Core.Graphics;
using TrelamiumIII.Core.Text;

namespace TrelamiumIII;

public sealed partial class TrelamiumIII : Mod
{
    public TrelamiumIII()
    {
        MusicAutoloadingEnabled = false; // tml-build handles this with its generators.
        PlaceholderTextures.Install(this);
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
        TrelamiumFonts.Load(this);
        SpeakerRegistry.Load();
<<<<<<< HEAD
=======

        if (!Main.dedServ)
        {
            SkyManager.Instance["TrelamiumIII:HighlandsSky"] = new HighlandsSky();
        }
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb
    }

    public override void Unload()
    {
        BookRegistry.Unload();
        DialogueRegistry.Unload();
        SpeakerRegistry.Unload();
        TrelamiumFonts.Unload();
    }
}

/// <summary>
///     Provides <see cref="Mod" /> extension methods.
/// </summary>
public static class LangHelper
{
    /// <summary>
    ///     Retrieves the text value for a localization key belonging to this piece of content with the
    ///     given <paramref name="suffix" />. The text returned will be for the currently selected
    ///     language.
    /// </summary>
    /// <param name="mod">The mod used as the base for the localization key.</param>
    /// <param name="suffix">The suffix appended to the mod's localization key.</param>
    /// <returns>The localized text corresponding to the constructed key.</returns>
    public static string LocalizationValue(this Mod mod, string suffix)
    {
        return Language.GetTextValue(mod.GetLocalizationKey(suffix));
    }
    public static string LocalizationValue(string suffix)
    {
        return Language.GetTextValue(TrelamiumIII.Instance.GetLocalizationKey(suffix));
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
