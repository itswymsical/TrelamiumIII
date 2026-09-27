using System.Collections.Generic;
using System.Text;
using ReLogic.Content;
using Terraria.GameContent;
using Terraria.Localization;
using TrelamiumIII.Core.Text.Tooltips;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Core.Alchemy;

/// <summary>Station slot an item belongs to.</summary>
public enum ChemicalRole
{
    None,

    /// <summary>Solute slot. With a principle of None the item is a reagent.</summary>
    Solute,

    /// <summary>Solvent slot.</summary>
    Solvent,

    /// <summary>Terminal output. Accepted in the solvent slot as an augmentation target.</summary>
    Solution,
}

/// <summary>Which items are solutes, solvents and solutions, and the temperaments and traits each carries.</summary>
public static class ChemicalRoles
{
    private const string ICON_DIRECTORY = $"{nameof(TrelamiumIII)}/Assets/Textures/";

    private static readonly Dictionary<int, ChemicalRole> authored = [];
    private static readonly Dictionary<int, ChemicalRole> derived = [];

    /// <summary>Tags an item type by hand. Wins over anything the recipes imply.</summary>
    public static void Register(int itemType, ChemicalRole role)
    {
        if (itemType > 0)
            authored[itemType] = role;
    }

    public static void Clear()
    {
        authored.Clear();
        derived.Clear();
    }

    public static ChemicalRole Of(Item? item) => item is { IsAir: false } ? Of(item.type) : ChemicalRole.None;

    /// <summary>
    ///     Authored role, else inferred from recipe usage in this order: a potion used or produced by a recipe is a
    ///     solution, then component use (solute), input use (solvent), and production (solution).
    /// </summary>
    public static ChemicalRole Of(int itemType)
    {
        if (itemType <= 0)
            return ChemicalRole.None;

        if (authored.TryGetValue(itemType, out ChemicalRole role) || derived.TryGetValue(itemType, out role))
            return role;

        // Before the recipes exist every answer would be "none", and AlchemyIngredients would cache
        // that emptiness too. Leave both caches alone until there is something to read.
        if (AlchemyRecipes.All.Count == 0)
            return ChemicalRole.None;

        bool input = AlchemyIngredients.IsInputType(itemType);
        bool produced = AlchemyRecipes.Produces(itemType);

        role = (input || produced) && IsPotion(itemType) ? ChemicalRole.Solution
            : AlchemyIngredients.IsSubstanceType(itemType) ? ChemicalRole.Solute
            : input ? ChemicalRole.Solvent
            : produced ? ChemicalRole.Solution
            : ChemicalRole.None;

        derived[itemType] = role;

        return role;
    }

    public static bool IsReagent(int itemType) =>
        Of(itemType) == ChemicalRole.Solute && BrewCompositions.PrincipleOf(itemType) == PrincipleType.None;

    /// <summary>The instance's infusion, else the almanac's temperaments for the type. May hold several flags.</summary>
    public static Temperament TemperamentsOf(Item item)
    {
        Temperament infusion = SolutionGlobalItem.GetInfusion(item);

        if (infusion != Temperament.None)
            return infusion;

        // For() builds a default entry for an unauthored type; asking first keeps that off the draw path.
        return AlmanacEntryRegistry.Has(item.type)
            ? AlmanacEntryRegistry.For(item.type).Temperaments
            : Temperament.None;
    }

    /// <summary>The almanac's traits for the item's type. May hold several flags.</summary>
    public static ChemicalTrait TraitsOf(Item item)
        => AlmanacEntryRegistry.Has(item.type)
            ? AlmanacEntryRegistry.For(item.type).Traits
            : ChemicalTrait.None;

    /// <summary>Localised under <c>ChemicalRoles.{member}</c>; the member name when no translation exists.</summary>
    public static string DisplayName(ChemicalRole role) => Localize(role.ToString());

    /// <summary>As <see cref="DisplayName(ChemicalRole)" />, but names a reagent <c>ChemicalRoles.Reagent</c>.</summary>
    public static string DisplayName(int itemType) =>
        IsReagent(itemType) ? Localize("Reagent") : DisplayName(Of(itemType));

    /// <summary>The role's tooltip icon (<c>Assets/Textures/{Role}TooltipIcon</c>), or null while it has none.</summary>
    public static Asset<Texture2D>? Icon(ChemicalRole role)
    {
        if (role == ChemicalRole.None)
            return null;

        string path = $"{ICON_DIRECTORY}{role}TooltipIcon";

        return ModContent.HasAsset(path) ? ModContent.Request<Texture2D>(path) : null;
    }

    private static string Localize(string member)
    {
        string key = TrelamiumIII.Instance.GetLocalizationKey($"ChemicalRoles.{member}");
        string value = Language.GetTextValue(key);

        return value == key ? member : value;
    }

    internal static bool IsPotion(int itemType) =>
        ContentSamples.ItemsByType.TryGetValue(itemType, out Item? sample) && sample.IsManagedPotion();
}

/// <summary>
///     Glyph sprites under <c>Assets/Textures/UI/ChemicalGlyphs</c>, one per <see cref="Temperament" /> and
///     <see cref="ChemicalTrait" /> flag, named after the flag.
/// </summary>
public static class ChemicalGlyphs
{
    private const string DIRECTORY = $"{nameof(TrelamiumIII)}/Assets/Textures/UI/ChemicalGlyphs/";

