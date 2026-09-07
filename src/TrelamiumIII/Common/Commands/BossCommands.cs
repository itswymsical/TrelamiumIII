using TrelamiumIII.Content.Fungore;

namespace TrelamiumIII.Common.Commands;

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
            NetMessage.SendData(MessageID.SpawnBossUseLicenseStartEvent, number: caller.Player.whoAmI, number2: type);

            return;
        }

        NPC.SpawnOnPlayer(caller.Player.whoAmI, type);
    }
}
