using TrelamiumIII.Core.Books.Text;

namespace TrelamiumIII.Core.Books.Lore;

public abstract class ModLoreBook : ILoadable
{
    /// <summary>Registry key and localization scope (<c>LoreLibrary.{Key}</c>). No spaces: <c>/lorebook</c> reads one word.</summary>
    public abstract string Key { get; }

    /// <summary>The whole book's typeface (null for vanilla fonts), overridden by sections and then by element styles.</summary>
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
