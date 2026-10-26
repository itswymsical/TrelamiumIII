using System.Collections.Generic;
using Terraria.DataStructures;
using Terraria.ModLoader.IO;
using TrelamiumIII.Core.Alchemy.Brewing.Behaviors;

namespace TrelamiumIII.Core.Alchemy.Brewing;

/// <summary>
/// Applies brewed effects: the vanilla buff owns icon and timer, and each effect runs its <see cref="IEffectBehavior"/>. Keyed by buff id.
/// Timed modifiers keep their own timers and show as one <see cref="SaturationBuff"/>.
/// </summary>
public class AlchemyEffectPlayer : ModPlayer
{
    private readonly Dictionary<int, AlchemicalEffect> _active = new();   // keyed by vanilla buff id
    private readonly List<int> _pendingDuration = new();
    private readonly List<int> _expired = new();

    private readonly Dictionary<string, ActiveModifier> _modifiers = new();
    private readonly List<string> _modifierKeys = new();
    private bool _saturationPending;
    private float _regenCarry;

    /// <summary>A running <see cref="TimedModifier"/>: its value and ticks left.</summary>
    public record struct ActiveModifier(float Value, int TicksLeft);

    /// <summary>Register a brewed effect on consume: track it and flag its duration for override.</summary>
    public void OnDrink(in AlchemicalEffect fx)
    {
        if (fx.BaseBuff <= 0) return;   // virtual effects are instantaneous; nothing to tick
        _active[fx.BaseBuff] = fx;
        _pendingDuration.Add(fx.BaseBuff);
    }

    /// <summary>Start a timed modifier. A weaker one doesn't replace a stronger one; an equal one keeps the longer timer.</summary>
    public void OnDrink(in TimedModifier modifier)
    {
        if (modifier.DurationTicks <= 0 || BrewModifiers.Get(modifier.Key) is null) return;

        if (_modifiers.TryGetValue(modifier.Key, out ActiveModifier current))
        {
            if (current.Value > modifier.Value) return;
            if (current.Value == modifier.Value && current.TicksLeft >= modifier.DurationTicks) return;
        }

        _modifiers[modifier.Key] = new ActiveModifier(modifier.Value, modifier.DurationTicks);
        _saturationPending = true;
    }

    public IReadOnlyDictionary<string, ActiveModifier> ActiveModifiers => _modifiers;

    /// <summary>Extra percentage points of crate chance from an active brew. Rebuilt each tick.</summary>
    public int CrateChanceBonus;

    /// <summary>Percent added to positive life regeneration by <see cref="BrewStats.LifeRegen"/>. Rebuilt each tick.</summary>
    public float LifeRegenPercent;

    public override void ResetEffects()
    {
        CrateChanceBonus = 0;
        LifeRegenPercent = 0f;
    }

    public override void UpdateLifeRegen()
    {
        if (LifeRegenPercent <= 0f || Player.lifeRegen <= 0)
            return;

        // lifeRegen is an int per-tick rate, so the fractional part carries over instead of rounding to nothing.
        _regenCarry += Player.lifeRegen * LifeRegenPercent / 100f;
        int whole = (int)_regenCarry;
        Player.lifeRegen += whole;
        _regenCarry -= whole;
    }

    public override void NaturalLifeRegen(ref float regen)
    {
        if (LifeRegenPercent > 0f)
            regen *= 1f + LifeRegenPercent / 100f;
    }

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

        TickModifiers();

        // Stats consumed in UpdateEquips must be written here, between buff ticks and equips; PostUpdateEquips is a frame late.
        foreach (var (buff, fx) in _active)
            if (Player.HasBuff(buff))
                EffectBehaviors.Get(buff)?.ApplyPreEquips(Player, fx);

        foreach (var (key, modifier) in _modifiers)
            BrewModifiers.Get(key)?.ApplyPreEquips(Player, modifier.Value);
    }

    /// <summary>
    /// Counts modifiers down and keeps <see cref="SaturationBuff"/> on the longest one. The buff going missing outside a fresh
    /// drink means it expired or was cancelled from the buff bar, and either way the modifiers end with it.
    /// </summary>
    private void TickModifiers()
    {
        int saturation = ModContent.BuffType<SaturationBuff>();
        int index = Player.FindBuffIndex(saturation);

        if (_modifiers.Count > 0 && index < 0 && !_saturationPending)
            _modifiers.Clear();

        _modifierKeys.Clear();
        _modifierKeys.AddRange(_modifiers.Keys);
        int longest = 0;

        foreach (string key in _modifierKeys)
        {
            ActiveModifier modifier = _modifiers[key];
            int left = modifier.TicksLeft - 1;
            if (left <= 0) { _modifiers.Remove(key); continue; }

            _modifiers[key] = modifier with { TicksLeft = left };
            longest = Math.Max(longest, left);
        }

        if (longest > 0)
        {
            if (index >= 0) Player.buffTime[index] = longest;
            else Player.AddBuff(saturation, longest);
        }
        else if (index >= 0)
            Player.DelBuff(index);

        _saturationPending = false;
    }

    public override void PostUpdateEquips()
    {
        // Lift the local player's Spelunker gate before re-gating below, so a lapsed brew stops hiding ores.
        bool isLocal = Player.whoAmI == Main.myPlayer;
        if (isLocal) SpelunkerReveal.BeginFrame();

        foreach (var (key, modifier) in _modifiers)
            BrewModifiers.Get(key)?.Apply(Player, modifier.Value);

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
        if (_active.Count > 0)
        {
            var list = new List<TagCompound>();
            foreach (var fx in _active.Values)
                list.Add(new TagCompound
                {
                    ["buff"] = fx.BaseBuff, ["pot"] = fx.Potency, ["dur"] = fx.DurationTicks,
                    ["str"] = fx.StrengthPercent, ["tier"] = fx.Params.MaxOreTier,
                });
            tag["activeEffects"] = list;
        }

        if (_modifiers.Count > 0)
        {
            var list = new List<TagCompound>();
            foreach (var (key, modifier) in _modifiers)
                list.Add(new TagCompound { ["key"] = key, ["val"] = modifier.Value, ["left"] = modifier.TicksLeft });
            tag["activeModifiers"] = list;
        }
    }

    public override void LoadData(TagCompound tag)
    {
        _active.Clear();
        _modifiers.Clear();

        if (tag.TryGet("activeEffects", out List<TagCompound> effects))
        {
            foreach (var t in effects)
            {
                var fx = new AlchemicalEffect(t.GetInt("buff"), t.GetInt("pot"), t.GetInt("dur"),
                    t.GetInt("str"), new EffectParams(t.GetInt("tier")));
                _active[fx.BaseBuff] = fx;
            }
        }

        if (tag.TryGet("activeModifiers", out List<TagCompound> modifiers))
        {
            // The saved Saturation buff may be gone (buffNoSave, or another mod), so re-add it on the first tick.
            foreach (var t in modifiers)
                if (BrewModifiers.Get(t.GetString("key")) is not null)
                    _modifiers[t.GetString("key")] = new ActiveModifier(t.GetFloat("val"), t.GetInt("left"));
            _saturationPending = _modifiers.Count > 0;
        }
    }
}
