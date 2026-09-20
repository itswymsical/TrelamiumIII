using System.Collections.Generic;
using Terraria.GameContent;
using TrelamiumIII.Content.Skills;
using TrelamiumIII.Core.ExperientialSystem.Skills;
using TrelamiumIII.Core.Items.Reworks;
using TrelamiumIII.Core.Projectiles;

namespace TrelamiumIII.Content.Vanilla;

/// <summary>
/// Turns a swung pickaxe into a thrown <see cref="ThrownTool"/> that mines the tiles it passes through.
/// Gated on <see cref="PickaxeBoomerangSkill"/>, so the item is untouched for anyone without it.
/// </summary>
public sealed class PickaxeBoomerangRework : ItemRework
{
    /// <summary><see cref="Player.altFunctionUse"/> while an alt use is running.</summary>
    private const int ALT_USE = 2;

    public override string SkillID => PickaxeBoomerangSkill.Id;

    public override bool Applies(Item item) => IsPlainPickaxe(item);

    /// <summary>Right click throws; left click is left alone, because precise mining still needs it.</summary>
    public override bool AltFunctionUse(Item item, Player player) => true;

    /// <summary>Only the throw displaces mining. A left click still cuts the tile under the cursor.</summary>
    public override bool SuppressesMining(Item item, Player player) => player.altFunctionUse == ALT_USE;

    public static bool IsPlainPickaxe(Item item)
        => item.pick > 0
           && item.hammer == 0
           && item.shoot <= ProjectileID.None
           && item.useStyle == ItemUseStyleID.Swing;
    public override bool? CanUse(Item item, Player player)
        => player.altFunctionUse != ALT_USE || CanThrow(item, player);

    public override bool? Use(Item item, Player player)
    {
        if (player.altFunctionUse != ALT_USE)
            return null;

        // UseItem runs once per useTime and a pickaxe animates for longer than that, so without this the
        // back half of the swing throws a second tool. CanUse only gates the start of the swing.
        if (!CanThrow(item, player))
            return false;

        if (player.whoAmI != Main.myPlayer)
            return true;

        ThrownToolProfile profile = ThrownTool.ProfileFor(item.type);

        Vector2 origin = player.MountedCenter;
        Vector2 aim = (Main.MouseWorld - origin).SafeNormalize(Vector2.UnitX * player.direction);

        // ai[0] and ai[1] are vanilla boomerang AI's; the item type rides in ai[2].
        Projectile.NewProjectile(
            player.GetSource_ItemUse(item),
            origin,
            aim * profile.Speed,
            ModContent.ProjectileType<ThrownTool>(),
            player.GetWeaponDamage(item),
            player.GetWeaponKnockback(item, item.knockBack),
            player.whoAmI,
            ai2: item.type
        );

        return true;
    }

    private static bool CanThrow(Item item, Player player)
        => player.ownedProjectileCounts[ModContent.ProjectileType<ThrownTool>()]
           < ThrownTool.ProfileFor(item.type).MaxInFlight;

    /// <remarks>Reuses the skill's own description, so the tooltip cannot drift from what was bought.</remarks>
    public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
    {
        if (SkillRegistry.Get(SkillID) is { } skill)
            tooltips.Add(new TooltipLine(TrelamiumIII.Instance, "PickaxeBoomerang", skill.Description));
    }
}

/// <summary>
/// Gives every pickaxe a <see cref="ThrownToolProfile"/> scaled from its pick power, so modded pickaxes
/// are covered without a table. Runs in PostSetupContent, once <see cref="ContentSamples"/> is filled.
/// </summary>
public sealed class PickaxeBoomerangProfiles : ModSystem
{
    /// <summary>Pick power of a copper pickaxe and of the best vanilla one; the tiers between interpolate.</summary>
    private const float WEAKEST_PICK = 35f;
    private const float STRONGEST_PICK = 210f;

    public override void PostSetupContent()
    {
        foreach (Item sample in ContentSamples.ItemsByType.Values)
        {
            if (PickaxeBoomerangRework.IsPlainPickaxe(sample))
                ThrownTool.Register(sample.type, ProfileFor(sample));
        }
    }

    private static ThrownToolProfile ProfileFor(Item sample)
    {
        float tier = MathHelper.Clamp((sample.pick - WEAKEST_PICK) / (STRONGEST_PICK - WEAKEST_PICK), 0f, 1f);

        return ThrownToolProfile.Pickaxe with
        {
            // Boomerang AI turns around on a fixed tick count, so launch speed is also the reach.
            Speed = MathHelper.Lerp(10f, 15f, tier),

            // 3 clears ordinary stone in one pass at the weakest tier; 5 covers the tiles the strongest
            // pickaxes are gated on but only chip at a quarter power, such as Lihzahrd brick.
            MineHits = (int)MathHelper.Lerp(3f, 5f, tier),
        };
    }
}
