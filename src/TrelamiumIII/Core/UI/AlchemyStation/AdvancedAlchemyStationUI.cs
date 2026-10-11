using System;
using Terraria.Audio;
using Terraria.UI;
using TrelamiumIII.Content.Alchemy.AlchemyStations;
using TrelamiumIII.Core.Alchemy;

namespace TrelamiumIII.Core.UI.AlchemyStation;

/// <summary>
/// A "world" overlayed UI State for the Advanced Alchemy Station Tileset. The basic early-game station uses <see cref="AlchemyStationUI"/>.
/// </summary>
internal class AdvancedAlchemyStationUI : UIState, IAlchemyStationUI
{
    public const int COMPONENT_SLOT_COUNT = 3;

    /// <summary>Slot draw scale at zoom 1; multiplied by the game zoom each frame so slots track the sprite.</summary>
    private const float BaseSlotScale = .85f;

    private readonly AlchemyComponentSlot[] _componentSlots = new AlchemyComponentSlot[COMPONENT_SLOT_COUNT];
    private AlchemyInputSlot _inputSlot;
    private AlchemyOutputSlot _outputSlot;
    private AlchemyBottleReturnSlot _bottleReturnSlot;

    private UIElement _controls;
    private AlchemyCategoryLabel _categoryLabel;

    /// <summary>The station this UI is currently editing. Brewing runs on it, independent of the UI being open.</summary>
    private AdvancedAlchemyStationEntity _entity;

    /// <summary>World position (pixels) of the bound station's origin tile top-left; set by the system each frame.</summary>
    public Vector2? StationWorldOrigin;

    /// <summary>The world overlay draws its progress bar on the machine, not inside the output slot.</summary>
    public bool ShowInlineBrewBar => false;

    // Brew state lives on the entity now; the UI just reflects it.
    public bool IsBrewing => _entity?.IsBrewing ?? false;
    public bool IsResultReady => _entity?.ResultReady ?? false;
    public float BrewProgress => _entity?.BrewProgress ?? 0f;

    public AlchemyStationMode Mode => _entity?.Mode ?? AlchemyStationMode.Solution;

    /// <summary>Batches the next brew produces: the selected <see cref="AlchemyBatchSizes.Presets"/> entry clamped to <see cref="MaxOutputCount"/>.</summary>
    public int OutputCount => AlchemyBatchSizes.Of(_batchPreset, MaxOutputCount);

    /// <summary>Largest <see cref="OutputCount"/> the current ingredients can support.</summary>
    public int MaxOutputCount { get; private set; } = 1;

    private int _batchPreset;

    private readonly AlchemyPreview _preview = new();

    public override void OnInitialize()
    {
        // Only the control cluster is built up-front; the slots are (re)built in Bind against the station's arrays.
        BuildControls();
        Append(_controls);
    }

    /// <summary>
    /// Binds this UI to a station, (re)creating the item slots against the entity's own arrays so edits write
    /// straight to the entity (which the tile renders and which keeps brewing once the UI closes).
    /// </summary>
    public void Bind(AdvancedAlchemyStationEntity entity)
    {
        if (_componentSlots[0] != null)
        {
            for (int i = 0; i < COMPONENT_SLOT_COUNT; i++) RemoveChild(_componentSlots[i]);
            RemoveChild(_inputSlot);
            RemoveChild(_outputSlot);
            RemoveChild(_bottleReturnSlot);
        }

        _entity = entity;

        for (int i = 0; i < COMPONENT_SLOT_COUNT; i++)
        {
            _componentSlots[i] = new AlchemyComponentSlot(entity.Components, i, this, BaseSlotScale);
            Append(_componentSlots[i]);
        }
        _inputSlot = new AlchemyInputSlot(entity.Input, this, BaseSlotScale);
        Append(_inputSlot);
        _outputSlot = new AlchemyOutputSlot(entity.Output, this, BaseSlotScale);
        Append(_outputSlot);
        _bottleReturnSlot = new AlchemyBottleReturnSlot(entity.BottleReturn, BaseSlotScale);
        Append(_bottleReturnSlot);

        _batchPreset = 0;
        _preview.Reset();
    }

