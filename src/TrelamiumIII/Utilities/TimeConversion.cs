namespace TrelamiumIII.Utilities;

/// <summary>
/// Converts a combination of minutes, seconds, and ticks into a total tick count.
/// Terraria runs at 60 ticks per second.
/// </summary>
public readonly struct TimeConversion
{
    private const int TicksPerSecond = 60;
    private const int TicksPerMinute = TicksPerSecond * 60;

    public int TotalTicks { get; }

    private TimeConversion(int ticks) => TotalTicks = ticks;

    public static TimeConversion SetTime(int mins = 0, int secs = 0, int ticks = 0)
    {
        return new TimeConversion(mins * TicksPerMinute + secs * TicksPerSecond + ticks);
    }

    public int ToSeconds() => TotalTicks / TicksPerSecond;
    public float ToMinutes() => TotalTicks / (float)TicksPerMinute;

    public static implicit operator int(TimeConversion t) => t.TotalTicks;
}