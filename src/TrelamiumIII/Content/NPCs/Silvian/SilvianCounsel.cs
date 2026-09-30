using System.Collections.Generic;
using System.Linq;
using Terraria.GameContent;
using Terraria.Localization;
using TrelamiumIII.Content.Items.Alchemy;
using TrelamiumIII.Core.Alchemy;

namespace TrelamiumIII.Content.NPCs.Silvian;

/// <summary>
///     What Silvian says in his chat window, and when the player presses Help: about the item they hold up, or, holding
///     nothing he can speak to, the next of his general lessons.
/// </summary>
/// <remarks>
///     <para>
///         Every sentence is read from <c>SilvianCounsel</c> in the localization. The facts slotted into them come from
///         the almanac's own data: an item's role, principles and temperaments, its flavour text under
///         <c>Almanac.FlavorText</c>, and the recipes that make it or use it.
///     </para>
///     <para>
///         A brew the world has not discovered yet gets hints rather than its recipe. Each ask gives one more, up to
///         <see cref="MaxHintTier" />, which never names every ingredient.
///     </para>
/// </remarks>
internal static class SilvianCounsel
{
    private const string Root = "Mods.TrelamiumIII.SilvianCounsel";

    /// <summary>The vanilla chat box wraps at this width and shows at most this many lines.</summary>
    private const int CHAT_WIDTH = 460;

    private const int CHAT_LINES = 10;

    /// <summary>Longest list of brews named outright; the rest are counted.</summary>
    private const int MAX_LISTED = 3;

    /// <summary>The seven principles in almanac order, coloured as the almanac badges them.</summary>
    private static readonly (PrincipleType Flag, Color Color)[] Principles =
    [
        (PrincipleType.Vital, new Color(255, 215, 90)),
        (PrincipleType.Specular, new Color(180, 210, 255)),
        (PrincipleType.Martial, new Color(230, 95, 80)),
        (PrincipleType.Brisk, new Color(110, 220, 210)),
        (PrincipleType.Fortune, new Color(150, 210, 120)),
        (PrincipleType.Harmony, new Color(235, 150, 190)),
        (PrincipleType.Bane, new Color(175, 125, 215)),
    ];

    public static string HelpButton => Text("HelpButton");

    /// <summary>A line of banter for his chat window.</summary>
    public static string Chat(Player player)
    {
        int count = CountEntries("Chat");

        return count == 0 ? string.Empty : Format($"Chat.{Main.rand.Next(count)}", player.name);
    }

    /// <summary>Whether Silvian has anything to say about <paramref name="item" />.</summary>
    public static bool CanSpeakTo(Item? item) =>
        item is { IsAir: false }
        && (item.type == ModContent.ItemType<AlchemistsAlmanacItem>()
            || ChemicalRoles.Of(item.type) != ChemicalRole.None
            || AlmanacEntryRegistry.Has(item.type));

    /// <summary>
    ///     What pressing Help gets. An item on the cursor is always the subject, since picking it up was deliberate. Failing
    ///     that the held item is, if he can speak to it; otherwise he gives his next lesson.
    /// </summary>
    /// <param name="subject">The item talked about, or 0 for a lesson.</param>
    public static string Answer(Player player, Item cursor, Item held, out int subject)
    {
        Item? item = !cursor.IsAir ? cursor : CanSpeakTo(held) ? held : null;

        subject = item?.type ?? 0;

        if (item is null)
            return NextLesson(player.GetModPlayer<SilvianPlayer>());

        if (!CanSpeakTo(item))
            return Format("NotAlchemical", Tag(item.type));

        if (item.type == ModContent.ItemType<AlchemistsAlmanacItem>())
            return Text("AboutAlmanac");

        return About(player, item.type);
    }

    private static string NextLesson(SilvianPlayer silvian)
    {
        int count = CountEntries("Lessons");

        if (count == 0)
            return string.Empty;

        int index = silvian.NextLesson % count;
        silvian.NextLesson = index + 1;

        return Text($"Lessons.{index}");
    }

    #region about an item

    /// <summary>A part of an answer, and how long it survives trimming: the lowest goes first.</summary>
    private readonly record struct Part(string Text, int Keep);

    private static string About(Player player, int type)
    {
        var parts = new List<Part> { new(Format($"Role.{RoleKey(type)}", Tag(type)), 100) };

        if (Nature(type) is { Length: > 0 } nature)
            parts.Add(new Part(nature, 20));

        string flavour = AlmanacFlavor.Get(AlmanacEntryRegistry.For(type).ResolvedDescriptionKey);

        if (flavour.Length > 0)
            parts.Add(new Part(Format("Quote", flavour), 60));

        if (Making(player, type) is { Length: > 0 } making)
            parts.Add(new Part(making, 80));

        if (Uses(type) is { Length: > 0 } uses)
            parts.Add(new Part(uses, 40));

        return Fit(parts);
    }

