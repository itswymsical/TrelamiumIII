using TrelamiumIII.Core.NPCs.Actors;

namespace TrelamiumIII.Content.NPCs.WorldNPCs.Guide;

/// <summary>
/// The vanilla Guide drawn as a player model. He stays <see cref="NPCID.Guide"/> - housing, the crafting and help
/// buttons, the Voodoo Doll and every job that names him keep working - and only his look changes.
/// </summary>
public sealed class GuideModel : TerrarianModelProfile
{
    /// <summary>What vanilla draws in his hands when he shoots.</summary>
    private const int Bow = ItemID.WoodenBow;

    public override int NPCType => NPCID.Guide;

    public override void Dress(TerrarianModel model)
    {
        model.Player.Male = true;
        model.Outfit = ModContent.GetInstance<GuideOutfit>();
    }

    public override void Pose(TerrarianModel model, int stateTicks) => TownPoses.ApplyVanilla(model, stateTicks, Bow);
}
