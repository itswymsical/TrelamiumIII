using ReLogic.Graphics;

namespace TrelamiumIII.Core.Text;

/// <summary>
///     T3 typefaces that sit alongside <see cref="Terraria.GameContent.FontAssets" />. These are null on a dedicated server.
/// </summary>
public static class TrelamiumFonts
{
    private const string DIRECTORY = "Common/Fonts";
    // Book fonts
    public static DynamicSpriteFont AaarghFont { get; private set; } = null!;
    public static DynamicSpriteFont RajdhaniBold { get; private set; } = null!;
    public static DynamicSpriteFont RajdhaniMedium { get; private set; } = null!;

    // Old English / Latin fonts
    public static DynamicSpriteFont ManufacturingConsent { get; private set; } = null!;
    public static DynamicSpriteFont Cardinal { get; private set; } = null!;
    public static DynamicSpriteFont CardinalAlternate { get; private set; } = null!;
    public static DynamicSpriteFont OldEnglishFive { get; private set; } = null!;
    public static DynamicSpriteFont BlackFlag { get; private set; } = null!;
    public static DynamicSpriteFont AnglicanText { get; private set; } = null!;
    public static DynamicSpriteFont Canterbury { get; private set; } = null!;

    // Terraria-aesthetic fonts
    public static DynamicSpriteFont PatrickHand { get; private set; } = null!;
    public static DynamicSpriteFont PaprikaFont { get; private set; } = null!;

    public static DynamicSpriteFont AndikaRegular { get; private set; } = null!;
    public static DynamicSpriteFont AndikaItalic { get; private set; } = null!;
    public static DynamicSpriteFont AndikaBold { get; private set; } = null!;
    public static DynamicSpriteFont AndikaBoldItalic { get; private set; } = null!;

    public static DynamicSpriteFont AgravePro_ExtraLight { get; private set; } = null!;
    public static DynamicSpriteFont AgravePro_Light { get; private set; } = null!;
    public static DynamicSpriteFont AgravePro_Thin { get; private set; } = null!;
    public static DynamicSpriteFont AgravePro_Medium { get; private set; } = null!;
    public static DynamicSpriteFont AgravePro_Regular { get; private set; } = null!;
    public static DynamicSpriteFont AgravePro_SemiBold { get; private set; } = null!;
    public static DynamicSpriteFont AgravePro_Bold { get; private set; } = null!;

    // Futuristic fonts

    public static DynamicSpriteFont Hybrid { get; private set; } = null!;
    public static DynamicSpriteFont ZenDots { get; private set; } = null!;

    // Goofy ahh fonts
    public static DynamicSpriteFont Peralta { get; private set; } = null!;


    internal static void Load(Mod mod)
    {
        if (Main.dedServ) return;
        AaarghFont = BakedFont.Load(mod, $"{DIRECTORY}/Aaargh");

        AndikaRegular = BakedFont.Load(mod, $"{DIRECTORY}/Andika-Regular");
        AndikaItalic = BakedFont.Load(mod, $"{DIRECTORY}/Andika-Italic");
        AndikaBold = BakedFont.Load(mod, $"{DIRECTORY}/Andika-Bold");
        AndikaBoldItalic = BakedFont.Load(mod, $"{DIRECTORY}/Andika-BoldItalic");

        AgravePro_ExtraLight = BakedFont.Load(mod, $"{DIRECTORY}/AgravePro-ExtraLight");
        AgravePro_Light = BakedFont.Load(mod, $"{DIRECTORY}/AgravePro-Light");
        AgravePro_Thin = BakedFont.Load(mod, $"{DIRECTORY}/AgravePro-Thin");
        AgravePro_Medium = BakedFont.Load(mod, $"{DIRECTORY}/AgravePro-Medium");
        AgravePro_Regular = BakedFont.Load(mod, $"{DIRECTORY}/AgravePro-Regular");
        AgravePro_SemiBold = BakedFont.Load(mod, $"{DIRECTORY}/AgravePro-SemiBold");
        AgravePro_Bold = BakedFont.Load(mod, $"{DIRECTORY}/AgravePro-Bold");

        AnglicanText = BakedFont.Load(mod, $"{DIRECTORY}/AnglicanText");
        BlackFlag = BakedFont.Load(mod, $"{DIRECTORY}/BlackFlag");

        Canterbury = BakedFont.Load(mod, $"{DIRECTORY}/Canterbury");
        Cardinal = BakedFont.Load(mod, $"{DIRECTORY}/Cardinal");
        CardinalAlternate = BakedFont.Load(mod, $"{DIRECTORY}/Cardinal-Alternate");

        Hybrid = BakedFont.Load(mod, $"{DIRECTORY}/Hybrid");
        ManufacturingConsent = BakedFont.Load(mod, $"{DIRECTORY}/ManufacturingConsent-Regular");

        OldEnglishFive = BakedFont.Load(mod, $"{DIRECTORY}/OldEnglishFive");

        PaprikaFont = BakedFont.Load(mod, $"{DIRECTORY}/Paprika-Regular");
        PatrickHand = BakedFont.Load(mod, $"{DIRECTORY}/PatrickHand-Regular");
        Peralta = BakedFont.Load(mod, $"{DIRECTORY}/Peralta-Regular");

        RajdhaniBold = BakedFont.Load(mod, $"{DIRECTORY}/Rajdhani-Bold");
        RajdhaniMedium = BakedFont.Load(mod, $"{DIRECTORY}/Rajdhani-Medium");
        ZenDots = BakedFont.Load(mod, $"{DIRECTORY}/ZenDots-Regular");

    }

    internal static void Unload()
    {
        AaarghFont = null!;

        AndikaRegular = null!;
        AndikaItalic = null!;
        AndikaBold = null!;
        AndikaBoldItalic = null!;

        AgravePro_ExtraLight = null!;
        AgravePro_Light = null!;
        AgravePro_Thin = null!;
        AgravePro_Medium = null!;
        AgravePro_Regular = null!;
        AgravePro_SemiBold = null!;
        AgravePro_Bold = null!;

        AnglicanText = null!;
        BlackFlag = null!;
        Canterbury = null!;
        Cardinal = null!;
        CardinalAlternate = null!;

        Hybrid = null!;
        ManufacturingConsent = null!;
        OldEnglishFive = null!;

        PaprikaFont = null!;
        PatrickHand = null!; 
        Peralta = null!;
        RajdhaniBold = null!;
        RajdhaniMedium = null!;
        ZenDots = null!;

    }
}
