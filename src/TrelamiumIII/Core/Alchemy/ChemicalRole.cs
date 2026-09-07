using System.Collections.Generic;
using System.Text;
using ReLogic.Content;
using Terraria.GameContent;
using Terraria.Localization;
using TrelamiumIII.Core.Text.Tooltips;

namespace TrelamiumIII.Core.Alchemy;

/// <summary>What part an item plays at the alchemy station.</summary>
public enum ChemicalRole
{
    None,

    /// <summary>A reagent: dissolved into a solvent to make something.</summary>
    Solute,

    /// <summary>What solutes are dissolved into.</summary>
    Solvent,

    /// <summary>What comes out.</summary>
    Solution,
}

/// <summary>
///     Which items are solutes, solvents and solutions, and the <see cref="ChemicalQuality" /> each
///     one carries.
/// </summary>
/// <remarks>
///     <para>
///         Roles are read off the recipe registry: anything a recipe produces is a solution,
///         anything one consumes as its solvent is a solvent, and anything one takes in a component
///         slot is a solute -- in that order of precedence, for an item that is more than one. Nothing
///         has to be tagged by hand for the common case, and <see cref="Register" /> exists for the
///         item that is chemically something without any recipe saying so.
///     </para>
///     <para>
///         The quality is the instance's infusion when it has one (a suffixed bottle of water),
///         else whatever the almanac says about the type.
///     </para>
/// </remarks>
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

    public static ChemicalRole Of(int itemType)
    {
        if (itemType <= 0)
            return ChemicalRole.None;

        if (authored.TryGetValue(itemType, out ChemicalRole role) || derived.TryGetValue(itemType, out role))
            return role;

        // Before the recipes exist every answer would be "none", and AlchemyIngredients would cache
        // that emptiness too. Leave both caches alone until there is something to read.
        if (AlchemyRecipeRegistry.All.Count == 0)
            return ChemicalRole.None;

        role = AlchemyRecipeRegistry.Produces(itemType) ? ChemicalRole.Solution
            : AlchemyIngredients.IsSolventType(itemType) ? ChemicalRole.Solvent
            : AlchemyIngredients.IsSubstanceType(itemType) ? ChemicalRole.Solute
            : ChemicalRole.None;

        derived[itemType] = role;

        return role;
    }

    /// <summary>The instance's infusion, else the almanac's tags for the type. May hold several flags.</summary>
    public static ChemicalQuality QualityOf(Item item)
    {
        ChemicalQuality infusion = SolutionGlobalItem.GetInfusion(item);

        if (infusion != ChemicalQuality.None)
            return infusion;

        // For() builds a default entry for an unauthored type; asking first keeps that off the draw path.
        return AlmanacEntryRegistry.Has(item.type)
            ? AlmanacEntryRegistry.For(item.type).Chemical
            : ChemicalQuality.None;
    }

    /// <summary>Localised under <c>ChemicalRoles.{member}</c>; the member name when no translation exists.</summary>
    public static string DisplayName(ChemicalRole role)
    {
        string key = TrelamiumIII.Instance.GetLocalizationKey($"ChemicalRoles.{role}");
        string value = Language.GetTextValue(key);

        return value == key ? role.ToString() : value;
    }

    /// <summary>The role's tooltip icon (<c>Assets/Textures/{Role}TooltipIcon</c>), or null while it has none.</summary>
    public static Asset<Texture2D>? Icon(ChemicalRole role)
    {
        if (role == ChemicalRole.None)
            return null;

        string path = $"{ICON_DIRECTORY}{role}TooltipIcon";

        return ModContent.HasAsset(path) ? ModContent.Request<Texture2D>(path) : null;
    }
}

/// <summary>
///     The glyph sprites under <c>Assets/Textures/UI/ChemicalGlyphs</c>, one per
///     <see cref="ChemicalQuality" /> flag and named after it.
/// </summary>
public static class ChemicalQualityGlyphs
{
    private const string DIRECTORY = $"{nameof(TrelamiumIII)}/Assets/Textures/UI/ChemicalGlyphs/";

    /// <summary>The glyph for a single flag, or null for a quality with no sprite yet.</summary>
    public static Asset<Texture2D>? Icon(ChemicalQuality flag)
    {
        if (!IsSingleFlag(flag))
            return null;

        string path = DIRECTORY + flag;

        return ModContent.HasAsset(path) ? ModContent.Request<Texture2D>(path) : null;
    }

    /// <summary>Each set flag, lowest bit first.</summary>
    public static IEnumerable<ChemicalQuality> Flags(ChemicalQuality set)
    {
        int bits = (int)set;

        for (int bit = 1; bit > 0 && bit <= bits; bit <<= 1)
        {
            if ((bits & bit) != 0)
                yield return (ChemicalQuality)bit;
        }
    }

    /// <summary>The lowest set flag, or <see cref="ChemicalQuality.None" />.</summary>
    public static ChemicalQuality First(ChemicalQuality set)
    {
        int bits = (int)set;
        return (ChemicalQuality)(bits & -bits);
    }

    private static bool IsSingleFlag(ChemicalQuality flag)
    {
        int bits = (int)flag;
        return bits != 0 && (bits & (bits - 1)) == 0;
    }
}

/// <summary>
///     Labels every solute, solvent and solution in its tooltip, and marks its inventory slot with
///     the glyph of the quality it carries.
/// </summary>
/// <remarks>
///     <para>
///         The tooltip row sits under the name -- a subtitle, not a stat -- and reads role first,
///         then each quality with its glyph: "Solvent  Alkaline". The slot glyph is the first
///         quality's alone, in the bottom-left corner, small enough to stay clear of the sprite. On
///         a stack it shares that corner with vanilla's count, which is drawn afterwards and so
///         sits on top.
///     </para>
///     <para>
///         Applies to every item rather than gating on <see cref="GlobalItem.AppliesToEntity" />,
///         because whether an item has a role is decided by the recipe registry, which does not
///         exist when that gate is asked. The per-item cost is a dictionary lookup.
///     </para>
/// </remarks>
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

        string roleName = ChemicalRoles.DisplayName(role);
        var qualities = new List<(ChemicalQuality Flag, string Label, Color Color)>();

        foreach (ChemicalQuality flag in ChemicalQualityGlyphs.Flags(ChemicalRoles.QualityOf(item)))
        {
            if (!ChemicalTypeInfo.TryGet(flag, out string label, out Color color))
            {
                label = flag.ToString();
                color = Color.White;
            }

            qualities.Add((flag, label, color));
        }

        var fallback = new StringBuilder(roleName);

        for (int i = 0; i < qualities.Count; i++)
            fallback.Append(i == 0 ? ": " : ", ").Append(qualities[i].Label);

        var block = new TooltipRowsBlock(TooltipAnchor.AfterName, fallback.ToString());
        TooltipRow row = block.Row().Icon(ChemicalRoles.Icon(role)).Text(roleName, RoleColor);

        foreach (var (flag, label, color) in qualities)
            row.Gap(GROUP_GAP).Icon(ChemicalQualityGlyphs.Icon(flag)).Text(label, color);

        blocks.Add(block);
    }

    public override void PostDrawInInventory(Item item, SpriteBatch spriteBatch, Vector2 position,
        Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
    {
        if (ChemicalRoles.Of(item) == ChemicalRole.None)
            return;

        ChemicalQuality first = ChemicalQualityGlyphs.First(ChemicalRoles.QualityOf(item));

        if (ChemicalQualityGlyphs.Icon(first) is not { Value: { } glyph })
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
