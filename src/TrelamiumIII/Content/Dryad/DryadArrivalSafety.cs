namespace TrelamiumIII.Content.Dryad;

/// <summary>
///     Keeps the local player unhurt while the arrival holds their controls and camera. The arrival only starts in
///     town with no danger about, so this covers whatever turns up while the scene is playing.
/// </summary>
public sealed class DryadArrivalPlayer : ModPlayer
{
    public override bool FreeDodge(Player.HurtInfo info) => Player.whoAmI == Main.myPlayer && DryadArrivalSystem.Presenting;
}

/// <summary>No enemies spawn while an arrival runs, so nothing wanders into the scene or the conversation after it.</summary>
public sealed class DryadArrivalSpawns : GlobalNPC
{
    public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns)
    {
        if (DryadArrivalSystem.Running)
            maxSpawns = 0;
    }
}
