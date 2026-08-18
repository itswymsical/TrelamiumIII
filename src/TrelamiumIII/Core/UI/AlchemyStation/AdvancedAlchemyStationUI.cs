using System;
using Terraria.Audio;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;
using TrelamiumIII.Common.Systems.Alchemy;
using TrelamiumIII.Content.Alchemy.AlchemyStations;

namespace TrelamiumIII.Core.UI.AlchemyStation;

/// <summary>
/// A "world" overlayed UI State for the Advanced Alchemy Station Tileset. The basic early-game station uses <see cref="AlchemyStationUI"/>.
/// </summary>
internal class AdvancedAlchemyStationUI : UIState, IAlchemyStationUI
{
    public const int COMPONENT_SLOT_COUNT = 3;

    public const int BREW_DURATION_PER_OUTPUT = 30;

    /// <summary>Slot draw scale at zoom 1; multiplied by the game zoom each frame so slots track the sprite.</summary>
    private const float BaseSlotScale = .68f;

    private readonly AlchemyComponentSlot[] _componentSlots = new AlchemyComponentSlot[COMPONENT_SLOT_COUNT];
    private AlchemyVialSlot _vialSlot;
    private AlchemyOutputSlot _outputSlot;

    private UIElement _controls;
    private UIText _modeLabel;
    private UIText _countLabel;

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

    /// <summary>How many output units the next brew produces. Clamped to <see cref="MaxOutputCount"/>.</summary>
    public int OutputCount { get; private set; } = 1;

    /// <summary>Largest <see cref="OutputCount"/> the current ingredients can support.</summary>
    public int MaxOutputCount { get; private set; } = 1;

    /// <summary>Live preview: which recipe the sockets resolve to and what each owes it.</summary>
    private AlchemyResolution _resolution = AlchemyResolution.None;

    /// <summary>Player override for the active mode; <c>null</c> = auto-detect from vial.</summary>
    private AlchemyMode? _modeOverride;

    /// <summary>Last seen vial type. Mode override resets when this changes.</summary>
    private int _lastVialType;

