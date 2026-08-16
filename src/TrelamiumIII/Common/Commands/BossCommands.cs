using TrelamiumIII.Content.Fungore;

namespace TrelamiumIII.Common.Commands;

/// <summary>
///     Summons Fungore. Testing scaffolding until there is a summon item for him.
/// </summary>
/// <remarks>
///     Spawned on the caller rather than at the cursor: the entrance places itself anyway, and it
///     needs a player to find a floor underneath.
/// </remarks>
public class SummonFungoreCommand : ModCommand
{
    public override string Command => "fungore";

    public override CommandType Type => CommandType.Chat;

    public override string Usage => "/fungore";

    public override string Description => "Summons Fungore on top of you.";

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        int type = ModContent.NPCType<FungoreBoss>();

        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            // Clients cannot spawn NPCs. This is the packet the vanilla boss summons use, and the
            // server answers it by running the spawn itself.
            NetMessage.SendData(MessageID.SpawnBossUseLicenseStartEvent, number: caller.Player.whoAmI, number2: type);

            return;
        }

        NPC.SpawnOnPlayer(caller.Player.whoAmI, type);
    }
}
