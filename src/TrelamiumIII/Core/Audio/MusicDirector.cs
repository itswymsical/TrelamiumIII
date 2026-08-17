using System.Diagnostics;
using Microsoft.Xna.Framework.Audio;
using Terraria.Audio;

namespace TrelamiumIII.Core.Audio;

/// <summary>
///     Runs a <see cref="MusicScore" />: several tracks playing at once, locked together, with the
///     faders and the bar count in our hands rather than vanilla's.
/// </summary>
/// <remarks>
///     <para>
///         <b>Why this exists.</b> Vanilla music is one track at a time. Switching between two
///         arrangements of the same piece goes through <c>Main.newMusic</c>, which means the outgoing
///         track fades out over a couple of seconds while the incoming one fades in <em>from its own
///         beginning</em>. You hear a crossfade between two different points in the music. There is no
///         setting for this; the only fix is to not use the mechanism.
///     </para>
///     <para>
///         <b>How it works.</b> Every stem in a score is started on the same frame and left running for
///         as long as the score lasts, silent ones included. Moving between arrangements is then a
///         fader move between two tracks that are already at the same place in the music, and it is
///         seamless because nothing transitioned -- the audio was always there.
///     </para>
///     <para>
///         <b>How it coexists with vanilla.</b> Two hooks, both after the fact.
///         <see cref="Main.UpdateAudio_DecideOnNewMusic" /> is allowed to run and its answer is then
///         overwritten with the score's primary stem, so vanilla believes the score is the current
///         track and fades everything else out on its own. <see cref="Main.UpdateAudio" /> is allowed
///         to run and the volumes it just wrote are then overwritten with ours.
///         <see cref="Main.musicFade" /> is held above zero for every managed slot for one reason
///         only: vanilla stops any track whose fade reaches it, and a stopped stem loses its place.
///     </para>
///     <para>
///         <b>Faders are linear, deliberately.</b> Equal-power is for crossfading unrelated material.
///         Both things a score does -- swapping between arrangements of one performance, and adding a
///         stem to a mix it was bounced against -- involve signals that are either correlated or meant
///         to sum, and an equal-power curve would make both bulge in the middle.
///     </para>
/// </remarks>
internal sealed class MusicDirector : ModSystem
{
    /// <summary>
    ///     Floor held under every managed slot's <see cref="Main.musicFade" />.
    /// </summary>
    /// <remarks>
    ///     Vanilla walks a non-current track's fade down by a small step each frame and stops the
    ///     track when it reaches zero. Anything comfortably above one step survives indefinitely,
    ///     since this re-stamps it every frame. It is not a volume -- the real gain is written
    ///     straight to the track afterwards -- it is a "do not stop me" token.
    /// </remarks>
    private const float KeepAlive = 0.2f;

    /// <summary>Below this a fader counts as closed.</summary>
    private const float Silence = 0.0005f;

    /// <summary>
    ///     Longest frame the transport will believe in.
    /// </summary>
    /// <remarks>
    ///     A load hitch would otherwise jump the bar count forward by however long the game was busy,
    ///     while the audio, which does not care that the game stalled, has only advanced by its own
    ///     buffer. Capping keeps a stall from throwing the grid somewhere the music never went.
    /// </remarks>
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

    /// <summary>
    ///     Starts <paramref name="next" />, replacing whatever the director was running.
    /// </summary>
    /// <remarks>
    ///     Always from the top. Adopting a track vanilla already had going would mean a transport with
    ///     no known origin and stems that cannot be joined to it, since none of them can be seeked to
    ///     meet it -- so a score owns its own playback from the first sample or it does not work at all.
    /// </remarks>
    /// <param name="fadeIn">Seconds for the opening stems to reach their gain. Zero starts them up.</param>
    public static void Play(MusicScore next, float fadeIn = 0f)
    {
        if (Main.dedServ)
            return;

        Release();

        // Every track in the score goes back to its first sample, including one vanilla already had
        // running through ModNPC.Music or a scene effect. The transport is measured from this frame,
        // and a stem adopted mid-phrase would put the bar count somewhere the music is not -- which
        // is the one error this whole system exists to avoid. They are restarted rather than merely
        // rewound: DriveLayer starts anything stopped, so they all begin on the same frame.
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

    /// <summary>
    ///     Closes every fader and hands the slots back to vanilla once they are shut.
    /// </summary>
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

    /// <summary>
    ///     Schedules a cue to start on the next <paramref name="grid" /> boundary.
    /// </summary>
    /// <param name="cut">
    ///     Whether the stems are cut the instant the cue lands. True for material that continues the
    ///     track rather than sitting on top of it, which is what a death fill is.
    /// </param>
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

    /// <summary>
    ///     Lets vanilla pick, then tells it the answer is ours.
    /// </summary>
    /// <remarks>
    ///     Overriding the result rather than skipping the call: the method sets more than
    ///     <see cref="Main.newMusic" />, and a score should suppress vanilla's choice of track without
    ///     also suppressing its bookkeeping.
    /// </remarks>
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

            // Re-asserted here as well as in the decide hook. That hook is the one that matters --
            // it lands before vanilla acts on the answer -- but it is reached through a call chain
            // this makes no assumptions about, and a claim that arrives a frame late still beats a
            // claim that never arrives.
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
    ///     Seconds of music since the last frame.
    /// </summary>
    /// <remarks>
    ///     Measured off the wall clock rather than counted in frames, because the audio is playing in
    ///     real time whatever the frame rate is doing, and gated on the primary stem actually
    ///     sounding so a paused game does not advance the bar count past the music.
    /// </remarks>
    private static double Elapsed(IAudioTrack[] tracks, MusicScore current)
    {
        double delta = clock.Elapsed.TotalSeconds;

        clock.Restart();

        if (TrackAt(tracks, current.Primary.Slot) is not { IsPlaying: true })
            return 0d;

        return Math.Min(delta, MaxFrame);
    }

    /// <summary>
    ///     Walks each fader toward its target.
    /// </summary>
    /// <remarks>
    ///     Driven by the same gated delta as the transport, so a crossfade freezes along with the
    ///     music when the game is paused rather than completing behind a silent window.
    /// </remarks>
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

        // A cue that cut the stems on its way in has just played the last of the music. The score is
        // over rather than merely quiet, and sitting there holding silent slots would keep vanilla
        // locked out of the soundtrack indefinitely.
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

    /// <summary>
    ///     Transport time of the next <paramref name="grid" /> boundary.
    /// </summary>
    /// <remarks>
    ///     Resolution is one frame: a cue lands on the first frame at or after the boundary, so it can
    ///     be up to a frame late. There is no finer control available -- a track starts when
    ///     <c>Play</c> is called and the game only gets to call it once a frame.
    /// </remarks>
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

        // Solved in wrapped grid time, not against the raw transport. The two only agree for the
        // first pass through the loop: a file whose decoded length is not a whole number of beats --
        // which is every MP3 -- puts its real downbeat a little later on each repeat, and a boundary
        // measured from the start of playback would be a beat out after a few minutes.
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
    ///     Gives every slot back, stopped and at zero.
    /// </summary>
    /// <remarks>
    ///     The tracks are stopped outright rather than left to vanilla's own fade. The keep-alive
    ///     floor is a real number as far as vanilla is concerned, so releasing a silent stem without
    ///     stopping it would have the next frame's <c>UpdateAudio</c> read that floor back and bring
    ///     the track up at a fifth volume.
    /// </remarks>
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
