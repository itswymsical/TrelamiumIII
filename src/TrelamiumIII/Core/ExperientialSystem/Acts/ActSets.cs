using System.Linq;
using TrelamiumIII.Core.Tiles;

namespace TrelamiumIII.Core.ExperientialSystem.Acts;

/// <summary>
/// Content id sets shared between an individual act and the group act it belongs to, so the membership
/// of "the pre-hardmode metals" is written down exactly once. A group act whose member list drifts from
/// its individual acts would leave a member the player can never cover.
/// </summary>
public static class ActSets
{
    /// <summary>
    /// The tiered pre-hardmode metals, in progression order. Both alternates of each tier are included:
    /// a world only generates one, so full coverage is a two-world or trade-driven goal on purpose.
    /// </summary>
    public static readonly int[] PreHardmodeMetalOre =
    [
        TileID.Copper, TileID.Tin,
        TileID.Iron, TileID.Lead,
        TileID.Silver, TileID.Tungsten,
        TileID.Gold, TileID.Platinum
    ];

    /// <summary>
    /// The tiered hardmode metals. Chlorophyte and Luminite are deliberately left out - they are not part
    /// of the ore-tier ladder, and gating this group's coverage behind Moon Lord would stall it for a
    /// whole hardmode's worth of play.
    /// </summary>
    public static readonly int[] HardmodeMetalOre =
    [
        TileID.Cobalt, TileID.Palladium,
        TileID.Mythril, TileID.Orichalcum,
        TileID.Adamantite, TileID.Titanium
    ];

    /// <summary>Ores that are not part of either metal ladder but still read as "a vein worth mining".</summary>
    public static readonly int[] ExoticOre =
    [
        TileID.Demonite, TileID.Crimtane,
        TileID.Meteorite, TileID.Hellstone,
        TileID.Chlorophyte
    ];

    /// <summary>
    /// Gems as they generate inside stone. Amber is deliberately absent: it has no naturally generated
    /// tile (it comes out of an Extractinator), and TileID.AmberStoneBlock is the crafted decorative
    /// block - crediting that would mean crediting blocks the player placed themselves.
    /// </summary>
    public static readonly int[] GemTiles =
    [
        TileID.Amethyst, TileID.Topaz, TileID.Sapphire,
        TileID.Emerald, TileID.Ruby, TileID.Diamond
    ];

    public static readonly int[] Bars =
    [
        ItemID.CopperBar, ItemID.TinBar,
        ItemID.IronBar, ItemID.LeadBar,
        ItemID.SilverBar, ItemID.TungstenBar,
        ItemID.GoldBar, ItemID.PlatinumBar,
        ItemID.DemoniteBar, ItemID.CrimtaneBar,
        ItemID.MeteoriteBar, ItemID.HellstoneBar,
        ItemID.CobaltBar, ItemID.PalladiumBar,
        ItemID.MythrilBar, ItemID.OrichalcumBar,
        ItemID.AdamantiteBar, ItemID.TitaniumBar,
        ItemID.HallowedBar, ItemID.ChlorophyteBar,
        ItemID.ShroomiteBar, ItemID.SpectreBar,
        ItemID.LunarBar
    ];

    public static readonly int[] HerbTiles =
    [
        TileID.ImmatureHerbs, TileID.MatureHerbs, TileID.BloomingHerbs, .. TrelamiumIII.Instance.GetContent<CustomHerb>().Select(herb => (int)herb.Type)
    ];

    public static readonly int[] Slimes =
    [
        NPCID.BlueSlime, NPCID.GreenSlime, NPCID.PurpleSlime, NPCID.RedSlime,
        NPCID.YellowSlime, NPCID.BlackSlime, NPCID.MotherSlime, NPCID.BabySlime,
        NPCID.Pinky, NPCID.LavaSlime, NPCID.DungeonSlime, NPCID.CorruptSlime,
        NPCID.Slimer, NPCID.Slimeling, NPCID.IceSlime, NPCID.SpikedIceSlime,
        NPCID.JungleSlime, NPCID.SpikedJungleSlime, NPCID.SandSlime, NPCID.ToxicSludge,
        NPCID.UmbrellaSlime, NPCID.RainbowSlime, NPCID.IlluminantSlime, NPCID.Crimslime,
        NPCID.GoldenSlime, NPCID.SlimeSpiked
    ];

