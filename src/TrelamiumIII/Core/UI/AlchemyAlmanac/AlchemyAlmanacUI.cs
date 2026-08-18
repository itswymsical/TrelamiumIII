using Microsoft.Xna.Framework.Input;
using ReLogic.Content;
using TrelamiumIII.Common.Systems.Alchemy;
using TrelamiumIII.Common.Systems.Alchemy.Brewing;
using System.Collections.Generic;
using System.Linq;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.UI;
using TrelamiumIII.Core.Books.Lore;
using TrelamiumIII.Core.UI;
using TrelamiumIII.Common.UI;

namespace TrelamiumIII.Core.UI.AlchemyAlmanac;

/// <summary>
/// book that displays every alchemy output the player has
/// discovered.
/// <para>
/// The UI has two view modes: Browse and Detail. Page flips animate transitions
/// between any two spreads in either view.
/// </para>
/// </summary>
public class AlchemyAlmanacUI : IInGameNotification
{
    #region Constants
    public const int PAGE_WIDTH = 290;
    public const int PAGE_HEIGHT = 430;
    public const int GUTTER = 6;

    private const float OPEN_SPEED = 0.042f;
    private const float FLIP_SPEED = 0.05f;

    private const float MARGIN_X = 18f;
    private const float MARGIN_Y = 22f;
    private const float TITLE_SCALE = 1.05f;
    private const float HEADER_SCALE = 0.9f;
    private const float BODY_SCALE = 0.78f;

    public const int ICON_COLS = 4;
    public const int ICON_ROWS = 6;
    public const int ICONS_PER_PAGE = ICON_COLS * ICON_ROWS;       // 24
    public const int ICONS_PER_SPREAD = ICONS_PER_PAGE * 2;        // 48

    public const int VARIANTS_FIRST_SPREAD = 6;                    // detail spread 0 right page
    public const int VARIANTS_PER_PAGE = 6;                        // each detail page after the first

    // Notes guide layout
    private const int NOTE_TOC_ROWS_PER_PAGE = 12;
    private const int NOTE_TOC_ROWS_PER_SPREAD = NOTE_TOC_ROWS_PER_PAGE * 2;
    private const int NOTE_LINES_PER_PAGE = 16;                    // prose lines per page
    private const int NOTE_TITLE_LINES = 2;                        // lines the chapter title reserves on page 0
    private const float NOTE_SCALE = 0.8f;
    private const float NOTE_TOC_ROW_H = 28f;                      // table-of-contents row pitch

    public const float ICON_CELL_SIZE = 60f;
    public const float ICON_CELL_PAD = 4f;

    private const float OVERLAY_BUTTON_W = 110f;
    private const float OVERLAY_BUTTON_H = 28f;
    private const float OVERLAY_BUTTON_GAP = 4f;

    public const float UI_SCALE = 1f;

    private const float CLOSE_BTN = 24f;
    private const float PAGE_ARROW_PAD = 2f;
    #endregion

    #region View / State
    public enum ViewMode { Browse, Detail }

    /// <summary>Top-level section of the book. Each is its own spread space.</summary>
    public enum AlmanacCategory { Recipes, Ingredients, Notes }

    private enum BookState { Opening, Reading, Flipping, Closing }
    private enum FlipDir { Forward, Backward }

    /// <summary>
    /// Identifies a spread uniquely. <see cref="Category"/> + <see cref="View"/> +
    /// <see cref="Index"/> + <see cref="DetailItem"/> form the key; the render system keys
    /// every page off this so a flip between any two spreads composes correctly.
    /// </summary>
    public readonly record struct SpreadKey(AlmanacCategory Category, ViewMode View, int Index, int DetailItem);

    private BookState state = BookState.Opening;
    private FlipDir flipDir = FlipDir.Forward;

    private SpreadKey current;
    private SpreadKey next;

    /// <summary>
    /// Last browse spread index per category; restored when leaving Detail (including when a
    /// query is cleared) or returning to a category.
    /// </summary>
    private readonly int[] _lastBrowseIndex = { 0, 0, 0 };

    private float openProgress;
    private float pageFlipProgress;

    // IInGameNotification updates after vanilla UI handles input, so vanilla has
    // usually already consumed Main.mouseLeftRelease/RightRelease. We track edge
    // transitions ourselves to detect fresh clicks reliably.
    private bool _wasLeftDown = true;
    private bool _wasRightDown = true;

    private bool LeftPressed => Main.mouseLeft && !_wasLeftDown;
    private bool RightPressed => Main.mouseRight && !_wasRightDown;
    #endregion

    #region Filter state
    public AlchemyMode? ModeFilter { get; private set; }   // null = All
    public Item SearchItem { get; private set; } = new();   // air = no search

    /// <summary>Cached filtered output type list. Invalidated by mode-filter changes.</summary>
    private List<int> _filteredOutputs;
    public List<int> FilteredOutputs => _filteredOutputs ??= BuildFiltered();
    public int FilteredCount => FilteredOutputs.Count;

    /// <summary>Cached recipe list for the current detail item.</summary>
    private List<AlchemyRecipe> _detailRecipes;
    public List<AlchemyRecipe> DetailRecipes
    {
        get
        {
            if (_detailRecipes != null) return _detailRecipes;
            if (current.View != ViewMode.Detail || current.DetailItem <= 0)
                return _detailRecipes = new List<AlchemyRecipe>();
            return _detailRecipes = AlmanacIndex.RecipesProducing(current.DetailItem).ToList();
        }
    }

    /// <summary>
    /// Cached "used in" recipe list for the current ingredient detail item. The Ingredients tab
    /// owns the "recipes using this" lookup. Only recipes whose output is already discovered
    /// are included.
    /// </summary>
    private List<AlchemyRecipe> _detailUses;
    public List<AlchemyRecipe> DetailIngredientUses
    {
        get
        {
            if (_detailUses != null) return _detailUses;
            if (current.Category != AlmanacCategory.Ingredients
                || current.View != ViewMode.Detail || current.DetailItem <= 0)
                return _detailUses = new List<AlchemyRecipe>();
            return _detailUses = UnlockedUses(current.DetailItem).ToList();
        }
    }

    /// <summary>Every ingredient item type, sorted by name (Ingredients-category browse list).</summary>
    public IReadOnlyList<int> AllIngredients => AlchemyIngredients.AllIngredientTypes;

    /// <summary>The browse grid source list for a category (Recipes/Ingredients only).</summary>
    private IReadOnlyList<int> BrowseListFor(AlmanacCategory cat) =>
        cat == AlmanacCategory.Ingredients ? AllIngredients : FilteredOutputs;

    /// <summary>Wrapped body lines for the current Notes chapter (prose), cached by index.</summary>
    private List<string> _noteLines;
    private int _noteLinesFor = -1;

    /// <summary>
    /// Infusion the query slot pins onto a material lookup, so a queried "Bottled Water (Inert)"
    /// lists only the recipes keyed to that infusion. None whenever the slot holds anything else.
    /// </summary>
    private ChemicalType QueryInfusion(int itemType) =>
        !SearchItem.IsAir && SearchItem.type == itemType
            ? SolutionGlobalItem.GetInfusion(SearchItem)
            : ChemicalType.None;

    /// <summary>Discovered recipes that use the given ingredient (honouring a queried infusion).</summary>
    private IEnumerable<AlchemyRecipe> UnlockedUses(int itemType) =>
        itemType <= 0
            ? Enumerable.Empty<AlchemyRecipe>()
            : AlmanacIndex.RecipesUsing(itemType, QueryInfusion(itemType))
                .Where(r => AlmanacDiscoverySystem.IsUnlocked(AlmanacIndex.ResolvedOutputType(r)));

    /// <summary>Count of discovered recipes that use the given ingredient.</summary>
    private int UnlockedUsesCount(int itemType) => UnlockedUses(itemType).Count();

    private List<int> BuildFiltered()
    {
        IEnumerable<int> source = ModeFilter.HasValue
            ? AlmanacIndex.OutputsByMode(ModeFilter.Value)
            : AlmanacIndex.AllOutputTypes;

        return source
            .OrderBy(t => Lang.GetItemNameValue(t))
            .ToList();
    }

    private void InvalidateFilter()
    {
        _filteredOutputs = null;
        // Keep the player on whatever spread they were on; only clamp if the new
        // filtered list is shorter so they aren't stranded past the last page.
        if (current.Category == AlmanacCategory.Recipes && current.View == ViewMode.Browse && current.Index > 0)
        {
            int total = TotalSpreadsFor(AlmanacCategory.Recipes, ViewMode.Browse, 0);
            int clamped = Math.Clamp(current.Index, 0, Math.Max(0, total - 1));
            if (clamped != current.Index)
                current = current with { Index = clamped };
        }
    }

    public void SetModeFilter(AlchemyMode? mode)
    {
        if (ModeFilter == mode) return;
        ModeFilter = mode;
        InvalidateFilter();
        SoundEngine.PlaySound(SoundID.MenuTick);
    }
    #endregion

    #region Singleton + lifecycle
    public static AlchemyAlmanacUI Current { get; private set; }

    public bool ShouldBeRemoved => state == BookState.Closing && openProgress <= 0f;

    public AlchemyAlmanacUI()
    {
        Current = this;
        current = new SpreadKey(AlmanacCategory.Notes, ViewMode.Browse, 0, 0);
        next = current;

        AlchemyAlmanacSystem.InitTargets(PAGE_WIDTH, PAGE_HEIGHT);
    }
    internal static void ResetForWorldChange()
    {
        Current = null;
    }

    public static void ShowAlmanac()
    {
        if (Current != null) return;

        if (LoreBookUI.Current != null) return;

        var n = new AlchemyAlmanacUI();
        InGameNotificationsTracker.AddNotification(n);
    }
    #endregion

    #region Public render hints (read by AlchemyAlmanacSystem)
    public bool IsForwardFlip => flipDir == FlipDir.Forward;
    public float PageTurnProgress => state == BookState.Flipping ? pageFlipProgress : 0f;
    public float OpenProgress => state == BookState.Opening ? 1f - openProgress : 0f;
    public float CloseProgress => state == BookState.Closing ? 1f - openProgress : 0f;
    public float Opacity => state is BookState.Opening or BookState.Closing ? openProgress : 1f;

    public SpreadKey CurrentSpread => current;
    public SpreadKey NextSpread => next;

    /// <summary>True when <see cref="current"/> is the cover/title pair (Recipes browse, index 0).</summary>
    public bool OnCoverSpread => current.Category == AlmanacCategory.Notes && current.View == ViewMode.Browse && current.Index == 0;
    #endregion

    #region IInGameNotification anchor
    public void PushAnchor(ref Vector2 positionAnchorBottom) { }
    #endregion

    #region Update / state machine
    public void Update()
    {
        if (Main.gameMenu)
        {
            state = BookState.Closing;
            _wasLeftDown = Main.mouseLeft;
            _wasRightDown = Main.mouseRight;
            return;
        }

        // Modeless overlay: only capture the mouse when it's actually over the book, so the
        // player can still move, mine, and use items in the world around it.
        if (state != BookState.Closing && IsHoveringBookUI())
            Main.LocalPlayer.mouseInterface = true;

        switch (state)
        {
            case BookState.Opening:
                openProgress = Math.Min(1f, openProgress + OPEN_SPEED);
                if (openProgress >= 1f) state = BookState.Reading;
                break;

            case BookState.Reading:
                HandleInput();
                break;

            case BookState.Flipping:
                pageFlipProgress = Math.Min(1f, pageFlipProgress + FLIP_SPEED);
                if (pageFlipProgress >= 1f) OnFlipComplete();
                break;

            case BookState.Closing:
                openProgress = Math.Max(0f, openProgress - OPEN_SPEED);
                if (ShouldBeRemoved) Current = null;
                break;
        }

        _wasLeftDown = Main.mouseLeft;
        _wasRightDown = Main.mouseRight;
    }

