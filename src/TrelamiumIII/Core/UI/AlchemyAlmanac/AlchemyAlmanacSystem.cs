namespace TrelamiumIII.Core.UI.AlchemyAlmanac;

public sealed class AlchemyAlmanacSystem : ModSystem
{
    public override void OnWorldUnload() => AlchemyAlmanacUI.ResetForWorldChange();
}
