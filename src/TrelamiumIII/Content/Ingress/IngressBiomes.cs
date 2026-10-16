using SubworldLibrary;
using Terraria.GameContent.Skies;
using Terraria.Graphics.Effects;
using TrelamiumIII.Common.Generation;
using TrelamiumIII.Common.Subworlds.Realms;
using TrelamiumIII.Content.Ingress.HighlandsBiome.BirchFurniture;

namespace TrelamiumIII.Content.Ingress;

public class HighlandsSky : CustomSky
{
    private bool _isActive;
    private float _intensity;

    public override void Update(GameTime gameTime)
    {
        if (_isActive)
        {
            _intensity += 0.05f;
            if (_intensity > 1f) _intensity = 1f;
        }
        else
        {
            _intensity -= 0.05f;
            if (_intensity < 0f) _intensity = 0f;
        }
    }

    public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth)
    {
        if (minDepth <= 0f && maxDepth > 0f && _intensity > 0f)
        {
            Color tintColor = new Color(126, 192, 162);

            float targetAlpha = 0.11f * _intensity;
            Color finalColor = tintColor * targetAlpha;

            Rectangle screenRect = new Rectangle(0, 0, Main.screenWidth, Main.screenHeight);

            spriteBatch.Draw(Terraria.GameContent.TextureAssets.MagicPixel.Value, screenRect, finalColor);
        }
    }

    public override bool IsActive()
    {
        return _isActive || _intensity > 0f;
    }

    public override void Activate(Vector2 position, params object[] args)
    {
        _isActive = true;
    }

    public override void Deactivate(params object[] args)
    {
        _isActive = false;
    }

    public override void Reset()
    {
        _isActive = false;
        _intensity = 0f;
    }
}

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
    public override void SpecialVisuals(Player player, bool isActive)
    {
        base.SpecialVisuals(player, isActive);
    }
    public override bool IsBiomeActive(Player player)
    {
        if (!SubworldSystem.IsActive<IngressRealm>())
            return false;

        return player.ZoneOverworldHeight && Main.dayTime;
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