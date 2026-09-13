using System;
using Terraria.Audio;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;
using TrelamiumIII.Content.Alchemy.AlchemyStations;
using TrelamiumIII.Core.Alchemy;

namespace TrelamiumIII.Core.UI.AlchemyStation;

/// <summary>
/// Basic station panel: three component slots, a vial slot, an output slot and a bottle return slot. The output shows a
/// silhouette preview; clicking it starts a brew. The advanced station uses <see cref="AdvancedAlchemyStationUI"/>.
/// </summary>
internal class AlchemyStationUI : UIState, IAlchemyStationUI
{
    public const int COMPONENT_SLOT_COUNT = 3;

    public const int BREW_DURATION_PER_OUTPUT = 60;

    public readonly Item[] Components = new Item[COMPONENT_SLOT_COUNT];

    public readonly Item[] VialContainer = new Item[1];

    public readonly Item[] OutputContainer = new Item[1];

    public readonly Item[] BottleReturnContainer = new Item[1];

    private AlchemyStationPanel _panel;
    private UIText _categoryLabel;
    private UIText _hintLabel;
    private UIText _countLabel;

    /// <summary>World position (pixels) the panel anchors to, set by the system each frame; null leaves it put.</summary>
    public Vector2? WorldAnchor;

    /// <summary>Basic panel draws its own inline progress bar under the output slot.</summary>
    public bool ShowInlineBrewBar => true;

    /// <summary>True while a brew is running.</summary>
    public bool IsBrewing { get; private set; }

    /// <summary>True once a brew finishes.</summary>
    public bool IsResultReady { get; private set; }

    /// <summary>0..<see cref="_totalBrewTicks"/>, advances by 1 each frame while brewing.</summary>
    public int BrewTicks { get; private set; }

    /// <summary>0..1 progress through the active brew, used by the output slot's progress bar.</summary>
    public float BrewProgress => _totalBrewTicks > 0
        ? MathHelper.Clamp((float)BrewTicks / _totalBrewTicks, 0f, 1f)
        : 0f;

    /// <summary>How many output units the next/current brew produces. Clamped to <see cref="MaxOutputCount"/>.</summary>
    public int OutputCount { get; private set; } = 1;

    /// <summary>Largest <see cref="OutputCount"/> the current ingredients can support.</summary>
    public int MaxOutputCount { get; private set; } = 1;

    /// <summary>The result item locked in when the brew started. Used to populate the output on completion.</summary>
    private Item _brewSnapshot = new();

    /// <summary>Live preview: which recipe the slots resolve to and what each slot owes it.</summary>
    private AlchemyResolution _resolution = AlchemyResolution.None;

    /// <summary>Per-slot unit costs locked in when the brew started, so mid-brew edits can't change the bill.</summary>
    private int[] _brewCosts = Array.Empty<int>();

    /// <summary>Whether the active brew deposits bottles into <see cref="BottleReturnContainer" /> on completion.</summary>
    private bool _brewReturnsBottles;

    private int _totalBrewTicks;

    public override void OnInitialize()
    {
        for (int i = 0; i < COMPONENT_SLOT_COUNT; i++) Components[i] = new Item();
        VialContainer[0] = new Item();
        OutputContainer[0] = new Item();
        BottleReturnContainer[0] = new Item();

        BuildPanel();
    }

