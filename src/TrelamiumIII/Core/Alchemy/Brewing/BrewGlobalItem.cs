using System.Collections.Generic;
using System.IO;
using Terraria.ModLoader.IO;
using TrelamiumIII.Core.Alchemy.Brewing.Behaviors;
using TrelamiumIII.Core.UI;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Core.Alchemy.Brewing;

/// <summary>Per-instance brew payload; without one (or with the mod disabled) the item behaves exactly like vanilla.</summary>
public class BrewGlobalItem : GlobalItem
{
    public override bool InstancePerEntity => true;

    /// <summary>The brew on this item instance, or null for a plain (unbranded) item.</summary>
    public PotionBrew? Brew;

    public override void OnConsumeItem(Item item, Player player)
    {
        var fx = player.GetModPlayer<AlchemyEffectPlayer>();
        if (Brew is { Effects.Count: > 0 } b)
        {
            // Virtual effects (negative ids) are instantaneous and consumed item-side
            // (GetHealLife/GetHealMana); only buff-riding effects go to the effect player.
            foreach (var effect in b.Effects)
                if (effect.BaseBuff > 0)
                    fx.OnDrink(effect);

            foreach (var modifier in b.Modifiers)
                fx.OnDrink(modifier);

            // Healing potions add the flat restore in GetHealLife/GetHealMana; anything else restores it here.
            if (item.healLife <= 0 && b.HealLife > 0)
                player.Heal(b.HealLife);

            if (item.healMana <= 0 && b.HealMana > 0)
            {
                player.statMana = Math.Min(player.statManaMax2, player.statMana + b.HealMana);
                player.ManaEffect(b.HealMana);
            }
        }
        else if (item.buffType > 0)
            fx.ClearBrew(item.buffType);   // a plain potion supersedes any brewed override
    }

    public override void SetDefaults(Item entity)
    {
        base.SetDefaults(entity);

        if (!entity.IsManagedPotion()) return;

        // Buff potions ride their own buffType and duration. Healing/mana potions have no
        // buffType at all, they're described through the virtual Restoration id instead.
        bool hasBuff = entity.buffType > 0;
        int baseBuff = hasBuff ? entity.buffType : VirtualBuff.Restoration;
        int duration = hasBuff ? entity.buffTime : 0;

        Brew = new PotionBrew
        {
            Principle = PrincipleType.None,
            Effects = new List<AlchemicalEffect> { new AlchemicalEffect(baseBuff, 1, duration, 100) }
        };
    }

    /// <summary>Strength of this brew's <see cref="VirtualBuff.Restoration"/> effect, or 0 when none.</summary>
    private int RestorationStrength()
    {
        if (Brew is { } b)
            foreach (var e in b.Effects)
                if (e.BaseBuff == VirtualBuff.Restoration)
                    return e.StrengthPercent;
        return 0;
    }

    /// <summary><paramref name="baseHeal"/> scaled by the Restoration strength, plus the brew's flat <paramref name="bonus"/>.</summary>
    private int Restored(int baseHeal, int bonus)
    {
        int str = RestorationStrength();
        int heal = str > 0 ? (int)Math.Round(baseHeal * str / 100.0) : baseHeal;
        return heal + bonus;
    }

    public override void GetHealLife(Item item, Player player, bool quickHeal, ref int healValue)
    {
        // Compose politely with other mods: scale whatever the heal currently is, not item.healLife.
        if (Brew is { } b) healValue = Restored(healValue, b.HealLife);
    }

    public override void GetHealMana(Item item, Player player, bool quickHeal, ref int healValue)
    {
        if (Brew is { } b) healValue = Restored(healValue, b.HealMana);
    }

    // A reference-type payload must be deep-copied on clone, or two items would share one effect list.
    public override GlobalItem Clone(Item from, Item to)
    {
        var clone = (BrewGlobalItem)base.Clone(from, to);
        clone.Brew = Brew?.Clone();
        return clone;
    }

    // Brewed potions only merge with identical brews (so a strong and a weak Spelunker never stack).
    public override bool CanStack(Item destination, Item source) => SameBrew(destination, source);
    public override bool CanStackInWorld(Item destination, Item source) => SameBrew(destination, source);

    private static bool SameBrew(Item a, Item b)
    {
        var ba = a.GetGlobalItem<BrewGlobalItem>().Brew;
        var bb = b.GetGlobalItem<BrewGlobalItem>().Brew;
        return ba is null ? bb is null : ba.Equals(bb);
    }

