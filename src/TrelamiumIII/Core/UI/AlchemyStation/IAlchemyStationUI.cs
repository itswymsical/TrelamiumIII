namespace TrelamiumIII.Core.UI.AlchemyStation;

/// <summary>
/// The brew-state surface the shared slot widgets (<see cref="AlchemyComponentSlot"/>,
/// <see cref="AlchemyVialSlot"/>, <see cref="AlchemyOutputSlot"/>) need from whichever station UI hosts them.
/// Implemented by both the basic <see cref="AlchemyStationUI"/> and the advanced <see cref="AdvancedAlchemyStationUI"/>,
/// so the slots stay shared even though the two UIs are otherwise independent.
/// </summary>
internal interface IAlchemyStationUI
{
    bool IsBrewing { get; }
    bool IsResultReady { get; }
    float BrewProgress { get; }
    void TryStartBrew();

    /// <summary>
    /// Whether the output slot draws its own inline progress bar. The basic panel UI does; the advanced world
    /// overlay returns false because the bar is drawn on the machine (under the heat plate) instead.
    /// </summary>
    bool ShowInlineBrewBar { get; }

    /// <summary>
    /// Units the previewed recipe draws from solute slot <paramref name="index"/>.
    /// the slot renders as a star meter. 0 when no recipe is asking anything of that slot.
    /// </summary>
    int SoluteCost(int index);
}
