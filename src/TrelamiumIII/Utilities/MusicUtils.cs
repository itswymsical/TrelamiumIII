namespace TrelamiumIII.Utilities;
public enum MusicFadeMode
{
    Instant,
    CrossFade,
    FadeIn,
    FadeOut,
    NoFade
}

public static class MusicFadeHandler
{
    private static int? _storedMusicBox = null;
    private static int? _storedCurMusic = null;
    private static float _fadeTimer = 0f;
    private static float _fadeTargetTime = 0f;
    private static bool _isFading = false;
    private static MusicFadeMode _currentFadeMode = MusicFadeMode.CrossFade;
    private static bool _isCutsceneMusicActive = false;
    private static int _cutsceneMusicID = -1;

    /// <summary>
    /// Sets music with specified fade behavior
    /// </summary>
    /// <param name="musicID">Music ID to play</param>
    /// <param name="fadeMode">How the music should transition</param>
    /// <param name="fadeTime">Duration of fade in seconds (for FadeIn/FadeOut modes)</param>
    public static void SetMusic(int musicID, MusicFadeMode fadeMode = MusicFadeMode.CrossFade, float fadeTime = 1f)
    {
        if (!_isCutsceneMusicActive)
        {
            // Store current music state
            _storedMusicBox = MusicBoxOverride.Value;
            _storedCurMusic = Main.curMusic;
            _isCutsceneMusicActive = true;
        }

        _cutsceneMusicID = musicID;
        _currentFadeMode = fadeMode;
        _fadeTargetTime = fadeTime;
        _fadeTimer = 0f;

        switch (fadeMode)
        {
            case MusicFadeMode.Instant:
                SetMusicInstant(musicID);
                break;

            case MusicFadeMode.CrossFade:
                SetMusicCrossFade(musicID);
                break;

            case MusicFadeMode.FadeIn:
                SetMusicFadeIn(musicID, fadeTime);
                break;

            case MusicFadeMode.FadeOut:
                SetMusicFadeOut(musicID, fadeTime);
                break;

            case MusicFadeMode.NoFade:
                SetMusicNoFade(musicID);
                break;
        }
    }

    public static void Update(float deltaTime)
    {
        if (_isCutsceneMusicActive && _cutsceneMusicID >= 0)
        {
            if (MusicBoxOverride.Value != _cutsceneMusicID)
            {
                MusicBoxOverride.Value = _cutsceneMusicID;
            }
        }

        if (!_isFading) return;

        _fadeTimer += deltaTime;

        switch (_currentFadeMode)
        {
            case MusicFadeMode.FadeIn:
                UpdateFadeIn();
                break;

            case MusicFadeMode.FadeOut:
                UpdateFadeOut();
                break;
        }
    }

    public static void RestorePreviousMusic(MusicFadeMode fadeMode = MusicFadeMode.CrossFade, float fadeTime = 1f)
    {
        _isCutsceneMusicActive = false;
        _cutsceneMusicID = -1;

        if (_storedMusicBox.HasValue)
        {
            if (fadeMode == MusicFadeMode.FadeOut)
            {
                _currentFadeMode = MusicFadeMode.FadeOut;
                _fadeTargetTime = fadeTime;
                _fadeTimer = 0f;
                _isFading = true;
            }
            else
            {
                MusicBoxOverride.Value = _storedMusicBox.Value;

                if (fadeMode == MusicFadeMode.Instant && _storedCurMusic.HasValue && _storedCurMusic.Value < Main.musicFade.Length)
                {
                    Main.musicFade[_storedCurMusic.Value] = 1f;
                }
                else if (fadeMode == MusicFadeMode.NoFade && _storedCurMusic.HasValue && _storedCurMusic.Value < Main.musicNoCrossFade.Length)
                {
                    Main.musicNoCrossFade[_storedCurMusic.Value] = true;
                }
            }
        }
        else
        {
            MusicBoxOverride.Value = -1; // Return to ambient music
        }

        if (fadeMode != MusicFadeMode.FadeOut)
        {
            _storedMusicBox = null;
            _storedCurMusic = null;
            _isFading = false;
            _fadeTimer = 0f;
        }
    }

    public static void StopMusic(MusicFadeMode fadeMode = MusicFadeMode.FadeOut, float fadeTime = 1f)
    {
        if (fadeMode == MusicFadeMode.Instant)
        {
            MusicBoxOverride.Value = 0;
            if (Main.curMusic < Main.musicFade.Length)
            {
                Main.musicFade[Main.curMusic] = 0f;
            }
        }
        else if (fadeMode == MusicFadeMode.FadeOut)
        {
            _currentFadeMode = MusicFadeMode.FadeOut;
            _fadeTargetTime = fadeTime;
            _fadeTimer = 0f;
            _isFading = true;
        }
        else
        {
            MusicBoxOverride.Value = 0;
        }
    }