    private void HandleInput()
    {
        UpdateCategoryTabs();
        if (current.Category == AlmanacCategory.Recipes && current.View == ViewMode.Browse)
            UpdateModeTabs();
        // The Query slot is live in Recipes (look up a brew) and Ingredients (look up a material).
        if (current.Category == AlmanacCategory.Recipes || current.Category == AlmanacCategory.Ingredients)
            UpdateSearchSlot();

        var mouse = Main.MouseScreen.ToPoint();

        var leftRect = GetPageRect(false);
        var rightRect = GetPageRect(true);
        bool overSpread = leftRect.Contains(mouse) || rightRect.Contains(mouse);

        bool leftClick = LeftPressed;
        bool rightClick = RightPressed;

        // Notes table of contents: highlight the hovered chapter and tick when it changes.
        int hov = current.Category == AlmanacCategory.Notes && current.View == ViewMode.Browse
            ? HitTestNoteRow(mouse) : -1;
        if (hov != _hoveredNote)
        {
            if (hov >= 0) SoundEngine.PlaySound(SoundID.MenuTick);
            _hoveredNote = hov;
        }

        // Close + page-turn buttons take priority over anything beneath them.
        RecomputeBookButtons();
        if (leftClick)
        {
            if (_closeButton.Contains(mouse)) { CloseBook(); return; }
            if (_prevButton.Contains(mouse)) { BeginFlip(FlipDir.Backward); return; }
            if (_nextButton.Contains(mouse)) { BeginFlip(FlipDir.Forward); return; }
        }

        if (rightClick && overSpread)
        {
            if (current.View == ViewMode.Detail) BeginViewSwitchToBrowse();
            return;
        }

        // Click a TOC entry. Notes open immediately; in Recipes a locked entry just ticks (so the
        // player doesn't accidentally page-flip on top of it); ingredients are always viewable.
        if (leftClick && current.View == ViewMode.Browse)
        {
            if (current.Category == AlmanacCategory.Notes)
            {
                int note = HitTestNoteRow(mouse);
                if (note >= 0) { BeginNotesJump(note); return; }
            }
            else
            {
                int hitType = HitTestTOCIcon(mouse);
                if (hitType > 0)
                {
                    bool viewable = current.Category == AlmanacCategory.Ingredients
                        ? AlmanacDiscoverySystem.IsIngredientVisible(hitType)
                        : AlmanacDiscoverySystem.IsUnlocked(hitType);
                    if (viewable) BeginViewSwitchToDetail(hitType);
                    else SoundEngine.PlaySound(SoundID.MenuTick);
                    return;
                }
            }
        }

        // Page flip by clicking the left/right half of the spread. In Detail view the left page
        // steps back through variant spreads, except on the first spread where there's nothing
        // to flip back to, so it backs out to the TOC instead (right-click exits from anywhere).
        if (leftClick && overSpread)
        {
            bool forward = rightRect.Contains(mouse);
            if (!forward && current.View == ViewMode.Detail && current.Index == 0)
            {
                BeginViewSwitchToBrowse();
                return;
            }
            BeginFlip(forward ? FlipDir.Forward : FlipDir.Backward);
            return;
        }
    }

    private void CloseBook()
    {
        state = BookState.Closing;

        // return item if left in slot
        Main.LocalPlayer.QuickSpawnItem(
        Main.LocalPlayer.GetSource_Misc("AlmanacSearchClear"),
        SearchItem, SearchItem.stack);

        SoundEngine.PlaySound(Assets.SFX.BookClose.Asset, Main.LocalPlayer.Center);
    }

    private void BeginFlip(FlipDir dir)
    {
        if (state != BookState.Reading) return;

        int total = TotalSpreadsFor(current.Category, current.View, current.DetailItem);
        int delta = dir == FlipDir.Forward ? 1 : -1;
        int nextIdx = current.Index + delta;

        SpreadKey target;
        if (current.View == ViewMode.Browse)
        {
            // Browse is one continuous read: flipping off the end of a section continues into
            // the next (Notes -> Ingredients -> Recipes) and vice versa.
            if (nextIdx >= total)
            {
                var nc = AdjacentBrowseCategory(current.Category, +1);
                if (nc == null) return;
                target = new SpreadKey(nc.Value, ViewMode.Browse, 0, 0);
            }
            else if (nextIdx < 0)
            {
                var pc = AdjacentBrowseCategory(current.Category, -1);
                if (pc == null) return;
                int last = TotalSpreadsFor(pc.Value, ViewMode.Browse, 0) - 1;
                target = new SpreadKey(pc.Value, ViewMode.Browse, Math.Max(0, last), 0);
            }
            else target = current with { Index = nextIdx };
        }
        else
        {
            if (nextIdx < 0 || nextIdx >= total) return;
            target = current with { Index = nextIdx };
        }

        flipDir = dir;
        next = target;
        _noteLinesFor = -1;
        pageFlipProgress = 0f;
        state = BookState.Flipping;

        SoundEngine.PlaySound(Assets.SFX.PageFlip.Asset, Main.LocalPlayer.Center);
    }

    private void BeginViewSwitchToDetail(int detailItem)
    {
        if (state != BookState.Reading) return;

        // Remember which browse spread we leave from so "back" lands the player here again.
        if (current.View == ViewMode.Browse)
            _lastBrowseIndex[(int)current.Category] = current.Index;

        flipDir = FlipDir.Forward;
        next = new SpreadKey(current.Category, ViewMode.Detail, 0, detailItem);
        // Detail caches must be cleared since DetailItem changed.
        _detailRecipes = null;
        _detailUses = null;
        _noteLinesFor = -1;
        pageFlipProgress = 0f;
        state = BookState.Flipping;

        SoundEngine.PlaySound(Assets.SFX.PageFlip.Asset, Main.LocalPlayer.Center);
    }

    private void BeginViewSwitchToBrowse()
    {
        if (state != BookState.Reading) return;

        // Return to the browse spread we left from (clamped to range).
        var cat = current.Category;
        int total = TotalSpreadsFor(cat, ViewMode.Browse, 0);
        int idx = Math.Clamp(_lastBrowseIndex[(int)cat], 0, Math.Max(0, total - 1));

        flipDir = FlipDir.Backward;
        next = new SpreadKey(cat, ViewMode.Browse, idx, 0);
        _detailRecipes = null;
        _detailUses = null;
        _noteLinesFor = -1;
        pageFlipProgress = 0f;
        state = BookState.Flipping;

        SoundEngine.PlaySound(Assets.SFX.PageFlip.Asset, Main.LocalPlayer.Center);
    }

    // Category tabs are quick-nav into the chronological read: each jumps to its section start.
    private void BeginCategorySwitch(AlmanacCategory cat)
    {
        if (state != BookState.Reading) return;
        if (cat == current.Category && current.View == ViewMode.Browse) return;

        flipDir = Array.IndexOf(_browseOrder, cat) >= Array.IndexOf(_browseOrder, current.Category)
            ? FlipDir.Forward : FlipDir.Backward;
        next = new SpreadKey(cat, ViewMode.Browse, 0, 0);
        _detailRecipes = null;
        _detailUses = null;
        _noteLinesFor = -1;
        pageFlipProgress = 0f;
        state = BookState.Flipping;

        SoundEngine.PlaySound(Assets.SFX.PageFlip.Asset, Main.LocalPlayer.Center);
    }

    private void OnFlipComplete()
    {
        current = next;
        pageFlipProgress = 0f;
        state = BookState.Reading;
        // Clear detail caches; we may have landed on a different detail item.
        _detailRecipes = null;
        _detailUses = null;
        _noteLinesFor = -1;
    }

    public int TotalSpreadsFor(AlmanacCategory category, ViewMode view, int detailItem)
    {
        if (view == ViewMode.Browse)
        {
            // Notes is the chronological read: cover + table of contents + chapters.
            if (category == AlmanacCategory.Notes)
                return NotesBrowseTotal();

            // The cover now lives in Notes, so Recipes/Ingredients are just their TOC spreads.
            int items = BrowseListFor(category).Count;
            return items <= 0 ? 1 : (items + ICONS_PER_SPREAD - 1) / ICONS_PER_SPREAD;
        }

        if (category == AlmanacCategory.Notes)
            return 1;   // Notes has no separate detail view; chapters live in the browse flow.

        int rows = category == AlmanacCategory.Recipes
            ? (detailItem > 0 ? AlmanacIndex.RecipesProducing(detailItem).Count() : 0)
            : UnlockedUsesCount(detailItem);
        if (rows <= VARIANTS_FIRST_SPREAD) return 1;
        return 1 + (rows - VARIANTS_FIRST_SPREAD + (VARIANTS_PER_PAGE * 2) - 1) / (VARIANTS_PER_PAGE * 2);
    }
    #endregion

    #region Category + mode tabs
    private static readonly (string label, AlmanacCategory cat)[] _categoryTabs =
    {
        ("Notes", AlmanacCategory.Notes),
        ("Ingredients", AlmanacCategory.Ingredients),
        ("Recipes", AlmanacCategory.Recipes),
    };

    // Mode filter, shown only while browsing the Recipes category.
    private static readonly (string label, AlchemyMode? mode)[] _modeTabs =
    {
        ("All", null),
        ("Synthesis", AlchemyMode.Synthesis),
        ("Augmentation", AlchemyMode.Augmentation),
        ("Alchemy", AlchemyMode.Alchemy),
    };

    private const float MODE_TAB_W = 96f;
    private const float MODE_TAB_H = 26f;
    private const float MODE_TAB_GAP = 4f;

    private readonly Rectangle[] _categoryTabBounds = new Rectangle[_categoryTabs.Length];
    private readonly Rectangle[] _modeTabBounds = new Rectangle[_modeTabs.Length];

    private Rectangle _closeButton;
    private Rectangle _prevButton;
    private Rectangle _nextButton;

    /// <summary>Hovered Notes TOC row (global note index), -1 when none. Drives highlight + tick.</summary>
    private int _hoveredNote = -1;

    private static Asset<Texture2D> _arrowNext;
    private static Asset<Texture2D> _arrowPrev;

    // Category tabs: horizontal row centred above the spread.
    private void RecomputeCategoryTabBounds()
    {
        var origin = GetSpreadOrigin();
        float s = UI_SCALE;
        float spreadW = (PAGE_WIDTH * 2 + GUTTER) * s;
        float bw = OVERLAY_BUTTON_W * s, bh = OVERLAY_BUTTON_H * s, gap = OVERLAY_BUTTON_GAP * s;
        float totalW = bw * _categoryTabs.Length + gap * (_categoryTabs.Length - 1);
        float startX = origin.X + (spreadW - totalW) * 0.5f;
        float y = origin.Y - bh - 8f * s;

        for (int i = 0; i < _categoryTabs.Length; i++)
            _categoryTabBounds[i] = new Rectangle(
                (int)(startX + i * (bw + gap)), (int)y, (int)bw, (int)bh);
    }

    // Mode tabs: vertical stack hugging the left edge of the spread.
    private void RecomputeModeTabBounds()
    {
        var origin = GetSpreadOrigin();
        float s = UI_SCALE;
        float tw = MODE_TAB_W * s, th = MODE_TAB_H * s, gap = MODE_TAB_GAP * s;
        float totalH = th * _modeTabs.Length + gap * (_modeTabs.Length - 1);
        float y0 = origin.Y + (PAGE_HEIGHT * s - totalH) * 0.5f;
        float x = origin.X - tw - 6f * s;

        for (int i = 0; i < _modeTabs.Length; i++)
            _modeTabBounds[i] = new Rectangle(
                (int)x, (int)(y0 + i * (th + gap)), (int)tw, (int)th);
    }

    private void UpdateCategoryTabs()
    {
        RecomputeCategoryTabBounds();
        var mouse = Main.MouseScreen.ToPoint();

        for (int i = 0; i < _categoryTabs.Length; i++)
        {
            if (!_categoryTabBounds[i].Contains(mouse)) continue;

            Main.LocalPlayer.mouseInterface = true;
            if (LeftPressed) BeginCategorySwitch(_categoryTabs[i].cat);
            return;
        }
    }

    private void UpdateModeTabs()
    {
        RecomputeModeTabBounds();
        var mouse = Main.MouseScreen.ToPoint();

        for (int i = 0; i < _modeTabs.Length; i++)
        {
            if (!_modeTabBounds[i].Contains(mouse)) continue;

            Main.LocalPlayer.mouseInterface = true;
            if (LeftPressed) SetModeFilter(_modeTabs[i].mode);
            return;
        }
    }
    #endregion

