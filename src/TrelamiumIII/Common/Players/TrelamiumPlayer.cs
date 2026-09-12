using TrelamiumIII.Common.Systems;
using static Terraria.ModLoader.BackupIO;

namespace TrelamiumIII.Common.Players;

public class TrelamiumPlayer : ModPlayer
{
    public static float ShieldOpacity { get; private set; }

    private const float ShieldFadeSpeed = 0.04f;

    public override void PreUpdate()
    {
        int tileX = (int)(Player.position.X / 16);
        int tileY = (int)(Player.position.Y / 16);

        if (StructureProtectionSystem.IsProtected(tileX, tileY))
            Player.noBuilding = true;

        bool holding = Player.HeldItem.pick > 0 || Player.HeldItem.hammer > 0;

        if (holding)
            ShieldOpacity = MathHelper.Clamp(ShieldOpacity + ShieldFadeSpeed, 0f, 1f);
        else
            ShieldOpacity = MathHelper.Clamp(ShieldOpacity - ShieldFadeSpeed, 0f, 1f);
    }

    //public override IEnumerable<Item> AddStartingItems(bool mediumCoreDeath)
    //{
    //    return [new(ModContent.ItemType<StartBagItem>())];
    //}
}
