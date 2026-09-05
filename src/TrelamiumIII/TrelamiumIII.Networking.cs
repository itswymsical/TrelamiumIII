using TrelamiumIII.Content.Liquids;
using TrelamiumIII.Core.Tiles.Conditions;
using System.IO;

namespace TrelamiumIII;

partial class TrelamiumIII
{
    internal enum MessageType : byte
    {
        LiquidMergeExplosion,
        TileCondition,
    }

    public override void HandlePacket(BinaryReader reader, int whoAmI)
    {
        MessageType msgType = (MessageType)reader.ReadByte();

        switch (msgType)
        {
            case MessageType.LiquidMergeExplosion:
                int x = reader.ReadInt32();
                int y = reader.ReadInt32();
                CrudeOilLiquid.ExplosionEffect(x, y);
                break;
            case MessageType.TileCondition:
                TileConditionSystem.ReceiveApply(reader, whoAmI);
                break;
            default:
                Logger.WarnFormat("TrelamiumIII: Unknown Message type: {0}", msgType);
                break;
        }
    }
}