    #region Search slot
    private Rectangle _searchSlotBounds;

    private void RecomputeSearchSlotBounds()
    {
        var origin = GetSpreadOrigin();
        float s = UI_SCALE;
        float spreadW = (PAGE_WIDTH * 2 + GUTTER) * s;
        int slotSize = (int)(52 * 0.85f * s);

        _searchSlotBounds = new Rectangle(
            (int)(origin.X + spreadW + 16f * s),
            (int)(origin.Y + 8f * s),
            slotSize, slotSize);
    }

    private void UpdateSearchSlot()
    {
        RecomputeSearchSlotBounds();
        var mouse = Main.MouseScreen.ToPoint();
        if (!_searchSlotBounds.Contains(mouse)) return;

        Main.LocalPlayer.mouseInterface = true;

        // Hover tooltip for whatever is in the slot.
        if (!SearchItem.IsAir)
        {
            Item s = SearchItem;
            ItemSlot.MouseHover(ref s, ItemSlot.Context.InventoryItem);
        }

        if (LeftPressed)
        {
            Item incoming = Main.mouseItem;
            // Both tabs are "look this up" queries, and each accepts what its own entries cover:
            // Ingredients takes any item (even a material no recipe touches, like Inert Solution)
            // and lists what it goes into; Recipes takes a discovered output and opens its
            // variants, so it only accepts entries it could open by click.
            bool acceptIncoming = incoming.IsAir
                || current.Category == AlmanacCategory.Ingredients
                || AlmanacDiscoverySystem.IsUnlocked(incoming.type) && AlmanacIndex.HasRecipeFor(incoming.type);

            if (!acceptIncoming)
            {
                SoundEngine.PlaySound(SoundID.MenuTick);
                return;
            }

            // Manual swap: ItemSlot.Handle is unreliable here because vanilla
            // consumes Main.mouseLeftRelease before this notification updates.
            int prevType = SearchItem.IsAir ? 0 : SearchItem.type;
            var prevInfusion = SolutionGlobalItem.GetInfusion(SearchItem);
            (Main.mouseItem, SearchItem) = (SearchItem, Main.mouseItem);
            int curType = SearchItem.IsAir ? 0 : SearchItem.type;

            // Infusion counts as part of the query: swapping plain water for inert water is a
            // different lookup even though the item type didn't change.
            if (curType != prevType || SolutionGlobalItem.GetInfusion(SearchItem) != prevInfusion)
            {
                // Physically holding a material counts as having obtained it, so a queried
                // item reveals its "?" slot (and its detail page is viewable immediately).
                // Non-materials (the Ingredients slot accepts any item) don't count.
                if (curType > 0 && (AlchemyIngredients.IsSubstanceType(curType) || AlchemyIngredients.IsSolventType(curType)))
                    AlmanacDiscoverySystem.ObtainIngredient(curType);

                if (curType > 0) BeginViewSwitchToDetail(curType);
                else OnSearchCleared();
            }
            SoundEngine.PlaySound(SoundID.Grab);
        }
        else if (RightPressed && !SearchItem.IsAir)
        {
            // Right-click clears the slot. Picks up if the cursor is empty,
            // otherwise hands the item back to the player's inventory.
            if (Main.mouseItem.IsAir)
            {
                Main.mouseItem = SearchItem;
            }
            else
            {
                Main.LocalPlayer.QuickSpawnItem(
                    Main.LocalPlayer.GetSource_Misc("AlmanacSearchClear"),
                    SearchItem, SearchItem.stack);
            }
            SearchItem = new Item();
            OnSearchCleared();
            SoundEngine.PlaySound(SoundID.Grab);
        }
    }

    /// <summary>
    /// Clearing the query backs out of the entry it jumped to, which lands the player on the
    /// browse spread they queried from.
    /// </summary>
    private void OnSearchCleared()
    {
        if (current.View == ViewMode.Detail && current.Category != AlmanacCategory.Notes)
            BeginViewSwitchToBrowse();
    }
    #endregion

    #region Book buttons (close + page turn)
    private void RecomputeBookButtons()
    {
        var origin = GetSpreadOrigin();
        float s = UI_SCALE;
        float spreadW = (PAGE_WIDTH * 2 + GUTTER) * s;
        float spreadH = PAGE_HEIGHT * s;

        _closeButton = new Rectangle((int)(origin.X + PAGE_WIDTH / 2), (int)(origin.Y + PAGE_HEIGHT + 9), 300, 40);

        // Page-turn arrows: bottom outer corners of the spread.
        float pad = PAGE_ARROW_PAD * s;
        var prev = ArrowTex(false);
        if (prev != null)
            _prevButton = new Rectangle(
                (int)(origin.X + pad),
                (int)(origin.Y + spreadH - prev.Height * s - pad),
                (int)(prev.Width * s), (int)(prev.Height * s));
        var next = ArrowTex(true);
        if (next != null)
            _nextButton = new Rectangle(
                (int)(origin.X + spreadW - next.Width * s - pad),
                (int)(origin.Y + spreadH - next.Height * s - pad),
                (int)(next.Width * s), (int)(next.Height * s));
    }

    /// <summary>True when the cursor is over any interactive part of the book (so we capture it).</summary>
    private bool IsHoveringBookUI()
    {
        var mouse = Main.MouseScreen.ToPoint();
        if (GetPageRect(false).Contains(mouse) || GetPageRect(true).Contains(mouse)) return true;

        RecomputeCategoryTabBounds();
        foreach (var b in _categoryTabBounds) if (b.Contains(mouse)) return true;

        RecomputeBookButtons();
        if (_closeButton.Contains(mouse) || _prevButton.Contains(mouse) || _nextButton.Contains(mouse)) return true;

        if (current.Category == AlmanacCategory.Recipes && current.View == ViewMode.Browse)
        {
            RecomputeModeTabBounds();
            foreach (var b in _modeTabBounds) if (b.Contains(mouse)) return true;
        }
        if (current.Category == AlmanacCategory.Recipes || current.Category == AlmanacCategory.Ingredients)
        {
            RecomputeSearchSlotBounds();
            if (_searchSlotBounds.Contains(mouse)) return true;
        }
        return false;
    }

    private static Texture2D ArrowTex(bool next)
    {
        if (next)
        {
            _arrowNext ??= ModContent.Request<Texture2D>($"{Assets.Textures.UI.Jobs.Next.KEY}", AssetRequestMode.ImmediateLoad);
            return _arrowNext.Value;
        }
        _arrowPrev ??= ModContent.Request<Texture2D>($"{Assets.Textures.UI.Jobs.Prev.KEY}", AssetRequestMode.ImmediateLoad);
        return _arrowPrev.Value;
    }
    #endregion

    #region Hit testing
    private int HitTestTOCIcon(Point mouse)
    {
        if (current.View != ViewMode.Browse) return 0;
        if (current.Category == AlmanacCategory.Notes) return 0;

        bool right;
        if (GetPageRect(false).Contains(mouse)) right = false;
        else if (GetPageRect(true).Contains(mouse)) right = true;
        else return 0;
        int pageOffset = right ? ICONS_PER_PAGE : 0;

        // Grid math is in unscaled page-local space; convert the mouse in.
        var local = ToPageLocal(mouse, right);
        var (gridX, gridY) = GetTOCGridOrigin(new Rectangle(0, 0, PAGE_WIDTH, PAGE_HEIGHT));
        float relX = local.X - gridX;
        float relY = local.Y - gridY;
        if (relX < 0 || relY < 0) return 0;

        float stride = ICON_CELL_SIZE + ICON_CELL_PAD;
        int col = (int)(relX / stride);
        int row = (int)(relY / stride);
        if (col < 0 || col >= ICON_COLS || row < 0 || row >= ICON_ROWS) return 0;

        var list = BrowseListFor(current.Category);
        int globalIdx = current.Index * ICONS_PER_SPREAD + pageOffset + row * ICON_COLS + col;
        if (globalIdx < 0 || globalIdx >= list.Count) return 0;
        return list[globalIdx];
    }

    /// <summary>
    /// Hit-tests the icon boxes inside detail-mode variant rows (solvent / components /
    /// output) by reconstructing <see cref="DrawVariantRow"/>'s layout. Returns the hovered
    /// item type (0 = none) and the infusion it requires or carries, for suffixed names.
    /// </summary>
    private int HitTestVariantIcon(Point mouse, out ChemicalType infusion)
    {
        infusion = ChemicalType.None;

        if (current.View != ViewMode.Detail) return 0;
        if (current.Category == AlmanacCategory.Notes) return 0;

        bool right;
        if (GetPageRect(false).Contains(mouse)) right = false;
        else if (GetPageRect(true).Contains(mouse)) right = true;
        else return 0;

        // Which rows this page shows (mirrors DrawPageToTarget's detail routing).
        int rowStart, maxCount;
        if (current.Index == 0)
        {
            if (!right) return 0;   // left page of the first detail spread is the header
            rowStart = 0;
            maxCount = VARIANTS_FIRST_SPREAD;
        }
        else
        {
            rowStart = VARIANTS_FIRST_SPREAD + (current.Index - 1) * (VARIANTS_PER_PAGE * 2)
                     + (right ? VARIANTS_PER_PAGE : 0);
            maxCount = VARIANTS_PER_PAGE;
        }

        var rows = current.Category == AlmanacCategory.Recipes
            ? DetailRecipes
            : DetailIngredientUses;

        // Row geometry (mirrors DrawDetailVariants / DrawVariantRow).
        const float ROW_HEIGHT = 56f, CARD_H = 50f, ICON = 36f;
        float rowTop = MARGIN_Y + 12f;

        var local = ToPageLocal(mouse, right);
        float rel = local.Y - rowTop;
        if (rel < 0f) return 0;
        int ri = (int)(rel / ROW_HEIGHT);
        if (rel - ri * ROW_HEIGHT > CARD_H) return 0;
        if (ri >= maxCount || rowStart + ri >= rows.Count) return 0;

        var recipe = rows[rowStart + ri];
        float cy = rowTop + ri * ROW_HEIGHT + 7f;
        if (local.Y < cy || local.Y > cy + ICON) return 0;

        // Walk the icon boxes exactly as DrawVariantRow advances them.
        float cx = MARGIN_X + 6f;
        if (local.X >= cx && local.X < cx + ICON)
        {
            infusion = recipe.SolventInfusion;
            return recipe.SolventType;
        }
        cx += ICON + 8f - 4f + 12f;   // icon advance + "+" separator

        for (int i = 0; i < recipe.Components.Length; i++)
        {
            var matcher = recipe.Components[i];
            if (local.X >= cx && local.X < cx + ICON)
            {
                infusion = matcher.Infusion;
                if (matcher.Group == null) return matcher.ExactType;

                // Group slots cycle their displayed member; name the one on screen.
                var members = matcher.Group.ItemTypes;
                if (members.Count == 0) return 0;
                return members.ElementAt((int)(Main.GameUpdateCount / 30) % members.Count);
            }
            cx += ICON + 8f - 4f;
            if (i < recipe.Components.Length - 1) cx += 12f;
        }

        cx += 18f;   // arrow
        if (local.X >= cx && local.X < cx + ICON)
        {
            Item output = recipe.CreateOutput?.Invoke() ?? new Item();
            if (output.IsAir) return 0;
            infusion = SolutionGlobalItem.GetInfusion(output);
            return output.type;
        }
        return 0;
    }

    private static (float x, float y) GetTOCGridOrigin(Rectangle pageRect)
    {
        float gridW = ICON_COLS * ICON_CELL_SIZE + (ICON_COLS - 1) * ICON_CELL_PAD;
        float gridH = ICON_ROWS * ICON_CELL_SIZE + (ICON_ROWS - 1) * ICON_CELL_PAD;
        float x = pageRect.X + (pageRect.Width - gridW) * 0.5f;
        float y = pageRect.Y + (pageRect.Height - gridH) * 0.5f + 6f;
        return (x, y);
    }
    #endregion

    #region Layout
    private static Vector2 GetSpreadOrigin()
    {
        float w = (PAGE_WIDTH * 2 + GUTTER) * UI_SCALE;
        float h = PAGE_HEIGHT * UI_SCALE;
        return new Vector2(Main.screenWidth / 2f - w / 2f, Main.screenHeight / 2f - h / 2f);
    }