    private static string RoleKey(int type) => ChemicalRoles.Of(type) switch
    {
        ChemicalRole.Solvent => "Solvent",
        ChemicalRole.Solute => ChemicalRoles.IsReagent(type) ? "Reagent" : "Solute",
        ChemicalRole.Solution => "Solution",
        _ => "Other",
    };

    /// <summary>What it leans toward and how it behaves, or empty when the almanac says neither.</summary>
    private static string Nature(int type)
    {
        List<string> principles = PrincipleNames(BrewCompositions.PrincipleOf(type));
        List<string> temperaments = [];

        if (AlmanacEntryRegistry.TryGet(type, out AlmanacEntry? entry))
        {
            foreach (Temperament flag in ChemicalGlyphs.Flags(entry.Temperaments))
                temperaments.Add(Label(flag));

            foreach (ChemicalTrait flag in ChemicalGlyphs.Flags(entry.Traits))
                temperaments.Add(Label(flag));
        }

        string? key = (principles.Count > 0, temperaments.Count > 0) switch
        {
            (true, true) => "Nature.Both",
            (true, false) => "Nature.Principle",
            (false, true) => "Nature.Temperament",
            _ => null,
        };

        return key is null ? string.Empty : Format(key, JoinAnd(principles), JoinAnd(temperaments));
    }

    /// <summary>How it is made: the recipe once brewed, hints before that. Empty when nothing makes it and it isn't a brew.</summary>
    private static string Making(Player player, int type)
    {
        if (!AlchemyRecipes.Produces(type))
            return ChemicalRoles.Of(type) == ChemicalRole.Solution ? Text("NoRecipe") : string.Empty;

        AlchemyRecipe? recipe = Simplest(type);

        if (recipe is null)
            return Text("NoRecipe");

        if (AlmanacDiscoverySystem.IsUnlocked(type))
            return Format("Known", Tag(recipe.InputType), JoinAnd(recipe.Components.Select(Ingredient).ToList()));

        return Hints(player.GetModPlayer<SilvianPlayer>(), type, recipe);
    }

    /// <summary>
    ///     Every hint given so far for this brew, plus the next. The first names the solvent and how many things go in, the
    ///     second says what each leans toward, and the third names one of them. A single-ingredient brew stops at the
    ///     second, since the third would give it away.
    /// </summary>
    private static string Hints(SilvianPlayer silvian, int type, AlchemyRecipe recipe)
    {
        int max = MaxHintTier(recipe);
        int tier = silvian.HintTier(type);
        bool exhausted = tier >= max;

        if (!exhausted)
            silvian.SetHintTier(type, ++tier);

        var lines = new List<string>
        {
            Text("Unbrewed"),
            Format("Hint1", Tag(recipe.InputType), recipe.Components.Length),
        };

        if (tier >= 2)
            lines.Add(Format("Hint2", JoinAnd(recipe.Components.Select(Describe).ToList())));

        if (tier >= 3)
            lines.Add(Format("Hint3", Ingredient(Named(recipe))));

        if (exhausted)
            lines.Add(Text("HintsDone"));
        else if (tier < max)
            lines.Add(Text("HintMore"));

        return string.Join(" ", lines);
    }

    private static int MaxHintTier(AlchemyRecipe recipe) => recipe.Components.Length switch
    {
        0 => 1,
        1 => 2,
        _ => 3,
    };

    /// <summary>
    ///     The recipe he talks about: the plainest base, then the fewest and weakest ingredients. The same one every time,
    ///     so the hints build on each other.
    /// </summary>
    private static AlchemyRecipe? Simplest(int outputType) =>
        AlmanacIndex.RecipesProducing(outputType)
            .OrderBy(r => r.InputType == ItemID.BottledWater ? 0 : r.InputType == ItemID.Bottle ? 1 : 2)
            .ThenBy(r => r.InputInfusion == Temperament.None ? 0 : 1)
            .ThenBy(r => r.Components.Length)
            .ThenBy(r => r.TotalMinPotency)
            .FirstOrDefault();

    /// <summary>The ingredient the third hint names: the first exact one, else the first group.</summary>
    private static ComponentMatcher Named(AlchemyRecipe recipe)
    {
        foreach (ComponentMatcher matcher in recipe.Components)
        {
            if (matcher.Group is null)
                return matcher;
        }

        return recipe.Components[0];
    }