    private void BuildPanel()
    {
        // Layout follows the station mockup:
        // one centre line running solvent, solute row,
        // solution, with the linkage drawn behind the slots by AlchemyStationPanel:
        //
        //             [solvent]
        //     [solute]-[solute]-[solute]
        //              [solution]-[bottle]
        //              [-] xN [+]
        const float PANEL_W = 330f;
        const float PANEL_H = 292f;

        const float SLOT = 52f * 0.85f;      // BasicItemSlot's box at the scale these slots use
        const float SOLUTE_GAP = 25f;        // wide enough that the links read between the slots
        const float CENTER_X = PANEL_W * 0.5f;
        const float COLUMN_LEFT = CENTER_X - SLOT * 0.5f;   // the centred pair: solvent, solution

        const float SOLVENT_TOP = 52f;
        const float SOLUTE_TOP = SOLVENT_TOP + SLOT + 32f;
        const float OUTPUT_TOP = SOLUTE_TOP + SLOT + 26f;

        const float SOLUTE_STRIDE = SLOT + SOLUTE_GAP;
        const float ROW_LEFT = CENTER_X - (SLOT * 3f + SOLUTE_GAP * 2f) * 0.5f;

        _panel = new AlchemyStationPanel
        {
            Width = StyleDimension.FromPixels(PANEL_W),
            Height = StyleDimension.FromPixels(PANEL_H),
            HAlign = 0f,
            VAlign = 0f,
            Left = StyleDimension.FromPixels(80f),
            Top = StyleDimension.FromPixels(260f),
        };
        // No padding, so every offset below is a straight panel-local pixel and lines up with the
        // link coordinates handed to the panel.
        _panel.SetPadding(0f);
        Append(_panel);

        _categoryLabel = new UIText(string.Empty, 0.95f, large: false)
        {
            HAlign = 0.5f,
            Top = StyleDimension.FromPixels(8f),
            TextColor = Color.White,
        };
        _panel.Append(_categoryLabel);

        _hintLabel = new UIText("Place a solvent and solutes.", 0.78f, large: false)
        {
            HAlign = 0.5f,
            Top = StyleDimension.FromPixels(30f),
            TextColor = new Color(180, 180, 180),
        };
        _panel.Append(_hintLabel);

        var solvent = new AlchemyVialSlot(VialContainer, this)
        {
            Left = StyleDimension.FromPixels(COLUMN_LEFT),
            Top = StyleDimension.FromPixels(SOLVENT_TOP),
        };
        _panel.Append(solvent);

        for (int i = 0; i < COMPONENT_SLOT_COUNT; i++)
        {
            var slot = new AlchemyComponentSlot(Components, i, this)
            {
                Left = StyleDimension.FromPixels(ROW_LEFT + i * SOLUTE_STRIDE),
                Top = StyleDimension.FromPixels(SOLUTE_TOP),
            };
            _panel.Append(slot);
        }

        var output = new AlchemyOutputSlot(OutputContainer, this)
        {
            Left = StyleDimension.FromPixels(COLUMN_LEFT),
            Top = StyleDimension.FromPixels(OUTPUT_TOP),
        };
        _panel.Append(output);

        var bottleReturn = new AlchemyBottleReturnSlot(BottleReturnContainer)
        {
            Left = StyleDimension.FromPixels(COLUMN_LEFT + SOLUTE_STRIDE),
            Top = StyleDimension.FromPixels(OUTPUT_TOP),
        };
        _panel.Append(bottleReturn);

        // The flow behind the slots: solvent drops into the middle solute, the solutes chain
        // across, and the outer two funnel down into the solution.
        const float SOLUTE_MID_Y = SOLUTE_TOP + SLOT * 0.5f;
        const float SOLUTE_BOTTOM = SOLUTE_TOP + SLOT;
        const float FUNNEL_Y = OUTPUT_TOP + 5f;   // just inside the solution's top corners
        _panel.Links =
        [
            (new Vector2(CENTER_X, SOLVENT_TOP + SLOT), new Vector2(CENTER_X, SOLUTE_TOP)),

            (new Vector2(ROW_LEFT + SLOT, SOLUTE_MID_Y),
             new Vector2(ROW_LEFT + SOLUTE_STRIDE, SOLUTE_MID_Y)),
            (new Vector2(ROW_LEFT + SOLUTE_STRIDE + SLOT, SOLUTE_MID_Y),
             new Vector2(ROW_LEFT + SOLUTE_STRIDE * 2f, SOLUTE_MID_Y)),

            (new Vector2(ROW_LEFT + SLOT * 0.75f, SOLUTE_BOTTOM),
             new Vector2(COLUMN_LEFT, FUNNEL_Y)),
            (new Vector2(ROW_LEFT + SOLUTE_STRIDE * 2f + SLOT * 0.25f, SOLUTE_BOTTOM),
             new Vector2(COLUMN_LEFT + SLOT, FUNNEL_Y)),

            (new Vector2(COLUMN_LEFT + SLOT, OUTPUT_TOP + SLOT * 0.5f),
             new Vector2(COLUMN_LEFT + SOLUTE_STRIDE, OUTPUT_TOP + SLOT * 0.5f)),
        ];

        // Batch row, centred under the solution (below the progress bar the output slot draws).
        const float BUTTON_W = 18f;
        const float COUNT_TEXT_W = 28f;
        const float BATCH_TOP = OUTPUT_TOP + SLOT + 16f;
        const float BATCH_LEFT = CENTER_X - (BUTTON_W * 2f + COUNT_TEXT_W) * 0.5f;

        var minus = new AlchemyButton("-", width: BUTTON_W, height: BUTTON_W, scale: 0.95f)
        {
            Left = StyleDimension.FromPixels(BATCH_LEFT),
            Top = StyleDimension.FromPixels(BATCH_TOP),
            OnClick = () => AdjustOutputCount(-1),
            IsEnabled = () => CanAdjustCount() && OutputCount > 1,
            Tooltip = () => "Decrease batch",
        };
        _panel.Append(minus);

        _countLabel = new UIText("x1", 0.85f, large: false)
        {
            Left = StyleDimension.FromPixels(BATCH_LEFT + BUTTON_W),
            Top = StyleDimension.FromPixels(BATCH_TOP + 2f),
            Width = StyleDimension.FromPixels(COUNT_TEXT_W),
            Height = StyleDimension.FromPixels(BUTTON_W),
            TextColor = Color.White,
        };
        _countLabel.TextOriginX = 0.5f;
        _panel.Append(_countLabel);

        var plus = new AlchemyButton("+", width: BUTTON_W, height: BUTTON_W, scale: 0.95f)
        {
            Left = StyleDimension.FromPixels(BATCH_LEFT + BUTTON_W + COUNT_TEXT_W),
            Top = StyleDimension.FromPixels(BATCH_TOP),
            OnClick = () => AdjustOutputCount(1),
            IsEnabled = () => CanAdjustCount() && OutputCount < MaxOutputCount,
            Tooltip = () => "Increase batch",
        };
        _panel.Append(plus);
    }

