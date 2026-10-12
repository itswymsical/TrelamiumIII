using Terraria.Audio;
using Terraria.UI;
using TrelamiumIII.Core.Alchemy;

namespace TrelamiumIII.Core.UI.AlchemyStation;

/// <summary>
///     Preview, batch, mode and brew-start logic shared by the station panels, read by the slot widgets. Subclasses own
///     the slot arrays and the brew state: the basic panel brews itself, the advanced station's entity brews on its own.
/// </summary>
internal abstract class AlchemyStationUIBase : UIState
{
    public const int COMPONENT_SLOT_COUNT = 3;

    protected readonly AlchemyPreview Preview = new();

    protected int BatchPreset;

    /// <summary>Batches the next brew produces: the selected <see cref="AlchemyBatchSizes.Presets"/> entry clamped to <see cref="MaxOutputCount"/>.</summary>
    public int OutputCount => AlchemyBatchSizes.Of(BatchPreset, MaxOutputCount);

    /// <summary>Largest <see cref="OutputCount"/> the current ingredients can support.</summary>
    public int MaxOutputCount { get; private set; } = 1;

    public abstract bool IsBrewing { get; }

    public abstract bool IsResultReady { get; }

    /// <summary>0..1 progress through the active brew.</summary>
    public abstract float BrewProgress { get; }

    public abstract AlchemyStationMode Mode { get; }

    /// <summary>Whether the output slot draws its own progress bar. False when the bar is drawn on the station sprite.</summary>
    public abstract bool ShowInlineBrewBar { get; }

    /// <summary>The bound input slot, or null while no station is bound.</summary>
    protected abstract Item[]? Input { get; }

    protected abstract Item[]? Components { get; }

    protected abstract Item[]? Output { get; }

    protected abstract Item[]? BottleReturn { get; }

    /// <summary>Minimum potency the previewed recipe requires of component slot <paramref name="index"/>; 0 when unclaimed.</summary>
    public int RequiredPotency(int index) => Preview.Resolution.RequiredPotencyFor(index);

    /// <summary>Starts a brew of the current preview when the slots allow it. Called by <see cref="AlchemyOutputSlot"/> on click.</summary>
    public void TryStartBrew()
    {
        if (Input is null || Output is null || BottleReturn is null || IsBrewing || IsResultReady)
            return;

        if (Output[0].IsAir || Input[0].IsAir)
            return;

        AlchemyResolution resolution = Preview.Resolution;
        int count = OutputCount;

        if (resolution.ReturnsBottle && StationBottles.Capacity(BottleReturn[0]) < count)
            return;

        StartBrew(resolution, count);
        SoundEngine.PlaySound(Assets.SFX.Brewing.Asset);
    }

    /// <summary>Begins brewing the previewed output, which is already scaled by <paramref name="count"/>.</summary>
    protected abstract void StartBrew(AlchemyResolution resolution, int count);

    protected abstract void SetMode(AlchemyStationMode mode);

    /// <summary>Refreshes the preview from the slots and shows it in the output slot.</summary>
    protected void UpdatePreview()
    {
        if (Input is null || Components is null || Output is null || BottleReturn is null)
            return;

        Preview.Refresh(Input[0], Components, BottleReturn[0]);
        MaxOutputCount = Preview.MaxBatches;
        Output[0] = Preview.ItemFor(OutputCount);
    }

    protected bool CanCycleBatch()
        => Output is not null && !IsBrewing && !IsResultReady && !Output[0].IsAir && MaxOutputCount > 1;

    protected void CycleBatch() => BatchPreset = AlchemyBatchSizes.Next(BatchPreset, MaxOutputCount);

    protected bool CanCycleMode()
        => Input is not null && Components is not null && !IsBrewing && !IsResultReady
           && AlchemyStationRules.NextMode(Mode, Input[0], Components) != Mode;

    protected void CycleMode()
    {
        if (Input is not null && Components is not null)
            SetMode(AlchemyStationRules.NextMode(Mode, Input[0], Components));
    }

    /// <summary>Shows the previewed brew's category, or nothing when there is none.</summary>
    protected void ShowCategory(AlchemyCategoryLabel label)
    {
        BrewCategory category = Preview.Resolution.Composition.Category;

        label.SetText(category == BrewCategory.None ? string.Empty : BrewCategories.DisplayName(category), BrewCategories.ColorOf(category));
    }

    /// <summary>Clears the batch choice and the cached preview.</summary>
    protected void ResetPreview()
    {
        BatchPreset = 0;
        Preview.Reset();
    }
}
