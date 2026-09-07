namespace TrelamiumIII.Content.FarmingOverhaul.SatchelContent;

/// <summary>
/// A player stat a flower can feed. Adding a member here plus one <see cref="FlowerStats"/> entry is
/// the whole cost of making a new stat available to every flower and bouquet in the catalog.
/// </summary>
public enum FlowerStat
{
    MoveSpeed,
    JumpSpeed,
    PickSpeed,
    BuildSpeed,
    Defense,
    Endurance,
    Damage,
    MeleeDamage,
    MagicDamage,
    Crit,
    Knockback,
    Thorns,
    Life,
    LifeRegen,
    Mana,
    ManaRegen,
}

/// <summary>How a stat's accumulated value reads on the satchel panel.</summary>
public enum FlowerStatFormat
{
    Fraction,

    Points,

    Flat,

    Decimal,
}

/// <summary>
/// The one table that says, for each <see cref="FlowerStat"/>, what it is called, how it reads, and
/// how it lands on the <see cref="Player"/>. Profiles in the catalog only ever name a stat and an
/// amount.
/// </summary>
public static class FlowerStats
{
    private readonly record struct Entry(string Noun, FlowerStatFormat Format, Action<Player, float> Apply);

    public static readonly int Count = Enum.GetValues<FlowerStat>().Length;

    private static readonly Entry[] _entries = new Entry[Count];

    static FlowerStats()
    {
        Set(FlowerStat.MoveSpeed, "movement speed", FlowerStatFormat.Fraction,
            (p, v) => p.moveSpeed += v);

        // jumpSpeedBoost is a raw pixels-per-tick addition
        // accessories sit in the low single digits, so it reads as a decimal amount.
        Set(FlowerStat.JumpSpeed, "jump speed", FlowerStatFormat.Decimal,
            (p, v) => p.jumpSpeedBoost += v);

        // pickSpeed is a time multiplier: lower swings faster, so a "+mining speed" bonus subtracts.
        Set(FlowerStat.PickSpeed, "mining speed", FlowerStatFormat.Fraction,
            (p, v) => p.pickSpeed -= v);

        Set(FlowerStat.BuildSpeed, "placement speed", FlowerStatFormat.Fraction,
            (p, v) => { p.tileSpeed += v; p.wallSpeed += v; });

        Set(FlowerStat.Defense, "defense", FlowerStatFormat.Flat,
            (p, v) => p.statDefense += (int)v);

        Set(FlowerStat.Endurance, "damage reduction", FlowerStatFormat.Fraction,
            (p, v) => p.endurance += v);

        Set(FlowerStat.Damage, "damage", FlowerStatFormat.Fraction,
            (p, v) => p.GetDamage(DamageClass.Generic) += v);

        Set(FlowerStat.MeleeDamage, "melee damage", FlowerStatFormat.Fraction,
            (p, v) => p.GetDamage(DamageClass.Melee) += v);

        Set(FlowerStat.MagicDamage, "magic damage", FlowerStatFormat.Fraction,
            (p, v) => p.GetDamage(DamageClass.Magic) += v);

        // Vanilla crit chance is in percentage points
        Set(FlowerStat.Crit, "critical strike chance", FlowerStatFormat.Points,
            (p, v) => p.GetCritChance(DamageClass.Generic) += v);

        Set(FlowerStat.Knockback, "knockback", FlowerStatFormat.Fraction,
            (p, v) => p.GetKnockback(DamageClass.Generic) += v);

        Set(FlowerStat.Thorns, "thorn damage", FlowerStatFormat.Fraction,
            (p, v) => p.thorns += v);

        Set(FlowerStat.Life, "maximum life", FlowerStatFormat.Flat,
            (p, v) => p.statLifeMax2 += (int)v);

        Set(FlowerStat.LifeRegen, "life regeneration", FlowerStatFormat.Flat,
            (p, v) => p.lifeRegen += (int)v);

        Set(FlowerStat.Mana, "maximum mana", FlowerStatFormat.Flat,
            (p, v) => p.statManaMax2 += (int)v);

        Set(FlowerStat.ManaRegen, "mana regeneration", FlowerStatFormat.Flat,
            (p, v) => p.manaRegen += (int)v);
    }

    private static void Set(FlowerStat stat, string noun, FlowerStatFormat format, Action<Player, float> apply)
        => _entries[(int)stat] = new Entry(noun, format, apply);

    public static string Noun(FlowerStat stat) => _entries[(int)stat].Noun;

    public static FlowerStatFormat Format(FlowerStat stat) => _entries[(int)stat].Format;

    public static void Apply(FlowerStat stat, Player player, float value)
        => _entries[(int)stat].Apply(player, value);

    /// <summary>One panel line's worth of text for a value of this stat, e.g. "+3.0% mining speed".</summary>
    public static string Describe(FlowerStat stat, float value)
    {
        var entry = _entries[(int)stat];
        string amount = entry.Format switch
        {
            FlowerStatFormat.Fraction => value.ToString("P1"),
            FlowerStatFormat.Points => $"{value:F1}%",
            FlowerStatFormat.Decimal => value.ToString("0.##"),
            _ => ((int)value).ToString(),
        };
        return $"+{amount} {entry.Noun}";
    }

    /// <summary>
    /// True when a contribution is too small to be worth a line: it would read as "+0", which looks
    /// broken rather than modest. The stat still accumulates; only the display skips it.
    /// </summary>
    public static bool IsNegligible(FlowerStat stat, float value) => _entries[(int)stat].Format switch
    {
        FlowerStatFormat.Fraction => value < 0.00005f,   // rounds to 0.0%
        FlowerStatFormat.Points => value < 0.05f,        // rounds to 0.0%
        FlowerStatFormat.Decimal => value < 0.005f,      // rounds to 0
        _ => (int)value == 0,
    };
}

/// <summary>
/// One frame's accumulated flower bonuses, keyed by <see cref="FlowerStat"/>. Replaces the old
/// hand-maintained field-per-stat trio (declare, reset, apply): a new stat now costs nothing here.
/// </summary>
public sealed class FlowerStatSet
{
    private readonly float[] _values = new float[FlowerStats.Count];

    public float this[FlowerStat stat] => _values[(int)stat];

    public void Clear() => Array.Clear(_values);

    public void Add(FlowerStat stat, float value) => _values[(int)stat] += value;

    /// <summary>Writes every non-zero total onto the player. Called once per equips pass.</summary>
    public void ApplyAll(Player player)
    {
        for (int i = 0; i < _values.Length; i++)
        {
            if (_values[i] != 0f)
                FlowerStats.Apply((FlowerStat)i, player, _values[i]);
        }
    }
}