    /// <summary>
    /// Anchors the panel above its <see cref="WorldAnchor"/> (the station's output spout), tracking the tile
    /// as the camera moves and clamping to the screen. Uses the game-view transform + physical pixels, matching
    /// how the rest of the mod's world-anchored UI is positioned.
    /// </summary>
    private void RepositionPanel()
    {
        if (_panel == null || WorldAnchor == null)
            return;

        Vector2 screen = Vector2.Transform(WorldAnchor.Value - Main.screenPosition, Main.GameViewMatrix.EffectMatrix);

        float w = _panel.Width.Pixels;
        float h = _panel.Height.Pixels;
        float left = screen.X - w * 0.5f;
        float top = screen.Y - h - 24f;

        left = MathHelper.Clamp(left, 8f, Main.screenWidth - w - 8f);
        top = MathHelper.Clamp(top, 8f, Main.screenHeight - h - 8f);

        _panel.Left.Set(left, 0f);
        _panel.Top.Set(top, 0f);
        _panel.Recalculate();
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        RepositionPanel();

        // Mouse-click failsafe
        if (_panel != null && _panel.ContainsPoint(Main.MouseScreen))
            Main.LocalPlayer.mouseInterface = true;

        if (IsBrewing)
        {
            BrewTicks++;
            OutputContainer[0] = _brewSnapshot;

            if (BrewTicks >= _totalBrewTicks)
                CompleteBrew();
        }
        else if (IsResultReady)
        {
            // Player has taken the result = return to preview mode.
            if (OutputContainer[0].IsAir)
            {
                IsResultReady = false;
                OutputCount = 1;
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
        _resolution = AlchemyRecipeRegistry.Resolve(VialContainer[0], Components);

        MaxOutputCount = ComputeMaxOutputCount(_resolution);
        if (OutputCount > MaxOutputCount) OutputCount = MaxOutputCount;
        if (OutputCount < 1) OutputCount = 1;

        var preview = _resolution.Output;
        if (!preview.IsAir)
            preview.stack = Math.Max(1, preview.stack) * OutputCount;

        OutputContainer[0] = preview;
    }

    /// <summary>
    /// Largest batch the slotted stacks can pay for. Each solute is divided by what the recipe
    /// draws from it, so a potency-5 slot needs five per unit, and the batch is capped so it
    /// can't overflow the output item's own stack limit.
    /// </summary>
    private int ComputeMaxOutputCount(AlchemyResolution resolution)
    {
        if (!resolution.Found) return 1;

        int max = resolution.MaxBatches(VialContainer[0], Components);

        int baseStack = Math.Max(1, resolution.Output.stack);
        if (resolution.Output.maxStack > 0)
            max = Math.Min(max, Math.Max(1, resolution.Output.maxStack / baseStack));

        if (resolution.ReturnsBottle)
            max = Math.Min(max, StationBottles.Capacity(BottleReturnContainer[0]));

        return Math.Max(1, max);
    }

    /// <inheritdoc/>
    public int SoluteCost(int index) => _resolution.CostFor(index);

    private bool CanAdjustCount() => !IsBrewing && !IsResultReady && !OutputContainer[0].IsAir;

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
            int secsLeft = (int)Math.Ceiling((_totalBrewTicks - BrewTicks) / 60f);
            _categoryLabel.SetText("Brewing...");
            _categoryLabel.TextColor = new Color(255, 240, 160);
            _hintLabel.SetText($"{secsLeft}s remaining");
            return;
        }

        if (IsResultReady)
        {
            _categoryLabel.SetText("Done");
            _categoryLabel.TextColor = new Color(180, 240, 160);
            _hintLabel.SetText("Chemicals produced!");
            return;
        }

        // Blank until the slotted ingredients resolve to a category.
        BrewCategory category = _resolution.Composition.Category;
        _categoryLabel.SetText(category == BrewCategory.None ? string.Empty : BrewCategories.DisplayName(category));
        _categoryLabel.TextColor = BrewCategories.ColorOf(category);
        _hintLabel.SetText(IdleHint());
    }

