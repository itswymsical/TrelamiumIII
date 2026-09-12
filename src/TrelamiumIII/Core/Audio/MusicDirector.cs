using System.Diagnostics;
using Microsoft.Xna.Framework.Audio;
using Terraria.Audio;

namespace TrelamiumIII.Core.Audio;

/// <summary>
///     Runs a <see cref="MusicScore" />: its tracks start together and keep rolling under our faders and bar count, so arrangements
///     swap seamlessly. Vanilla's track choice and volumes are overwritten after it runs; faders are linear on purpose.
/// </summary>
internal sealed class MusicDirector : ModSystem
{
    /// <summary>
    ///     Floor held under managed slots' <see cref="Main.musicFade" /> so vanilla never stops them: a "do not stop" token, not a volume.
    /// </summary>
    private const float KeepAlive = 0.2f;

    /// <summary>Below this a fader counts as closed.</summary>
    private const float Silence = 0.0005f;

    /// <summary>Longest frame the transport accepts, so a load hitch can't jump the bar count past the audio.</summary>
    private const double MaxFrame = 0.25d;

    private static MusicScore? score;

    private static float[] gain = [];
    private static float[] target = [];

    /// <summary>Fader speed in gain units per second. Zero is an instant move.</summary>
    private static float[] rate = [];

    /// <summary>Whether a cue has been fired and is currently sounding.</summary>
    private static bool[] sounding = [];

    /// <summary>Seconds of music elapsed since the score started. The bar count is derived from it.</summary>
    private static double transport;

    private static readonly Stopwatch clock = new();

    private static int pendingCue = -1;
    private static double pendingAt;
    private static bool pendingCut;

    /// <summary>Transport time the running cue ends at, or -1 when none is running.</summary>
    private static double cueEndsAt = -1d;

    private static bool releasing;

    public static bool IsPlaying => score is not null;

    public static string? Playing => score?.Name;

    /// <summary>Beats elapsed, wrapped into the loop. Fractional -- the whole point is the fraction.</summary>
    public static float Beats
    {
        get
        {
            if (score is not { } current)
                return 0f;

            return (float)(GridTime(current) / current.SecondsPerBeat);
        }
    }

    public static float Bars => score is { } current ? Beats / Math.Max(current.BeatsPerBar, 1) : 0f;

    /// <summary>0 on the beat, approaching 1 just before the next one. For anything that wants to pulse.</summary>
    public static float BeatPhase => Beats - MathF.Floor(Beats);

    public static float BarPhase => Bars - MathF.Floor(Bars);

    /// <summary>Starts <paramref name="next" /> from the top, replacing whatever was running.</summary>
    /// <param name="fadeIn">Seconds for the opening stems to reach their gain. Zero starts them up.</param>
    public static void Play(MusicScore next, float fadeIn = 0f)
    {
        if (Main.dedServ)
            return;

        Release();

        // Every track restarts from its first sample, even ones vanilla already had going: the transport is measured from
        // this frame, and an adopted mid-phrase stem would desync the bar count.
        if (Tracks is { } tracks)
        {
            foreach (MusicLayer layer in next.Layers)
            {
                if (TrackAt(tracks, layer.Slot) is not { } track)
                    continue;

                if (!track.IsStopped)
                    track.Stop(AudioStopOptions.Immediate);

                track.Reuse();
            }
        }

        score = next;

        int count = next.Layers.Length;

        gain = new float[count];
        target = new float[count];
        rate = new float[count];
        sounding = new bool[count];

        for (int i = 0; i < count; i++)
        {
            MusicLayer layer = next.Layers[i];

            bool open = layer.Kind == MusicLayerKind.Stem && layer.StartsOn;

            target[i] = open ? 1f : 0f;
            gain[i] = open && fadeIn > 0f ? 0f : target[i];
            rate[i] = fadeIn > 0f ? 1f / fadeIn : 0f;
        }

        transport = 0d;
        clock.Restart();
    }

    /// <summary>Closes every fader and hands the slots back to vanilla once they're shut.</summary>
    public static void Stop(float fade = 1.5f)
    {
        if (score is null || releasing)
            return;

        releasing = true;

        for (int i = 0; i < target.Length; i++)
        {
            target[i] = 0f;
            rate[i] = fade > 0f ? 1f / fade : 0f;
        }

        if (fade <= 0f)
            Release();
    }

    /// <summary>Opens or closes a stem's fader over <paramref name="fade" /> seconds.</summary>
    public static void SetLayer(string name, bool on, float fade = 1f)
    {
        if (score is not { } current)
            return;

        int index = current.IndexOf(name);

        if (index < 0)
            return;

        target[index] = on ? 1f : 0f;
        rate[index] = fade > 0f ? 1f / fade : 0f;
    }

    /// <summary>Closes <paramref name="from" /> and opens <paramref name="to" /> over the same window.</summary>
    public static void Crossfade(string from, string to, float fade = 1.5f)
    {
        SetLayer(from, false, fade);
        SetLayer(to, true, fade);
    }

