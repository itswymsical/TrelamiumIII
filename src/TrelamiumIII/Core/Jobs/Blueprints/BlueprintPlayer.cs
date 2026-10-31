using System.Linq;
using Terraria.Audio;
using Terraria.GameInput;
using TrelamiumIII.Common.Systems;

namespace TrelamiumIII.Core.Jobs.Blueprints;

/// <summary>
///     Staking a blueprint's site by hand. The stake key picks up a blueprint underway, unstaked ones first; it then
///     follows the cursor until a left click puts it down there. Right click, or the key again, lets go of it.
/// </summary>
/// <remarks>A site the job fixed can't be picked up. One the player staked can be moved at any time, since progress is re-read from wherever it lands.</remarks>
public sealed class BlueprintPlayer : ModPlayer
{
    /// <summary>The blueprint following the cursor, or null.</summary>
    public Blueprint? Holding { get; private set; }

    /// <summary>Set by the click that staked or let go, so it doesn't also swing the held item. Cleared once both buttons are up.</summary>
    private bool swallowClick;

    public override void OnEnterWorld() => Holding = null;

    public override void ProcessTriggers(TriggersSet triggersSet)
    {
        if (KeybindsSystem.StakeBlueprintKeybind.JustPressed)
        {
            var wasHolding = Holding is not null;

            Holding = wasHolding ? null : FirstMovable();

            if (wasHolding || Holding is not null)
                SoundEngine.PlaySound(SoundID.MenuTick);

            return;
        }

        if (Holding is null)
            return;

        // The job finished, or the stage moved on, while the blueprint was in hand.
        if (!BlueprintTask.Underway(Player.GetModPlayer<JobPlayer>()).Any(task => task.Blueprint == Holding))
        {
            Holding = null;
            return;
        }

        if (Main.mouseRight && Main.mouseRightRelease)
        {
            Holding = null;
            swallowClick = true;
            SoundEngine.PlaySound(SoundID.MenuTick);
            return;
        }

        if (!Main.mouseLeft || !Main.mouseLeftRelease || Player.mouseInterface)
            return;

        var topLeft = HeldOrigin(Holding);

        if (!Holding.FitsAt(topLeft))
            return;

        Holding.Stake(topLeft);
        Holding = null;
        swallowClick = true;
        SoundEngine.PlaySound(SoundID.Dig);
    }

    public override void SetControls()
    {
        if (swallowClick && !Main.mouseLeft && !Main.mouseRight)
            swallowClick = false;

        if (Holding is null && !swallowClick)
            return;

        Player.controlUseItem = false;
        Player.controlUseTile = false;
    }

    /// <summary>Where <paramref name="blueprint"/> would be staked if the player clicked now.</summary>
    public static Point HeldOrigin(Blueprint blueprint) => blueprint.OriginAt(Main.MouseWorld.ToTileCoordinates());

    private Blueprint? FirstMovable()
        => BlueprintTask.Underway(Player.GetModPlayer<JobPlayer>())
            .Select(task => task.Blueprint)
            .Where(blueprint => !blueprint.IsFixed)
            .OrderBy(blueprint => blueprint.Site is not null)
            .FirstOrDefault();
}
