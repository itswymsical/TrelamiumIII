namespace TrelamiumIII.Core.Books.Lore;

public abstract class ModLoreBook : ILoadable
{
    /// <summary>
    ///     Registry key, and the localization scope: everything this book reads lives under
    ///     <c>LoreLibrary.{Key}</c>. Must not contain spaces -- the <c>/lorebook</c> command reads a
    ///     single word.
    /// </summary>
    public abstract string Key { get; }

    /// <summary>Declares the book's blocks. Called once, at load.</summary>
    protected abstract void Build(LoreBookBuilder book);

    void ILoadable.Load(Mod mod)
    {
        LoreBookBuilder builder = LoreBook.Define(Key);

        Build(builder);
        builder.Register();
    }

    void ILoadable.Unload() { }
}
