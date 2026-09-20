using System.Collections.Generic;
using System.Linq;
using TrelamiumIII.Core.ExperientialSystem;
using TrelamiumIII.Core.ExperientialSystem.Skills;

namespace TrelamiumIII.Common.Commands;

/// <summary>Buys, refunds and lists skills until the skill panel exists.</summary>
public sealed class SkillCommand : ModCommand
{
    public override string Command => "skill";

    public override CommandType Type => CommandType.Chat;

    public override string Usage => "/skill [buy|refund <id>|respec]";

    public override string Description => "Lists the skill ladder, or buys and refunds a skill by id.";

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        var skills = caller.Player.GetModPlayer<SkillPlayer>();
        var experiential = caller.Player.GetModPlayer<ExperientialPlayer>();

        switch (args.Length == 0 ? "list" : args[0].ToLowerInvariant())
        {
            case "list":
                List(caller, skills, experiential);
                break;

            case "respec":
                caller.Reply($"Refunded {skills.RefundAll()} Exp - {experiential.Exp} / {experiential.Capacity}", Color.LightGreen);
                break;

            case "buy" when args.Length > 1:
                Buy(caller, skills, experiential, args[1]);
                break;

            case "refund" when args.Length > 1:
                Refund(caller, skills, experiential, args[1]);
                break;

            default:
                caller.Reply("Usage: " + Usage, Color.Red);
                break;
        }
    }

    private static void List(CommandCaller caller, SkillPlayer skills, ExperientialPlayer experiential)
    {
        caller.Reply(
            $"Threshold {experiential.Threshold} - {experiential.Exp} / {experiential.Capacity} Exp " +
            $"(skills open at {ExperientialConstants.FirstSkillThreshold})", Color.LightGray);

        foreach (int band in SkillRegistry.Bands)
        {
            bool open = experiential.Threshold >= band;

            caller.Reply($"-- Threshold {band} {(open ? "" : "(locked)")}", open ? Color.Goldenrod : Color.Gray);

            foreach (Skill skill in SkillRegistry.AtThreshold(band))
            {
                SkillPurchaseResult state = skills.CanUnlock(skill);

                caller.Reply(
                    $"   {skill.ID}  {skill.Name} [{skill.Group.DisplayName()}]  {skill.Cost} Exp  - {Describe(state)}",
                    ColorFor(state));
            }
        }
    }

    private static void Buy(CommandCaller caller, SkillPlayer skills, ExperientialPlayer experiential, string query)
    {
        if (Find(caller, query) is not { } skill)
            return;

        SkillPurchaseResult result = skills.TryUnlock(skill);

        caller.Reply(
            result == SkillPurchaseResult.Success
                ? $"Unlocked {skill.Name} for {skill.Cost} Exp - {experiential.Exp} / {experiential.Capacity} left"
                : $"Cannot buy {skill.Name}: {Describe(result)}",
            ColorFor(result));
    }

    private static void Refund(CommandCaller caller, SkillPlayer skills, ExperientialPlayer experiential, string query)
    {
        if (Find(caller, query) is not { } skill)
            return;

        if (!skills.Has(skill))
        {
            caller.Reply($"{skill.Name} is not unlocked.", Color.Red);
            return;
        }

        int returned = skills.Refund(skill);

        caller.Reply($"Refunded {skill.Name} for {returned} Exp - {experiential.Exp} / {experiential.Capacity}", Color.LightGreen);
    }

    /// <summary>Exact id first, then a unique substring, so "/skill buy dash" finds "skill.dash".</summary>
    private static Skill? Find(CommandCaller caller, string query)
    {
        if (SkillRegistry.Get(query) is { } exact)
            return exact;

        List<Skill> matches = SkillRegistry.Skills
            .Where(s => s.ID.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();

        switch (matches.Count)
        {
            case 1:
                return matches[0];

            case 0:
                caller.Reply($"No skill matches '{query}'.", Color.Red);
                return null;

            default:
                caller.Reply($"'{query}' matches {string.Join(", ", matches.Select(s => s.ID))}.", Color.Red);
                return null;
        }
    }

    private static string Describe(SkillPurchaseResult result) => result switch
    {
        SkillPurchaseResult.Success => "available",
        SkillPurchaseResult.AlreadyOwned => "unlocked",
        SkillPurchaseResult.ThresholdTooLow => "Threshold too low",
        SkillPurchaseResult.Unavailable => "unavailable",
        SkillPurchaseResult.CannotAfford => "not enough Exp",
        _ => "unknown skill"
    };

    private static Color ColorFor(SkillPurchaseResult result) => result switch
    {
        SkillPurchaseResult.Success => Color.LightGreen,
        SkillPurchaseResult.AlreadyOwned => Color.SkyBlue,
        _ => Color.Gray
    };
}
