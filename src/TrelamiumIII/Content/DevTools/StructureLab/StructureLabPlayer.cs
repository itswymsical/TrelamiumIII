using Terraria.DataStructures;

namespace TrelamiumIII.Content.DevTools.StructureLab;

/// <summary>
///     Building in the structure lab: the player is an invisible camera that flies through tiles on the movement keys
///     (jump to go faster), with long reach, fast tools and no damage.
/// </summary>
public sealed class StructureLabPlayer : ModPlayer
{
    private const float FlySpeed = 8f;
    private const float FastFlySpeed = 24f;
    private const int ExtraReach = 150;

    /// <summary>How close to the world edge the player may fly, in pixels; the camera stops about 41 tiles in.</summary>
    private const float EdgeClearance = 42 * 16f;

    private Vector2 flightPosition;
    private bool flying;
    private bool fast;

    public override void SetControls()
    {
        if (!StructureEditor.InLab)
            return;

        fast = Player.controlJump;
        Player.controlJump = false;
        Player.controlHook = false;
        Player.controlMount = false;
    }

    public override void PostUpdateEquips()
    {
        if (!StructureEditor.InLab)
            return;

        Player.blockRange += ExtraReach;
        Player.tileSpeed += 3f;
        Player.wallSpeed += 3f;
        Player.pickSpeed *= 0.25f;
        Player.gills = true;
        Player.lavaImmune = true;
        Player.noFallDmg = true;
    }

    public override void PreUpdateMovement()
    {
        if (!StructureEditor.InLab || Player.whoAmI != Main.myPlayer)
            return;

        var input = new Vector2(
            (Player.controlRight ? 1 : 0) - (Player.controlLeft ? 1 : 0),
            (Player.controlDown ? 1 : 0) - (Player.controlUp ? 1 : 0));

        if (input != Vector2.Zero)
            input.Normalize();

        var min = new Vector2(EdgeClearance);
        var max = new Vector2(Main.maxTilesX * 16f - EdgeClearance - Player.width, Main.maxTilesY * 16f - EdgeClearance - Player.height);

        flightPosition = Vector2.Clamp(Player.position + input * (fast ? FastFlySpeed : FlySpeed), min, max);
        flying = true;

        Player.position = flightPosition;
        Player.velocity = Vector2.Zero;
    }

    /// <summary>Undoes whatever collision did after <see cref="PreUpdateMovement" />, which is what lets the player pass through tiles.</summary>
    public override void PostUpdate()
    {
        if (!flying)
            return;

        flying = false;

        Player.position = flightPosition;
        Player.velocity = Vector2.Zero;
        Player.fallStart = (int)(Player.position.Y / 16f);
    }

    public override bool ImmuneTo(PlayerDeathReason damageSource, int cooldownCounter, bool dodgeable) => StructureEditor.InLab;

    public override void HideDrawLayers(PlayerDrawSet drawInfo)
    {
        if (!StructureEditor.InLab)
            return;

        foreach (PlayerDrawLayer layer in PlayerDrawLayerLoader.Layers)
            layer.Hide();
    }
}

/// <summary>Placing blocks and walls in the structure lab does not use them up.</summary>
public sealed class StructureLabGlobalItem : GlobalItem
{
    public override bool ConsumeItem(Item item, Player player)
        => !StructureEditor.InLab || (item.createTile < TileID.Dirt && item.createWall <= WallID.None);
}

public sealed class StructureLabGlobalNPC : GlobalNPC
{
    public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns)
    {
        if (StructureEditor.InLab)
            maxSpawns = 0;
    }
}
