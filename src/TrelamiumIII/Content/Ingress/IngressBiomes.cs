using SubworldLibrary;
using TrelamiumIII.Common.Generation;
using TrelamiumIII.Common.Subworlds.Realms;

namespace TrelamiumIII.Content.Ingress;

public class HighlandDayBiome : ModBiome
{
    public override int Music => Assets.Music.HighlandsDay.Slot;
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

public class HighlandNightBiome : ModBiome
{
    public override int Music => Assets.Music.HighlandsNight.Slot;
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