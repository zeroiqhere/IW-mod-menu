using System;
using System.Collections.Generic;

namespace syndishanx_iw_tool.Overlay;

// Inputs describe menu movement without binding it to a keyboard or window
public enum OverlayInput { Toggle, Up, Down, Select, Back, PreviousChoice, NextChoice, Close, IncreaseWidth, DecreaseWidth, IncreaseWidthStep, DecreaseWidthStep, FinishWidthEditor, MoveInstructionsUp, MoveInstructionsDown, MoveInstructionsLeft, MoveInstructionsRight, FinishInstructionPositionEditor, MoveMenuUp, MoveMenuDown, MoveMenuLeft, MoveMenuRight, IncreaseMenuPositionStep, DecreaseMenuPositionStep, FinishMenuPositionEditor, FinishMenuCredits,
    // Append scoped monitor intents without changing the older wire IDs
    OpenMain, OpenQuick, JumpBack }

public sealed class OverlayNavigation {
    private const int MaximumRows = 128;
    // Paths retain page names only; the shared cursor is read when a parent returns
    private readonly Stack<MenuPage> mainParents = new();
    private readonly Stack<MenuPage> quickParents = new();
    private MenuPage mainPage = MenuPage.Main;
    private MenuPage quickPage = MenuPage.QuickMenu;
    private readonly Dictionary<MenuPage, int> cursors = new();
    private IReadOnlyList<MenuRow> rows = Array.AsReadOnly(Array.Empty<MenuRow>());

    // A retained snapshot cannot act for a different page before its fresh rows arrive
    private bool rowsCurrent;

    public MenuSurface Surface { get; private set; } = MenuSurface.Main;
    private Stack<MenuPage> Parents => Surface == MenuSurface.Main ? mainParents : quickParents;
    public bool IsOpen { get; private set; }
    public MenuPage Page => PageFor(Surface);
    // Delayed actions use the cursor epoch while animation uses the menu context
    public ulong Revision { get; private set; }
    public ulong MenuRevision { get; private set; }
    public int SelectedIndex { get; private set; }
    public IReadOnlyList<MenuRow> Rows => rows;
    public OverlayNavigation() { }

    // Owned boundary tests can start at the final counter without millions of transitions
    internal OverlayNavigation(ulong revision, ulong menuRevision) {
        Revision = revision;
        MenuRevision = menuRevision;
    }

    public void SetRows(IReadOnlyList<MenuRow> newRows) {
        ArgumentNullException.ThrowIfNull(newRows);
        if (newRows.Count > MaximumRows) {
            throw new ArgumentOutOfRangeException(nameof(newRows), "The menu supports at most 128 rows");
        }
        // Validate and copy the complete snapshot before comparing its logical structure
        var copy = new MenuRow[newRows.Count];
        bool changed = rows.Count != copy.Length;
        for (int index = 0; index < copy.Length; index++) {
            MenuRow row = newRows[index]
                ?? throw new ArgumentException("Menu rows must not contain null", nameof(newRows));
            if (row.Action is null) throw new ArgumentException("Menu rows require an action", nameof(newRows));
            copy[index] = row;
            if (index >= rows.Count || !SameRowTopology(rows[index], row)) changed = true;
        }
        int selected = Math.Clamp(SelectedIndex, 0, Math.Max(0, copy.Length - 1));
        if (copy.Length > 0 && copy[selected].IsSpacer) {
            int replacement = -1;
            for (int scanned = 1; scanned <= copy.Length; scanned++) {
                int index = (selected + scanned) % copy.Length;
                if (!copy[index].IsSpacer) { replacement = index; break; }
            }
            if (replacement < 0) throw new ArgumentException("A menu cannot contain only spacers", nameof(newRows));
            selected = replacement;
        }
        // Check both counters before replacing rows or clamping the retained cursor
        ulong revision = checked(Revision + (selected != SelectedIndex ? 1UL : 0UL));
        ulong menuRevision = checked(MenuRevision + (changed ? 1UL : 0UL));
        rows = Array.AsReadOnly(copy);
        Revision = revision;
        MenuRevision = menuRevision;
        StoreSelection(selected);
        rowsCurrent = true;
    }

    internal bool RowsMatchTopology(IReadOnlyList<MenuRow> candidate) {
        if (candidate is null || candidate.Count != rows.Count) return false;
        for (int index = 0; index < rows.Count; index++) {
            if (candidate[index] is null || !SameRowTopology(rows[index], candidate[index])) return false;
        }
        return true;
    }

    // Display samples and enabled flags do not change the navigation presentation context
    private static bool SameRowTopology(MenuRow first, MenuRow second) =>
        string.Equals(first.Label, second.Label, StringComparison.Ordinal)
        && first.Action == second.Action && first.IsSpacer == second.IsSpacer
        && first.OpeningAction == second.OpeningAction && first.SourceChoiceRole == second.SourceChoiceRole;