    private static Vector2 GetPageOrigin(bool right)
    {
        var o = GetSpreadOrigin();
        return right ? o + new Vector2((PAGE_WIDTH + GUTTER) * UI_SCALE, 0f) : o;
    }

    private static Rectangle GetPageRect(bool right)
    {
        var o = GetPageOrigin(right);
        return new Rectangle((int)o.X, (int)o.Y, (int)(PAGE_WIDTH * UI_SCALE), (int)(PAGE_HEIGHT * UI_SCALE));
    }

    /// <summary>Convert a screen point to unscaled page-local coords (0..PAGE_WIDTH/HEIGHT).</summary>
    private static Vector2 ToPageLocal(Point mouse, bool right)
    {
        var o = GetPageOrigin(right);
        return new Vector2((mouse.X - o.X) / UI_SCALE, (mouse.Y - o.Y) / UI_SCALE);
    }
    #endregion

    #region Page content drawing
    /// <summary>
    /// Draws the contents of one side of the given spread in target-local coordinates
    /// (origin = 0,0 = top-left of the page). Called by
    /// <see cref="AlchemyAlmanacSystem.BuildFrame"/> with the target already bound.
    /// </summary>
    public void DrawPageToTarget(SpriteBatch sb, SpreadKey key, bool isLeft)
    {
        if (key.Category == AlmanacCategory.Notes)
        {
            if (key.View == ViewMode.Browse)
            {
                int kind = NotesBrowseAt(key.Index, out int a, out int b);
                if (kind == 0) { if (!isLeft) DrawTitleContent(sb); return; }  // cover: left = CoverTarget
                if (kind == 1) { DrawNotesTOC(sb, a, isLeft); return; }        // table of contents, spread a
                var ch = AlmanacNotes.At(a);                                   // chapter a, page-spread b
                if (ch != null && ch.Kind == NoteKind.Principles) { DrawPrinciplesPage(sb, ch, isLeft); return; }
                if (ch != null && ch.Kind == NoteKind.Chemistry) { DrawChemistryPage(sb, ch, isLeft); return; }
                DrawNoteProsePage(sb, a, b * 2 + (isLeft ? 0 : 1));
            }
            return;
        }

        bool recipes = key.Category == AlmanacCategory.Recipes;

        if (key.View == ViewMode.Browse)
        {
            // The cover lives in Notes now, so these sections start straight at the TOC grid.
            int globalStart = key.Index * ICONS_PER_SPREAD + (isLeft ? 0 : ICONS_PER_PAGE);
            DrawTOCContent(sb, key.Category, globalStart);
            return;
        }

        // Detail view: left of the first spread is the header; the rest are recipe rows.
        if (key.Index == 0)
        {
            if (isLeft)
            {
                if (recipes) DrawDetailHeader(sb, key.DetailItem);
                else DrawIngredientHeader(sb, key.DetailItem);
                return;
            }
            if (recipes) DrawDetailVariants(sb, key.DetailItem, 0, VARIANTS_FIRST_SPREAD);
            else DrawIngredientUses(sb, key.DetailItem, 0, VARIANTS_FIRST_SPREAD);
            return;
        }

        int rowStart = VARIANTS_FIRST_SPREAD + (key.Index - 1) * (VARIANTS_PER_PAGE * 2) + (isLeft ? 0 : VARIANTS_PER_PAGE);
        if (recipes) DrawDetailVariants(sb, key.DetailItem, rowStart, VARIANTS_PER_PAGE);
        else DrawIngredientUses(sb, key.DetailItem, rowStart, VARIANTS_PER_PAGE);
    }

    /// <summary>
    /// Draws the gilded title imprint onto the (already-cleared) cover surface.
    /// Called once by <see cref="AlchemyAlmanacSystem"/> to populate <c>CoverTarget</c>.
    /// </summary>
    public static void DrawCoverImprint(SpriteBatch sb)
    {
        var font = FontAssets.MouseText.Value;
        const string title = "OOPSIEES";
        const string subtitle = "NO SPRITE YET :PPP";

        Vector2 t1 = font.MeasureString(title) * 0.95f;
        Vector2 t2 = font.MeasureString(subtitle) * 1.4f;

        Utils.DrawBorderString(sb, title,
            new Vector2((PAGE_WIDTH - t1.X) * 0.5f, PAGE_HEIGHT * 0.36f),
            new Color(255, 255, 60), 0.95f);

        Utils.DrawBorderString(sb, subtitle,
            new Vector2((PAGE_WIDTH - t2.X) * 0.5f, PAGE_HEIGHT * 0.36f + 24f),
            new Color(255, 255, 80), 1.4f);

        var px = TextureAssets.MagicPixel.Value;
        var rule = new Color(150, 110, 60, 200);
        sb.Draw(px, new Rectangle((int)(PAGE_WIDTH * 0.22f), (int)(PAGE_HEIGHT * 0.30f), (int)(PAGE_WIDTH * 0.56f), 1), rule);
        sb.Draw(px, new Rectangle((int)(PAGE_WIDTH * 0.22f), (int)(PAGE_HEIGHT * 0.62f), (int)(PAGE_WIDTH * 0.56f), 1), rule);
    }

    private static void DrawTitleContent(SpriteBatch sb)
    {
        var font = FontAssets.MouseText.Value;

        string title = ModExtensions.GetLocalizationValue(TrelamiumIII.Instance, "AlmanacUI.Title");
        Vector2 size = font.MeasureString(title) * TITLE_SCALE;
        Utils.DrawBorderString(sb, title,
            new Vector2((PAGE_WIDTH - size.X) * 0.5f, PAGE_HEIGHT * 0.16f),
            Color.White, TITLE_SCALE);

        // Decorative line
        var px = TextureAssets.MagicPixel.Value;
        sb.Draw(px,
            new Rectangle((int)MARGIN_X, (int)(PAGE_HEIGHT * 0.16f + size.Y + 6f),
                          PAGE_WIDTH - (int)MARGIN_X * 2, 1),
            Color.SaddleBrown * 0.5f);

        // Discovery stats: brewed recipes, then ingredients revealed in the index.
        int recipeTotal = AlmanacIndex.AllOutputTypes.Count;
        int recipeKnown = AlmanacDiscoverySystem.Count;
        string recipeStat = $"{recipeKnown} / {recipeTotal} {ModExtensions.GetLocalizationValue(TrelamiumIII.Instance, "AlmanacUI.RecipesDiscovered")}";
        Vector2 statSize = font.MeasureString(recipeStat) * HEADER_SCALE;
        Utils.DrawBorderString(sb, recipeStat,
            new Vector2((PAGE_WIDTH - statSize.X) * 0.5f, PAGE_HEIGHT * 0.34f),
            Color.GreenYellow, HEADER_SCALE);

        var allIngredients = AlchemyIngredients.AllIngredientTypes;
        int ingKnown = 0;
        foreach (int t in allIngredients)
            if (AlmanacDiscoverySystem.IsIngredientVisible(t)) ingKnown++;
        string ingStat = $"{ingKnown} / {allIngredients.Count} {ModExtensions.GetLocalizationValue(TrelamiumIII.Instance, "AlmanacUI.IngredientsDiscovered")}";
        statSize = font.MeasureString(ingStat) * HEADER_SCALE;
        Utils.DrawBorderString(sb, ingStat,
            new Vector2((PAGE_WIDTH - statSize.X) * 0.5f, PAGE_HEIGHT * 0.395f),
            new Color(135, 190, 255), HEADER_SCALE);

        // controls hint
        string[] hints =
        {
            ModExtensions.GetLocalizationValue(TrelamiumIII.Instance, "AlmanacUI.Hint1"),
            "",
            ModExtensions.GetLocalizationValue(TrelamiumIII.Instance, "AlmanacUI.Hint2"),
            "",
            ModExtensions.GetLocalizationValue(TrelamiumIII.Instance, "AlmanacUI.Hint3"),
            "",
            ModExtensions.GetLocalizationValue(TrelamiumIII.Instance, "AlmanacUI.Hint3A"),
            "",
            ModExtensions.GetLocalizationValue(TrelamiumIII.Instance, "AlmanacUI.Hint4"),
            ModExtensions.GetLocalizationValue(TrelamiumIII.Instance, "AlmanacUI.Hint5"),
        };
        float y = PAGE_HEIGHT * 0.46f;
        foreach (var line in hints)
        {
            if (string.IsNullOrEmpty(line)) { y += 12f; continue; }
            Vector2 sz = font.MeasureString(line) * BODY_SCALE;
            Utils.DrawBorderString(sb, line,
                new Vector2((PAGE_WIDTH - sz.X) * 0.5f, y),
                Color.White, BODY_SCALE);
            y += 18f;
        }
    }

    private void DrawTOCContent(SpriteBatch sb, AlmanacCategory category, int globalStart)
    {
        var list = BrowseListFor(category);
        int count = list.Count;

        // Empty page (no icons would draw on this side) -> render nothing at all.
        if (globalStart >= count) return;

        // Page header
        var font = FontAssets.MouseText.Value;
        string header = category == AlmanacCategory.Ingredients ? "Ingredients" : HeaderForCurrentBrowse();
        Vector2 hSize = font.MeasureString(header) * HEADER_SCALE;
        Utils.DrawBorderString(sb, header,
            new Vector2((PAGE_WIDTH - hSize.X) * 0.5f, MARGIN_Y - 14f),
            Color.White, HEADER_SCALE);

        // Grid of icons
        var pageRect = new Rectangle(0, 0, PAGE_WIDTH, PAGE_HEIGHT);
        var (gx, gy) = GetTOCGridOrigin(pageRect);

        for (int row = 0; row < ICON_ROWS; row++)
        {
            for (int col = 0; col < ICON_COLS; col++)
            {
                int idx = globalStart + row * ICON_COLS + col;
                if (idx >= count) return;

                int type = list[idx];
                bool unlocked = category == AlmanacCategory.Ingredients
                    ? AlmanacDiscoverySystem.IsIngredientVisible(type)
                    : AlmanacDiscoverySystem.IsUnlocked(type);

                float x = gx + col * (ICON_CELL_SIZE + ICON_CELL_PAD);
                float y = gy + row * (ICON_CELL_SIZE + ICON_CELL_PAD);

                DrawIconCell(sb, new Rectangle((int)x, (int)y,
                                               (int)ICON_CELL_SIZE, (int)ICON_CELL_SIZE),
                             type, unlocked);
            }
        }
    }

    private string HeaderForCurrentBrowse() => ModeFilter switch
    {
        AlchemyMode.Synthesis => ModExtensions.GetLocalizationValue(TrelamiumIII.Instance, "AlmanacUI.SynthesisRecipes"),
        AlchemyMode.Augmentation => ModExtensions.GetLocalizationValue(TrelamiumIII.Instance, "AlmanacUI.AugmentationRecipes"),
        AlchemyMode.Alchemy => ModExtensions.GetLocalizationValue(TrelamiumIII.Instance, "AlmanacUI.AlchemyRecipes"),
        _ => ModExtensions.GetLocalizationValue(TrelamiumIII.Instance, "AlmanacUI.AllRecipes"),
    };

