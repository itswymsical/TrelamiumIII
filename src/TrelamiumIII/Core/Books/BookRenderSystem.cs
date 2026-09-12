using System.Collections.Generic;

namespace TrelamiumIII.Core.Books;

/// <summary>
///     Owns the shared page-curl mesh and composes open books in <c>CheckMonoliths</c>, while render targets can still be swapped.
/// </summary>
public sealed class BookRenderSystem : ModSystem
{
    internal static PageFlipMesh? Mesh { get; private set; }

    private static readonly List<BookUI> openBooks = [];

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

        // Closing normally only releases the targets on the next Update, and there is no next Update
        // during an unload, so the books are torn down here instead.
        foreach (BookUI book in openBooks.ToArray())
            book.ForceRelease();

        openBooks.Clear();

        // Unload can run off the main thread during a reload, and the effect inside is a graphics
        // resource, so disposal is queued rather than done here.
        PageFlipMesh? pending = Mesh;
        Mesh = null;

        Main.QueueMainThreadAction(() => pending?.Dispose());
    }

    internal static void Register(BookUI book)
    {
        if (!openBooks.Contains(book))
            openBooks.Add(book);
    }

    internal static void Unregister(BookUI book) => openBooks.Remove(book);

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
