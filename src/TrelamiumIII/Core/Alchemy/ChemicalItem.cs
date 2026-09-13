namespace TrelamiumIII.Core.Alchemy;

/// <summary>
///     A <see cref="ModItem" /> carrying its own almanac entry; <see cref="AlmanacDefinitions.Register" /> collects it and stamps <see cref="AlmanacEntry.ItemType" />.
/// </summary>
public interface IAlmanacDefined
{
    AlmanacEntry AlmanacEntry { get; }
}

/// <summary>A <see cref="ModItem" /> that states its station role, winning over <see cref="ChemicalRoles.Of(int)" />'s inference.</summary>
public interface IChemicalRoleDefined
{
    ChemicalRole ChemicalRole { get; }
}

/// <summary>
///     A behaviourless alchemy item that is just data. Most are <see cref="SoluteItem" />-style instances registered by name from a table;
///     no parameterless constructor keeps tModLoader from autoloading the shared class. Items that need code subclass a role class.
/// </summary>
public abstract class ChemicalItem : ModItem, IAlmanacDefined, IChemicalRoleDefined
{
    [CloneByReference]
    private readonly string? nameOverride;

    /// <param name="name">Internal name for an instance sharing its class; null for a subclass, which uses its class name.</param>
    protected ChemicalItem(string? name) => nameOverride = name;

    /// <summary>Clones the loaded instance, since the shared classes have no parameterless constructor.</summary>
    protected override bool CloneNewInstances => true;

    #region Item defaults

    /// <summary>Width and height, in pixels. Square; these items are all bottles and powders.</summary>
    public int Size = 30;

    /// <summary>The item's own <c>Item.rare</c>. Distinct from <see cref="AlmanacRarity" />.</summary>
    public int Rare = ItemRarityID.White;

    /// <summary>Sell value in copper, as from <c>Item.sellPrice</c>.</summary>
    public int Value;

    /// <summary>Texture path. Left null, see <see cref="Texture" />.</summary>
    [CloneByReference]
    public string? TexturePath;

    /// <summary>Anything else this item needs set on <c>Item</c>, run at the end of <see cref="SetDefaults" />.</summary>
    [CloneByReference]
    public Action<Item>? Defaults;

    /// <summary>Vanilla crafting recipes, run from <see cref="AddRecipes" />. Station recipes live in <c>ChemistryRecipeSystem</c>.</summary>
    [CloneByReference]
    public Action<ModItem>? Recipes;

    /// <summary>Former internal names for old saves, registered per instance since <c>[LegacyName]</c> is per class.</summary>
    [CloneByReference]
    public string[]? LegacyNames;

    #endregion

    #region Almanac entry

    /// <summary>Star rating, 1-5. <c>0</c> draws no stars. <see cref="AlmanacEntry.AlmanacPotency" />.</summary>
    public int AlmanacPotency;

    /// <summary><see cref="AlmanacEntry.StackPerPotency" />.</summary>
    public int StackPerPotency = 1;

    /// <summary>The chemical tag(s) this item carries. <see cref="AlmanacEntry.Chemical" />.</summary>
    public ChemicalQuality Quality;

    /// <summary><see cref="AlmanacEntry.Principle" />. None on a solute marks it as a reagent.</summary>
    public PrincipleType Principle;

    /// <summary>The rarity the book judges this at, which need not be <see cref="Rare" />. <see cref="AlmanacEntry.Rarity" />.</summary>
    public int AlmanacRarity = AlmanacEntry.UseVanillaRarity;

    /// <summary>Hidden in the Ingredients index until obtained. <see cref="AlmanacEntry.Hidden" />.</summary>
    public bool Hidden = true;

    /// <summary>Flavour-text sub-key, or null to derive it from the display name. <see cref="AlmanacEntry.DescriptionKey" />.</summary>
    [CloneByReference]
    public string? DescriptionKey;

    #endregion

    /// <inheritdoc cref="IChemicalRoleDefined.ChemicalRole" />
    public abstract ChemicalRole ChemicalRole { get; }

    public override string Name => nameOverride ?? base.Name;

    /// <summary><see cref="TexturePath" /> when set, else the class path (only valid for subclasses).</summary>
    public override string Texture => TexturePath ?? base.Texture;

    AlmanacEntry IAlmanacDefined.AlmanacEntry => new()
    {
        AlmanacPotency = AlmanacPotency,
        StackPerPotency = StackPerPotency,
        Chemical = Quality,
        Principle = Principle,
        Rarity = AlmanacRarity,
        Hidden = Hidden,
        DescriptionKey = DescriptionKey,
    };

    public override void SetDefaults()
    {
        base.SetDefaults();

        Item.width = Item.height = Size;
        Item.rare = Rare;
        Item.value = Value;
        Item.maxStack = Item.CommonMaxStack;

        Defaults?.Invoke(Item);
    }

    public override void AddRecipes()
    {
        base.AddRecipes();

        Recipes?.Invoke(this);
    }
}

/// <summary>Solute-slot item. With a principle of None it is a reagent. <see cref="ChemicalRole.Solute" />.</summary>
public class SoluteItem(string? name = null) : ChemicalItem(name)
{
    public override ChemicalRole ChemicalRole => ChemicalRole.Solute;
}

/// <summary>Solvent-slot item. A principle is only valid if a recipe produces it from solutes. <see cref="ChemicalRole.Solvent" />.</summary>
public class SolventItem(string? name = null) : ChemicalItem(name)
{
    public override ChemicalRole ChemicalRole => ChemicalRole.Solvent;
}

/// <summary>Terminal output. In the solvent slot it makes the brew an augmentation. <see cref="ChemicalRole.Solution" />.</summary>
public class SolutionItem(string? name = null) : ChemicalItem(name)
{
    public override ChemicalRole ChemicalRole => ChemicalRole.Solution;
}

/// <summary>Loads <see cref="ChemicalItem" /> instances that share a class, for the content tables.</summary>
public static class ChemicalItems
{
    /// <summary>
    ///     Adds an item and returns its type, falling back to <paramref name="fallbackTexture" /> and registering its legacy names.
    /// </summary>
    public static int Add(Mod mod, ChemicalItem item, string fallbackTexture)
    {
        item.TexturePath ??= fallbackTexture;

        mod.AddContent(item);

        if (item.LegacyNames is { Length: > 0 } legacy)
            ModTypeLookup<ModItem>.RegisterLegacyNames(item, legacy);

        return item.Type;
    }
}
