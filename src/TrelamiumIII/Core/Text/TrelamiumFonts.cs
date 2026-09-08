using ReLogic.Graphics;

namespace TrelamiumIII.Core.Text;

/// <summary>
///     T3 typefaces that sit alongside <see cref="Terraria.GameContent.FontAssets" />. These are null on a dedicated server.
/// </summary>
public static class TrelamiumFonts
{
    private const string DIRECTORY = "Common/Fonts";

    public static DynamicSpriteFont AndikaRegular { get; private set; } = null!;

    public static DynamicSpriteFont AndikaItalic { get; private set; } = null!;

    public static DynamicSpriteFont AndikaBold { get; private set; } = null!;

    public static DynamicSpriteFont AndikaBoldItalic { get; private set; } = null!;

    public static DynamicSpriteFont PaprikaFont { get; private set; } = null!;

    public static DynamicSpriteFont AaarghFont { get; private set; } = null!;

    internal static void Load(Mod mod)
    {
        if (Main.dedServ) return;

        AndikaRegular = BakedFont.Load(mod, $"{DIRECTORY}/Andika-Regular");
        AndikaItalic = BakedFont.Load(mod, $"{DIRECTORY}/Andika-Italic");
        AndikaBold = BakedFont.Load(mod, $"{DIRECTORY}/Andika-Bold");
        AndikaBoldItalic = BakedFont.Load(mod, $"{DIRECTORY}/Andika-BoldItalic");
        PaprikaFont = BakedFont.Load(mod, $"{DIRECTORY}/Paprika-Regular");
        AaarghFont = BakedFont.Load(mod, $"{DIRECTORY}/Aaargh");
    }

    internal static void Unload()
    {
        AndikaRegular = null!;
        AndikaItalic = null!;
        AndikaBold = null!;
        AndikaBoldItalic = null!;
        PaprikaFont = null!;
        AaarghFont = null!;
    }
}
