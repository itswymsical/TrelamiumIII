namespace TrelamiumIII.Core.Alchemy;

/// <summary>Slot layout of an alchemy station. Selected with the station's mode button; recipe matching ignores it.</summary>
public enum AlchemyStationMode : byte
{
    /// <summary>Input: solvent or solution. Components: solutes.</summary>
    Solution,

    /// <summary>Input: solvent. Components: solutes and up to <see cref="AlchemyStationRules.MAX_COMPONENT_SOLVENTS" /> solvents.</summary>
    SolventMixture,

    /// <summary>Input: solute. Components: solutes.</summary>
    SoluteMixture,
}

/// <summary>Per-mode slot acceptance for alchemy stations.</summary>
public static class AlchemyStationRules
{
    public const int MAX_COMPONENT_SOLVENTS = 2;

    private static readonly AlchemyStationMode[] Modes =
        [AlchemyStationMode.Solution, AlchemyStationMode.SolventMixture, AlchemyStationMode.SoluteMixture];

    public static bool AcceptsInput(AlchemyStationMode mode, Item item)
    {
        if (item.IsAir)
            return true;

        ChemicalRole role = ChemicalRoles.Of(item);

        return mode switch
        {
            AlchemyStationMode.Solution => role is ChemicalRole.Solvent or ChemicalRole.Solution,
            AlchemyStationMode.SolventMixture => role == ChemicalRole.Solvent,
            AlchemyStationMode.SoluteMixture => role == ChemicalRole.Solute,
            _ => false,
        };
    }

    /// <summary>Slot <paramref name="index" /> is excluded from the solvent count, so its current item can be replaced.</summary>
    public static bool AcceptsComponent(AlchemyStationMode mode, Item[] components, int index, Item item)
    {
        if (item.IsAir)
            return true;

        ChemicalRole role = ChemicalRoles.Of(item);

        if (role == ChemicalRole.Solute)
            return true;

        if (mode != AlchemyStationMode.SolventMixture || role != ChemicalRole.Solvent)
            return false;

        int solvents = 0;

        for (int i = 0; i < components.Length; i++)
        {
            if (i != index && ChemicalRoles.Of(components[i]) == ChemicalRole.Solvent)
                solvents++;
        }

        return solvents < MAX_COMPONENT_SOLVENTS;
    }

    public static bool Accepts(AlchemyStationMode mode, Item input, Item[] components)
    {
        if (!AcceptsInput(mode, input))
            return false;

        for (int i = 0; i < components.Length; i++)
        {
            if (!AcceptsComponent(mode, components, i, components[i]))
                return false;
        }

        return true;
    }

    /// <summary>The next mode in cycle order that accepts the current contents; <paramref name="current" /> when none does.</summary>
    public static AlchemyStationMode NextMode(AlchemyStationMode current, Item input, Item[] components)
    {
        int start = Array.IndexOf(Modes, current);

        for (int step = 1; step < Modes.Length; step++)
        {
            AlchemyStationMode candidate = Modes[(start + step) % Modes.Length];

            if (Accepts(candidate, input, components))
                return candidate;
        }

        return current;
    }
}