    /// <summary>Idle hint. An under-stacked solute takes priority, since the slot itself doesn't show the requirement.</summary>
    private string IdleHint()
    {
        if (!OutputContainer[0].IsAir)
            return "Click the result to brew.";

        var missing = _resolution.Shortfall;

        if (missing.Exists)
            return $"Needs {missing.Required} {Lang.GetItemNameValue(missing.ItemType)} ({missing.Have} slotted).";

        return VialContainer[0].IsAir ? "Place a solvent and solutes." : "Add solutes to the solvent.";
    }

    /// <summary>
    /// Called by <see cref="AlchemyOutputSlot"/> on left-click. Snapshots the current
    /// preview and starts the brew timer.
    /// </summary>
    public void TryStartBrew()
    {
        if (IsBrewing || IsResultReady) return;
        if (OutputContainer[0].IsAir) return;
        if (VialContainer[0].IsAir) return;

        if (_resolution.ReturnsBottle && StationBottles.Capacity(BottleReturnContainer[0]) < Math.Max(1, OutputCount)) return;

        _brewSnapshot = OutputContainer[0].Clone();
        _brewCosts = (int[])_resolution.ComponentCosts.Clone();
        _brewReturnsBottles = _resolution.ReturnsBottle;
        _totalBrewTicks = BREW_DURATION_PER_OUTPUT * Math.Max(1, OutputCount);
        BrewTicks = 0;
        IsBrewing = true;

        SoundEngine.PlaySound(Assets.SFX.Brewing.Asset);
    }

