using Terraria.UI;
using TrelamiumIII.Content.Alchemy.AlchemyStations;
using TrelamiumIII.Core.Alchemy;

namespace TrelamiumIII.Core.UI.AlchemyStation;

/// <summary>
/// World overlay for the Advanced Alchemy Station: slots pinned to the machine's sprite, editing the bound
/// <see cref="AdvancedAlchemyStationEntity"/>, which runs brews whether or not the UI is open.
/// </summary>
internal sealed class AdvancedAlchemyStationUI : AlchemyStationUIBase
{
    /// <summary>Slot draw scale at zoom 1; multiplied by the game zoom each frame so slots track the sprite.</summary>
    private const float BaseSlotScale = .85f;

    private readonly AlchemyComponentSlot[] _componentSlots = new AlchemyComponentSlot[COMPONENT_SLOT_COUNT];
    private AlchemyInputSlot _inputSlot;
    private AlchemyOutputSlot _outputSlot;
    private AlchemyBottleReturnSlot _bottleReturnSlot;

    private UIElement _controls;
    private AlchemyCategoryLabel _categoryLabel;

    /// <summary>The station being edited, or null while closed.</summary>
    private AdvancedAlchemyStationEntity _entity;

    /// <summary>World position (pixels) of the bound station's origin tile top-left; set by the system each frame.</summary>
    public Vector2? StationWorldOrigin;

    /// <summary>The overlay draws its progress bar on the machine, not in the output slot.</summary>
    public override bool ShowInlineBrewBar => false;

    public override bool IsBrewing => _entity?.IsBrewing ?? false;

    public override bool IsResultReady => _entity?.ResultReady ?? false;

    public override float BrewProgress => _entity?.BrewProgress ?? 0f;

    public override AlchemyStationMode Mode => _entity?.Mode ?? AlchemyStationMode.Solution;

    protected override Item[]? Input => _entity?.Input;

    protected override Item[]? Components => _entity?.Components;

    protected override Item[]? Output => _entity?.Output;

    protected override Item[]? BottleReturn => _entity?.BottleReturn;

    protected override void SetMode(AlchemyStationMode mode)
    {
        if (_entity != null)
            _entity.Mode = mode;
    }

    public override void OnInitialize()
    {
        // The slots are (re)built in Bind against the station's arrays.
        BuildControls();
        Append(_controls);
    }

    /// <summary>
    /// Binds this UI to a station, (re)creating the item slots against the entity's own arrays so edits write
    /// straight to the entity.
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

        ResetPreview();
    }

    /// <summary>Releases the bound station on close, clearing an idle preview since it is not a real item.</summary>
    public void Release()
    {
        if (_entity != null && !_entity.IsBrewing && !_entity.ResultReady && !_entity.Output[0].IsAir)
            _entity.Output[0] = new Item();

        _entity = null;
        Preview.Reset();
    }

    /// <summary>A fixed-size cluster (category label, batch count, mode button) that floats above the chamber.</summary>
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
            IsCapped = () => OutputCount < AlchemyBatchSizes.Presets[BatchPreset],
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
    /// Pins every widget to its spot on the machine in screen space. Slots scale with the game zoom; the control
    /// cluster stays a fixed readable size above the chamber.
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

    /// <summary>World to screen through the game's zoom matrix, so widgets stay on their world spot and grow with zoom.</summary>
    private static Vector2 ToScreen(Vector2 world)
        => Vector2.Transform(world - Main.screenPosition, Main.GameViewMatrix.ZoomMatrix);

    /// <summary>Current zoom factor, from the zoom matrix used by <see cref="ToScreen"/>.</summary>
    private static float ZoomScale => Main.GameViewMatrix.ZoomMatrix.M11;

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        LayoutWorld();

        if (_entity == null)
            return;

        // The entity ticks an active brew itself.
        if (!IsBrewing)
        {
            if (!IsResultReady)
            {
                UpdatePreview();
            }
            else if (_entity.Output[0].IsAir)
            {
                // The player collected the result: back to previewing.
                _entity.ResultReady = false;
                _entity.SyncToClients();
            }
        }

        UpdateLabels();
    }

    private void UpdateLabels()
    {
        if (_categoryLabel == null) return;

        if (IsBrewing)
            _categoryLabel.SetText("Brewing", new Color(255, 240, 160));
        else if (IsResultReady)
            _categoryLabel.SetText("Done", new Color(180, 240, 160));
        else
            ShowCategory(_categoryLabel);
    }

    /// <summary>Hands the brew to the entity with its costs, since it may finish after a save/load with no UI open.</summary>
    protected override void StartBrew(AlchemyResolution resolution, int count)
    {
        Item result = _entity.Output[0].Clone();

        _entity.StartBrew(result, resolution.BrewTicks(AdvancedAlchemyStationBase.BrewSpeed), count, resolution.InputCost,
            resolution.ComponentCosts, resolution.ReturnsBottle);

        _entity.SyncToClients();
    }
}
