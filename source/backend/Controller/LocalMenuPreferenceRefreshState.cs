using syndishanx_iw_tool.Overlay;
using Apparition.Backend.OpenControls;

namespace Apparition.Backend.Controller;

// Actual preferences commit first while this owner state retains their previous presentation
internal sealed record LocalMenuPreferenceRefreshState(MenuAppearance PreviousAppearance, long StartedTimestamp,
    long LastTimestamp, int BaseMilliseconds = 0, bool PresentationApplied = false,
    MenuPreferenceRefreshCause Cause = MenuPreferenceRefreshCause.Appearance,
    MainOpeningChord? PreviousMainOpeningChord = null, OpeningControlDraft? PreviousOpeningSelector = null,
    QuickOpeningPolicies? PreviousOpeningPolicies = null) {
    private const int RefreshMilliseconds = 180;
    private const int TotalMilliseconds = 430;
    public static bool AppliesTo(MenuPage page, int preference) => page == MenuPage.Preferences
        && preference is (int)MenuPreference.QuickExit or (int)MenuPreference.SpotlightCursor
            or (int)MenuPreference.ColoredCursor or (int)MenuPreference.LargeCursor or (int)MenuPreference.StealthUI;

    public static LocalMenuPreferenceRefreshState Start(MenuAppearance previous, TimeProvider clock) {
        long timestamp = clock.GetTimestamp();
        return new(previous, timestamp, timestamp);
    }

    public static LocalMenuPreferenceRefreshState StartOpeningControls(MenuAppearance appearance,
        MainOpeningChord previousChord, OpeningControlDraft previousSelector, MenuPreferenceRefreshCause cause, TimeProvider clock,
        QuickOpeningPolicies? previousPolicies = null) {
        ArgumentNullException.ThrowIfNull(previousChord);
        ArgumentNullException.ThrowIfNull(previousSelector);
        if (cause is not (MenuPreferenceRefreshCause.MainOpeningControls or MenuPreferenceRefreshCause.MainOpeningSelector
            or MenuPreferenceRefreshCause.QuickOpeningControls or MenuPreferenceRefreshCause.QuickOpeningSelector
            or MenuPreferenceRefreshCause.QuickMenuDisabled or MenuPreferenceRefreshCause.MainOpeningSelectorNoRedraw
            or MenuPreferenceRefreshCause.QuickOpeningSelectorNoRedraw)) {
            throw new ArgumentOutOfRangeException(nameof(cause));
        }
        if (cause is (MenuPreferenceRefreshCause.QuickOpeningControls or MenuPreferenceRefreshCause.QuickOpeningSelector
            or MenuPreferenceRefreshCause.QuickMenuDisabled or MenuPreferenceRefreshCause.QuickOpeningSelectorNoRedraw)
            && previousPolicies is null) {
            throw new ArgumentException("Quick refresh requires a captured policy pair", nameof(previousPolicies));
        }
        if (previousPolicies is not null && !ReferenceEquals(previousPolicies.Main, previousChord)) {
            throw new ArgumentException("Captured Main must belong to the captured policy pair", nameof(previousChord));
        }
        long timestamp = clock.GetTimestamp();
        bool selector = cause is MenuPreferenceRefreshCause.MainOpeningSelector or MenuPreferenceRefreshCause.QuickOpeningSelector
            or MenuPreferenceRefreshCause.MainOpeningSelectorNoRedraw or MenuPreferenceRefreshCause.QuickOpeningSelectorNoRedraw;
        // Selector callers wait 250 ms whether their handler redraws or keeps the existing HUD
        return new(appearance, timestamp, timestamp, selector ? RefreshMilliseconds : 0, selector,
            cause, previousChord, previousSelector, previousPolicies);
    }

    public MainOpeningChord RowsOpeningChord(MainOpeningChord actual) =>
        Cause == MenuPreferenceRefreshCause.MainOpeningControls && !PresentationApplied
            ? PreviousMainOpeningChord ?? throw new InvalidOperationException("Missing captured opening chord") : actual;

    public OpeningControlDraft RowsOpeningSelector(OpeningControlDraft actual) =>
        RetainsOpeningRows && !PresentationApplied
            ? PreviousOpeningSelector ?? throw new InvalidOperationException("Missing captured opening selector") : actual;

    public QuickOpeningPolicies? RowsOpeningPolicies(QuickOpeningPolicies? actual) =>
        RetainsOpeningRows && !PresentationApplied ? PreviousOpeningPolicies : actual;

    private bool RetainsOpeningRows => Cause is MenuPreferenceRefreshCause.MainOpeningControls
        or MenuPreferenceRefreshCause.QuickOpeningControls or MenuPreferenceRefreshCause.QuickMenuDisabled;

    // A same-target selector wait does not own or cancel the independent row fade
    public bool IsNoRedrawSelector => Cause is MenuPreferenceRefreshCause.MainOpeningSelectorNoRedraw
        or MenuPreferenceRefreshCause.QuickOpeningSelectorNoRedraw;

    public (LocalMenuPreferenceRefreshState? State, string? Phase, bool RefreshPresentation) Read(TimeProvider clock) {
        long current = clock.GetTimestamp();
        TimeSpan elapsed;
        try {
            elapsed = clock.GetElapsedTime(StartedTimestamp, current);
        } catch (OverflowException) {
            return Restart(current);
        }
        // A clock fault cannot revert an already displayed style or schedule a second rebuild
        if (current < LastTimestamp || elapsed < TimeSpan.Zero || elapsed == TimeSpan.MaxValue) return Restart(current);
        bool refresh = !PresentationApplied && elapsed >= TimeSpan.FromMilliseconds(RefreshMilliseconds - BaseMilliseconds);
        // A late owner poll still requests the single redraw before retiring the whole wait
        if (elapsed >= TimeSpan.FromMilliseconds(TotalMilliseconds - BaseMilliseconds)) return (null, null, refresh);
        return (this with { LastTimestamp = current }, IsNoRedrawSelector ? "selector"
            : BaseMilliseconds == RefreshMilliseconds || elapsed >= TimeSpan.FromMilliseconds(RefreshMilliseconds)
                ? "cooldown" : "pending", refresh);
    }

    // The stage base avoids timestamp subtraction and never restores an obsolete style
    private (LocalMenuPreferenceRefreshState State, string Phase, bool RefreshPresentation) Restart(long timestamp) =>
        (this with { StartedTimestamp = timestamp, LastTimestamp = timestamp,
            BaseMilliseconds = PresentationApplied ? RefreshMilliseconds : 0 }, IsNoRedrawSelector ? "selector"
                : PresentationApplied ? "cooldown" : "pending", false);

    // Keep incompatible settings together so every pending frame remains valid
    // Pending rows keep the audited flags and Stealth dependencies together
    public MenuAppearance RowsAppearance(MenuAppearance actual) =>
        Cause != MenuPreferenceRefreshCause.Appearance || PresentationApplied ? actual : RetainAppearance(actual, PreviousAppearance);

    // Typed callbacks and legacy refresh share the same source HUD dependencies
    internal static MenuAppearance RetainAppearance(MenuAppearance actual, MenuAppearance previous) => actual with {
        QuickExit = previous.QuickExit, SpotlightCursor = previous.SpotlightCursor,
        ColoredCursor = previous.ColoredCursor, LargeCursor = previous.LargeCursor,
        StealthUI = previous.StealthUI, BooleanDisplay = previous.BooleanDisplay,
        OptionCounter = previous.OptionCounter
    };
}
