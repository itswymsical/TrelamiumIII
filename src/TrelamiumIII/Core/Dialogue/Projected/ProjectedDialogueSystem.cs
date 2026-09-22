using System.Collections.Generic;

namespace TrelamiumIII.Core.Dialogue.Projected;

/// <summary>
///     Owns the one quad mesh every projected box shares, and redraws their faces once per frame.
/// </summary>
/// <remarks>
///     <para>
///         The faces have to be composed while render targets can still be swapped, which rules out
///         the interface layer the boxes actually draw in -- by then the back buffer is bound and
///         setting a target would discard the frame so far. <c>DoDraw_UpdateCameraPosition</c> runs just
///         before vanilla does its own off-screen work, so this hooks in there.
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

        On_Main.DoDraw_UpdateCameraPosition += BuildFrame;
        On_Main.OpenPlayerChat += SuppressChatWhileSpeaking;
    }

    public override void Unload()
    {
        On_Main.DoDraw_UpdateCameraPosition -= BuildFrame;
        On_Main.OpenPlayerChat -= SuppressChatWhileSpeaking;

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

    /// <summary>
    ///     Holds the chat window shut while a box is open, so Enter advances the conversation.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Enter is the obvious key for "go on", and it is also the key that opens chat. Taking it
    ///         for the dialogue without closing that door means the first press advances a line and
    ///         opens a chat box over the top of it, and the second press sends an empty message.
    ///     </para>
    ///     <para>
    ///         Refusing to open is enough, and is narrower than swallowing the keypress: everything
    ///         else Enter does still works, chat opened before the conversation started stays open,
    ///         and the moment the last box closes the key goes back to what it was.
    ///     </para>
    /// </remarks>
    private static void SuppressChatWhileSpeaking(On_Main.orig_OpenPlayerChat orig)
    {
        if (live.Count > 0)
            return;

        orig();
    }

    private static void BuildFrame(On_Main.orig_DoDraw_UpdateCameraPosition orig)
    {
        orig();

        if (Main.dedServ)
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

            // The sweep above runs even on the menu. Building a surface there would be pointless, but
            // leaving finished boxes in the list would not -- an open box holds the chat window shut,
            // and a list that only drains during play is a list that can strand one.
            if (!Main.gameMenu)
                box.BuildSurfaces();
        }
    }
}
