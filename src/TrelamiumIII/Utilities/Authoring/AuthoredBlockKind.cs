using System.Collections.Generic;

namespace TrelamiumIII.Utilities.Authoring;

/// <summary>Which builder call an authored block turns into.</summary>
public enum AuthoredBlockKind
{
    Prose,
    Heading,
    Chapter,
    Quote,
    Caption,
    Rule,
    Space,
    PageBreak,
}