    private static void SetMusicInstant(int musicID)
    {
        if (Main.curMusic > 0 && Main.curMusic < Main.musicFade.Length && Main.curMusic != musicID)
        {
            Main.musicFade[Main.curMusic] = 0f;
        }

        MusicBoxOverride.Value = musicID;
        if (musicID < Main.musicFade.Length)
        {
            Main.musicFade[musicID] = 1f;
        }
        _isFading = false;
    }

    private static void SetMusicCrossFade(int musicID)
    {
        MusicBoxOverride.Value = musicID;
        _isFading = false;
    }

    private static void SetMusicFadeIn(int musicID, float fadeTime)
    {
        if (Main.curMusic > 0 && Main.curMusic < Main.musicFade.Length && Main.curMusic != musicID)
        {
            Main.musicFade[Main.curMusic] = 0f;
        }

        MusicBoxOverride.Value = musicID;
        if (musicID < Main.musicFade.Length)
        {
            Main.musicFade[musicID] = 0f;
        }
        _isFading = true;
    }

    private static void SetMusicFadeOut(int musicID, float fadeTime)
    {
        _isFading = true;
    }

    private static void SetMusicNoFade(int musicID)
    {
        if (Main.curMusic > 0 && Main.curMusic < Main.musicFade.Length && Main.curMusic != musicID)
        {
            Main.musicFade[Main.curMusic] = 0f;
        }

        MusicBoxOverride.Value = musicID;
        if (musicID < Main.musicNoCrossFade.Length)
        {
            Main.musicNoCrossFade[musicID] = true;
        }
        _isFading = false;
    }

    private static void UpdateFadeIn()
    {
        float progress = Math.Min(_fadeTimer / _fadeTargetTime, 1f);

        if (Main.curMusic < Main.musicFade.Length)
        {
            Main.musicFade[Main.curMusic] = progress;
        }

        if (progress >= 1f)
        {
            _isFading = false;
        }
    }

    private static void UpdateFadeOut()
    {
        float progress = Math.Min(_fadeTimer / _fadeTargetTime, 1f);
        float fadeLevel = 1f - progress;

        if (Main.curMusic < Main.musicFade.Length)
        {
            Main.musicFade[Main.curMusic] = fadeLevel;
        }

        if (progress >= 1f)
        {
            _isFading = false;

            if (!_isCutsceneMusicActive && _storedMusicBox.HasValue)
            {
                MusicBoxOverride.Value = _storedMusicBox.Value;
                _storedMusicBox = null;
                _storedCurMusic = null;
            }
            else
            {
                MusicBoxOverride.Value = 0;
            }
        }
    }

    /// <summary>
    /// Check if music is currently fading
    /// </summary>
    public static bool IsFading => _isFading;

    /// <summary>
    /// Check if cutscene music is currently active
    /// </summary>
    public static bool IsCutsceneMusicActive => _isCutsceneMusicActive;

    /// <summary>
    /// Get the current fade progress (0.0 to 1.0)
    /// </summary>
    public static float FadeProgress => _fadeTargetTime > 0 ? Math.Min(_fadeTimer / _fadeTargetTime, 1f) : 1f;

    /// <summary>
    /// Force stop any ongoing fade operations
    /// </summary>
    public static void ForceStopFade()
    {
        _isFading = false;
        _fadeTimer = 0f;
    }

    /// <summary>
    /// Immediately stops all music
    /// </summary>
    public static void StopAllMusic()
    {
        MusicBoxOverride.Value = 0;
        for (int i = 0; i < Main.musicFade.Length; i++)
        {
            Main.musicFade[i] = 0f;
        }
        _isFading = false;
        _isCutsceneMusicActive = false;
        _cutsceneMusicID = -1;
    }
}

/// <summary>
///     A music box value held on the local player until changed. Stands in for 1.4.4's global
///     <c>Main.musicBox2</c>: its 1.4.5 replacement, <see cref="Player.musicBox" />, is cleared every tick.
/// </summary>
public sealed class MusicBoxOverride : ModPlayer
{
    /// <summary>The music box value to force, or -1 for none.</summary>
    public static int Value { get; set; } = -1;

    public override void OnEnterWorld() => Value = -1;

    public override void PostUpdateEquips()
    {
        if (Player.whoAmI == Main.myPlayer && Value >= 0)
            Player.musicBox = Value;
    }
}