    private void CompleteBrew()
    {
        var result = _brewSnapshot.Clone();
        int count = OutputCount;

        // The solvent is one per unit: a bottle in, a bottle out.
        if (!VialContainer[0].IsAir)
        {
            VialContainer[0].stack -= count;
            if (VialContainer[0].stack <= 0) VialContainer[0].TurnToAir();
        }

        if (_brewReturnsBottles)
            StationBottles.Deposit(BottleReturnContainer, count);

        // Solutes go by what the recipe drew from each slot (its potency), times the batch.
        for (int i = 0; i < Components.Length; i++)
        {
            if (Components[i].IsAir) continue;
            int perUnit = i < _brewCosts.Length ? Math.Max(1, _brewCosts[i]) : 1;
            Components[i].stack -= perUnit * count;
            if (Components[i].stack <= 0) Components[i].TurnToAir();
        }

        // Result lives in the output slot until the player picks it up.
        OutputContainer[0] = result;
        IsResultReady = true;

        // Almanac discovery  only flag real recipe outputs, not fallback brews.
        if (!result.IsAir && AlmanacIndex.HasRecipeFor(result.type))
            AlmanacDiscoverySystem.Unlock(result.type);

        IsBrewing = false;
        BrewTicks = 0;
        _totalBrewTicks = 0;
        _brewSnapshot = new Item();

        SoundEngine.PlaySound(SoundID.ResearchComplete);
        SoundEngine.PlaySound(Assets.SFX.BrewingComplete.Asset);
    }

    /// <summary>Drop all current slot contents into the player inventory. Used by the transient (simple) station on close.</summary>
    public void ReturnAllToPlayer()
    {
        var player = Main.LocalPlayer;
        var src = player.GetSource_Misc("AlchemyStationClose");

        // Hand back a finished result before resetting state. Previews are not real items and never get returned.
        if (IsResultReady && !OutputContainer[0].IsAir)
            player.QuickSpawnItem(src, OutputContainer[0], OutputContainer[0].stack);

        if (!VialContainer[0].IsAir)
            player.QuickSpawnItem(src, VialContainer[0], VialContainer[0].stack);

        if (!BottleReturnContainer[0].IsAir)
            player.QuickSpawnItem(src, BottleReturnContainer[0], BottleReturnContainer[0].stack);

        for (int i = 0; i < Components.Length; i++)
            if (!Components[i].IsAir)
                player.QuickSpawnItem(src, Components[i], Components[i].stack);

        ClearSlots();
        ResetBrewState();
    }

    /// <summary>Copies a bound station entity's contents into the live slots. Used when an advanced station opens.</summary>
    public void LoadFromEntity(AdvancedAlchemyStationEntity entity)
    {
        for (int i = 0; i < Components.Length && i < entity.Components.Length; i++)
            Components[i] = entity.Components[i] ?? new Item();
        VialContainer[0] = entity.Vial[0] ?? new Item();
        OutputContainer[0] = entity.Output[0] ?? new Item();
        BottleReturnContainer[0] = entity.BottleReturn[0] ?? new Item();
        ResetBrewState();
    }

    /// <summary>Mirrors the live slot contents back onto the bound entity so the world tile renders them.</summary>
    public void SaveToEntity(AdvancedAlchemyStationEntity entity)
    {
        for (int i = 0; i < Components.Length && i < entity.Components.Length; i++)
            entity.Components[i] = Components[i];
        entity.Vial[0] = VialContainer[0];
        entity.Output[0] = OutputContainer[0];
        entity.BottleReturn[0] = BottleReturnContainer[0];
        entity.IsBrewing = IsBrewing;
    }

    /// <summary>Resets transient state and clears slots without returning anything (advanced station contents live in the entity).</summary>
    public void ResetForClose()
    {
        ClearSlots();
        ResetBrewState();
    }

    private void ClearSlots()
    {
        for (int i = 0; i < Components.Length; i++)
            Components[i] = new Item();
        VialContainer[0] = new Item();
        OutputContainer[0] = new Item();
        BottleReturnContainer[0] = new Item();
    }

    private void ResetBrewState()
    {
        IsBrewing = false;
        IsResultReady = false;
        BrewTicks = 0;
        _totalBrewTicks = 0;
        _brewSnapshot = new Item();
        _brewCosts = Array.Empty<int>();
        _brewReturnsBottles = false;
        _resolution = AlchemyResolution.None;
        OutputCount = 1;
    }
}
