using System.Collections.Generic;

namespace TrelamiumIII.Core.Dialogue.Projected;

/// <summary>
///     Owns the one quad mesh every projected box shares, and redraws their faces once per frame.
/// </summary>
/// <remarks>
///     <para>
///         The faces have to be composed while render targets can still be swapped, which rules out
///         the interface layer the boxes actually draw in -- by then the back buffer is bound and
///         setting a target would discard the frame so far. <c>CheckMonoliths</c> is where vanilla
///         does its own off-screen work, so this hooks in alongside it.
///     </para>
///     <para>
///         Boxes register rather than being handed over by the <see cref="DialogueManager" />, which
///         keeps the manager free of any knowledge that one of its two presentations needs a slot
///         earlier in the frame than the other.
///     </para>
/// </remarks>
public sealed class ProjectedDialogueSystem : ModSystem
{
    internal static ProjectedQuad? Quad { get; private set; }

    private static readonly List<ProjectedDialogueBox> live = [];

    public override void Load()
    {
        if (Main.dedServ)
            return;

        Quad = new ProjectedQuad();
        Quad.Load();

        On_Main.CheckMonoliths += BuildFrame;
    }

    public override void Unload()
    {
        On_Main.CheckMonoliths -= BuildFrame;

        // A box is normally released the frame it finishes closing, and there is no next frame during
        // an unload, so anything still open is torn down here.
        foreach (ProjectedDialogueBox box in live)
            box.Dispose();

        live.Clear();

        // Unload can run off the main thread during a reload, and the effect inside is a graphics
        // resource, so disposal is queued rather than done here.
        ProjectedQuad? pending = Quad;
        Quad = null;

        Main.QueueMainThreadAction(() => pending?.Dispose());
    }

    internal static void Register(ProjectedDialogueBox box)
    {
        if (!live.Contains(box))
            live.Add(box);
    }

    private static void BuildFrame(On_Main.orig_CheckMonoliths orig)
    {
        orig();

        if (Main.gameMenu || Main.dedServ)
            return;

        for (int i = live.Count - 1; i >= 0; i--)
        {
            ProjectedDialogueBox box = live[i];

            if (box.ShouldBeRemoved)
            {
                live.RemoveAt(i);
                box.Dispose();
                continue;
            }

            box.BuildSurfaces();
        }
    }
}
