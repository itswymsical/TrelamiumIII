namespace TrelamiumIII.Core.Rarities;

/// <summary>
///     A <see cref="ModRarity" /> that carries a <see cref="RarityStyle" />, so declaring the rarity
///     and declaring how it looks are the same act.
/// </summary>
/// <remarks>
///     <para>
///         The style is built in <see cref="SetStaticDefaults" /> rather than in a field initialiser
///         or the constructor. Two things have to be true before a style can be built and neither is
///         true at construction: <see cref="ModType.Type" /> has been assigned (registration runs
///         first), and localisation and assets are available, which is what lets
///         <see cref="RarityStyle.Flavor" /> be resolved with <c>Language.GetOrRegister</c> and lets
///         a flair hold an <c>Asset</c> without a load-order gamble.
///     </para>
///     <para>
///         Nothing forces a rarity through this class. Anything that ends up in
///         <see cref="RarityStyles" /> draws, so a plain <see cref="ModRarity" /> can register a
///         style from elsewhere, and a vanilla tier can be restyled without a <c>ModRarity</c> at
///         all. This is the short path, not the only one.
///     </para>
/// </remarks>
public abstract class FlavorRarity : ModRarity
{
    /// <summary>The built style, available once static defaults have run.</summary>
    public RarityStyle Style { get; private set; } = null!;

    /// <summary>
    ///     Builds this rarity's look. Called once, after <see cref="ModType.Type" /> is known.
    /// </summary>
    protected abstract RarityStyle BuildStyle();

    public override void SetStaticDefaults()
    {
        Style = BuildStyle();

        RarityStyles.Register(Type, Style);
    }
}
