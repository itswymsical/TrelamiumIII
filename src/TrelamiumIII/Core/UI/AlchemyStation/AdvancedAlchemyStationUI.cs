using System;
using Terraria.Audio;
using Terraria.GameContent.UI.Elements;
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

    public const int BREW_DURATION_PER_OUTPUT = 30;

    /// <summary>Slot draw scale at zoom 1; multiplied by the game zoom each frame so slots track the sprite.</summary>
    private const float BaseSlotScale = .68f;

    private readonly AlchemyComponentSlot[] _componentSlots = new AlchemyComponentSlot[COMPONENT_SLOT_COUNT];
    private AlchemyVialSlot _vialSlot;
    private AlchemyOutputSlot _outputSlot;
    private AlchemyBottleReturnSlot _bottleReturnSlot;

    private UIElement _controls;
    private UIText _categoryLabel;
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
            RemoveChild(_bottleReturnSlot);
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
        _bottleReturnSlot = new AlchemyBottleReturnSlot(entity.BottleReturn);
        Append(_bottleReturnSlot);

        OutputCount = 1;
        _resolution = AlchemyResolution.None;
    }

    /// <summary>Releases the bound station on close. A lingering idle preview isn't a real item, so clear it.</summary>
    public void Release()
    {
        if (_entity != null && !_entity.IsBrewing && !_entity.ResultReady && !_entity.Output[0].IsAir)
            _entity.Output[0] = new Item();

        _entity = null;
        _resolution = AlchemyResolution.None;
    }

    /// <summary>A small fixed-size cluster (category label + batch count) that floats just above the chamber.</summary>
    private void BuildControls()
    {
        const float W = 120f;
        const float H = 44f;

        _controls = new UIElement
        {
            Width = StyleDimension.FromPixels(W),
            Height = StyleDimension.FromPixels(H),
        };

        _categoryLabel = new UIText(string.Empty, 0.8f, large: false)
        {
            HAlign = 0.5f,
            Top = StyleDimension.FromPixels(2f),
            TextColor = Color.White,
        };
        _controls.Append(_categoryLabel);

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
        _resolution = AlchemyRecipeRegistry.Resolve(_entity.Vial[0], _entity.Components);

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

        if (resolution.ReturnsBottle)
            max = Math.Min(max, StationBottles.Capacity(_entity.BottleReturn[0]));

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

    private void UpdateLabels()
    {
        if (_categoryLabel == null) return;

        _countLabel?.SetText($"x{OutputCount}");

        if (IsBrewing)
        {
            _categoryLabel.SetText("Brewing");
            _categoryLabel.TextColor = new Color(255, 240, 160);
            return;
        }

        if (IsResultReady)
        {
            _categoryLabel.SetText("Done");
            _categoryLabel.TextColor = new Color(180, 240, 160);
            return;
        }

        // Blank until the slotted ingredients resolve to a category.
        BrewCategory category = _resolution.Composition.Category;
        _categoryLabel.SetText(category == BrewCategory.None ? string.Empty : BrewCategories.DisplayName(category));
        _categoryLabel.TextColor = BrewCategories.ColorOf(category);
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
        if (_resolution.ReturnsBottle && StationBottles.Capacity(_entity.BottleReturn[0]) < count) return;

        Item result = _entity.Output[0].Clone(); // the live preview, already scaled by count
        // The per-socket costs go with the brew: the entity runs it to completion on its own,
        // possibly after a save/load, so it can't re-derive them from a UI that's long gone.
        _entity.StartBrew(result, BREW_DURATION_PER_OUTPUT * count, count, _resolution.ComponentCosts,
            _resolution.ReturnsBottle);

        SoundEngine.PlaySound(Assets.SFX.Brewing.Asset);
        _entity.SyncToClients();
    }
}