    /// <summary>What it goes into: the brews already found by name, the rest by count.</summary>
    private static string Uses(int type)
    {
        if (!AlchemyIngredients.IsInputType(type) && !AlchemyIngredients.IsSubstanceType(type))
            return string.Empty;

        List<int> outputs = AlmanacIndex.RecipesUsing(type)
            .Select(AlmanacIndex.ResolvedOutputType)
            .Where(output => output > 0 && output != type)
            .Distinct()
            .ToList();

        List<int> known = outputs.Where(AlmanacDiscoverySystem.IsUnlocked).ToList();
        int unknown = outputs.Count - known.Count;

        if (known.Count == 0)
            return unknown > 0 ? Format("UsesUnknown", string.Empty, unknown) : string.Empty;

        List<string> named = known.Take(MAX_LISTED).Select(Tag).ToList();

        if (known.Count > MAX_LISTED)
            named.Add(Format("AndMore", known.Count - MAX_LISTED));

        string uses = Format("UsesKnown", JoinAnd(named), unknown);

        return unknown > 0 ? uses + " " + Format("UsesMore", string.Empty, unknown) : uses;
    }

    /// <summary>Drops the least important parts until the answer fits the chat box.</summary>
    private static string Fit(List<Part> parts)
    {
        while (true)
        {
            string text = string.Join("\n", parts.Select(part => part.Text));

            if (parts.Count <= 1 || LineCount(text) <= CHAT_LINES)
                return text;

            parts.Remove(parts.MinBy(part => part.Keep));
        }
    }

    private static int LineCount(string text) =>
        Utils.WordwrapStringSmart(text, Color.White, FontAssets.MouseText.Value, CHAT_WIDTH, -1).Count;

    #endregion

    #region naming things

    /// <summary>The item's icon and name, as chat tags.</summary>
    private static string Tag(int type) => $"[i:{type}] {Lang.GetItemNameValue(type)}";

    /// <summary>One ingredient by name: a group by one of its members, with the stars it needs past the first.</summary>
    private static string Ingredient(ComponentMatcher matcher)
    {
        string name = matcher.Group is null
            ? Tag(matcher.ExactType)
            : Format("OrTheLike", Tag(BrewCompositions.FirstMember(matcher)));

        return matcher.MinPotency > 1 ? Format("AtPotency", name, matcher.MinPotency) : name;
    }

    /// <summary>One ingredient without naming it: what it leans toward, else how it behaves.</summary>
    private static string Describe(ComponentMatcher matcher)
    {
        int type = BrewCompositions.FirstMember(matcher);

        if (PrincipleNames(BrewCompositions.PrincipleOf(type)) is [var principle, ..])
            return Format("Something", principle);

        if (AlmanacEntryRegistry.TryGet(type, out AlmanacEntry? entry) && ChemicalGlyphs.First(entry.Temperaments) is not Temperament.None and var temperament)
            return Format("Something", Label(temperament));

        return ChemicalRoles.Of(type) is ChemicalRole.Solvent or ChemicalRole.Solution
            ? Text("SomethingLiquid")
            : Text("SomethingPlain");
    }

    private static List<string> PrincipleNames(PrincipleType set)
    {
        List<string> names = [];

        foreach ((PrincipleType flag, Color color) in Principles)
        {
            if ((set & flag) != 0)
                names.Add(Coloured(Text($"Principles.{flag}"), color));
        }

        return names;
    }

    private static string Label(Temperament flag)
    {
        ChemicalTypeInfo.TryGet(flag, out string label, out Color color);

        return Coloured(label, color);
    }

    private static string Label(ChemicalTrait flag)
    {
        ChemicalTypeInfo.TryGet(flag, out string label, out Color color);

        return Coloured(label, color);
    }

    private static string Coloured(string text, Color color) => $"[c/{color.Hex3()}:{text}]";

    /// <summary>"a", "a and b", "a, b and c", in the localization's own words.</summary>
    private static string JoinAnd(IReadOnlyList<string> items) => items.Count switch
    {
        0 => string.Empty,
        1 => items[0],
        2 => Format("And", items[0], items[1]),
        _ => Format("Comma", items[0], JoinAnd(items.Skip(1).ToList())),
    };

    #endregion

    #region localization

    private static string Key(string suffix) => $"{Root}.{suffix}";

    private static string Text(string suffix) => Language.GetTextValue(Key(suffix));

    /// <summary>Formatted with plural support, so entries can say <c>{^1:brew;brews}</c>.</summary>
    private static string Format(string suffix, params object[] args) => Language.GetText(Key(suffix)).Format(args);

    /// <summary>How many numbered entries <c>{group}.0</c>, <c>{group}.1</c>, ... exist.</summary>
    private static int CountEntries(string group)
    {
        int count = 0;

        while (Language.Exists(Key($"{group}.{count}")))
            count++;

        return count;
    }

    #endregion
}
