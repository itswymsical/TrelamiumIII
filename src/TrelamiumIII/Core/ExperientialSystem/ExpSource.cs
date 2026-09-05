using System.Collections.Generic;
using Terraria.ModLoader.IO;

namespace TrelamiumIII.Core.ExperientialSystem;

/// <summary>What kind of thing paid out. Drives the per-Threshold allowance in <see cref="ExperientialConstants.AllowanceFor"/>.</summary>
public enum ExpSourceKind : byte
{
    Unknown,
    Npc,
    Boss,
    Tile,
    Item,
    Fish,
    Act,
    Housing
}

/// <summary>
/// Identifies the thing that paid out, at the granularity the allowance is metered on.
/// <see cref="Id"/> is a content id for the concrete kinds (NPC type, tile type, item type) and a
/// stable string hash for <see cref="ExpSourceKind.Act"/>, whose ids are authored strings.
/// </summary>
public readonly record struct ExpSource(ExpSourceKind Kind, int Id)
{
    public static ExpSource Of(NPC npc) => new(npc.boss ? ExpSourceKind.Boss : ExpSourceKind.Npc, npc.type);
    public static ExpSource Tile(int tileType) => new(ExpSourceKind.Tile, tileType);
    public static ExpSource Item(int itemType) => new(ExpSourceKind.Item, itemType);
    public static ExpSource Fish(int itemType) => new(ExpSourceKind.Fish, itemType);
    public static ExpSource Housing() => new(ExpSourceKind.Housing, 0);

    /// <summary>
    /// Acts are keyed by authored string id. Hashed with FNV-1a rather than <see cref="string.GetHashCode()"/>,
    /// which is randomised per process and would scramble saved allowance between sessions.
    /// </summary>
    public static ExpSource Act(string actId) => new(ExpSourceKind.Act, StableHash(actId));

    /// <summary>Fraction of one Threshold this source is allowed to supply.</summary>
    public float AllowanceFraction => ExperientialConstants.AllowanceFor(Kind);

    internal static int StableHash(string value)
    {
        if (string.IsNullOrEmpty(value))
            return 0;

        unchecked
        {
            const uint offset = 2166136261;
            const uint prime = 16777619;

            uint hash = offset;
            foreach (char c in value)
            {
                hash ^= c;
                hash *= prime;
            }

            return (int)hash;
        }
    }
}

/// <summary>
/// Tracks how much each source has already paid out at the current Threshold, and refuses anything past
/// its share. Wiped on every ascend, so each Threshold is a fresh budget rather than a lifetime one -
/// that keeps the saved state bounded no matter how long a world runs.
/// </summary>
public sealed class SourceAllowanceLedger
{
    private readonly Dictionary<ExpSource, int> _granted = [];

    /// <summary>Exp this source may still pay out at a Threshold whose meter holds <paramref name="capacity"/>.</summary>
    public int Remaining(in ExpSource source, int capacity)
    {
        int allowance = (int)(capacity * source.AllowanceFraction);
        int used = _granted.GetValueOrDefault(source);

        return Math.Max(allowance - used, 0);
    }

    /// <summary>Trims <paramref name="amount"/> to whatever this source has left. Does not record it.</summary>
    public int Clamp(in ExpSource source, int amount, int capacity)
        => Math.Clamp(amount, 0, Remaining(source, capacity));

    /// <summary>Books an award against this source's allowance.</summary>
    public void Record(in ExpSource source, int amount)
    {
        if (amount <= 0)
            return;

        _granted[source] = _granted.GetValueOrDefault(source) + amount;
    }

    /// <summary>Every source gets its allowance back when the player crosses into a new Threshold.</summary>
    public void Reset() => _granted.Clear();

    public void Save(TagCompound tag, string prefix)
    {
        var kinds = new List<byte>(_granted.Count);
        var ids = new List<int>(_granted.Count);
        var amounts = new List<int>(_granted.Count);

        foreach (var (source, amount) in _granted)
        {
            kinds.Add((byte)source.Kind);
            ids.Add(source.Id);
            amounts.Add(amount);
        }

        tag[prefix + "Kinds"] = kinds;
        tag[prefix + "Ids"] = ids;
        tag[prefix + "Amounts"] = amounts;
    }

    public void Load(TagCompound tag, string prefix)
    {
        _granted.Clear();

        var kinds = tag.GetList<byte>(prefix + "Kinds");
        var ids = tag.GetList<int>(prefix + "Ids");
        var amounts = tag.GetList<int>(prefix + "Amounts");

        // A truncated or hand-edited tag should cost the player their allowance record, not crash their save.
        int count = Math.Min(kinds.Count, Math.Min(ids.Count, amounts.Count));

        for (int i = 0; i < count; i++)
            _granted[new ExpSource((ExpSourceKind)kinds[i], ids[i])] = amounts[i];
    }
}