    /// <summary>Currently active mode.</summary>
    public AlchemyMode CurrentMode => _entity == null
        ? AlchemyMode.None
        : _modeOverride ?? AlchemyIngredients.GetModeForSolvent(_entity.Vial[0]);

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
            RemoveChild(_vialSlot);
            RemoveChild(_outputSlot);
        }

        _entity = entity;

        for (int i = 0; i < COMPONENT_SLOT_COUNT; i++)
        {
            _componentSlots[i] = new AlchemyComponentSlot(entity.Components, i, this);
            Append(_componentSlots[i]);
        }
        _vialSlot = new AlchemyVialSlot(entity.Vial, this);
        Append(_vialSlot);
        _outputSlot = new AlchemyOutputSlot(entity.Output, this);
        Append(_outputSlot);

        OutputCount = 1;
        _modeOverride = null;
        _resolution = AlchemyResolution.None;
        _lastVialType = entity.Vial[0].IsAir ? 0 : entity.Vial[0].type;
    }

    /// <summary>Releases the bound station on close. A lingering idle preview isn't a real item, so clear it.</summary>
    public void Release()
    {
        if (_entity != null && !_entity.IsBrewing && !_entity.ResultReady && !_entity.Output[0].IsAir)
            _entity.Output[0] = new Item();

        _entity = null;
        _resolution = AlchemyResolution.None;
    }

    /// <summary>A small fixed-size cluster (mode + batch count) that floats just above the chamber.</summary>
    private void BuildControls()
    {
        const float W = 120f;
        const float H = 44f;

        _controls = new UIElement
        {
            Width = StyleDimension.FromPixels(W),
            Height = StyleDimension.FromPixels(H),
        };

        _modeLabel = new UIText("Idle", 0.8f, large: false)
        {
            Left = StyleDimension.FromPixels(2f),
            Top = StyleDimension.FromPixels(2f),
            TextColor = Color.White,
        };
        _controls.Append(_modeLabel);

        var modeCycle = new AlchemyButton(">", width: 28f, height: 28f, scale: 1.85f)
        {
            Left = StyleDimension.FromPixels(W - 20f),
            Top = StyleDimension.FromPixels(0f),
            OnClick = CycleMode,
            IsEnabled = () => !IsBrewing && !IsResultReady && _entity != null && !_entity.Vial[0].IsAir,
            Tooltip = () => "Cycle mode",
        };
        _controls.Append(modeCycle);

        // Batch count row, centred under the mode label.
        const float ROW2 = 22f;
        const float BUTTON_W = 18f;
        const float COUNT_TEXT_W = 28f;
        float rowLeft = (W - (BUTTON_W * 2 + COUNT_TEXT_W)) * 0.5f;

        var minus = new AlchemyButton("-", width: BUTTON_W, height: BUTTON_W, scale: 0.95f)
        {
            Left = StyleDimension.FromPixels(rowLeft),
            Top = StyleDimension.FromPixels(ROW2),
            OnClick = () => AdjustOutputCount(-1),
            IsEnabled = () => CanAdjustCount() && OutputCount > 1,
            Tooltip = () => "Decrease batch",
        };
        _controls.Append(minus);

        _countLabel = new UIText("x1", 0.85f, large: false)
        {
            Left = StyleDimension.FromPixels(rowLeft + BUTTON_W),
            Top = StyleDimension.FromPixels(ROW2 + 2f),
            Width = StyleDimension.FromPixels(COUNT_TEXT_W),
            Height = StyleDimension.FromPixels(BUTTON_W),
            TextColor = Color.White,
        };
        _countLabel.TextOriginX = 0.5f;
        _controls.Append(_countLabel);

        var plus = new AlchemyButton("+", width: BUTTON_W, height: BUTTON_W, scale: 0.95f)
        {
            Left = StyleDimension.FromPixels(rowLeft + BUTTON_W + COUNT_TEXT_W),
            Top = StyleDimension.FromPixels(ROW2),
            OnClick = () => AdjustOutputCount(1),
            IsEnabled = () => CanAdjustCount() && OutputCount < MaxOutputCount,
            Tooltip = () => "Increase batch",
        };
        _controls.Append(plus);
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

        PlaceSlot(_vialSlot, origin + AdvancedAlchemyStationRenderer.VialSlotWorldOffset);
        PlaceSlot(_outputSlot, origin + AdvancedAlchemyStationRenderer.OutputSlotWorldOffset);

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

        // Reset mode override when the vial changes type.
        int curVialType = _entity.Vial[0].IsAir ? 0 : _entity.Vial[0].type;
        if (curVialType != _lastVialType)
        {
            _modeOverride = null;
            _lastVialType = curVialType;
        }

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
                OutputCount = 1;
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
        _resolution = AlchemyRecipeRegistry.Resolve(_entity.Vial[0], _entity.Components, CurrentMode);

        MaxOutputCount = ComputeMaxOutputCount(_resolution);
        if (OutputCount > MaxOutputCount) OutputCount = MaxOutputCount;
        if (OutputCount < 1) OutputCount = 1;

        var preview = _resolution.Output;
        if (!preview.IsAir)
            preview.stack = Math.Max(1, preview.stack) * OutputCount;

        _entity.Output[0] = preview;
    }

    /// <summary>
    /// Largest batch the socketed stacks can pay for. Each solute is divided by what the recipe
    /// draws from it (its potency), and the batch is capped so it can't overflow the output
    /// item's own stack limit.
    /// </summary>
    private int ComputeMaxOutputCount(AlchemyResolution resolution)
    {
        if (!resolution.Found) return 1;

        int max = resolution.MaxBatches(_entity.Vial[0], _entity.Components);

        int baseStack = Math.Max(1, resolution.Output.stack);
        if (resolution.Output.maxStack > 0)
            max = Math.Min(max, Math.Max(1, resolution.Output.maxStack / baseStack));

        return Math.Max(1, max);
    }

    /// <inheritdoc/>
    public int SoluteCost(int index) => _resolution.CostFor(index);

    private bool CanAdjustCount() => _entity != null && !IsBrewing && !IsResultReady && !_entity.Output[0].IsAir;

    private void AdjustOutputCount(int delta)
    {
        int next = OutputCount + delta;
        if (next < 1) next = 1;
        if (next > MaxOutputCount) next = MaxOutputCount;
        OutputCount = next;
    }

    /// <summary>Step to the next mode in the cycle (Synthesis -> Augmentation -> Utility -> ...).</summary>
    public void CycleMode()
    {
        if (_entity == null || IsBrewing || IsResultReady) return;
        if (_entity.Vial[0].IsAir) return;

        var current = CurrentMode;
        AlchemyMode next = current switch
        {
            AlchemyMode.Synthesis => AlchemyMode.Augmentation,
            AlchemyMode.Augmentation => AlchemyMode.Alchemy,
            AlchemyMode.Alchemy => AlchemyMode.Synthesis,
            _ => AlchemyMode.Synthesis,
        };
        _modeOverride = next;
    }

    private void UpdateLabels()
    {
        if (_modeLabel == null) return;

        _countLabel?.SetText($"x{OutputCount}");

        if (IsBrewing)
        {
            _modeLabel.SetText("Brewing");
            _modeLabel.TextColor = new Color(255, 240, 160);
            return;
        }

        if (IsResultReady)
        {
            _modeLabel.SetText("Done");
            _modeLabel.TextColor = new Color(180, 240, 160);
            return;
        }

        switch (CurrentMode)
        {
            case AlchemyMode.Augmentation:
                _modeLabel.SetText("Augment");
                _modeLabel.TextColor = new Color(255, 200, 120);
                break;
            case AlchemyMode.Synthesis:
                _modeLabel.SetText("Synthesis");
                _modeLabel.TextColor = new Color(140, 200, 255);
                break;
            case AlchemyMode.Alchemy:
                _modeLabel.SetText("Utility");
                _modeLabel.TextColor = new Color(255, 130, 130);
                break;
            default:
                _modeLabel.SetText("Idle");
                _modeLabel.TextColor = Color.White;
                break;
        }
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
        if (_entity.Vial[0].IsAir) return;

        int count = Math.Max(1, OutputCount);
        Item result = _entity.Output[0].Clone(); // the live preview, already scaled by count
        // The per-socket costs go with the brew: the entity runs it to completion on its own,
        // possibly after a save/load, so it can't re-derive them from a UI that's long gone.
        _entity.StartBrew(result, BREW_DURATION_PER_OUTPUT * count, count, _resolution.ComponentCosts);

        SoundEngine.PlaySound(Assets.SFX.Brewing.Asset);
        _entity.SyncToClients();
    }
}