    /// <summary>The glyph for a single flag, or null for None, several flags, or a flag with no sprite yet.</summary>
    public static Asset<Texture2D>? Icon(Temperament flag) => Icon((int)flag, flag.ToString());

    /// <inheritdoc cref="Icon(Temperament)" />
    public static Asset<Texture2D>? Icon(ChemicalTrait flag) => Icon((int)flag, flag.ToString());

    /// <summary>Each set flag, lowest bit first.</summary>
    public static IEnumerable<Temperament> Flags(Temperament set)
    {
        foreach (int bit in Bits((int)set))
            yield return (Temperament)bit;
    }

    /// <inheritdoc cref="Flags(Temperament)" />
    public static IEnumerable<ChemicalTrait> Flags(ChemicalTrait set)
    {
        foreach (int bit in Bits((int)set))
            yield return (ChemicalTrait)bit;
    }

    /// <summary>The lowest set flag, or None.</summary>
    public static Temperament First(Temperament set) => (Temperament)((int)set & -(int)set);

    /// <inheritdoc cref="First(Temperament)" />
    public static ChemicalTrait First(ChemicalTrait set) => (ChemicalTrait)((int)set & -(int)set);

    private static Asset<Texture2D>? Icon(int bits, string name)
    {
        if (bits == 0 || (bits & (bits - 1)) != 0)
            return null;

        string path = DIRECTORY + name;

        return ModContent.HasAsset(path) ? ModContent.Request<Texture2D>(path) : null;
    }

    private static IEnumerable<int> Bits(int bits)
    {
        for (int bit = 1; bit > 0 && bit <= bits; bit <<= 1)
        {
            if ((bits & bit) != 0)
                yield return bit;
        }
    }
}

/// <summary>Labels solutes, solvents and solutions in tooltips and marks their inventory slot with a quality glyph.</summary>
public sealed class ChemicalRoleTag : GlobalItem, ITooltipBlockProvider
{
    /// <summary>Longest edge of the slot glyph, in unscaled slot pixels.</summary>
    private const float SLOT_GLYPH_FIT = 16f;

    /// <summary>Inset from the slot's corner, in unscaled slot pixels.</summary>
    private const float SLOT_PADDING = 3f;

    /// <summary>Between the role and its qualities, and between qualities.</summary>
    private const float GROUP_GAP = 6f;

    /// <summary>A quiet tint for the role word: a category, not a name.</summary>
    private static readonly Color RoleColor = new(196, 204, 222);

    public void AddTooltipBlocks(Item item, List<TooltipBlock> blocks)
    {
        ChemicalRole role = ChemicalRoles.Of(item);

        if (role == ChemicalRole.None)
            return;

        string roleName = ChemicalRoles.DisplayName(item.type);
        var qualities = new List<(Asset<Texture2D>? Glyph, string Label, Color Color)>();

        foreach (Temperament flag in ChemicalGlyphs.Flags(ChemicalRoles.TemperamentsOf(item)))
        {
            ChemicalTypeInfo.TryGet(flag, out string label, out Color color);
            qualities.Add((ChemicalGlyphs.Icon(flag), label, color));
        }

        foreach (ChemicalTrait flag in ChemicalGlyphs.Flags(ChemicalRoles.TraitsOf(item)))
        {
            ChemicalTypeInfo.TryGet(flag, out string label, out Color color);
            qualities.Add((ChemicalGlyphs.Icon(flag), label, color));
        }

        var fallback = new StringBuilder(roleName);

        for (int i = 0; i < qualities.Count; i++)
            fallback.Append(i == 0 ? ": " : ", ").Append(qualities[i].Label);

        var block = new TooltipRowsBlock(TooltipAnchor.AfterName, fallback.ToString());
        TooltipRow row = block.Row().Icon(ChemicalRoles.Icon(role)).Text(roleName, RoleColor);

        foreach (var (glyph, label, color) in qualities)
            row.Gap(GROUP_GAP).Icon(glyph).Text(label, color);

        blocks.Add(block);
    }

    public override void PostDrawInInventory(Item item, SpriteBatch spriteBatch, Vector2 position,
        Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
    {
        if (ChemicalRoles.Of(item) == ChemicalRole.None)
            return;

        Temperament first = ChemicalGlyphs.First(ChemicalRoles.TemperamentsOf(item));

        Asset<Texture2D>? icon = first != Temperament.None
            ? ChemicalGlyphs.Icon(first)
            : ChemicalGlyphs.Icon(ChemicalGlyphs.First(ChemicalRoles.TraitsOf(item)));

        if (icon is not { Value: { } glyph })
            return;

        // position is the slot's centre; the slot itself is drawn at Main.inventoryScale.
        float slotScale = Main.inventoryScale;
        Vector2 halfSlot = TextureAssets.InventoryBack.Value.Size() * (0.5f * slotScale);
        Vector2 corner = position + new Vector2(-halfSlot.X + SLOT_PADDING * slotScale, halfSlot.Y - SLOT_PADDING * slotScale);

        float glyphScale = SLOT_GLYPH_FIT / Math.Max(glyph.Width, glyph.Height) * slotScale;

        spriteBatch.Draw(glyph, corner, null, Color.White * (drawColor.A / 255f), 0f,
            new Vector2(0f, glyph.Height), glyphScale, SpriteEffects.None, 0f);
    }
}

/// <summary>Drops the role caches with the mod, alongside the registries they were read from.</summary>
public sealed class ChemicalRoleSystem : ModSystem
{
    public override void Unload() => ChemicalRoles.Clear();
}