    /// <summary>Releases the bound station on close. A lingering idle preview isn't a real item, so clear it.</summary>
    public void Release()
    {
        if (_entity != null && !_entity.IsBrewing && !_entity.ResultReady && !_entity.Output[0].IsAir)
            _entity.Output[0] = new Item();

        _entity = null;
        _preview.Reset();
    }

    /// <summary>A small fixed-size cluster (category label, batch count, mode button) that floats just above the chamber.</summary>
    private void BuildControls()
    {
        const float W = 120f;
        const float H = 44f;

        _controls = new UIElement
        {
            Width = StyleDimension.FromPixels(W),
            Height = StyleDimension.FromPixels(H),
        };

        _categoryLabel = new AlchemyCategoryLabel(0.8f)
        {
            Width = StyleDimension.FromPercent(1f),
            Height = StyleDimension.FromPixels(20f),
        };
        _controls.Append(_categoryLabel);

        // Batch row, centred under the category label, with the mode button at its right edge.
        const float ROW2 = 21f;
        const float BATCH_W = 52f;
        const float BATCH_H = 22f;
        const float MODE_BUTTON_SIZE = 22f;

        var batch = new AlchemyBatchButton(BATCH_W, BATCH_H, textScale: 0.8f)
        {
            Left = StyleDimension.FromPixels((W - BATCH_W) * 0.5f),
            Top = StyleDimension.FromPixels(ROW2),
            OnClick = CycleBatch,
            IsEnabled = CanCycleBatch,
            Count = () => OutputCount,
            IsCapped = () => OutputCount < AlchemyBatchSizes.Presets[_batchPreset],
        };
        _controls.Append(batch);

        var modeButton = new AlchemyModeButton(MODE_BUTTON_SIZE)
        {
            Left = StyleDimension.FromPixels(W - MODE_BUTTON_SIZE),
            Top = StyleDimension.FromPixels(ROW2 - 2f),
            OnClick = CycleMode,
            IsEnabled = CanCycleMode,
        };
        _controls.Append(modeButton);
    }

    /// <summary>
    /// Pins every widget to its spot on the machine in screen space. Slots scale with the game zoom so they
    /// stay glued to the sprite; the control cluster stays a fixed readable size, floating above the chamber.
    /// </summary>
    private void LayoutWorld()
    {
        if (StationWorldOrigin == null)
            return;

        Vector2 origin = StationWorldOrigin.Value;

        for (int k = 0; k < COMPONENT_SLOT_COUNT; k++)
            PlaceSlot(_componentSlots[k], origin + AdvancedAlchemyStationRenderer.ComponentSlotWorldOffset(k));

        PlaceSlot(_inputSlot, origin + AdvancedAlchemyStationRenderer.InputSlotWorldOffset);
        PlaceSlot(_outputSlot, origin + AdvancedAlchemyStationRenderer.OutputSlotWorldOffset);
        PlaceSlot(_bottleReturnSlot, origin + AdvancedAlchemyStationRenderer.BottleReturnSlotWorldOffset);

        if (_controls != null)
        {
            Vector2 screen = ToScreen(origin + AdvancedAlchemyStationRenderer.UiAnchorWorldOffset);
            _controls.Left.Set(screen.X - _controls.Width.Pixels * 0.5f, 0f);
            _controls.Top.Set(screen.Y - _controls.Height.Pixels - 6f, 0f);
            _controls.Recalculate();
        }
    }

    private static void PlaceSlot(BasicItemSlot slot, Vector2 worldCenter)
    {
        if (slot == null) return;

        Vector2 screen = ToScreen(worldCenter);
        slot.SetScale(BaseSlotScale * ZoomScale);

        float size = 68f * slot.Scale;
        slot.Left.Set(screen.X - size * 0.5f, 0f);
        slot.Top.Set(screen.Y - size * 0.5f, 0f);
        slot.Recalculate();
    }

