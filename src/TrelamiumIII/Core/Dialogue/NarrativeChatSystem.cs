namespace TrelamiumIII.Core.Dialogue;

/// <summary>
/// Suppresses the vanilla chat window for NPCs that talk through the dialogue system.
///
/// <para>
/// Everything that opens a chat window - right-clicking an NPC in
/// <c>Main.HoverOverNPCs</c>, the gamepad interact, a mod calling it directly - goes
/// through <see cref="Player.SetTalkNPC"/> first, so that is the single place worth
/// standing in. Refusing to set <c>talkNPC</c> is enough to suppress the window:
/// <c>Main.GUIChatDrawInner</c> bails while it is negative and clears the pending text
/// itself.
/// </para>
///
/// <para>
/// The caller carries on with its own local NPC reference afterwards, so
/// <c>Main.npcChatText = npc.GetChat()</c> still runs. That is deliberate - the
/// <see cref="Events.EventNPC.GetChat"/> hook rides on it, which is how a Job's
/// "go and talk to them" task still ticks over while the conversation is ours.
/// </para>
/// </summary>
public sealed class NarrativeChatSystem : ModSystem
{
    public override void Load()
    {
        On_Player.SetTalkNPC += SuppressVanillaChat;
    }

    public override void Unload()
    {
        On_Player.SetTalkNPC -= SuppressVanillaChat;
        NarrativeChat.Unload();
    }

    private static void SuppressVanillaChat(On_Player.orig_SetTalkNPC orig, Player self, int npcIndex, bool fromNet)
    {
        if (!TryTakeOver(self, npcIndex, fromNet))
        {
            orig(self, npcIndex, fromNet);
            return;
        }

        // Clear rather than skip: the player may have been mid-conversation with someone
        // else, and vanilla still expects that window closed behind us.
        orig(self, -1, fromNet);
    }

    /// <summary>
    /// True when this NPC's conversation belongs to the dialogue system. Starts it as a
    /// side effect, unless that same line is already playing or waiting to - click spam
    /// shouldn't stack the same box five deep, but a line clicked for behind another
    /// still has to get its turn.
    /// </summary>
    private static bool TryTakeOver(Player player, int npcIndex, bool fromNet)
    {
        // Dialogue is a client-side UI, and only for the player actually clicking.
        if (Main.dedServ || fromNet || player.whoAmI != Main.myPlayer)
            return false;

        if (npcIndex < 0 || npcIndex >= Main.maxNPCs)
            return false;

        NPC npc = Main.npc[npcIndex];
        if (npc == null || !npc.active)
            return false;

        ChatEncounter encounter = NarrativeChat.Resolve(npc, player);
        if (!encounter.Exists)
            return false;

        if (!NarrativeChat.IsPending(encounter))
            NarrativeChat.Begin(npc, encounter);

        // Vanilla records the meeting inside SetTalkNPC, which we are about to skip.
        // Talking is talking, whichever box it happens in.
        if (Main.netMode != NetmodeID.MultiplayerClient)
            Main.BestiaryTracker.Chats.RegisterChatStartWith(npc);

        return true;
    }
}
