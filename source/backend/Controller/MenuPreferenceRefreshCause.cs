namespace Apparition.Backend.Controller;

// Source boolean callbacks and selector callbacks have different captured data and waits
internal enum MenuPreferenceRefreshCause {
    Appearance,
    MainOpeningControls,
    MainOpeningSelector,
    QuickOpeningControls,
    QuickOpeningSelector,
    QuickMenuDisabled,
    MainOpeningSelectorNoRedraw,
    QuickOpeningSelectorNoRedraw
}