    /// <summary>
    /// World → screen through the game's zoom matrix (zoom about the screen centre). This keeps a widget glued
    /// to its world spot while letting it grow with zoom, which suits this in-world style of UI.
    /// </summary>
    private static Vector2 ToScreen(Vector2 world)
        => Vector2.Transform(world - Main.screenPosition, Main.GameViewMatrix.ZoomMatrix);

    /// <summary>Current zoom factor, taken straight from the zoom matrix (matches <see cref="ToScreen"/>).</summary>
    private static float ZoomScale => Main.GameViewMatrix.ZoomMatrix.M11;

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        LayoutWorld();

        if (_entity == null)
            return;

        if (IsBrewing)
        {
            // The entity ticks the brew on its own
            // nothing to do here but display it.
        }
        else if (IsResultReady)
        {
            // Player collected the result (the output slot emptied it) -> return to preview mode.
            if (_entity.Output[0].IsAir)
            {
                _entity.ResultReady = false;
                _entity.SyncToClients();
            }
        }
        else
        {
            UpdatePreview();
        }

        UpdateLabels();
    }

    private void UpdatePreview()
    {
        _preview.Refresh(_entity.Input[0], _entity.Components, _entity.BottleReturn[0]);
        MaxOutputCount = _preview.MaxBatches;
        _entity.Output[0] = _preview.ItemFor(OutputCount);
    }

    /// <inheritdoc/>
    public int RequiredPotency(int index) => _preview.Resolution.RequiredPotencyFor(index);

    private bool CanCycleBatch() =>
        _entity != null && !IsBrewing && !IsResultReady && !_entity.Output[0].IsAir && MaxOutputCount > 1;

    private void CycleBatch() => _batchPreset = AlchemyBatchSizes.Next(_batchPreset, MaxOutputCount);

    private bool CanCycleMode() => _entity != null && !IsBrewing && !IsResultReady
        && AlchemyStationRules.NextMode(_entity.Mode, _entity.Input[0], _entity.Components) != _entity.Mode;

    private void CycleMode()
    {
        if (_entity != null)
            _entity.Mode = AlchemyStationRules.NextMode(_entity.Mode, _entity.Input[0], _entity.Components);
    }

    private void UpdateLabels()
    {
        if (_categoryLabel == null) return;

        if (IsBrewing)
        {
            _categoryLabel.SetText("Brewing", new Color(255, 240, 160));
            return;
        }

        if (IsResultReady)
        {
            _categoryLabel.SetText("Done", new Color(180, 240, 160));
            return;
        }

        BrewCategory category = _preview.Resolution.Composition.Category;
        _categoryLabel.SetText(category == BrewCategory.None ? string.Empty : BrewCategories.DisplayName(category),
            BrewCategories.ColorOf(category));
    }

    /// <summary>
    /// Called by <see cref="AlchemyOutputSlot"/> on left-click. Locks in the current preview and hands the brew
    /// to the entity, which runs it to completion whether or not the UI stays open.
    /// </summary>
    public void TryStartBrew()
    {
        if (_entity == null) return;
        if (IsBrewing || IsResultReady) return;
        if (_entity.Output[0].IsAir) return;
        if (_entity.Input[0].IsAir) return;

        AlchemyResolution resolution = _preview.Resolution;
        int count = OutputCount;
        if (resolution.ReturnsBottle && StationBottles.Capacity(_entity.BottleReturn[0]) < count) return;

        Item result = _entity.Output[0].Clone(); // the live preview, already scaled by count
        // The per-socket costs go with the brew: the entity runs it to completion on its own,
        // possibly after a save/load, so it can't re-derive them from a UI that's long gone.
        _entity.StartBrew(result, resolution.BrewTicks(AdvancedAlchemyStationBase.BrewSpeed), count, resolution.InputCost,
            resolution.ComponentCosts, resolution.ReturnsBottle);

        SoundEngine.PlaySound(Assets.SFX.Brewing.Asset);
        _entity.SyncToClients();
    }
}
