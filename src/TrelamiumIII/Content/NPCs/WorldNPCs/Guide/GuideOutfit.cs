using TrelamiumIII.Core;
using TrelamiumIII.Core.NPCs.Actors;

namespace TrelamiumIII.Content.NPCs.WorldNPCs.Guide;

/// <summary>The Guide as a player model. The sheets paint his face, hair, hands and skin, so the player's own stay hidden.</summary>
public sealed class GuideOutfit : ActorOutfit
{
    public override string HeadTexture => AssetReferences.Assets.Textures.NPCAssets.Guide.WorldGuide_Head.KEY;

    public override string BodyTexture => AssetReferences.Assets.Textures.NPCAssets.Guide.WorldGuide_Body.KEY;

    public override string LegsTexture => AssetReferences.Assets.Textures.NPCAssets.Guide.WorldGuide_Legs.KEY;

    public override void SetStaticDefaults()
    {
        ArmorIDs.Head.Sets.DrawHead[HeadSlot] = false;
        ArmorIDs.Body.Sets.HidesTopSkin[BodySlot] = true;
        ArmorIDs.Legs.Sets.HidesBottomSkin[LegsSlot] = true;
    }
}