    /// <summary>Schedules a cue to start on the next <paramref name="grid" /> boundary.</summary>
    /// <param name="cut">Whether stems cut when the cue lands; true for material that continues the track, like a death fill.</param>
    /// <returns>Seconds until it lands, or -1 if there is no such cue.</returns>
    public static float Fire(string name, MusicGrid grid = MusicGrid.Bar, bool cut = true)
    {
        if (score is not { } current)
            return -1f;

        int index = current.IndexOf(name);

        if (index < 0 || current.Layers[index].Kind != MusicLayerKind.Cue)
            return -1f;

        pendingCue = index;
        pendingCut = cut;
        pendingAt = NextBoundary(current, grid);

        return (float)(pendingAt - transport);
    }

    public override void Load()
    {
        if (Main.dedServ)
            return;

        On_Main.UpdateAudio += DriveMix;
        On_Main.UpdateAudio_DecideOnNewMusic += ClaimCurrentTrack;
    }

    public override void Unload()
    {
        if (Main.dedServ)
            return;

        On_Main.UpdateAudio -= DriveMix;
        On_Main.UpdateAudio_DecideOnNewMusic -= ClaimCurrentTrack;

        score = null;
    }

    public override void OnWorldUnload() => Stop(0f);

    /// <summary>Lets vanilla decide the track, then overrides the answer while keeping the method's other bookkeeping.</summary>
    private static void ClaimCurrentTrack(On_Main.orig_UpdateAudio_DecideOnNewMusic orig, Main self)
    {
        orig(self);

        if (score is { } current && !releasing)
            Main.newMusic = current.Primary.Slot;
    }

    private static void DriveMix(On_Main.orig_UpdateAudio orig, Main self)
    {
        orig(self);

        try
        {
            Update();

            // Re-asserted here too, in case the decide hook's call chain lands a frame late.
            if (score is { } current && !releasing)
                Main.newMusic = current.Primary.Slot;
        }
        catch (Exception exception)
        {
            // Audio runs every frame and an exception here would be thrown every frame. One bad
            // score should cost the layering, not the game.
            TrelamiumIII.Instance.Logger.Error($"Music director failed, releasing: {exception}");

            Release();
        }
    }

    private static void Update()
    {
        if (score is not { } current)
            return;

        if (Tracks is not { } tracks)
            return;

        // Deferred while a cue is sounding: the condition that ends a fight is usually the boss
        // ceasing to exist, which is the exact moment the fill is meant to be playing.
        if (!releasing && cueEndsAt < 0d && current.StaysActive is { } stays && !stays())
        {
            Stop();
            return;
        }

        double delta = Elapsed(tracks, current);

        transport += delta;

        AdvanceFaders(delta);
        ServiceCue(current, tracks);

        for (int i = 0; i < current.Layers.Length; i++)
            DriveLayer(current.Layers[i], i, tracks);

        if (releasing && Closed())
            Release();
    }

    private static void DriveLayer(MusicLayer layer, int index, IAudioTrack[] tracks)
    {
        // A cue that has not been fired is not ours yet, and a slot vanilla is free to use for
        // something else should be left alone.
        if (layer.Kind == MusicLayerKind.Cue && !sounding[index])
            return;

        if (TrackAt(tracks, layer.Slot) is not { } track)
            return;

        if (layer.Slot < Main.musicFade.Length)
            Main.musicFade[layer.Slot] = Math.Max(gain[index] * layer.Gain, KeepAlive);

        // IsStopped rather than !IsPlaying: a track vanilla has paused for an unfocused window is
        // neither, and calling Play on it would restart the piece from the top.
        if (track.IsStopped)
            track.Play();

        track.SetVariable("Volume", gain[index] * layer.Gain * Main.musicVolume);
    }

    /// <summary>
    ///     Seconds of music since last frame, from the wall clock and gated on the primary stem sounding, so pauses don't advance it.
    /// </summary>
    private static double Elapsed(IAudioTrack[] tracks, MusicScore current)
    {
        double delta = clock.Elapsed.TotalSeconds;

        clock.Restart();

        if (TrackAt(tracks, current.Primary.Slot) is not { IsPlaying: true })
            return 0d;

        return Math.Min(delta, MaxFrame);
    }

    /// <summary>Walks each fader toward its target on the gated delta, so crossfades freeze when the game pauses.</summary>
    private static void AdvanceFaders(double delta)
    {
        for (int i = 0; i < gain.Length; i++)
        {
            if (gain[i] == target[i])
                continue;

            float step = rate[i] <= 0f ? 1f : rate[i] * (float)delta;

            gain[i] = gain[i] < target[i]
                ? Math.Min(gain[i] + step, target[i])
                : Math.Max(gain[i] - step, target[i]);
        }
    }