    public static readonly int[] Undead =
    [
        NPCID.Zombie, NPCID.BaldZombie, NPCID.PincushionZombie, NPCID.SlimedZombie,
        NPCID.SwampZombie, NPCID.TwiggyZombie, NPCID.FemaleZombie, NPCID.ZombieEskimo,
        NPCID.ArmedZombie, NPCID.ArmedZombieEskimo, NPCID.ArmedZombiePincussion,
        NPCID.ArmedZombieSlimed, NPCID.ArmedZombieSwamp, NPCID.ArmedZombieTwiggy,
        NPCID.ZombieMerman, NPCID.Drippler, NPCID.Ghost, NPCID.Wraith,
        NPCID.DemonEye, NPCID.PurpleEye, NPCID.GreenEye, NPCID.DialatedEye,
        NPCID.SleepyEye, NPCID.CataractEye, NPCID.Skeleton, NPCID.ArmoredSkeleton, NPCID.SkeletonArcher, 
        NPCID.BigPantlessSkeleton, NPCID.BigHeadacheSkeleton, NPCID.BigMisassembledSkeleton, NPCID.HeavySkeleton, NPCID.GreekSkeleton, 
        NPCID.BoneThrowingSkeleton, NPCID.BoneThrowingSkeleton2, NPCID.BoneThrowingSkeleton3, NPCID.BoneThrowingSkeleton4, 
        NPCID.SmallSkeleton, NPCID.SporeSkeleton, NPCID.BigSkeleton, NPCID.SkeletonTopHat, NPCID.SkeletonAstonaut, NPCID.SkeletonAlien,
        NPCID.CursedSkull, NPCID.GiantCursedSkull
    ];

    public static readonly int[] Eyes =
    [
        NPCID.DemonEye, NPCID.DemonEye2, NPCID.EyeballFlyingFish, NPCID.Eyezor,
            NPCID.EyeofCthulhu, NPCID.CataractEye, NPCID.CataractEye2, NPCID.DemonEyeSpaceship,
            NPCID.DialatedEye, NPCID.DialatedEye2, NPCID.GreenEye,
            NPCID.GreenEye2, NPCID.WallofFleshEye, NPCID.WanderingEye,
            NPCID.SleepyEye, NPCID.SleepyEye2, NPCID.DemonEyeOwl, NPCID.Wraith,
            NPCID.DemonEye, NPCID.PurpleEye, NPCID.GreenEye, NPCID.DialatedEye,
            NPCID.Retinazer, NPCID.Spazmatism
    ];

    // FIH BELOW
    public static readonly int[] AllFish =
    [
        ItemID.Tuna, ItemID.Bass, ItemID.Shrimp, ItemID.AtlanticCod,ItemID.Trout, ItemID.SpecularFish, 
        ItemID.RockLobster, ItemID.RedSnapper, ItemID.NeonTetra, ItemID.Flounder, ItemID.CrimsonTigerfish
    ];

    public static readonly int[] CommonFish =
    [
        ItemID.AtlanticCod, ItemID.CrimsonTigerfish, ItemID.NeonTetra, ItemID.RedSnapper, ItemID.SpecularFish, 
        ItemID.Tuna, ItemID.Salmon, 
        ItemID.Bass, ItemID.Trout, ItemID.RockLobster, ItemID.RedSnapper, ItemID.Flounder, ItemID.CrimsonTigerfish
    ];

