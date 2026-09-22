using System.Diagnostics.CodeAnalysis;
using ReLogic.Graphics;

namespace TrelamiumIII.Utilities.Extensions;

/// <summary>
///     Provides <see cref="DynamicSpriteFont"/> extension methods.
/// </summary>
public static class FontExtensions
{
    /// <summary>
    ///     The glyph baked for <paramref name="character"/>, without falling back to the default character. Replaces
    ///     1.4.4's <c>SpriteCharacters</c> dictionary, which 1.4.5 turned into an array indexed by character.
    /// </summary>
    public static bool TryGetCharacterData(this DynamicSpriteFont font, char character,
        [NotNullWhen(true)] out DynamicSpriteFont.SpriteCharacterData? data)
    {
        DynamicSpriteFont.SpriteCharacterData[] characters = font._spriteCharacters;
        data = character < characters.Length ? characters[character] : null;
        return data is not null;
    }
}
