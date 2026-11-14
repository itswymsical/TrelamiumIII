using SubworldLibrary;
<<<<<<< HEAD
using Terraria.Graphics.Effects;
using TrelamiumIII.Common.Generation;
using TrelamiumIII.Common.Subworlds.Realms;
using TrelamiumIII.Content.Ingress.HighlandsBiome;
=======
using Terraria.GameContent.Skies;
using Terraria.Graphics.Effects;
using TrelamiumIII.Common.Generation;
using TrelamiumIII.Common.Subworlds.Realms;
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb
using TrelamiumIII.Content.Ingress.HighlandsBiome.BirchFurniture;

namespace TrelamiumIII.Content.Ingress;

<<<<<<< HEAD
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
                return Assets.Music.Ingress.HighlandsWindy.Slot;

            if (Main.IsItStorming)
                return Assets.Music.Ingress.HighlandsThunderstorm.Slot;

            if (Main.raining)
                return Assets.Music.Ingress.HighlandsRain.Slot;

            return Assets.Music.Ingress.HighlandsDay.Slot;
        }

        bool rollAltTrack = (Main.time / 32400f) % 2 >= 1f;

        return rollAltTrack ? Assets.Music.Ingress.HighlandsNightAlt.Slot : Assets.Music.Ingress.HighlandsNight.Slot;

=======
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
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb
    }

<<<<<<< HEAD
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
=======
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
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb
    public override bool IsBiomeActive(Player player)
    {
        if (!SubworldSystem.IsActive<IngressRealm>())
            return false;

<<<<<<< HEAD
        return player.ZoneOverworldHeight || player.ZoneSkyHeight;
=======
        return player.ZoneOverworldHeight && Main.dayTime;
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb
    }
}

// the underground
public class IngressGardenBiome : ModBiome
{
<<<<<<< HEAD
    public override int Music => Assets.Music.Ingress.HighlandsDay.Slot; // placeholder
=======
    public override int Music => Assets.Music.HighlandsDay.Slot; // placeholder
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb
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
    public override int Music => Assets.Music.Ingress.LigneousTemple.Slot;
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
    public override int Music => Assets.Music.Ingress.ReverieLibrary.Slot;
    public override SceneEffectPriority Priority => SceneEffectPriority.Environment;
    public override Color? BackgroundColor => new Color(88, 150, 112);
    public override bool IsBiomeActive(Player player)
    {
        if (!SubworldSystem.IsActive<ReverieRealm>())
            return false;

        return true;
    }
}