    public void Open(MenuSurface surface) {
        if (!Enum.IsDefined(surface)) throw new ArgumentOutOfRangeException(nameof(surface));
        if (IsOpen) {
            if (surface != Surface) throw new InvalidOperationException("Close the active menu before switching families");
            return;
        }
        MenuPage page = PageFor(surface);
        int selected = CursorFor(page);
        bool changedFamily = surface != Surface;
        // Check both epochs before changing the owner or clearing old dispatchable rows
        AdvanceContext(selected);
        Surface = surface;
        StoreSelection(selected);
        if (changedFamily) {
            rows = Array.AsReadOnly(Array.Empty<MenuRow>());
            rowsCurrent = false;
        }
        IsOpen = true;
    }

    // Legacy callers always request the retained Main page
    public void Open() => Open(MenuSurface.Main);

    // Cleanup recovery opens its administrative root without changing Quick history
    internal void OpenCleanupRoot() {
        int selected = CursorFor(MenuPage.Main);
        AdvanceContext(selected);
        Surface = MenuSurface.Main;
        mainPage = MenuPage.Main;
        mainParents.Clear();
        StoreSelection(selected);
        rowsCurrent = false;
        IsOpen = true;
    }

    public void Close() {
        if (!IsOpen) return;
        AdvanceContext(SelectedIndex);
        IsOpen = false;
    }

    public void OpenPage(MenuPage page) {
        if (!Enum.IsDefined(page)) throw new ArgumentOutOfRangeException(nameof(page));
        int selected = CursorFor(page);
        // A failed counter check must leave both the parent path and page untouched
        AdvanceContext(selected);
        Parents.Push(Page);
        StorePage(page);
        StoreSelection(selected);
        // Retain old published snapshots without allowing them to dispatch on this page
        rowsCurrent = false;
    }

    public void SelectIndex(int index) {
        if (!rowsCurrent) throw new InvalidOperationException("Fresh rows are required for this page");
        if (index < 0 || index >= rows.Count || rows[index].IsSpacer) throw new ArgumentOutOfRangeException(nameof(index));
        Revision = checked(Revision + (SelectedIndex != index ? 1UL : 0UL));
        StoreSelection(index);
    }

    public void InvalidatePresentation() {
        // A deliberate source-style rebuild does not change a delayed action's cursor epoch
        MenuRevision = checked(MenuRevision + 1);
    }

    public void InvalidateActionContext() {
        // Applied editor changes invalidate delayed actions without moving the cursor or repainting rows
        Revision = checked(Revision + 1);
    }

    public MenuAction? Handle(OverlayInput input) {
        if (!Enum.IsDefined(input)) throw new ArgumentOutOfRangeException(nameof(input));
        if (input == OverlayInput.Toggle) {
            if (IsOpen) Close(); else Open();
            return null;
        }
        if (!IsOpen) return null;
        // Local close retains the page, cursor and parent path for the next open
        if (input == OverlayInput.Close) { Close(); return null; }
        if (input == OverlayInput.Back) {
            if (Parents.TryPeek(out MenuPage parent)) {
                int selected = CursorFor(parent);
                AdvanceContext(selected);
                Parents.Pop();
                StorePage(parent);
                StoreSelection(selected);
                rowsCurrent = false;
            } else Close();
            return null;
        }
        if (!rowsCurrent || rows.Count == 0) return null;
        // Fresh rows normally clamp first; direct navigation callers receive the same guard
        SelectIndex(Math.Clamp(SelectedIndex, 0, rows.Count - 1));
        switch (input) {
            case OverlayInput.Up:
                MovePastSpacers(-1);
                break;
            case OverlayInput.Down:
                MovePastSpacers(1);
                break;
            case OverlayInput.Select:
                MenuRow row = rows[SelectedIndex];
                return row.Enabled && !row.IsSpacer ? row.Action : null;
        }
        return null;
    }

    private void MovePastSpacers(int direction) {
        int index = SelectedIndex;
        // Disabled capability rows remain selectable; only typed source blanks are skipped
        for (int scanned = 0; scanned < rows.Count; scanned++) {
            index = (index + direction + rows.Count) % rows.Count;
            if (!rows[index].IsSpacer) {
                SelectIndex(index);
                return;
            }
        }
    }

    private void AdvanceContext(int selected) {
        // Compute every affected counter before committing either one
        ulong revision = checked(Revision + 1UL + (selected != SelectedIndex ? 1UL : 0UL));
        ulong menuRevision = checked(MenuRevision + 1UL);
        Revision = revision;
        MenuRevision = menuRevision;
    }

    private MenuPage PageFor(MenuSurface surface) => surface == MenuSurface.Main ? mainPage : quickPage;

    // Source Basic Scripts uses one cursor while private action scopes keep their target
    private static MenuPage CursorKey(MenuPage page) => page == MenuPage.SelectedBasicScripts ? MenuPage.Personal : page;

    private int CursorFor(MenuPage page) => cursors.TryGetValue(CursorKey(page), out int cursor) ? cursor : 0;

    private void StorePage(MenuPage page) {
        if (Surface == MenuSurface.Main) mainPage = page;
        else quickPage = page;
    }

    private void StoreSelection(int selected) {
        SelectedIndex = selected;
        cursors[CursorKey(Page)] = selected;
    }
}