    private static void DrawIconCell(SpriteBatch sb, Rectangle cell, int itemType, bool unlocked)
    {
        var px = TextureAssets.MagicPixel.Value;

        // Cell background
        Color bg = unlocked ? new Color(60, 40, 20, 80) : new Color(40, 30, 20, 140);
        sb.Draw(px, cell, bg);

        // Cell border
        Color border = unlocked ? new Color(150, 110, 60, 200) : new Color(80, 60, 40, 180);
        sb.Draw(px, new Rectangle(cell.X, cell.Y, cell.Width, 1), border);
        sb.Draw(px, new Rectangle(cell.X, cell.Bottom - 1, cell.Width, 1), border);
        sb.Draw(px, new Rectangle(cell.X, cell.Y, 1, cell.Height), border);
        sb.Draw(px, new Rectangle(cell.Right - 1, cell.Y, 1, cell.Height), border);

        if (!unlocked)
        {
            var font = FontAssets.MouseText.Value;
            string q = "?";
            Vector2 size = font.MeasureString(q) * 1.4f;
            Utils.DrawBorderString(sb, q,
                new Vector2(cell.X + (cell.Width - size.X) * 0.5f,
                            cell.Y + (cell.Height - size.Y) * 0.9f),
                new Color(140, 110, 70), 1.4f);
            return;
        }

        // Unlocked
        Main.instance.LoadItem(itemType);
        var tex = TextureAssets.Item[itemType]?.Value;
        if (tex == null) return;

        var anim = Main.itemAnimations[itemType];
        Rectangle frame = anim != null ? anim.GetFrame(tex) : tex.Frame();
        Vector2 origin = frame.Size() * 0.5f;

        float fit = 1f;
        if (frame.Width > cell.Width - 8 || frame.Height > cell.Height - 8)
            fit = frame.Width > frame.Height
                ? (cell.Width - 8f) / frame.Width
                : (cell.Height - 8f) / frame.Height;

        var center = new Vector2(cell.X + cell.Width * 0.5f, cell.Y + cell.Height * 0.5f);
        sb.Draw(tex, center, frame, Color.White, 0f, origin, fit, SpriteEffects.None, 0f);
    }

    private static void DrawDetailHeader(SpriteBatch sb, int outputType)
    {
        if (outputType <= 0) return;

        var font = FontAssets.MouseText.Value;

        // Big icon centered near top
        DrawCenteredItemIcon(sb, outputType, PAGE_WIDTH * 0.5f, PAGE_HEIGHT * 0.18f, 96f);

        // Entry metadata (potency / chemical / rarity). Falls back to sensible
        // defaults for outputs that haven't been authored yet.
        var entry = AlmanacEntryRegistry.For(outputType);

        // Name, coloured by the entry's judged rarity.
        string name = Lang.GetItemNameValue(outputType);
        Vector2 nSize = font.MeasureString(name) * TITLE_SCALE;
        Utils.DrawBorderString(sb, name,
            new Vector2((PAGE_WIDTH - nSize.X) * 0.5f, PAGE_HEIGHT * 0.30f),
            RarityColor(entry.ResolvedRarity), TITLE_SCALE);

        // Mode badge: based on the FIRST recipe producing this output
        var firstRecipe = AlmanacIndex.RecipesProducing(outputType).FirstOrDefault();
        if (firstRecipe != null)
        {
            string modeLabel = firstRecipe.Mode switch
            {
                AlchemyMode.Synthesis => ModExtensions.GetLocalizationValue(TrelamiumIII.Instance, "AlmanacUI.SynthesisRecipes"),
                AlchemyMode.Augmentation => ModExtensions.GetLocalizationValue(TrelamiumIII.Instance, "AlmanacUI.AugmentationRecipes"),
                AlchemyMode.Alchemy => ModExtensions.GetLocalizationValue(TrelamiumIII.Instance, "AlmanacUI.AlchemyRecipes"),
                _ => ModExtensions.GetLocalizationValue(TrelamiumIII.Instance, "AlmanacUI.AlchemyRecipes"),
            };
            Color modeColor = firstRecipe.Mode switch
            {
                AlchemyMode.Synthesis => new Color(140, 200, 255),
                AlchemyMode.Augmentation => new Color(255, 200, 120),
                AlchemyMode.Alchemy => new Color(255, 130, 130),
                _ => Color.White,
            };
            Vector2 mSize = font.MeasureString(modeLabel) * HEADER_SCALE;
            Utils.DrawBorderString(sb, modeLabel,
                new Vector2((PAGE_WIDTH - mSize.X) * 0.5f, PAGE_HEIGHT * 0.37f),
                modeColor, HEADER_SCALE);
        }

        // Potency stars + chemical-type badges.
        DrawPotencyStars(sb, entry.Potency, PAGE_WIDTH * 0.5f, PAGE_HEIGHT * 0.43f);
        DrawChemicalBadges(sb, entry.Chemical, entry.Principle, PAGE_WIDTH * 0.5f, PAGE_HEIGHT * 0.485f);

        // Decorative rules
        var px = TextureAssets.MagicPixel.Value;
        var ruleColor = Color.SaddleBrown * 0.5f;
        sb.Draw(px, new Rectangle((int)MARGIN_X, (int)(PAGE_HEIGHT * 0.53f),
                                   PAGE_WIDTH - (int)MARGIN_X * 2, 1), ruleColor);

        // Variant count footer
        int count = AlmanacIndex.RecipesProducing(outputType).Count();
        string footer = count == 1 ? ModExtensions.GetLocalizationValue(TrelamiumIII.Instance, "AlmanacUI.RecipeVariant")
            : $"{count} {ModExtensions.GetLocalizationValue(TrelamiumIII.Instance, "AlmanacUI.RecipeVariants")}";
        Vector2 fSize = font.MeasureString(footer) * BODY_SCALE;
        Utils.DrawBorderString(sb, footer,
            new Vector2((PAGE_WIDTH - fSize.X) * 0.5f, PAGE_HEIGHT * 0.565f),
            Color.GreenYellow, BODY_SCALE);

        // Flavour description, localized under Almanac.FlavorText.<key> (empty when unauthored).
        DrawWrappedDescription(sb, entry.GetDescription(), PAGE_HEIGHT * 0.61f);
    }

