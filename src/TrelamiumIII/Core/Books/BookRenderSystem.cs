using System.Collections.Generic;

namespace TrelamiumIII.Core.Books;

/// <summary>An open book composed by <see cref="BookRenderSystem" /> each frame.</summary>
internal interface IOpenBook
{
    /// <summary>Composes this frame's spread. Runs in <c>CheckMonoliths</c> with no batch open.</summary>
    void BuildFrame();

    /// <summary>Releases the book's targets immediately, for unload, where the close animation can't finish.</summary>
    void ForceRelease();
}

/// <summary>
///     Owns the shared page-curl mesh and composes open books in <c>CheckMonoliths</c>, while render targets can still be swapped.
/// </summary>
public sealed class BookRenderSystem : ModSystem
{
    internal static PageFlipMesh? Mesh { get; private set; }

    private static readonly List<IOpenBook> openBooks = [];

    public override void Load()
    {
        if (Main.dedServ)
            return;

        Mesh = new PageFlipMesh();
        Mesh.Load();

        On_Main.CheckMonoliths += BuildFrame;
    }

    public override void Unload()
    {
        On_Main.CheckMonoliths -= BuildFrame;

        foreach (IOpenBook book in openBooks.ToArray())
            book.ForceRelease();

        openBooks.Clear();

        // Unload can run off the main thread; the mesh holds a graphics resource.
        PageFlipMesh? pending = Mesh;
        Mesh = null;

        Main.QueueMainThreadAction(() => pending?.Dispose());
    }

    internal static void Register(IOpenBook book)
    {
        if (!openBooks.Contains(book))
            openBooks.Add(book);
    }

    internal static void Unregister(IOpenBook book) => openBooks.Remove(book);

    private static void BuildFrame(On_Main.orig_CheckMonoliths orig)
    {
        orig();

        if (Main.gameMenu || Main.dedServ)
            return;

        // Iterated backwards: a book that finishes closing removes itself during the pass.
        for (int i = openBooks.Count - 1; i >= 0; i--)
        {
            if (i < openBooks.Count)
                openBooks[i].BuildFrame();
        }
    }
}
