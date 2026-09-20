using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria.ModLoader.IO;

namespace TrelamiumIII.Core.ExperientialSystem.Skills;

/// <summary>Why a skill could not be bought, so the UI can say something specific.</summary>
public enum SkillPurchaseResult
{
    Success,
    UnknownSkill,
    AlreadyOwned,
    ThresholdTooLow,
    Unavailable,
    CannotAfford
}

/// <summary>
/// Which skills a character holds, and the buy/refund path between them and the meter.
///
/// <para>
/// Skills are held as ids rather than instances so an unlock survives the mod reloading, and effects are
/// re-applied from scratch every tick in <see cref="ResetEffects"/>. That is what makes respeccing free of
/// bookkeeping: dropping a skill from the set is all that dropping its effect takes.
/// </para>
/// </summary>
public sealed class SkillPlayer : ModPlayer
{
    private readonly HashSet<string> _unlocked = [];

    public override void Initialize() => _unlocked.Clear();

    public IReadOnlyCollection<string> Unlocked => _unlocked;

    public bool Has(string skillId) => _unlocked.Contains(skillId);

    public bool Has(Skill? skill) => skill is not null && Has(skill.ID);

    /// <summary>Whether this player holds a one-off skill, by its class. See <see cref="SkillRegistry.Get{T}"/>.</summary>
    public bool Has<T>() where T : Skill => Has(SkillRegistry.Get<T>());

    public IEnumerable<Skill> UnlockedSkills => _unlocked.Select(SkillRegistry.Get).OfType<Skill>();

    /// <summary>Exp that would come back from refunding everything currently held, before the meter clamps it.</summary>
    public int TotalRefundValue => UnlockedSkills.Sum(s => s.RefundValue);

    #region Buying and refunding

    /// <summary>Whether this skill can be bought right now, without buying it.</summary>
    public SkillPurchaseResult CanUnlock(Skill? skill)
    {
        if (skill is null)
            return SkillPurchaseResult.UnknownSkill;

        if (Has(skill))
            return SkillPurchaseResult.AlreadyOwned;

        var experiential = Player.GetModPlayer<ExperientialPlayer>();

        if (experiential.Threshold < skill.UnlockThreshold)
            return SkillPurchaseResult.ThresholdTooLow;

        if (!skill.IsAvailableTo(Player))
            return SkillPurchaseResult.Unavailable;

        return experiential.CanAfford(skill.Cost) ? SkillPurchaseResult.Success : SkillPurchaseResult.CannotAfford;
    }

    /// <summary>Buys a skill, spending Exp straight out of the live meter.</summary>
    public SkillPurchaseResult TryUnlock(Skill? skill)
    {
        SkillPurchaseResult check = CanUnlock(skill);
        if (check != SkillPurchaseResult.Success || skill is null)
            return check;

        // CanUnlock already proved affordability, but spend through the same guard so the two can never
        // disagree if one of them is later changed.
        if (!Player.GetModPlayer<ExperientialPlayer>().TrySpend(skill.Cost))
            return SkillPurchaseResult.CannotAfford;

        _unlocked.Add(skill.ID);
        skill.OnUnlock(Player);

        return SkillPurchaseResult.Success;
    }

    /// <summary>Gives a skill back for part of its price. Returns the Exp that actually landed in the meter.</summary>
    public int Refund(Skill? skill)
    {
        if (skill is null || !_unlocked.Remove(skill.ID))
            return 0;

        skill.OnRefund(Player);

        return Player.GetModPlayer<ExperientialPlayer>().Refund(skill.RefundValue);
    }

    /// <summary>
    /// Refunds everything. The meter caps what comes back, so a full respec at high Threshold will
    /// usually lose some of the total - worth surfacing in the UI before the player confirms.
    /// </summary>
    public int RefundAll()
    {
        int returned = 0;

        foreach (Skill skill in UnlockedSkills.ToList())
            returned += Refund(skill);

        return returned;
    }

    #endregion

    /// <summary>
    /// Re-applies every held skill from scratch each tick, the same way vanilla rebuilds accessory effects.
    /// Nothing a skill does should outlive the tick unless <see cref="Skill.OnUnlock"/> made it permanent.
    /// </summary>
    public override void ResetEffects()
    {
        foreach (Skill skill in UnlockedSkills)
            skill.Apply(Player);
    }

    #region Multiplayer

    /// <summary>
    /// Held skills are synced because other clients read them: a rework decides how this player's items
    /// look and behave on every machine, not just the owner's.
    /// </summary>
    public override void SyncPlayer(int toWho, int fromWho, bool newPlayer) => SendSkills(toWho, fromWho);

    public override void CopyClientState(ModPlayer targetCopy)
    {
        var copy = (SkillPlayer)targetCopy;

        copy._unlocked.Clear();
        copy._unlocked.UnionWith(_unlocked);
    }

    public override void SendClientChanges(ModPlayer clientPlayer)
    {
        if (!_unlocked.SetEquals(((SkillPlayer)clientPlayer)._unlocked))
            SendSkills(-1, Player.whoAmI);
    }

    private void SendSkills(int toWho, int fromWho)
    {
        ModPacket packet = Mod.GetPacket();

        packet.Write((byte)TrelamiumIII.MessageType.SkillSet);
        packet.Write((byte)Player.whoAmI);
        packet.Write((ushort)_unlocked.Count);

        foreach (string id in _unlocked)
            packet.Write(id);

        packet.Send(toWho, fromWho);
    }

    internal static void Receive(BinaryReader reader, int sender)
    {
        int whoAmI = reader.ReadByte();
        int count = reader.ReadUInt16();

        List<string> ids = new(count);

        for (int i = 0; i < count; i++)
            ids.Add(reader.ReadString());

        if (whoAmI < 0 || whoAmI >= Main.maxPlayers)
            return;

        var skills = Main.player[whoAmI].GetModPlayer<SkillPlayer>();

        skills._unlocked.Clear();
        skills._unlocked.UnionWith(ids.Where(static id => SkillRegistry.Get(id) is not null));

        // The server holds no skill state of its own; it only passes the owner's set on to everyone else.
        if (Main.netMode == NetmodeID.Server)
            skills.SendSkills(-1, sender);
    }

    #endregion

    #region Save data

    public override void SaveData(TagCompound tag) => tag["skills"] = _unlocked.ToList();

    public override void LoadData(TagCompound tag)
    {
        _unlocked.Clear();

        foreach (string id in tag.GetList<string>("skills"))
        {
            // A skill that no longer exists (renamed, or from a removed addon) is dropped rather than kept
            // as a dangling id. The Exp is gone; that is the cost of an id change.
            if (SkillRegistry.Get(id) is not null)
                _unlocked.Add(id);
        }
    }

    #endregion
}
