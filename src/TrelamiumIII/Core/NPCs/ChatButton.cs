using Terraria.GameContent;

namespace TrelamiumIII.Core.NPCs;

/// <summary>
///     An NPC chat button that resolves the <typeparamref name="T" /> being talked to. Interactions are registered
///     once per NPC type against its content sample, so callbacks take the live instance instead of capturing one.
/// </summary>
/// <param name="label">The button text, or <see langword="null" /> to hide the button.</param>
internal sealed class ChatButton<T>(Func<T, string?> label, Action<T> interact) : NPCInteraction where T : ModNPC
{
    private T? Speaker => TalkNPC?.ModNPC as T;

    public override bool Condition() => Speaker is { } npc && label(npc) is not null;

    public override string GetText() => Speaker is { } npc ? label(npc) ?? string.Empty : string.Empty;

    public override void Interact()
    {
        if (Speaker is { } npc)
            interact(npc);
    }
}
