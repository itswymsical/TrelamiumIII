using TrelamiumIII.Content.Liquids;
using TrelamiumIII.Core.Liquids;
using TrelamiumIII.Core.Residues;
using TrelamiumIII.Core.Tiles.Seals;
using System.IO;
using TrelamiumIII.Core.ExperientialSystem;
using TrelamiumIII.Core.ExperientialSystem.Combat;
using TrelamiumIII.Core.ExperientialSystem.Skills;
using TrelamiumIII.Content.Dryad;
using TrelamiumIII.Content.NPCs.WorldNPCs.Silvian;
using TrelamiumIII.Content.NPCs.WorldNPCs.Jobbers;

namespace TrelamiumIII;

partial class TrelamiumIII
{
    internal enum MessageType : byte
    {
        LiquidMergeExplosion,
        Residue,
        GrantExperience,
        SkillSet,
        JobberClaim,
        DryadBriefed,
        DryadArrivalRequest,
        DryadArrivalStart,
        IngressRiftRequest,
        SilvianEncounter,
        LiquidConditions,
        Seals,
        SharedWorldData,
        AlmanacDiscovery,
        VillagerTrade,
        BlueprintMoveIn,
        BodyCoat,
    }

    public override void HandlePacket(BinaryReader reader, int whoAmI)
    {
        MessageType msgType = (MessageType)reader.ReadByte();

        switch (msgType)
        {
            case MessageType.LiquidMergeExplosion:
                int x = reader.ReadInt32();
                int y = reader.ReadInt32();
                OilLiquids.ExplosionEffect(x, y);
                break;
            case MessageType.Residue:
                ResidueSystem.Receive(reader, whoAmI);
                break;
            case MessageType.GrantExperience:
                ReceiveExperientialGrant(reader);
                break;
            case MessageType.SkillSet:
                SkillPlayer.Receive(reader, whoAmI);
                break;
            case MessageType.JobberClaim:
                Jobber.ReceiveClaim(reader);
                break;
            case MessageType.DryadBriefed:
            case MessageType.DryadArrivalRequest:
            case MessageType.DryadArrivalStart:
                DryadArrivalSystem.Receive(msgType, reader, whoAmI);
                break;
            case MessageType.IngressRiftRequest:
                IngressRift.Receive(whoAmI);
                break;
            case MessageType.LiquidConditions:
                LiquidConditionSystem.Receive(reader, whoAmI);
                break;
            case MessageType.SilvianEncounter:
                SilvianEncounter.Receive(reader, whoAmI);
                break;
            case MessageType.Seals:
                SealSystem.Receive(reader, whoAmI);
                break;
            case MessageType.SharedWorldData:
                Common.Subworlds.SharedWorldData.Receive(reader, whoAmI);
                break;
            case MessageType.AlmanacDiscovery:
                Core.Alchemy.AlmanacDiscoverySystem.Receive(reader, whoAmI);
                break;
            case MessageType.VillagerTrade:
                Core.NPCs.Trading.TradeNet.Receive(reader, whoAmI);
                break;
            case MessageType.BlueprintMoveIn:
                Core.Jobs.Blueprints.BlueprintHousing.Receive(reader);
                break;
            case MessageType.BodyCoat:
                Core.Residues.Bodies.BodyCoatNet.Receive(reader, whoAmI);
                break;
            default:
                Logger.WarnFormat("TrelamiumIII: Unknown Message type: {0}", msgType);
                break;
        }
    }

    /// <summary>
    /// Server to owning client: "you earned this fraction of a meter, from this source."
    ///
    /// <para>
    /// A fraction rather than an Exp amount, because the two halves of an award live on different machines.
    /// The server has what sizes it - NPC stats and everyone's damage share - while Threshold, combat
    /// history and the per-Threshold allowance are all save-file state on the owning client. Sending the
    /// dimensionless half lets each side apply only what it actually knows, and keeps the award correct
    /// even if the player ascends between the kill and this packet landing.
    /// </para>
    /// </summary>
    public static void SendExperientialGrant(int playerId, float meterFraction, ExpSource source)
    {
        if (Main.netMode != NetmodeID.Server)
            return;

        if (playerId < 0 || playerId >= Main.maxPlayers || meterFraction <= 0f)
            return;

        ModPacket packet = Instance.GetPacket();

        packet.Write((byte)MessageType.GrantExperience);
        packet.Write((byte)playerId);
        packet.Write(meterFraction);
        packet.Write((byte)source.Kind);
        packet.Write(source.Id);

        packet.Send(toClient: playerId);
    }

    private static void ReceiveExperientialGrant(BinaryReader reader)
    {
        int playerId = reader.ReadByte();
        float meterFraction = reader.ReadSingle();
        var kind = (ExpSourceKind)reader.ReadByte();
        int sourceId = reader.ReadInt32();

        // Only the owning client holds the state an award is applied against, and the server addressed
        // this packet to exactly that client. Anything else arriving here is a mis-send; drop it.
        if (playerId != Main.myPlayer)
            return;

        Player player = Main.player[playerId];
        if (!player.active)
            return;

        ExperientialNPC.Apply(player, meterFraction, new ExpSource(kind, sourceId));
    }
}
