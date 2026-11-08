using TrelamiumIII.Core.Books.Text;

namespace TrelamiumIII.Core.Books;

public abstract class ModBook : ILoadable
{
    /// <summary>Registry key and localization scope (<c>LoreLibrary.{Key}</c>). No spaces: <c>/lorebook</c> reads one word.</summary>
    public abstract string Key { get; }

    /// <summary>The whole book's typeface (null for vanilla fonts), overridden by sections and then by element styles.</summary>
    protected virtual BookTypeface? Typeface => null;

    /// <summary>The book's cover, paper and inks. Override one field at a time: <c>BookPalette.Default() with { Cover = ... }</c>.</summary>
    protected virtual BookPalette Palette => BookPalette.Default();

    /// <summary>Declares the book's blocks. Called once, at load.</summary>
    protected abstract void Build(BookBuilder book);

    void ILoadable.Load(Mod mod)
    {
        BookBuilder builder = BookNovel.Define(Key).Typeface(Typeface).Palette(Palette);

        Build(builder);
        builder.Register();
    }

    void ILoadable.Unload() { }
}
