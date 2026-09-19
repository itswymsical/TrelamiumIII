using System.Collections.Generic;
using Terraria.DataStructures;
using Terraria.ModLoader.IO;
using TrelamiumIII.Core.Alchemy.Brewing.Behaviors;

namespace TrelamiumIII.Core.Alchemy.Brewing;

/// <summary>
/// Applies brewed effects: the vanilla buff owns icon and timer, and each effect runs its <see cref="IEffectBehavior"/>. Keyed by buff id.
/// </summary>
public class AlchemyEffectPlayer : ModPlayer
{
    private readonly Dictionary<int, AlchemicalEffect> _active = new();   // keyed by vanilla buff id
    private readonly List<int> _pendingDuration = new();
    private readonly List<int> _expired = new();

    /// <summary>Register a brewed effect on consume: track it and flag its duration for override.</summary>
    public void OnDrink(in AlchemicalEffect fx)
    {
        if (fx.BaseBuff <= 0) return;   // virtual effects are instantaneous; nothing to tick
        _active[fx.BaseBuff] = fx;
        _pendingDuration.Add(fx.BaseBuff);
    }

    /// <summary>Extra percentage points of crate chance from an active brew. Rebuilt each tick.</summary>
    public int CrateChanceBonus;

    public override void ResetEffects() => CrateChanceBonus = 0;

    public override void ModifyFishingAttempt(ref FishingAttempt attempt)
    {
        if (CrateChanceBonus == 0) return;

        // Vanilla rolled attempt.crate at 10 (+15 while cratePotion) out of 100 in
        // Projectile.FishingCheck_RollDropLevels. That roll is discarded and redone at the brewed chance.
        int chance = 10 + (Player.cratePotion ? 15 : 0) + CrateChanceBonus;
        attempt.crate = Main.rand.Next(100) < chance;
    }

    public override void ModifyLuck(ref float luck)
    {
        // luckPotion is vanilla's tier (1-3), each worth LuckBehavior.PerTier. Zero means vanilla added nothing to rescale.
        if (Player.luckPotion <= 0) return;
        if (!TryGetActiveBrew(BuffID.Lucky, out var fx)) return;   // stock potion keeps vanilla's values

        // Swap vanilla's contribution for the brewed one. The tier (and so the decay) is left alone;
        // only its magnitude scales, which keeps duration meaning what it means in vanilla.
        float vanilla = Player.luckPotion * LuckBehavior.PerTier;
        luck -= vanilla;
        luck += vanilla * (fx.StrengthPercent / 100f);
    }

    /// <summary>A plain (unbranded) potion of this buff was drunk, drop any brewed override.</summary>
    public void ClearBrew(int buff) => _active.Remove(buff);

    /// <summary>The active brewed effect riding the given buff, if any (for tooltips/buff text).</summary>
    public bool TryGetActiveBrew(int buff, out AlchemicalEffect fx) => _active.TryGetValue(buff, out fx);

    public override void PostUpdateBuffs()
    {
        // Vanilla applied the buff with the item's stock time this frame; stamp the brewed time.
        if (_pendingDuration.Count > 0)
        {
            foreach (int buff in _pendingDuration)
            {
                if (!_active.TryGetValue(buff, out var fx)) continue;
                int idx = Player.FindBuffIndex(buff);
                if (idx >= 0) Player.buffTime[idx] = fx.DurationTicks;
                else Player.AddBuff(buff, fx.DurationTicks);
            }
            _pendingDuration.Clear();
        }

        // Stats consumed in UpdateEquips must be written here, between buff ticks and equips; PostUpdateEquips is a frame late.
        if (_active.Count == 0) return;
        foreach (var (buff, fx) in _active)
            if (Player.HasBuff(buff))
                EffectBehaviors.Get(buff)?.ApplyPreEquips(Player, fx);
    }

    public override void PostUpdateEquips()
    {
        // Lift the local player's Spelunker gate before re-gating below, so a lapsed brew stops hiding ores.
        bool isLocal = Player.whoAmI == Main.myPlayer;
        if (isLocal) SpelunkerReveal.BeginFrame();

        if (_active.Count == 0) return;

        _expired.Clear();
        foreach (var (buff, fx) in _active)
        {
            if (!Player.HasBuff(buff)) { _expired.Add(buff); continue; }
            EffectBehaviors.Get(buff)?.Apply(Player, fx);
        }
        foreach (int buff in _expired) _active.Remove(buff);
    }

    public override void SaveData(TagCompound tag)
    {
        if (_active.Count == 0) return;
        var list = new List<TagCompound>();
        foreach (var fx in _active.Values)
            list.Add(new TagCompound
            {
                ["buff"] = fx.BaseBuff, ["pot"] = fx.Potency, ["dur"] = fx.DurationTicks,
                ["str"] = fx.StrengthPercent, ["tier"] = fx.Params.MaxOreTier,
            });
        tag["activeEffects"] = list;
    }

    public override void LoadData(TagCompound tag)
    {
        _active.Clear();
        if (!tag.TryGet("activeEffects", out List<TagCompound> list)) return;
        foreach (var t in list)
        {
            var fx = new AlchemicalEffect(t.GetInt("buff"), t.GetInt("pot"), t.GetInt("dur"),
                t.GetInt("str"), new EffectParams(t.GetInt("tier")));
            _active[fx.BaseBuff] = fx;
        }
    }
}