    public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
    {
        if (Brew is not { Effects.Count: > 0 } b) return;
        var primary = b.Primary;

        // Rewrite the stock buff lines to match the brew. Virtual primaries (negative ids) have no vanilla buff to describe.
        string? desc = EffectBehaviors.Describe(primary);
        string? vanillaDesc = primary.BaseBuff > 0 ? Lang.GetBuffDescription(primary.BaseBuff) : null;
        foreach (var line in tooltips)
        {
            if (line.Mod != "Terraria") continue;
            if (line.Name == "BuffTime" && primary.BaseBuff > 0)
                line.Text = BrewText.DurationWords(primary.DurationTicks);
            else if (desc != null && primary.BaseBuff > 0
                     && (line.Name == "Tooltip0" || line.Text == vanillaDesc))
                line.Text = desc;
            else if (line.Name == "HealLife")
                line.Text = $"Restores {Restored(item.healLife, b.HealLife)} life";
            else if (line.Name == "HealMana")
                line.Text = $"Restores {Restored(item.healMana, b.HealMana)} mana";
        }

        var extraColor = new Color(190, 245, 190);

        // Extra brewed effects (augmentation: combine) each get their own line, with duration.
        for (int i = 1; i < b.Effects.Count; i++)
        {
            var e = b.Effects[i];
            string? extra = EffectBehaviors.Describe(e);
            if (extra == null) continue;
            string time = e.BaseBuff > 0 && e.DurationTicks > 0 ? $" ({BrewText.ShortDuration(e.DurationTicks)})" : "";
            tooltips.Add(new TooltipLine(Mod, $"BrewExtra{i}", $"Also: {extra}{time}") { OverrideColor = extraColor });
        }

        for (int i = 0; i < b.Modifiers.Count; i++)
        {
            var m = b.Modifiers[i];
            if (BrewModifiers.Describe(m) is not { } text) continue;
            tooltips.Add(new TooltipLine(Mod, $"BrewModifier{i}", $"Also: {text} ({BrewText.ShortDuration(m.DurationTicks)})")
            { OverrideColor = extraColor });
        }

        if (item.healLife <= 0 && b.HealLife > 0)
            tooltips.Add(new TooltipLine(Mod, "BrewHealLife", $"Also: Restores {b.HealLife} life") { OverrideColor = extraColor });

        if (item.healMana <= 0 && b.HealMana > 0)
            tooltips.Add(new TooltipLine(Mod, "BrewHealMana", $"Also: Restores {b.HealMana} mana") { OverrideColor = extraColor });

        if (b.Stage > 0)
        {
            string diluted = b.Stage > BrewDilution.FREE_STAGES ? ", diluted" : "";
            tooltips.Add(new TooltipLine(Mod, "BrewStage", $"Augmented ×{b.Stage}{diluted}")
            { OverrideColor = BrewCategories.ColorOf(BrewCategory.Augmentation) });
        }

        // Potency stars are drawn over this label in PostDrawTooltipLine.
        tooltips.Add(new TooltipLine(Mod, "BrewPotency", "Potency")
        { OverrideColor = new Color(255, 220, 130) });
    }

    public override void SaveData(Item item, TagCompound tag)
    {
        if (Brew is not { Effects.Count: > 0 } b) return;
        tag["domain"] = (int)b.Principle;
        var list = new List<TagCompound>();
        foreach (var e in b.Effects)
            list.Add(new TagCompound
            {
                ["buff"] = e.BaseBuff, ["pot"] = e.Potency, ["dur"] = e.DurationTicks,
                ["str"] = e.StrengthPercent, ["tier"] = e.Params.MaxOreTier,
            });
        tag["effects"] = list;

        if (b.Modifiers.Count > 0)
        {
            var modifiers = new List<TagCompound>();
            foreach (var m in b.Modifiers)
                modifiers.Add(new TagCompound { ["key"] = m.Key, ["val"] = m.Value, ["dur"] = m.DurationTicks });
            tag["modifiers"] = modifiers;
        }

        if (b.HealLife > 0) tag["healLife"] = b.HealLife;
        if (b.HealMana > 0) tag["healMana"] = b.HealMana;
        if (b.Augments.Count > 0) tag["augments"] = new List<string>(b.Augments);
    }

