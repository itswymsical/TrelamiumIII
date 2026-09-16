using TrelamiumIII.Content.DevTools;
using TrelamiumIII.Core.Gas;
using TrelamiumIII.Core.Graphics.Fields;

namespace TrelamiumIII.Common.Commands;

public class FieldWandCommand : ModCommand
{
    public override string Command => "fieldwand";

    public override CommandType Type => CommandType.Chat;

    public override string Usage => "/fieldwand [list]";

    public override string Description => "Gives the field wand, or lists every registered field layer.";

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        if (args.Length > 0 && args[0].Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            List();
            return;
        }

        caller.Player.QuickSpawnItem(caller.Player.GetSource_Misc("FieldWandCommand"), ModContent.ItemType<FieldWand>());
        caller.Reply($"Field wand spawned. {FieldSystem.Layers.Count} layers registered.", Color.LightGreen);
    }

    private static void List()
    {
        foreach (FieldLayer layer in FieldSystem.Layers)
        {
            string kind = layer is GasField ? "gas" : "visual";

            Main.NewText($"[{layer.LoadOrder}] {FieldWandSelection.NameOf(layer)} ({kind}) - {layer.NodeCount} nodes",
                layer is GasField ? new Color(120, 205, 140) : new Color(150, 175, 230));
        }
    }
}
