using SubworldLibrary;
using Terraria.Graphics.Effects;
using TrelamiumIII.Common.Generation;
using TrelamiumIII.Common.Subworlds.Realms;
using TrelamiumIII.Content.Ingress.HighlandsBiome;
using TrelamiumIII.Content.Ingress.HighlandsBiome.BirchFurniture;

namespace TrelamiumIII.Content.Ingress;

/// <summary> The Temperate Highlands: the whole of Ingress's surface, by day and night. Its sky, fog and light live in <see cref="HighlandsAtmosphere"/>. </summary>
public class HighlandBiome : ModBiome
{
    public int GetMusic()
    {
        if (Main.LocalPlayer.ZoneGraveyard || Main.bloodMoon)
            return -1;

        if (Main.dayTime)
        {
            if (Main._shouldUseWindyDayMusic)
                return Assets.Music.HighlandsWindy.Slot;

            if (Main.IsItStorming)
                return Assets.Music.HighlandsThunderstorm.Slot;

            if (Main.raining)
                return Assets.Music.HighlandsRain.Slot;

            return Assets.Music.HighlandsDay.Slot;
        }

        bool rollAltTrack = (Main.time / 32400f) % 2 >= 1f;

        return rollAltTrack ? Assets.Music.HighlandsNightAlt.Slot : Assets.Music.HighlandsNight.Slot;

    }

    public override int Music => GetMusic();
    public override SceneEffectPriority Priority => SceneEffectPriority.BiomeLow;
    public override Color? BackgroundColor => new Color(88, 150, 112);

    public override int BiomeTorchItemType => ModContent.ItemType<BirchTorchItem>();
    public override int BiomeCampfireItemType => ModContent.ItemType<BirchCampfireItem>();
    /// <summary>
    /// Drives the sky through <see cref="SkyManager"/> directly: <see cref="Player.ManageSpecialBiomeVisuals"/> also
    /// dereferences a scene filter under the same key, unchecked, and the highlands' fog is a filter of its own.
    /// </summary>
    public override void SpecialVisuals(Player player, bool isActive)
    {
        if (player.whoAmI != Main.myPlayer || SkyManager.Instance[HighlandsSky.Key] is not HighlandsSky sky || sky.Wanted == isActive)
            return;

        if (isActive)
            SkyManager.Instance.Activate(HighlandsSky.Key);
        else
            SkyManager.Instance.Deactivate(HighlandsSky.Key);
    }

    /// <summary> Sky height too, since the tallest peaks can reach toward it. </summary>
    public override bool IsBiomeActive(Player player)
    {
        if (!SubworldSystem.IsActive<IngressRealm>())
            return false;

        return player.ZoneOverworldHeight || player.ZoneSkyHeight;
    }
}

// the underground
public class IngressGardenBiome : ModBiome
{
    public override int Music => Assets.Music.HighlandsDay.Slot; // placeholder
    public override SceneEffectPriority Priority => SceneEffectPriority.BiomeLow;
    //public override ModSurfaceBackgroundStyle SurfaceBackgroundStyle => ModContent.GetInstance<TaigaBackgroundStyle>();
    //public override string BestiaryIcon => "Reverie/Assets/Textures/Bestiary/TaigaIcon";
    //public override string BackgroundPath => "Reverie/Assets/Textures/Bestiary/TaigaMapBG";
    public override Color? BackgroundColor => new Color(88, 150, 112);
    //public override ModWaterStyle WaterStyle => ModContent.GetInstance<TaigaWaterStyle>();

    public override bool IsBiomeActive(Player player)
    {
        if (!SubworldSystem.IsActive<IngressRealm>())
            return false;

        return player.ZoneDirtLayerHeight || player.ZoneRockLayerHeight;
    }
}

public class IngressTempleBiome : ModBiome
{
    public override int Music => Assets.Music.LigneousTemple.Slot;
    public override SceneEffectPriority Priority => SceneEffectPriority.Environment;
    //public override ModSurfaceBackgroundStyle SurfaceBackgroundStyle => ModContent.GetInstance<TaigaBackgroundStyle>();
    //public override string BestiaryIcon => "Reverie/Assets/Textures/Bestiary/TaigaIcon";
    //public override string BackgroundPath => "Reverie/Assets/Textures/Bestiary/TaigaMapBG";
    public override Color? BackgroundColor => new Color(88, 150, 112);
    //public override ModWaterStyle WaterStyle => ModContent.GetInstance<TaigaWaterStyle>();

    public override bool IsBiomeActive(Player player)
    {
        if (!SubworldSystem.IsActive<IngressRealm>())
            return false;

        return TileCounts.Instance.ligneousTempleCount > 200;
    }
}

public class ReverieBiome : ModBiome
{
    public override int Music => Assets.Music.ReverieLibrary.Slot;
    public override SceneEffectPriority Priority => SceneEffectPriority.Environment;
    public override Color? BackgroundColor => new Color(88, 150, 112);
    public override bool IsBiomeActive(Player player)
    {
        if (!SubworldSystem.IsActive<ReverieRealm>())
            return false;

        return true;
    }
}