using System.Collections.Generic;
using System.IO;
using System.Text;
using ReLogic.Content;
using ReLogic.Graphics;

namespace TrelamiumIII.Core.Text;

/// <summary>
///     Rebuilds a font baked by <c>tools/FontBaker</c> into a ReLogic
///     <see cref="DynamicSpriteFont" /> that Terraria draws exactly like one of its own.
/// </summary>
/// <remarks>
///     <para>
///         Terraria's fonts arrive as <c>.xnb</c> compiled by ReLogic's content pipeline, which
///         does not ship with tModLoader -- only the reader for it does. So there is no way to
///         author one offline; the font has to be assembled in memory instead, which is what
///         <see cref="DynamicSpriteFont.SetPages" /> is for.
///     </para>
///     <para>
///         Both <see cref="DynamicSpriteFont.SetPages" /> and <see cref="FontPage" /> are hidden
///         in the shipped ReLogic assembly. This file compiles and runs only because
///         <c>build/TrelamiumIII.Publicizer.props</c> publicizes ReLogic and the build emits
///         <c>IgnoresAccessChecksTo("ReLogic")</c>. Dropping ReLogic from the publicize list
///         breaks this at build time.
///     </para>
/// </remarks>
public static class BakedFont
{
    private const string MAGIC = "TDSF";

    private const int FORMAT_VERSION = 1;

    /// <summary>
    ///     Loads the <c>.dsf</c> metric table at <paramref name="path" /> and the atlas pages
    ///     beside it.
    /// </summary>
    /// <param name="path">
    ///     Mod-relative path without extension, e.g. <c>Common/Fonts/Andika-Regular</c>. Atlas
    ///     pages are expected at that path suffixed <c>_0</c>, <c>_1</c> and so on.
    /// </param>
    public static DynamicSpriteFont Load(Mod mod, string path)
    {
        using MemoryStream stream = new(mod.GetFileBytes($"{path}.dsf"));
        using BinaryReader reader = new(stream);

        if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != MAGIC)
        {
            throw new InvalidDataException($"{path}.dsf is not a baked font");
        }

        int version = reader.ReadInt32();

        if (version != FORMAT_VERSION)
        {
            throw new InvalidDataException(
                $"{path}.dsf is format version {version}, expected {FORMAT_VERSION}; re-run tools/FontBaker");
        }

        reader.ReadString(); // Source face name, kept for diagnostics only.
        reader.ReadSingle(); // Em size the bake was rasterised at; the atlas already encodes it.

        int lineSpacing = reader.ReadInt32();
        float characterSpacing = reader.ReadSingle();
        char defaultCharacter = (char)reader.ReadUInt16();
        int pageCount = reader.ReadInt32();
        int glyphCount = reader.ReadInt32();

        List<char>[] characters = new List<char>[pageCount];
        List<Rectangle>[] glyphs = new List<Rectangle>[pageCount];
        List<Rectangle>[] padding = new List<Rectangle>[pageCount];
        List<Vector3>[] kerning = new List<Vector3>[pageCount];

        for (int i = 0; i < pageCount; i++)
        {
            characters[i] = [];
            glyphs[i] = [];
            padding[i] = [];
            kerning[i] = [];
        }

        for (int i = 0; i < glyphCount; i++)
        {
            char character = (char)reader.ReadUInt16();
            int page = reader.ReadByte();
            int x = reader.ReadInt16();
            int y = reader.ReadInt16();
            int width = reader.ReadInt16();
            int height = reader.ReadInt16();
            int xOffset = reader.ReadInt16();
            int yOffset = reader.ReadInt16();
            float advance = reader.ReadSingle();

            characters[page].Add(character);
            glyphs[page].Add(new Rectangle(x, y, width, height));

            // Padding.Y is the drop from the top of the line box, which is what
            // BookText.TryGetGlyphInk reads back out as a glyph's top bearing.
            padding[page].Add(new Rectangle(0, yOffset, width, height));

            // ReLogic walks the pen X -> draw -> Y + Z, so the three add up to one advance.
            // Splitting it into true bearings rather than dumping the lot into Y is what lets
            // GetCharacterMetrics -- and so WrappedTextBuilder and ChatManager -- wrap sanely.
            kerning[page].Add(new Vector3(xOffset, width, advance - xOffset - width));
        }

        FontPage[] pages = new FontPage[pageCount];

        for (int i = 0; i < pageCount; i++)
        {
            // Straight-alpha PNGs: ReLogic's PngReader premultiplies them on the way in, which is
            // why the baker deliberately does not.
            Texture2D texture = ModContent
                .Request<Texture2D>($"{mod.Name}/{path}_{i}", AssetRequestMode.ImmediateLoad)
                .Value;

            pages[i] = new FontPage(texture, glyphs[i], padding[i], characters[i], kerning[i]);
        }

        DynamicSpriteFont font = new(characterSpacing, lineSpacing, defaultCharacter);
        font.SetPages(pages);

        return font;
    }
}