    public static readonly int[] UncommonFish =
    [
        ItemID.ArmoredCavefish, ItemID.Damselfish, ItemID.DoubleCod, ItemID.Ebonkoi, ItemID.FrostMinnow, ItemID.Hemopiranha,
            ItemID.Honeyfin, ItemID.PrincessFish, ItemID.Shrimp, ItemID.VariegatedLardfish
    ];

    public static readonly int[] RareFish =
    [
        ItemID.BlueJellyfish, ItemID.GreenJellyfish, ItemID.PinkJellyfish, ItemID.Obsidifish, ItemID.Prismite, ItemID.Stinkfish
    ];

    public static readonly int[] JellyFih =
    [
        ItemID.BlueJellyfish, ItemID.GreenJellyfish, ItemID.PinkJellyfish
    ];

    public static readonly int[] EvilBiomeFish =
    [
        ItemID.Hemopiranha, ItemID.CrimsonTigerfish, ItemID.Ebonkoi
    ];

    public static readonly int[] HallowFish =
    [
        ItemID.Prismite, ItemID.ChaosFish, ItemID.PrincessFish
    ];

    public static readonly int[] LegendaryFish =
    [
        ItemID.ChaosFish, ItemID.FlarefinKoi, ItemID.GoldenCarp
    ];
}

/// <summary>
/// Shared threshold ladders. Every act runs exactly <see cref="ExperientialConstants.ActPayoutTiers"/>
/// rungs, so payout shape is decided once in <see cref="ExperientialConstants.ActPayoutFor"/> and an
/// act only has to say how much doing counts as one rung.
/// </summary>
public static class ActCurves
{
    // Tier 10 is set near the top of what a dedicated playthrough actually reaches, not beyond it.
    // The payout curve peaks at the last rung, so tiers 8-10 carry ~51% of an act's total Exp. An
    // unreachable top rung does not make an act "aspirational", it silently throws away half its value.
    // These are first-pass estimates and the obvious thing to move once there is playtest data.

    /// <summary>A single ore type.</summary>
    public static readonly int[] Ore = [20, 55, 120, 220, 380, 600, 900, 1300, 1800, 2500];

    /// <summary>An ore group, fed simultaneously by every member ore.</summary>
    public static readonly int[] OreGroup = [100, 260, 550, 1000, 1700, 2700, 4000, 5800, 8000, 11000];

    /// <summary>Things you break by the thousand without trying - dirt, stone, snow.</summary>
    public static readonly int[] Bulk = [100, 300, 700, 1500, 3000, 5200, 8500, 13000, 19000, 27000];

    /// <summary>Crafting, where one action often yields a whole stack.</summary>
    public static readonly int[] Craft = [30, 90, 200, 400, 700, 1150, 1750, 2600, 3800, 5200];

    public static readonly int[] Fishing = [10, 25, 40, 60, 85, 110, 175, 225, 360, 500];
    public static readonly int[] Coins = [1000, 5000, 7500, 10000, 100000, 250000, 500000, 7500000, 25000000, 500000000];


    /// <summary>Ordinary enemy kills, counted per family.</summary>
    public static readonly int[] Combat = [20, 50, 110, 210, 380, 620, 950, 1400, 2000, 2800];
    public static readonly int[] CombatDamage = [200, 450, 700, 1250, 2200, 4250, 7600, 9500, 10200, 16500];

    /// <summary>
    /// Loot containers smashed in passing. Sized well below <see cref="Bulk"/>: a world only generates a
    /// few thousand pots in total, so a bulk ladder would leave the top rungs - and over half the act's
    /// Exp - permanently out of reach.
    /// </summary>
    public static readonly int[] Scavenge = [15, 40, 90, 170, 290, 460, 700, 1020, 1450, 2000];

    /// <summary>Deliberate, slower acts - planting a herb, felling a tree.</summary>
    public static readonly int[] Deliberate = [8, 22, 50, 95, 165, 270, 420, 620, 880, 1200];
}