    private static void ServiceCue(MusicScore current, IAudioTrack[] tracks)
    {
        if (pendingCue >= 0 && transport >= pendingAt)
        {
            int index = pendingCue;

            pendingCue = -1;
            sounding[index] = true;

            gain[index] = target[index] = 1f;
            rate[index] = 0f;

            MusicLayer cue = current.Layers[index];

            cueEndsAt = cue.Seconds > 0f ? transport + cue.Seconds : -1d;

            if (pendingCut)
            {
                for (int i = 0; i < current.Layers.Length; i++)
                {
                    if (current.Layers[i].Kind != MusicLayerKind.Stem)
                        continue;

                    gain[i] = target[i] = 0f;
                }
            }

            // Reuse before Play: a cue fired a second time in one session would otherwise resume
            // from wherever the previous firing left the stream.
            if (TrackAt(tracks, cue.Slot) is { } track)
            {
                track.Reuse();
                track.Play();
            }

            return;
        }

        if (cueEndsAt < 0d || transport < cueEndsAt)
            return;

        cueEndsAt = -1d;

        // Sampled before the cue is taken down, because taking it down is what would make the score
        // look finished either way.
        bool stemsStillUp = StemsAudible(current);

        SilenceCues(current, tracks);

        // A cue that cut the stems just played the last of the music, so stop rather than hold silent slots from vanilla.
        if (!stemsStillUp)
            Stop(0.25f);
    }

    private static bool StemsAudible(MusicScore current)
    {
        for (int i = 0; i < current.Layers.Length; i++)
        {
            if (current.Layers[i].Kind != MusicLayerKind.Stem)
                continue;

            if (gain[i] > Silence || target[i] > Silence)
                return true;
        }

        return false;
    }

    private static void SilenceCues(MusicScore current, IAudioTrack[] tracks)
    {
        for (int i = 0; i < current.Layers.Length; i++)
        {
            if (current.Layers[i].Kind != MusicLayerKind.Cue || !sounding[i])
                continue;

            sounding[i] = false;
            gain[i] = target[i] = 0f;

            StopSlot(current.Layers[i].Slot, tracks);
        }
    }

    /// <summary>Transport time of the next <paramref name="grid" /> boundary; a cue can land up to a frame late.</summary>
    private static double NextBoundary(MusicScore current, MusicGrid grid)
    {
        if (grid == MusicGrid.Immediate)
            return transport;

        double step = grid switch
        {
            MusicGrid.Beat => current.SecondsPerBeat,
            MusicGrid.HalfBar => current.SecondsPerBar * 0.5d,
            MusicGrid.Bar => current.SecondsPerBar,
            MusicGrid.TwoBars => current.SecondsPerBar * 2d,
            _ => current.SecondsPerBar * 4d,
        };

        // Solved in wrapped grid time: an MP3 loop isn't a whole number of beats, so raw-transport boundaries drift over repeats.
        double now = GridTime(current);
        double next = Math.Floor(now / step + 1d) * step;

        // The loop point is itself a downbeat, and it is the next one whenever the bar the track is
        // in would otherwise run off the end.
        if (current.LoopSeconds > 0f && next > current.LoopSeconds)
            next = current.LoopSeconds;

        return transport + (next - now);
    }

    /// <summary>Position within the loop, in seconds, measured from beat one.</summary>
    private static double GridTime(MusicScore current)
    {
        double time = transport - current.GridOffset;

        if (current.LoopSeconds <= 0f)
            return time;

        double period = current.LoopSeconds;

        return (time % period + period) % period;
    }

    private static bool Closed()
    {
        foreach (float value in gain)
        {
            if (value > Silence)
                return false;
        }

        return true;
    }

    /// <summary>
    ///     Stops every slot outright at zero; left to vanilla, the keep-alive floor would bring silent stems back at a fifth volume.
    /// </summary>
    private static void Release()
    {
        if (score is { } current && Tracks is { } tracks)
        {
            for (int i = 0; i < current.Layers.Length && i < sounding.Length; i++)
            {
                MusicLayer layer = current.Layers[i];

                // A cue that never fired was never ours to stop.
                if (layer.Kind == MusicLayerKind.Cue && !sounding[i])
                    continue;

                StopSlot(layer.Slot, tracks);
            }
        }

        score = null;
        releasing = false;

        gain = [];
        target = [];
        rate = [];
        sounding = [];

        transport = 0d;
        pendingCue = -1;
        cueEndsAt = -1d;

        clock.Reset();
    }

    private static void StopSlot(int slot, IAudioTrack[] tracks)
    {
        if (slot >= 0 && slot < Main.musicFade.Length)
            Main.musicFade[slot] = 0f;

        if (TrackAt(tracks, slot) is { } track && !track.IsStopped)
            track.Stop(AudioStopOptions.Immediate);
    }

    /// <summary>Null whenever audio is unavailable, which is a normal state and not an error.</summary>
    private static IAudioTrack[]? Tracks => Main.audioSystem is LegacyAudioSystem legacy ? legacy.AudioTracks : null;

    private static IAudioTrack? TrackAt(IAudioTrack[] tracks, int slot)
        => slot >= 0 && slot < tracks.Length ? tracks[slot] : null;
}
