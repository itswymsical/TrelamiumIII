using TrelamiumIII.Core.Books.Text;

namespace TrelamiumIII.Core.Books.Lore;

public abstract class ModLoreBook : ILoadable
{
    /// <summary>
    ///     Registry key, and the localization scope: everything this book reads lives under
    ///     <c>LoreLibrary.{Key}</c>. Must not contain spaces -- the <c>/lorebook</c> command reads a
    ///     single word.
    /// </summary>
    public abstract string Key { get; }

    /// <summary>
    ///     The whole book's typeface. Null leaves it on Terraria's own fonts.
    /// </summary>
    /// <remarks>
    ///     Applied before <see cref="Build" /> runs, so it is the bottom of the cascade: a
    ///     <see cref="BookBuilder.Section" /> overrides it for a run of blocks, and a
    ///     <see cref="Layout.BlockStyle.Typeface" /> overrides both for one element.
    /// </remarks>
    protected virtual BookTypeface? Typeface => null;

    /// <summary>Declares the book's blocks. Called once, at load.</summary>
    protected abstract void Build(BookBuilder book);

    void ILoadable.Load(Mod mod)
    {
        BookBuilder builder = BookNovel.Define(Key).Typeface(Typeface);

        Build(builder);
        builder.Register();
    }

    void ILoadable.Unload() { }
}
