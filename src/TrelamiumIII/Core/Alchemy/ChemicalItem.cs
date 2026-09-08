namespace TrelamiumIII.Core.Alchemy;

/// <summary>
///     A <see cref="ModItem" /> that carries its own almanac entry, instead of being listed by hand in
///     <see cref="AlmanacDefinitions" />.
/// </summary>
/// <remarks>
///     Collected once at <c>PostSetupContent</c>, in <see cref="AlmanacDefinitions.Register" />. The
///     item's type is stamped onto the entry there, so implementations leave
///     <see cref="AlmanacEntry.ItemType" /> alone. <see cref="AlmanacDefinitions" /> is left holding
///     only the vanilla items, which have no class of ours to put this on.
/// </remarks>
public interface IAlmanacDefined
{
    AlmanacEntry AlmanacEntry { get; }
}

/// <summary>
///     A <see cref="ModItem" /> that states what it is at the station, rather than leaving
///     <see cref="ChemicalRoles.Of(int)" /> to infer it from the recipes it happens to appear in.
///     An authored role wins over the inferred one.
/// </summary>
public interface IChemicalRoleDefined
{
    ChemicalRole ChemicalRole { get; }
}

/// <summary>
///     An item that exists to be dissolved, poured or bottled. It has no behaviour of its own -- only
///     a role at the station and the almanac data that goes with it -- so the item <em>is</em> its
///     data, and one class serves all of them.
/// </summary>
/// <remarks>
///     <para>
///         Two ways to use this. Most of them are one instance of <see cref="SoluteItem" /> and co.,
///         registered by name from a table (see <c>Reagent</c> and <c>Solution</c>); the constructor
///         taking a name is what makes that possible, and having no parameterless constructor is what
///         keeps tModLoader from autoloading the shared class as an item of its own.
///     </para>
///     <para>
///         An item that needs real code of its own -- a custom tooltip, a use effect -- subclasses one
///         of the role classes instead and autoloads under its own class name, setting these fields in
///         its constructor.
///     </para>
/// </remarks>
public abstract class ChemicalItem : ModItem, IAlmanacDefined, IChemicalRoleDefined
{
    [CloneByReference]
    private readonly string? nameOverride;

    /// <param name="name">
    ///     The internal name, for an instance sharing its class with other items. <c>null</c> for a
    ///     subclass, which takes its class name the usual way.
    /// </param>
    protected ChemicalItem(string? name) => nameOverride = name;

    /// <summary>
    ///     Every <c>Item</c> gets its own copy of this, and the default way of making one is the
    ///     parameterless constructor -- which the shared classes deliberately do not have, and which
    ///     would in any case hand back an item with none of the data below. Copy the loaded one.
    /// </summary>
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

    /// <summary>
    ///     Internal names this item used to go by, so items in old saves still resolve. The attribute
    ///     form (<c>[LegacyName]</c>) is per-class and so is no use to instances sharing one; these are
    ///     registered per instance instead, by whichever table loads the item.
    /// </summary>
    [CloneByReference]
    public string[]? LegacyNames;

    #endregion

    #region Almanac entry

    /// <summary>Star rating, 1-5. <c>0</c> draws no stars. <see cref="AlmanacEntry.Potency" />.</summary>
    public int Potency;

    /// <summary>The chemical tag(s) this item carries. <see cref="AlmanacEntry.Chemical" />.</summary>
    public ChemicalQuality Quality;

    /// <summary>Thematic classification, for the book's flavour and sorting. <see cref="AlmanacEntry.Principle" />.</summary>
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

    /// <summary>
    ///     <see cref="TexturePath" /> when set. Otherwise the class path, which only works for a
    ///     subclass -- instances sharing a class would all land on the same sprite, so a table
    ///     registering them fills this in first.
    /// </summary>
    public override string Texture => TexturePath ?? base.Texture;

    AlmanacEntry IAlmanacDefined.AlmanacEntry => new()
    {
        Potency = Potency,
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

/// <summary>A reagent: dissolved into a solvent to make something. <see cref="ChemicalRole.Solute" />.</summary>
public class SoluteItem(string? name = null) : ChemicalItem(name)
{
    public override ChemicalRole ChemicalRole => ChemicalRole.Solute;
}

/// <summary>What solutes are dissolved into -- the vial slot. <see cref="ChemicalRole.Solvent" />.</summary>
public class SolventItem(string? name = null) : ChemicalItem(name)
{
    public override ChemicalRole ChemicalRole => ChemicalRole.Solvent;
}

/// <summary>What comes out of the station. <see cref="ChemicalRole.Solution" />.</summary>
public class SolutionItem(string? name = null) : ChemicalItem(name)
{
    public override ChemicalRole ChemicalRole => ChemicalRole.Solution;
}

/// <summary>Loads <see cref="ChemicalItem" /> instances that share a class, for the content tables.</summary>
public static class ChemicalItems
{
    /// <summary>
    ///     Adds one item to <paramref name="mod" /> and hands back its type, for the table's static
    ///     accessor. Falls back to <paramref name="fallbackTexture" /> when the item named no sprite of
    ///     its own -- instances sharing a class have no class path to fall back on -- and registers
    ///     whatever <see cref="ChemicalItem.LegacyNames" /> it carries, now that there is an identity
    ///     to hang them on.
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
