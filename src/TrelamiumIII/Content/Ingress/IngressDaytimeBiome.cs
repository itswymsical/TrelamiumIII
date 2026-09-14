using SubworldLibrary;
using TrelamiumIII.Common.Generation;
using TrelamiumIII.Common.Subworlds.Realms;

namespace TrelamiumIII.Content.Ingress;

public class IngressDaytimeBiome : ModBiome
{
    public override int Music => Assets.Music.Woodhaven.Slot;
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
        
        return player.ZoneOverworldHeight && Main.dayTime;
    }
}

public class IngressNighttimeBiome : ModBiome
{
    public override int Music => Assets.Music.IngressNight.Slot;
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

        return player.ZoneOverworldHeight && !Main.dayTime;
    }
}
public class IngressTempleBiome : ModBiome
{
    public override int Music => Assets.Music.LigneousTemple.Slot;
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

        return (player.ZoneDirtLayerHeight || player.ZoneRockLayerHeight) && TileCounts.Instance.ligneousTempleCount > 140;
    }
}