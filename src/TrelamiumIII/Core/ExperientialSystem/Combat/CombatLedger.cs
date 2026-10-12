using System.Collections.Generic;
using Terraria.DataStructures;
using Terraria.ModLoader.IO;

namespace TrelamiumIII.Core.ExperientialSystem.Combat;

/// <summary>
/// The players's history with each NPC type: how many they have killed, and how many times
/// that type has killed them. Two exponential curves come out of it.
///
/// <para>
/// <b>Familiarity</b> decays with kills. The first kill of a type is worth the most, and each subsequent kill is worth less.
/// </para>
///
/// <para>
/// <b>Grudge</b> grows with deaths, but saturates. The first death to a type is worth the least, and each subsequent death is worth more, but never more than a fixed maximum.
/// </para>
///
/// <para>This is save-file data, so it lives on the owning client and is never computed server-side.</para>
/// </summary>
public sealed class CombatLedger : ModPlayer
{
    private readonly Dictionary<int, int> _kills = [];
    private readonly Dictionary<int, int> _deaths = [];

    public override void Initialize()
    {
        _kills.Clear();
        _deaths.Clear();
    }

    #region Queries

    public int KillsOf(int npcType) => _kills.GetValueOrDefault(npcType);

    public int DeathsTo(int npcType) => _deaths.GetValueOrDefault(npcType);

    /// <summary>True the first time this character meets a type, before any kill has been booked.</summary>
    public bool IsFirstEncounter(int npcType) => !_kills.ContainsKey(npcType);

    /// <summary>
    /// Award multiplier from repetition: 1 at the first kill, decaying exponentially toward
    /// <see cref="ExperientialConstants.FamiliarityFloor"/> and never reaching zero.
    /// </summary>
    public float Familiarity(int npcType)
    {
        float decay = MathF.Exp(-KillsOf(npcType) / ExperientialConstants.FamiliarityScale);

        return ExperientialConstants.FamiliarityFloor
             + (1f - ExperientialConstants.FamiliarityFloor) * decay;
    }

    /// <summary>
    /// Award multiplier from unfinished business: 1 with no deaths, approaching
    /// 1 + <see cref="ExperientialConstants.GrudgeMax"/> as the deaths mount.
    /// </summary>
    public float Grudge(int npcType)
    {
        float saturation = 1f - MathF.Exp(-DeathsTo(npcType) / ExperientialConstants.GrudgeScale);

        return 1f + ExperientialConstants.GrudgeMax * saturation;
    }

    /// <summary>
    /// The full history multiplier for one kill, read <i>before</i> that kill is booked so the kill being
    /// paid for is not already discounting itself.
    /// </summary>
    public float RewardMultiplier(int npcType)
    {
        float multiplier = Familiarity(npcType) * Grudge(npcType);

        if (IsFirstEncounter(npcType))
            multiplier *= ExperientialConstants.FirstEncounterBonus;

        return multiplier;
    }

    #endregion

    #region Recording

    /// <summary>Books a kill and spends part of any grudge against that type. Call after reading the multiplier.</summary>
    public void RegisterKill(int npcType)
    {
        _kills[npcType] = KillsOf(npcType) + 1;

        int remaining = DeathsTo(npcType) - ExperientialConstants.GrudgeSpentPerKill;

        if (remaining > 0)
            _deaths[npcType] = remaining;
        else
            _deaths.Remove(npcType);
    }

    public void RegisterDeath(int npcType) => _deaths[npcType] = DeathsTo(npcType) + 1;

    /// <summary>
    /// Books the death against whatever NPC landed it. Only the owning client records - on a server this
    /// also runs for remote players, whose ledgers live in their own save files.
    /// </summary>
    public override void Kill(double damage, int hitDirection, bool pvp, PlayerDeathReason damageSource)
    {
        // Death is processed on the server too, where Main.myPlayer is 255 and no save file is in play.
        if (pvp || Main.netMode == NetmodeID.Server || Player.whoAmI != Main.myPlayer)
            return;

        int index = damageSource.SourceNPCIndex;
        if (index < 0 || index >= Main.maxNPCs)
            return;

        NPC killer = Main.npc[index];
        if (killer.active)
            RegisterDeath(killer.type);
    }

    #endregion

    #region Save data

    public override void SaveData(TagCompound tag)
    {
        Write(tag, "ledgerKill", _kills);
        Write(tag, "ledgerDeath", _deaths);
    }

    public override void LoadData(TagCompound tag)
    {
        Read(tag, "ledgerKill", _kills);
        Read(tag, "ledgerDeath", _deaths);
    }

    /// <summary>TagCompound has no dictionary form, so both tallies go out as a pair of parallel lists.</summary>
    private static void Write(TagCompound tag, string prefix, Dictionary<int, int> source)
    {
        var types = new List<int>(source.Count);
        var counts = new List<int>(source.Count);

        foreach (var (type, count) in source)
        {
            types.Add(type);
            counts.Add(count);
        }

        tag[prefix + "Types"] = types;
        tag[prefix + "Counts"] = counts;
    }

    private static void Read(TagCompound tag, string prefix, Dictionary<int, int> target)
    {
        target.Clear();

        var types = tag.GetList<int>(prefix + "Types");
        var counts = tag.GetList<int>(prefix + "Counts");

        // Mismatched lengths mean a truncated or hand-edited save; drop the tail rather than throwing.
        int pairs = Math.Min(types.Count, counts.Count);

        for (int i = 0; i < pairs; i++)
            target[types[i]] = counts[i];
    }

    #endregion
}