    /// <summary>Draws an item icon centred at (centerX, centerY), scaled to fit maxIcon then by extraScale.</summary>
    private static void DrawCenteredItemIcon(SpriteBatch sb, int itemType, float centerX, float centerY, float maxIcon)
    {
        if (itemType <= 0) return;

        Main.instance.LoadItem(itemType);
        var tex = TextureAssets.Item[itemType]?.Value;
        if (tex == null) return;

        var anim = Main.itemAnimations[itemType];
        Rectangle frame = anim != null ? anim.GetFrame(tex) : tex.Frame();
        Vector2 origin = frame.Size() * 0.5f;

        float fit = 1.5f;
        if (frame.Width > maxIcon || frame.Height > maxIcon)
            fit = frame.Width > frame.Height ? maxIcon / frame.Width : maxIcon / frame.Height;
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, Main.Rasterizer);
        sb.Draw(tex, new Vector2(centerX, centerY), frame, Color.White, 0f, origin, fit, SpriteEffects.None, 0f);
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
    }

    /// <summary>
    /// Word-wraps text to maxWidth at the given scale. Honors explicit paragraph breaks via the
    /// <c>[p]</c> token or newlines (each emitted as a blank line).
    /// </summary>
    private static List<string> WrapText(string text, float maxWidth, float scale)
    {
        var lines = new List<string>();
        if (string.IsNullOrEmpty(text)) return lines;

        var font = FontAssets.MouseText.Value;
        string normalized = text.Replace("[p]", "\n");
        foreach (var para in normalized.Split('\n'))
        {
            if (para.Length == 0) { lines.Add(string.Empty); continue; }

            string line = "";
            foreach (var word in para.Split(' '))
            {
                string test = line.Length > 0 ? line + " " + word : word;
                if (font.MeasureString(test).X * scale > maxWidth && line.Length > 0)
                {
                    lines.Add(line);
                    line = word;
                }
                else line = test;
            }
            if (line.Length > 0) lines.Add(line);
        }
        return lines;
    }

    /// <summary>Draws a centred, word-wrapped paragraph starting at baseY. No-op for empty text.</summary>
    private static void DrawWrappedDescription(SpriteBatch sb, string desc, float baseY)
    {
        if (string.IsNullOrEmpty(desc)) return;

        const float scale = 0.73f;
        var font = FontAssets.MouseText.Value;
        var lines = WrapText(desc, PAGE_WIDTH - 16f, scale);
        if (lines.Count == 0) return;

        float lineH = font.MeasureString("A").Y * scale;
        float y = baseY + (lines.Count - 1) * lineH * 0.15f;
        for (int i = 0; i < lines.Count; i++)
        {
            Vector2 sz = font.MeasureString(lines[i]) * scale;
            Utils.DrawBorderString(sb, lines[i], new Vector2((PAGE_WIDTH - sz.X) * 0.5f, y + i * lineH), Color.White, scale);
        }
    }

    private static void DrawIngredientHeader(SpriteBatch sb, int itemType)
    {
        if (itemType <= 0) return;

        var font = FontAssets.MouseText.Value;
        var entry = AlmanacEntryRegistry.For(itemType);

        DrawCenteredItemIcon(sb, itemType, PAGE_WIDTH * 0.5f, PAGE_HEIGHT * 0.18f, 96f);

        // Name, coloured by judged rarity (authored on the entry, else the item's vanilla rarity).
        string name = Lang.GetItemNameValue(itemType);
        Vector2 nSize = font.MeasureString(name) * TITLE_SCALE;
        Utils.DrawBorderString(sb, name,
            new Vector2((PAGE_WIDTH - nSize.X) * 0.5f, PAGE_HEIGHT * 0.30f),
            RarityColor(entry.ResolvedRarity), TITLE_SCALE);

        // Role subtitle: solvent (vial), solute (substance), or both.
        bool isVial = AlchemyIngredients.IsSolventType(itemType);
        bool isComp = AlchemyIngredients.IsSubstanceType(itemType);
        string role = isVial && isComp ? "Solute & Solvent" : isVial ? "Solvent" : isComp ? "Solute/Reagent" : "";
        if (role.Length > 0)
        {
            Vector2 rSize = font.MeasureString(role) * HEADER_SCALE;
            Utils.DrawBorderString(sb, role,
                new Vector2((PAGE_WIDTH - rSize.X) * 0.5f, PAGE_HEIGHT * 0.37f),
                new Color(200, 180, 140), HEADER_SCALE);
        }

        // Authored ratings: potency stars + principle badge.
        DrawPotencyStars(sb, entry.Potency, PAGE_WIDTH * 0.5f, PAGE_HEIGHT * 0.43f);
        DrawChemicalBadges(sb, entry.Chemical, entry.Principle, PAGE_WIDTH * 0.5f, PAGE_HEIGHT * 0.485f);

        // Decorative rule.
        var px = TextureAssets.MagicPixel.Value;
        sb.Draw(px, new Rectangle((int)MARGIN_X, (int)(PAGE_HEIGHT * 0.55f),
                                   PAGE_WIDTH - (int)MARGIN_X * 2, 1), Color.SaddleBrown * 0.5f);

        // Flavour description, localized under Almanac.FlavorText.<key>.
        DrawWrappedDescription(sb, AlmanacIngredientInfo.GetDescription(itemType), PAGE_HEIGHT * 0.59f);
    }

    private void DrawIngredientUses(SpriteBatch sb, int itemType, int startIndex, int maxCount)
    {
        if (itemType <= 0) return;

        var font = FontAssets.MouseText.Value;
        var uses = DetailIngredientUses;

        if (startIndex == 0)
        {
            string header = "Used In";
            var pinned = QueryInfusion(itemType);
            if (pinned != ChemicalType.None && ChemicalTypeInfo.TryGet(pinned, out string label, out _))
                header = $"Used In ({label})";

            Vector2 hSize = font.MeasureString(header) * HEADER_SCALE;
            Utils.DrawBorderString(sb, header,
                new Vector2((PAGE_WIDTH - hSize.X) * 0.5f, MARGIN_Y - 14f),
                Color.White, HEADER_SCALE);

            if (uses.Count == 0)
            {
                DrawWrappedDescription(sb, "No discovered recipes use this yet.", PAGE_HEIGHT * 0.45f);
                return;
            }
        }

        if (startIndex >= uses.Count) return;

        const float ROW_HEIGHT = 56f;
        const float ROW_TOP = MARGIN_Y + 12f;

        int drawn = 0;
        for (int i = startIndex; i < uses.Count && drawn < maxCount; i++, drawn++)
        {
            float y = ROW_TOP + drawn * ROW_HEIGHT;
            DrawVariantRow(sb, uses[i], MARGIN_X, y, PAGE_WIDTH - MARGIN_X * 2f);
        }
    }

    // ---- Notes guide: table of contents, prose pages, and the Principles glossary ----

    private void DrawNotesTOC(SpriteBatch sb, int spreadIndex, bool isLeft)
    {
        int total = AlmanacNotes.Count;
        int pageOffset = isLeft ? 0 : NOTE_TOC_ROWS_PER_PAGE;
        int globalStart = spreadIndex * NOTE_TOC_ROWS_PER_SPREAD + pageOffset;
        if (globalStart >= total) return;

        var font = FontAssets.MouseText.Value;
        var px = TextureAssets.MagicPixel.Value;

        if (isLeft)
        {
            string header = "Contents";
            Vector2 hSize = font.MeasureString(header) * HEADER_SCALE;
            Utils.DrawBorderString(sb, header,
                new Vector2((PAGE_WIDTH - hSize.X) * 0.5f, MARGIN_Y - 14f),
                Color.White, HEADER_SCALE);
        }

        float top = MARGIN_Y + 16f;
        for (int r = 0; r < NOTE_TOC_ROWS_PER_PAGE; r++)
        {
            int idx = globalStart + r;
            if (idx >= total) break;
            var note = AlmanacNotes.At(idx);
            if (note == null) continue;

            float y = top + r * NOTE_TOC_ROW_H;
            bool hovered = state == BookState.Reading && idx == _hoveredNote;
            if (hovered)
                sb.Draw(px, new Rectangle((int)MARGIN_X - 4, (int)y - 4,
                    PAGE_WIDTH - (int)MARGIN_X * 2 + 8, (int)NOTE_TOC_ROW_H - 2), new Color(150, 110, 60, 70));

            Color textColor = hovered ? new Color(255, 240, 200) : Color.White;
            Utils.DrawBorderString(sb, $"{idx + 1}.  {note.Title}", new Vector2(MARGIN_X, y), textColor, 0.88f);
        }
    }

    private int HitTestNoteRow(Point mouse)
    {
        if (current.Category != AlmanacCategory.Notes || current.View != ViewMode.Browse) return -1;
        if (!NotesIndexIsToc(current.Index, out int tocSpread)) return -1;

        bool right;
        if (GetPageRect(false).Contains(mouse)) right = false;
        else if (GetPageRect(true).Contains(mouse)) right = true;
        else return -1;
        int pageOffset = right ? NOTE_TOC_ROWS_PER_PAGE : 0;

        var local = ToPageLocal(mouse, right);
        float top = MARGIN_Y + 16f;
        int r = (int)((local.Y - top) / NOTE_TOC_ROW_H);
        if (r < 0 || r >= NOTE_TOC_ROWS_PER_PAGE) return -1;

        int idx = tocSpread * NOTE_TOC_ROWS_PER_SPREAD + pageOffset + r;
        return idx >= 0 && idx < AlmanacNotes.Count ? idx : -1;
    }

    // ---- Notes is one chronological read: cover -> table of contents -> chapters ----

    private static int NotesTocSpreads() =>
        Math.Max(1, (AlmanacNotes.Count + NOTE_TOC_ROWS_PER_SPREAD - 1) / NOTE_TOC_ROWS_PER_SPREAD);

    /// <summary>Total Notes browse spreads: cover + table of contents + every chapter.</summary>
    private int NotesBrowseTotal()
    {
        int t = 1 + NotesTocSpreads();
        for (int c = 0; c < AlmanacNotes.Count; c++) t += NoteSpreadCount(c);
        return t;
    }

    private static bool NotesIndexIsToc(int index, out int tocSpread)
    {
        tocSpread = index - 1;
        return index >= 1 && index < 1 + NotesTocSpreads();
    }

    /// <summary>Browse index of a chapter's first spread (used by the TOC quick-jumps).</summary>
    private int NotesChapterBrowseIndex(int chapter)
    {
        int idx = 1 + NotesTocSpreads();
        for (int c = 0; c < chapter; c++) idx += NoteSpreadCount(c);
        return idx;
    }

    /// <summary>Resolve a Notes browse index: 0 = cover; 1 = TOC spread <c>a</c>; 2 = chapter <c>a</c>, spread <c>b</c>.</summary>
    private int NotesBrowseAt(int index, out int a, out int b)
    {
        a = 0; b = 0;
        if (index <= 0) return 0;
        int i = index - 1;
        int toc = NotesTocSpreads();
        if (i < toc) { a = i; return 1; }
        i -= toc;
        for (int c = 0; c < AlmanacNotes.Count; c++)
        {
            int sc = NoteSpreadCount(c);
            if (i < sc) { a = c; b = i; return 2; }
            i -= sc;
        }
        return 0;
    }

    private void BeginNotesJump(int chapter)
    {
        if (state != BookState.Reading) return;

        int target = NotesChapterBrowseIndex(chapter);
        if (target == current.Index && current.Category == AlmanacCategory.Notes && current.View == ViewMode.Browse) return;

        flipDir = target >= current.Index ? FlipDir.Forward : FlipDir.Backward;
        next = new SpreadKey(AlmanacCategory.Notes, ViewMode.Browse, target, 0);
        pageFlipProgress = 0f;
        state = BookState.Flipping;
        SoundEngine.PlaySound(Assets.SFX.PageFlip.Asset, Main.LocalPlayer.Center);
    }

    // Browse reads as one chronological sequence across these sections; tabs jump between them.
    private static readonly AlmanacCategory[] _browseOrder =
        { AlmanacCategory.Notes, AlmanacCategory.Ingredients, AlmanacCategory.Recipes };

    private static AlmanacCategory? AdjacentBrowseCategory(AlmanacCategory cat, int dir)
    {
        int i = Array.IndexOf(_browseOrder, cat) + dir;
        return i >= 0 && i < _browseOrder.Length ? _browseOrder[i] : (AlmanacCategory?)null;
    }

    private List<string> NoteLines(int noteIndex)
    {
        if (noteIndex == _noteLinesFor && _noteLines != null) return _noteLines;

        var note = AlmanacNotes.At(noteIndex);
        _noteLines = note == null
            ? new List<string>()
            : WrapText(note.Body, PAGE_WIDTH - MARGIN_X * 2f, NOTE_SCALE);
        _noteLinesFor = noteIndex;
        return _noteLines;
    }

    private int NoteSpreadCount(int noteIndex)
    {
        var note = AlmanacNotes.At(noteIndex);
        if (note == null) return 1;
        if (note.Kind is NoteKind.Principles or NoteKind.Chemistry) return 1;

        int lineCount = NoteLines(noteIndex).Count;
        int cap0 = NOTE_LINES_PER_PAGE - NOTE_TITLE_LINES;
        int pages = lineCount <= cap0
            ? 1
            : 1 + (lineCount - cap0 + NOTE_LINES_PER_PAGE - 1) / NOTE_LINES_PER_PAGE;
        return Math.Max(1, (pages + 1) / 2);
    }

    private void DrawNoteProsePage(SpriteBatch sb, int noteIndex, int globalPage)
    {
        var note = AlmanacNotes.At(noteIndex);
        if (note == null) return;

        var font = FontAssets.MouseText.Value;
        var lines = NoteLines(noteIndex);
        float lineH = font.MeasureString("A").Y * NOTE_SCALE + 3f;
        int cap0 = NOTE_LINES_PER_PAGE - NOTE_TITLE_LINES;

        int start;
        float top;
        if (globalPage == 0)
        {
            Utils.DrawBorderString(sb, note.Title, new Vector2(MARGIN_X, MARGIN_Y - 6f),
                new Color(235, 225, 200), HEADER_SCALE);
            var px = TextureAssets.MagicPixel.Value;
            sb.Draw(px, new Rectangle((int)MARGIN_X, (int)(MARGIN_Y + 18f),
                PAGE_WIDTH - (int)MARGIN_X * 2, 1), Color.SaddleBrown * 0.5f);
            top = MARGIN_Y + 28f;
            start = 0;
        }
        else
        {
            top = MARGIN_Y;
            start = cap0 + (globalPage - 1) * NOTE_LINES_PER_PAGE;
        }

        int count = globalPage == 0 ? cap0 : NOTE_LINES_PER_PAGE;
        for (int i = 0; i < count; i++)
        {
            int li = start + i;
            if (li >= lines.Count) break;
            if (lines[li].Length == 0) continue;   // paragraph gap: leaves the slot empty
            Utils.DrawBorderString(sb, lines[li], new Vector2(MARGIN_X, top + i * lineH), Color.White, NOTE_SCALE);
        }
    }

    private static void DrawPrinciplesPage(SpriteBatch sb, AlmanacNote note, bool isLeft)
    {
        var font = FontAssets.MouseText.Value;

        if (isLeft)
        {
            Utils.DrawBorderString(sb, note?.Title ?? "The Seven Principles",
                new Vector2(MARGIN_X, MARGIN_Y - 6f), new Color(235, 225, 200), HEADER_SCALE);
            var px = TextureAssets.MagicPixel.Value;
            sb.Draw(px, new Rectangle((int)MARGIN_X, (int)(MARGIN_Y + 18f),
                PAGE_WIDTH - (int)MARGIN_X * 2, 1), Color.SaddleBrown * 0.5f);

            const float introScale = 0.72f;
            float introY = MARGIN_Y + 28f;
            var intro = WrapText(note?.Body ?? string.Empty, PAGE_WIDTH - MARGIN_X * 2f, introScale);
            float il = font.MeasureString("A").Y * introScale + 2f;
            for (int i = 0; i < intro.Count; i++)
                Utils.DrawBorderString(sb, intro[i], new Vector2(MARGIN_X, introY + i * il),
                    new Color(220, 210, 185), introScale);

            DrawPrincipleRows(sb, 0, 4, introY + intro.Count * il + 14f);
        }
        else
        {
            DrawPrincipleRows(sb, 4, _principles.Length, MARGIN_Y + 12f);
        }
    }

    // ---- Chemistry chapter: the three type axes + how solutes act on solvents ----

    private static readonly (ChemicalType A, string ADesc, ChemicalType B, string BDesc)[] _chemAxes =
    {
        (ChemicalType.Inert,    "refuses to react; stabilises and dilutes",
         ChemicalType.Catalytic, "enables and hastens reactions"),
        (ChemicalType.Alkaline, "binds compounds, neutralises acids",
         ChemicalType.Acidic,    "breaks other compounds down"),
        (ChemicalType.Volatile, "unstable; evaporates easily",
         ChemicalType.Kinetic,   "reacts to release raw energy"),
    };

    private static readonly (ChemicalType A, ChemicalType B, string Makes)[] _chemPairings =
    {
        (ChemicalType.Catalytic, ChemicalType.Kinetic,  "highly potent brews"),
        (ChemicalType.Volatile,  ChemicalType.Kinetic,  "dangerous, unstable products"),
        (ChemicalType.Acidic,    ChemicalType.Volatile, "corrosive agents"),
        (ChemicalType.Alkaline,  ChemicalType.Volatile, "cleansing utilities"),
    };

    /// <summary>Draws one chemical-type name in its badge colour; returns the advance width.</summary>
    private static float DrawChemName(SpriteBatch sb, ChemicalType flag, Vector2 pos, float scale)
    {
        ChemicalTypeInfo.TryGet(flag, out string label, out Color color);
        Utils.DrawBorderString(sb, label, pos, color, scale);
        return FontAssets.MouseText.Value.MeasureString(label).X * scale;
    }

    private static void DrawChemistryPage(SpriteBatch sb, AlmanacNote note, bool isLeft)
    {
        var font = FontAssets.MouseText.Value;
        var px = TextureAssets.MagicPixel.Value;

        if (isLeft)
        {
            // Title + localized intro, then the three opposing axes.
            Utils.DrawBorderString(sb, note?.Title ?? "Solvents & Solutes",
                new Vector2(MARGIN_X, MARGIN_Y - 6f), new Color(235, 225, 200), HEADER_SCALE);
            sb.Draw(px, new Rectangle((int)MARGIN_X, (int)(MARGIN_Y + 18f),
                PAGE_WIDTH - (int)MARGIN_X * 2, 1), Color.SaddleBrown * 0.5f);

            const float introScale = 0.72f;
            float introY = MARGIN_Y + 28f;
            var intro = WrapText(note?.Body ?? string.Empty, PAGE_WIDTH - MARGIN_X * 2f, introScale);
            float il = font.MeasureString("A").Y * introScale + 2f;
            for (int i = 0; i < intro.Count; i++)
                Utils.DrawBorderString(sb, intro[i], new Vector2(MARGIN_X, introY + i * il),
                    new Color(220, 210, 185), introScale);

            const float rowH = 72f;
            float startY = introY + intro.Count * il + 14f;
            for (int i = 0; i < _chemAxes.Length; i++)
            {
                var axis = _chemAxes[i];
                float y = startY + i * rowH;

                float x = MARGIN_X;
                x += DrawChemName(sb, axis.A, new Vector2(x, y), 0.95f);
                Utils.DrawBorderString(sb, " / ", new Vector2(x, y), new Color(175, 165, 145), 0.95f);
                x += font.MeasureString(" / ").X * 0.95f;
                DrawChemName(sb, axis.B, new Vector2(x, y), 0.95f);

                Utils.DrawBorderString(sb, axis.ADesc, new Vector2(MARGIN_X + 12f, y + 20f),
                    Color.White, 0.72f);
                Utils.DrawBorderString(sb, axis.BDesc, new Vector2(MARGIN_X + 12f, y + 38f),
                    new Color(175, 165, 145), 0.72f);
            }
            return;
        }

        // Right page: how solutes act on solvents, then the pairing outcomes.
        const float bodyScale = 0.72f;
        float lh = font.MeasureString("A").Y * bodyScale + 2f;
        float py = MARGIN_Y + 2f;

        void Paragraph(string text, Color color)
        {
            var lines = WrapText(text, PAGE_WIDTH - MARGIN_X * 2f, bodyScale);
            foreach (var l in lines)
            {
                Utils.DrawBorderString(sb, l, new Vector2(MARGIN_X, py), color, bodyScale);
                py += lh;
            }
            py += 6f;
        }

        Paragraph("Solutes that share a single temperament create an infusion " +
                  "when mixed into a solvent. You'll know what type of chemical solution you have when ya see it.",
                  new Color(220, 210, 185));
        Paragraph("Mind the stubborn ones: an inert solution refuses every catalyst. " +
                  "It accepts solvent augmentations and nothing else.",
                  new Color(175, 165, 145));

        Utils.DrawBorderString(sb, "When temperaments meet:", new Vector2(MARGIN_X, py + 4f),
            new Color(235, 225, 200), 0.85f);
        py += 30f;

        const float pairRowH = 46f;
        for (int i = 0; i < _chemPairings.Length; i++)
        {
            var pair = _chemPairings[i];
            float y = py + i * pairRowH;

            float x = MARGIN_X;
            x += DrawChemName(sb, pair.A, new Vector2(x, y), 0.9f);
            Utils.DrawBorderString(sb, " + ", new Vector2(x, y), new Color(175, 165, 145), 0.9f);
            x += font.MeasureString(" + ").X * 0.9f;
            DrawChemName(sb, pair.B, new Vector2(x, y), 0.9f);

            Utils.DrawBorderString(sb, $"-> {pair.Makes}", new Vector2(MARGIN_X + 12f, y + 19f),
                Color.White, 0.72f);
        }
    }

    private static void DrawPrincipleRows(SpriteBatch sb, int from, int toExclusive, float startY)
    {
        var px = TextureAssets.MagicPixel.Value;
        const float rowH = 58f;

        for (int i = from; i < toExclusive && i < _principles.Length; i++)
        {
            var p = _principles[i];
            float y = startY + (i - from) * rowH;

            sb.Draw(px, new Rectangle((int)MARGIN_X, (int)y + 3, 12, 12), p.Color);
            if (p.Name == "None")
                Utils.DrawBorderString(sb, $"{p.Name}", new Vector2(MARGIN_X + 20f, y - 2f), p.Color, 0.95f);
            else
                Utils.DrawBorderString(sb, $"{p.Name}  ({p.Metal})", new Vector2(MARGIN_X + 20f, y - 2f), p.Color, 0.95f);

            Utils.DrawBorderString(sb, p.Domain, new Vector2(MARGIN_X + 20f, y + 18f), Color.White, 0.78f);
            Utils.DrawBorderString(sb, $"reacts: {p.Reacts}", new Vector2(MARGIN_X + 20f, y + 34f),
                new Color(175, 165, 145), 0.72f);
        }
    }


    private void DrawDetailVariants(SpriteBatch sb, int outputType, int startIndex, int maxCount)
    {
        if (outputType <= 0) return;

        var recipes = DetailRecipes;
        // Empty page (no variants would draw on this side) -> render nothing at all.
        if (startIndex >= recipes.Count) return;

        // Header
        var hfont = FontAssets.MouseText.Value;
        string header = startIndex == 0 ? ModExtensions.GetLocalizationValue(TrelamiumIII.Instance, "AlmanacUI.HeaderRecipes") 
            : $"{ModExtensions.GetLocalizationValue(TrelamiumIII.Instance, "AlmanacUI.HeaderRecipes")} (cont.)";
        Vector2 hSize = hfont.MeasureString(header) * HEADER_SCALE;
        Utils.DrawBorderString(sb, header,
            new Vector2((PAGE_WIDTH - hSize.X) * 0.5f, MARGIN_Y - 14f),
            Color.White, HEADER_SCALE);

        // Variants
        const float ROW_HEIGHT = 56f;
        const float ROW_TOP = MARGIN_Y + 12f;

        int drawn = 0;
        for (int i = startIndex; i < recipes.Count && drawn < maxCount; i++, drawn++)
        {
            float y = ROW_TOP + drawn * ROW_HEIGHT;
            DrawVariantRow(sb, recipes[i], MARGIN_X, y, PAGE_WIDTH - MARGIN_X * 2f);
        }
    }

    private static void DrawVariantRow(SpriteBatch sb, AlchemyRecipe recipe, float x, float y, float w)
    {
        var px = TextureAssets.MagicPixel.Value;
        var font = FontAssets.MouseText.Value;

        // Row background card.
        sb.Draw(px, new Rectangle((int)x, (int)y, (int)w, 50),
            new Color(60, 40, 20, 60));
        sb.Draw(px, new Rectangle((int)x, (int)y, (int)w, 1), new Color(150, 110, 60, 180));
        sb.Draw(px, new Rectangle((int)x, (int)(y + 49), (int)w, 1), new Color(150, 110, 60, 100));

        // Layout: [solvent] + [solute][solute][solute] = [solution xN]
        const float ICON = 36f;
        const float GAP = 8f;
        float cx = x + 6f;
        float cy = y + 7f;

        DrawSmallItemIcon(sb, recipe.SolventType, new Vector2(cx, cy), ICON);

        cx += ICON + GAP - 4f;

        Utils.DrawBorderString(sb, "+",
            new Vector2(cx, cy + ICON * 0.25f),
            new Color(120, 90, 50), 0.9f);
        cx += 12f;

        for (int i = 0; i < recipe.Components.Length; i++)
        {
            var matcher = recipe.Components[i];
            DrawMatcherIcon(sb, matcher, new Vector2(cx, cy), ICON);

            // A solute drawn at more than one reads as its potency, overlaid on the icon's foot.
            // Single-draw slots are left bare so the authored ones are what catch the eye.
            if (matcher.Quantity > 1)
                DrawMiniPotencyStars(sb, matcher.Quantity, cx + ICON * 0.5f, y + 41f,
                    new Color(255, 220, 150));

            cx += ICON + GAP - 4f;

            if (i < recipe.Components.Length - 1)
            {
                Utils.DrawBorderString(sb, "+",
                    new Vector2(cx, cy + ICON * 0.25f),
                    new Color(120, 90, 50), 0.9f);
                cx += 12f;
            }
        }

        // Arrow + output
        Utils.DrawBorderString(sb, ">",
            new Vector2(cx + 4f, cy + ICON * 0.25f),
            new Color(120, 90, 50), 1f);
        cx += 18f;

        Item output = recipe.CreateOutput?.Invoke() ?? new Item();
        if (!output.IsAir)
        {
            DrawSmallItemIcon(sb, output.type, new Vector2(cx, cy), ICON);

            if (output.stack > 1)
            {
                string mult = $"x{output.stack}";
                Utils.DrawBorderString(sb, mult,
                    new Vector2(cx + ICON - 10f, cy + ICON - 14f),
                    Color.White, 0.75f);
            }

            // Brewed variants differ by potency; show it centred under the output so it's
            // obvious why one item has many recipe rows.
            var brew = output.GetGlobalItem<BrewGlobalItem>().Brew;
            if (brew is { Effects.Count: > 0 })
                DrawMiniPotencyStars(sb, brew.Primary.Potency, cx + ICON * 0.5f, y + 41f);
        }
    }

    /// <summary>Compact 5-slot potency meter for variant rows (fits the strip under a 36px icon).</summary>
    private static void DrawMiniPotencyStars(SpriteBatch sb, int potency, float centerX, float y,
                                             Color? tint = null)
        => PotencyMeter.Draw(sb, potency, centerX, y, pipHeight: 8f, gap: 1f, tint: tint);

    private static void DrawMatcherIcon(SpriteBatch sb, ComponentMatcher matcher, Vector2 pos, float size)
    {
        // Group matchers cycle through their members so the icon shows variety.
        if (matcher.Group != null)
        {
            var members = matcher.Group.ItemTypes;
            if (members.Count == 0) { DrawSmallItemIcon(sb, 0, pos, size); return; }

            int frame = (int)(Main.GameUpdateCount / 30) % members.Count;
            int picked = members.ElementAt(frame);
            DrawSmallItemIcon(sb, picked, pos, size);

            var px = TextureAssets.MagicPixel.Value;
            sb.Draw(px,
                new Rectangle((int)(pos.X + size - 6), (int)(pos.Y + size - 6), 5, 5),
                new Color(255, 200, 120, 220));
            return;
        }
        Item inv = new Item();
        inv.type = matcher.ExactType;
        DrawSmallItemIcon(sb, matcher.ExactType, pos, size);

    }

    private static void DrawSmallItemIcon(SpriteBatch sb, int itemType, Vector2 topLeft, float boxSize)
    {
        if (itemType <= 0) return;

        Main.instance.LoadItem(itemType);
        Item item = new Item();
        item.type = itemType;
        var tex = TextureAssets.Item[itemType]?.Value;
        if (tex == null) return;

        var anim = Main.itemAnimations[itemType];
        Rectangle frame = anim != null ? anim.GetFrame(tex) : tex.Frame();
        Vector2 origin = frame.Size() * 0.5f;

        float fit = 1f;
        if (frame.Width > boxSize || frame.Height > boxSize)
            fit = frame.Width > frame.Height ? boxSize / frame.Width : boxSize / frame.Height;

        var center = topLeft + new Vector2(boxSize * 0.5f, boxSize * 0.5f);
        sb.Draw(tex, center, frame, Color.White, 0f, origin, fit, SpriteEffects.None, 0f);
    }

    // ---- Entry detail visuals: potency stars, chemical badges, rarity colour ----

    /// <summary>The full-size entry-page meter. Pips come from <see cref="PotencyMeter"/>.</summary>
    private static void DrawPotencyStars(SpriteBatch sb, int potency, float centerX, float y)
    {
        if (potency <= 0) return;
        potency = Math.Clamp(potency, 0, PotencyMeter.PIPS);

        const int COUNT = PotencyMeter.PIPS;
        const float SCALE = 0.85f;
        const float GAP = 3f;

        var sample = PotencyMeter.Star(true);
        if (sample == null) return;

        float w = sample.Width * SCALE;
        float totalW = COUNT * w + (COUNT - 1) * GAP;
        float x = centerX - totalW * 0.5f;

        for (int i = 0; i < COUNT; i++)
        {
            Texture2D tex = PotencyMeter.Star(i < potency);
            sb.Draw(tex, new Vector2(x + i * (w + GAP), y), null, Color.White,
                0f, Vector2.Zero, SCALE, SpriteEffects.None, 0f);
        }
    }

    private static readonly (ChemicalType Flag, string Label, Color Color)[] _chemMeta =
    {
        (ChemicalType.Acidic,    "Acidic",    new Color(180, 230,  90)),
        (ChemicalType.Alkaline,  "Alkaline",  new Color(120, 180, 255)),
        (ChemicalType.Volatile,  "Volatile",  new Color(255, 160,  70)),
        (ChemicalType.Kinetic,  "Kinetic",     new Color(180, 255, 128)),
        (ChemicalType.Catalytic, "Catalytic", new Color(255, 215, 110)),
        (ChemicalType.Inert,     "Inert",     new Color(170, 170, 170)),
    };

    // Flag column drives badge matching bitwise (PrincipleType is [Flags] now, so
    // ToString() name-matching would silently fail on combined principles).
    private static readonly (PrincipleType Flag, string Name, string Metal, string Domain, string Reacts, Color Color)[] _principles =
    {
        (PrincipleType.Vital,    "Vital",    "Gold",     "life, regen, vigour, light",  "nourishes, perfects",  new Color(255, 215,  90)),
        (PrincipleType.Specular, "Specular", "Silver",   "perception, sight, the mind", "reveals, reflects",    new Color(180, 210, 255)),
        (PrincipleType.Martial,  "Martial",  "Iron",     "damage, crit, defence",       "ignites, amplifies",   new Color(230,  95,  80)),
        (PrincipleType.Brisk,    "Brisk",    "Tin",      "speed, motion, travel",       "quickens, catalyses",  new Color(110, 220, 210)),
        (PrincipleType.Fortune,  "Fortune",  "Platinum", "luck, yield, abundance",      "expands, multiplies",  new Color(150, 210, 120)),
        (PrincipleType.Harmony,  "Harmony",  "Copper",   "calm, allure, accord",        "binds, neutralises",   new Color(235, 150, 190)),
        (PrincipleType.Bane,     "Bane",     "Lead",     "toxin, decay, the curse",     "corrodes, lingers",    new Color(175, 125, 215)),
        (PrincipleType.None,     "None",     "",    "a neutral solvent",           "carries others",       new Color(165, 165, 165)),
    };

    /// <summary>
    /// One centred row of class tags: a badge per chemical flag, then the principle badge(s).
    /// Measured as a whole before drawing so the combined row centres on <paramref name="centerX"/>.
    /// </summary>
    private static void DrawChemicalBadges(SpriteBatch sb, ChemicalType chem, PrincipleType principle, float centerX, float y)
    {
        var font = FontAssets.MouseText.Value;
        var px = TextureAssets.MagicPixel.Value;
        const float SCALE = 0.7f;
        const float PAD_X = 6f, GAP = 5f, H = 18f;

        // Collect every badge in display order (chem flags, then principles).
        var badges = new List<(string Label, Color Color)>();
        foreach (var m in _chemMeta)
            if ((chem & m.Flag) != 0)
                badges.Add((m.Label, m.Color));
        foreach (var m in _principles)
            if (m.Flag != PrincipleType.None && (principle & m.Flag) != 0)
                badges.Add((m.Name, m.Color));

        if (badges.Count == 0) return;

        float totalW = -GAP;
        foreach (var (label, _) in badges)
            totalW += font.MeasureString(label).X * SCALE + PAD_X * 2 + GAP;

        float x = centerX - totalW * 0.5f;
        foreach (var (label, color) in badges)
        {
            float lw = font.MeasureString(label).X * SCALE;
            var rect = new Rectangle((int)x, (int)y, (int)(lw + PAD_X * 2), (int)H);
            sb.Draw(px, rect, color * 0.18f);
            DrawBorder(sb, rect, color * 0.85f);
            Utils.DrawBorderString(sb, label, new Vector2(x + PAD_X, y + 1f), color, SCALE);
            x += rect.Width + GAP;
        }
    }

    // Vanilla rarity value -> tooltip colour, mirroring Terraria's rarity palette.
    private static Color RarityColor(int rare) => rare switch
    {
        -1 => new Color(130, 130, 130),
         0 => Color.White,
         1 => new Color(150, 150, 255),
         2 => new Color(150, 255, 150),
         3 => new Color(255, 200, 150),
         4 => new Color(255, 150, 150),
         5 => new Color(255, 150, 255),
         6 => new Color(210, 160, 255),
         7 => new Color(150, 255,  10),
         8 => new Color(255, 255,  10),
         9 => new Color(  5, 200, 255),
        10 => new Color(255,  40, 100),
        11 => new Color(180,  40, 255),
        -11 => new Color(255, 175,   0),   // Quest
        -12 => Main.DiscoColor,            // Expert
        -13 => new Color(255, 200,   0),   // Master
        _ => Color.White,
    };
    #endregion

    #region IInGameNotification
    public void DrawInGame(SpriteBatch sb, Vector2 bottomAnchorPosition)
    {
        if (Main.gameMenu || AlchemyAlmanacSystem.CompositeTarget == null) return;

        var origin = GetSpreadOrigin();


        BookCoverDrawer.Draw(sb, origin, PAGE_WIDTH, PAGE_HEIGHT, GUTTER, Opacity, UI_SCALE);
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap, DepthStencilState.Default, Main.Rasterizer);
        sb.Draw(
            AlchemyAlmanacSystem.CompositeTarget,
            origin, null, Color.White * Opacity, 0f, Vector2.Zero, UI_SCALE, SpriteEffects.None, 0f);

        if (state == BookState.Reading || state == BookState.Flipping)
        {
            DrawCategoryTabsOverlay(sb);
            DrawBookButtons(sb);
            if (current.Category == AlmanacCategory.Recipes && current.View == ViewMode.Browse)
                DrawModeTabsOverlay(sb);

            if (current.Category == AlmanacCategory.Recipes || current.Category == AlmanacCategory.Ingredients)
            {
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearWrap, DepthStencilState.Default, Main.Rasterizer);

                DrawSearchSlotOverlay(sb);

                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, Main.Rasterizer);
            }

            DrawHoverTooltips(sb);
        }
    }

    private void DrawCategoryTabsOverlay(SpriteBatch sb)
    {
        RecomputeCategoryTabBounds();
        var mouse = Main.MouseScreen.ToPoint();
        for (int i = 0; i < _categoryTabs.Length; i++)
        {
            bool active = current.Category == _categoryTabs[i].cat;
            DrawOverlayButton(sb, _categoryTabBounds[i], _categoryTabs[i].label,
                _categoryTabBounds[i].Contains(mouse), active);
        }
    }

    private void DrawBookButtons(SpriteBatch sb)
    {
        RecomputeBookButtons();
        var mouse = Main.MouseScreen.ToPoint();

        DrawOverlayButton(sb, _closeButton, "Close", _closeButton.Contains(mouse), false, scale: 1f);

        // Page-turn arrows, greyed when that direction can't turn.
        int total = TotalSpreadsFor(current.Category, current.View, current.DetailItem);
        bool canPrev = current.Index > 0 || AdjacentBrowseCategory(current.Category, -1) != null && current.View == ViewMode.Browse;
        bool canNext = current.Index < total - 1 || AdjacentBrowseCategory(current.Category, +1) != null && current.View == ViewMode.Browse;

        var prev = ArrowTex(false);
        if (prev != null)
            sb.Draw(prev, _prevButton,
                canPrev ? (_prevButton.Contains(mouse) ? Color.White : new Color(235, 235, 235)) : new Color(110, 110, 110, 150));
        var next = ArrowTex(true);
        if (next != null)
            sb.Draw(next, _nextButton,
                canNext ? (_nextButton.Contains(mouse) ? Color.White : new Color(235, 235, 235)) : new Color(110, 110, 110, 150));
    }

    private void DrawModeTabsOverlay(SpriteBatch sb)
    {
        RecomputeModeTabBounds();
        var mouse = Main.MouseScreen.ToPoint();
        for (int i = 0; i < _modeTabs.Length; i++)
        {
            bool active = ModeFilter == _modeTabs[i].mode;
            DrawOverlayButton(sb, _modeTabBounds[i], _modeTabs[i].label,
                _modeTabBounds[i].Contains(mouse), active);
        }
    }

    private void DrawSearchSlotOverlay(SpriteBatch sb)
    {
        var px = TextureAssets.MagicPixel.Value;
        var font = FontAssets.MouseText.Value;

        // Label above the slot
        float labelScale = 0.85f * UI_SCALE;
        string label = ModExtensions.GetLocalizationValue(TrelamiumIII.Instance, "AlmanacUI.Search");
        Vector2 labelSize = font.MeasureString(label) * labelScale;
        Utils.DrawBorderString(sb, label,
            new Vector2(_searchSlotBounds.X + (_searchSlotBounds.Width - labelSize.X) * 0.025f,
                        _searchSlotBounds.Y - labelSize.Y - 2f),
            new Color(220, 200, 160), labelScale);

        // Slot background
        sb.Draw(px, _searchSlotBounds, new Color(30, 30, 35, 220));
        DrawBorder(sb, _searchSlotBounds, new Color(150, 130, 90, 220));

        // Item draw
        Item s = SearchItem;
        float oldScale = Main.inventoryScale;
        Main.inventoryScale = 0.85f * UI_SCALE;
        ItemSlot.Draw(sb, ref s, ItemSlot.Context.ChestItem,
            new Vector2(_searchSlotBounds.X, _searchSlotBounds.Y));
        Main.inventoryScale = oldScale;
    }

    private void DrawHoverTooltips(SpriteBatch sb)
    {
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, Main.Rasterizer);

        bool onToc = current.View == ViewMode.Browse && state == BookState.Reading
            && current.Category != AlmanacCategory.Notes;
        if (onToc)
        {
            int hit = HitTestTOCIcon(Main.MouseScreen.ToPoint());
            if (hit > 0)
            {
                bool known = current.Category == AlmanacCategory.Ingredients
                    ? AlmanacDiscoverySystem.IsIngredientVisible(hit)
                    : AlmanacDiscoverySystem.IsUnlocked(hit);
                string name = known ? Lang.GetItemNameValue(hit) : "???";
                Main.instance.MouseText(name, known ? AlmanacEntryRegistry.For(hit).ResolvedRarity : 0);
            }
        }
        else if (current.View == ViewMode.Detail && state == BookState.Reading
                 && current.Category != AlmanacCategory.Notes)
        {
            // Variant-row icons: name in the entry's judged rarity colour, with the
            // infusion suffix when the slot requires (or the output carries) one.
            int hit = HitTestVariantIcon(Main.MouseScreen.ToPoint(), out var infusion);
            if (hit > 0)
            {
                string name = Lang.GetItemNameValue(hit);
                if (infusion != ChemicalType.None && ChemicalTypeInfo.TryGet(infusion, out string label, out _))
                    name = $"{name} ({label})";
                Main.instance.MouseText(name, AlmanacEntryRegistry.For(hit).ResolvedRarity);
            }
        }
    }

    private static void DrawOverlayButton(SpriteBatch sb, Rectangle bounds, string label, bool hovered, bool active, float scale = 0.8f)
    {
        var px = TextureAssets.MagicPixel.Value;
        var font = FontAssets.MouseText.Value;

        Color bg = active
            ? new Color(95, 70, 35, 230)
            : hovered
                ? new Color(75, 55, 30, 220)
                : new Color(45, 35, 20, 210);

        Color border = active
            ? new Color(220, 180, 110, 240)
            : hovered
                ? new Color(180, 150, 90, 220)
                : new Color(110, 90, 60, 220);

        sb.Draw(px, bounds, bg);
        DrawBorder(sb, bounds, border);

        float ts = scale * UI_SCALE;
        Vector2 size = font.MeasureString(label) * ts;
        Vector2 pos = new Vector2(
            bounds.X + (bounds.Width - size.X) * 0.5f,
            bounds.Y + (bounds.Height - size.Y) * 0.5f - 1f);

        Color text = active ? new Color(255, 235, 190) : Color.White;
        Utils.DrawBorderString(sb, label, pos, text, ts);
    }

    private static void DrawBorder(SpriteBatch sb, Rectangle r, Color c)
    {
        var px = TextureAssets.MagicPixel.Value;
        sb.Draw(px, new Rectangle(r.X, r.Y, r.Width, 1), c);
        sb.Draw(px, new Rectangle(r.X, r.Bottom - 1, r.Width, 1), c);
        sb.Draw(px, new Rectangle(r.X, r.Y, 1, r.Height), c);
        sb.Draw(px, new Rectangle(r.Right - 1, r.Y, 1, r.Height), c);
    }
    #endregion
}
