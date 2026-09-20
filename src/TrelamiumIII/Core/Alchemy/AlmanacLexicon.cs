namespace TrelamiumIII.Core.Alchemy;

/// <summary>One line of the lexicon: a section heading, or a term and its gloss.</summary>
public readonly record struct LexiconRow(string Term, string Gloss, bool IsHeading)
{
    public static LexiconRow Heading(string title) => new(title, string.Empty, true);

    public static LexiconRow Entry(string term, string gloss) => new(term, gloss, false);
}

/// <summary>
///     The Latin terms the almanac's lexicon chapter lists, as a flat run of rows paginated across
///     as many spreads as they need.
/// </summary>
public static class AlmanacLexicon
{
    /// <summary>Rows on the chapter's first page, which also carries the title and intro.</summary>
    public const int ROWS_FIRST_PAGE = 10;

    /// <summary>Rows on every page after the first.</summary>
    public const int ROWS_PER_PAGE = 15;

    private static readonly LexiconRow[] Rows =
    [
        LexiconRow.Heading("Matter & Principle"),
        LexiconRow.Entry("Aqua", "water; the liquid medium"),
        LexiconRow.Entry("Terra", "earth; the fixed, terrestrial principle"),
        LexiconRow.Entry("Aer", "air"),
        LexiconRow.Entry("Ignis", "fire"),
        LexiconRow.Entry("Sal", "salt; the fixed, crystalline principle"),
        LexiconRow.Entry("Sulfur", "sulfur; the combustible, oily, active principle"),
        LexiconRow.Entry("Mercurius", "mercury; the volatile, fluid, intermediary principle"),
        LexiconRow.Entry("Spiritus", "spirit; a volatile or subtle component"),
        LexiconRow.Entry("Corpus", "body; a solid or fixed component"),
        LexiconRow.Entry("Anima", "soul; the animating principle"),
        LexiconRow.Entry("Materia", "matter, material"),
        LexiconRow.Entry("Prima materia", "\"first matter\"; primordial matter"),
        LexiconRow.Entry("Substantia", "substance"),
        LexiconRow.Entry("Essentia", "essence"),
        LexiconRow.Entry("Principium", "principle, origin"),
        LexiconRow.Entry("Elementum", "element"),

        LexiconRow.Heading("Solvents & Preparations"),
        LexiconRow.Entry("Solvens", "dissolving agent; a solvent"),
        LexiconRow.Entry("Solutio", "dissolution; a solution"),
        LexiconRow.Entry("Menstruum", "the liquid medium that dissolves or extracts"),
        LexiconRow.Entry("Liquor", "liquid"),
        LexiconRow.Entry("Aqua fortis", "\"strong water\"; nitric acid"),
        LexiconRow.Entry("Aqua regia", "\"royal water\"; dissolves even gold"),
        LexiconRow.Entry("Aqua vitae", "\"water of life\"; distilled spirit"),
        LexiconRow.Entry("Oleum", "oil"),
        LexiconRow.Entry("Tinctura", "tincture; a coloured extraction"),
        LexiconRow.Entry("Elixir", "a highly refined preparation"),
        LexiconRow.Entry("Extractum", "extract"),
        LexiconRow.Entry("Quintessentia", "quintessence; the \"fifth essence\""),

        LexiconRow.Heading("Processes"),
        LexiconRow.Entry("Dissolutio", "dissolution"),
        LexiconRow.Entry("Coagulatio", "coagulation"),
        LexiconRow.Entry("Separatio", "separation"),
        LexiconRow.Entry("Extractio", "extraction"),
        LexiconRow.Entry("Purificatio", "purification"),
        LexiconRow.Entry("Calcinatio", "calcination"),
        LexiconRow.Entry("Distillatio", "distillation"),
        LexiconRow.Entry("Sublimatio", "sublimation"),
        LexiconRow.Entry("Evaporatio", "evaporation"),
        LexiconRow.Entry("Fixatio", "fixation"),
        LexiconRow.Entry("Volatilizatio", "volatilization"),
        LexiconRow.Entry("Fermentatio", "fermentation"),
        LexiconRow.Entry("Putrefactio", "putrefaction, decomposition"),
        LexiconRow.Entry("Conjunctio", "conjunction, union"),
        LexiconRow.Entry("Multiplicatio", "multiplication, intensification"),
        LexiconRow.Entry("Incineratio", "incineration"),
        LexiconRow.Entry("Desiccatio", "drying"),
        LexiconRow.Entry("Humectatio", "moistening"),
        LexiconRow.Entry("Maceratio", "maceration, soaking"),

        LexiconRow.Heading("Qualities & States"),
        LexiconRow.Entry("Fixus", "fixed, nonvolatile"),
        LexiconRow.Entry("Volatilis", "volatile, flying"),
        LexiconRow.Entry("Solubilis", "soluble"),
        LexiconRow.Entry("Insolubilis", "insoluble"),
        LexiconRow.Entry("Purificatus", "purified"),
        LexiconRow.Entry("Impurus", "impure"),
        LexiconRow.Entry("Calcinatus", "calcined"),
        LexiconRow.Entry("Distillatus", "distilled"),
        LexiconRow.Entry("Sublimatus", "sublimated"),
        LexiconRow.Entry("Coagulatus", "coagulated"),
        LexiconRow.Entry("Dissolutus", "dissolved"),
        LexiconRow.Entry("Extractus", "extracted"),
        LexiconRow.Entry("Fermentatus", "fermented"),
        LexiconRow.Entry("Putrefactus", "putrefied, decomposed"),
        LexiconRow.Entry("Ardens", "burning"),
        LexiconRow.Entry("Fervens", "boiling"),
        LexiconRow.Entry("Frigidus", "cold"),
        LexiconRow.Entry("Humidus", "wet, moist"),
        LexiconRow.Entry("Siccus", "dry"),
        LexiconRow.Entry("Gravis", "heavy"),
        LexiconRow.Entry("Levis", "light"),
        LexiconRow.Entry("Subtilis", "subtle, fine"),
        LexiconRow.Entry("Crassus", "thick, coarse"),
    ];

    public static int Count => Rows.Length;

    /// <summary>Spreads the chapter occupies, counting its first page's smaller capacity.</summary>
    public static int SpreadCount
    {
        get
        {
            int rest = Math.Max(0, Count - ROWS_FIRST_PAGE);
            int pages = 1 + (rest + ROWS_PER_PAGE - 1) / ROWS_PER_PAGE;

            return Math.Max(1, (pages + 1) / 2);
        }
    }

    /// <summary>The first row index on <paramref name="page" />, counting from the chapter's first page.</summary>
    public static int StartOfPage(int page)
        => page <= 0 ? 0 : ROWS_FIRST_PAGE + (page - 1) * ROWS_PER_PAGE;

    public static int RowsOnPage(int page) => page <= 0 ? ROWS_FIRST_PAGE : ROWS_PER_PAGE;

    public static LexiconRow At(int index) => Rows[index];
}