    public override void LoadData(Item item, TagCompound tag)
    {
        if (tag.TryGet("effects", out List<TagCompound> list) && list.Count > 0)
        {
            var effects = new List<AlchemicalEffect>(list.Count);
            foreach (var t in list)
                effects.Add(new AlchemicalEffect(t.GetInt("buff"), t.GetInt("pot"), t.GetInt("dur"),
                    t.GetInt("str"), new EffectParams(t.GetInt("tier"))));

            // A modifier whose effect no longer loads is dropped rather than kept as a dead key.
            var modifiers = new List<TimedModifier>();
            if (tag.TryGet("modifiers", out List<TagCompound> saved))
                foreach (var t in saved)
                    if (BrewModifiers.Get(t.GetString("key")) is not null)
                        modifiers.Add(new TimedModifier(t.GetString("key"), t.GetFloat("val"), t.GetInt("dur")));

            Brew = new PotionBrew
            {
                Principle = (PrincipleType)tag.GetInt("domain"),
                Effects = effects,
                Modifiers = modifiers,
                HealLife = tag.GetInt("healLife"),
                HealMana = tag.GetInt("healMana"),
                Augments = tag.TryGet("augments", out List<string> augments) ? augments : new List<string>(),
            };
        }
        else if (tag.ContainsKey("buff"))   // legacy single-effect save format
        {
            Brew = new PotionBrew
            {
                Principle = (PrincipleType)tag.GetInt("domain"),
                Effects = { new AlchemicalEffect(tag.GetInt("buff"), tag.GetInt("pot"),
                            tag.GetInt("dur"), tag.GetInt("str")) },
            };
        }
        else Brew = null;
    }

    public override void NetSend(Item item, BinaryWriter writer)
    {
        if (Brew is not { Effects.Count: > 0 } b) { writer.Write((byte)0); return; }
        writer.Write((byte)b.Effects.Count);
        writer.Write((int)b.Principle);
        foreach (var e in b.Effects)
        {
            writer.Write(e.BaseBuff);
            writer.Write(e.Potency);
            writer.Write(e.DurationTicks);
            writer.Write(e.StrengthPercent);
            writer.Write(e.Params.MaxOreTier);
        }

        writer.Write((byte)b.Modifiers.Count);
        foreach (var m in b.Modifiers)
        {
            writer.Write(m.Key);
            writer.Write(m.Value);
            writer.Write(m.DurationTicks);
        }

        writer.Write(b.HealLife);
        writer.Write(b.HealMana);

        writer.Write((byte)b.Augments.Count);
        foreach (string augment in b.Augments)
            writer.Write(augment);
    }

    public override void NetReceive(Item item, BinaryReader reader)
    {
        byte count = reader.ReadByte();
        if (count == 0) { Brew = null; return; }
        var domain = (PrincipleType)reader.ReadInt32();
        var effects = new List<AlchemicalEffect>(count);
        for (int i = 0; i < count; i++)
            effects.Add(new AlchemicalEffect(reader.ReadInt32(), reader.ReadInt32(),
                reader.ReadInt32(), reader.ReadInt32(), new EffectParams(reader.ReadInt32())));

        byte modifierCount = reader.ReadByte();
        var modifiers = new List<TimedModifier>(modifierCount);
        for (int i = 0; i < modifierCount; i++)
            modifiers.Add(new TimedModifier(reader.ReadString(), reader.ReadSingle(), reader.ReadInt32()));

        int healLife = reader.ReadInt32();
        int healMana = reader.ReadInt32();

        byte augmentCount = reader.ReadByte();
        var augments = new List<string>(augmentCount);
        for (int i = 0; i < augmentCount; i++)
            augments.Add(reader.ReadString());

        Brew = new PotionBrew
        {
            Principle = domain, Effects = effects, Modifiers = modifiers,
            HealLife = healLife, HealMana = healMana, Augments = augments,
        };
    }

    // Draw potency as bestiary stars after the "Potency" label (matches the almanac's stars).
    public override void PostDrawTooltipLine(Item item, DrawableTooltipLine line)
    {
        if (Brew is not { Effects.Count: > 0 } b) return;
        if (line.Name != "BrewPotency" || line.Mod != Mod.Name) return;

        PotencyMeter.DrawAfterTooltipLabel(line, b.Primary.Potency);
    }
}

/// <summary>Duration wording shared by brew tooltips and buff text.</summary>
public static class BrewText
{
    public static string DurationWords(int ticks)
    {
        int totalSeconds = ticks / 60;
        int m = totalSeconds / 60, s = totalSeconds % 60;
        return s == 0 ? $"Duration: {m} minutes" : $"Duration: {m} minutes {s} seconds";
    }

    public static string ShortDuration(int ticks)
    {
        int totalSeconds = (ticks + 59) / 60;
        int m = totalSeconds / 60, s = totalSeconds % 60;
        return m == 0 ? $"{s}s" : s == 0 ? $"{m}m" : $"{m}m {s}s";
    }
}
