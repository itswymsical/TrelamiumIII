using TrelamiumIII.Core.Alchemy;

namespace TrelamiumIII.Core.UI.AlchemyStation;

/// <summary>Brew state the shared slot widgets read from the hosting station UI (basic or advanced).</summary>
internal interface IAlchemyStationUI
{
    bool IsBrewing { get; }
    bool IsResultReady { get; }
    float BrewProgress { get; }
    void TryStartBrew();

    /// <summary>Whether the output slot draws its own progress bar. False when the bar is drawn on the station sprite.</summary>
    bool ShowInlineBrewBar { get; }

    AlchemyStationMode Mode { get; }

    /// <summary>Minimum potency the previewed recipe requires of component slot <paramref name="index"/>; 0 when unclaimed.</summary>
    int RequiredPotency(int index);
}
