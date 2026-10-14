namespace syndishanx_iw_tool.Overlay.Themes;

// These facts describe the source's cached structure, never a newly read feature value
public sealed record ObservedThemeWidget {
    public ThemeWidgetIdentity Identity { get; }
    public bool IsBooleanOption { get; }
    public bool BooleanOn { get; }
    public bool IsSelected { get; }
    public bool SourceFeatureEnabled { get; }
    public int? RowIndex { get; }

    public ObservedThemeWidget(ThemeWidgetIdentity identity, bool isBooleanOption = false,
        bool booleanOn = false, bool isSelected = false, bool sourceFeatureEnabled = false, int? rowIndex = null) {
        Identity = identity ?? throw new ArgumentNullException(nameof(identity));
        bool row = identity.Lifetime == ThemeWidgetLifetime.Rows;
        if (row != rowIndex.HasValue || rowIndex < 0) throw new ArgumentException("Only row widgets require a nonnegative source index", nameof(rowIndex));
        RowIndex = rowIndex;
        IsBooleanOption = isBooleanOption;
        BooleanOn = booleanOn;
        IsSelected = isSelected;
        // LUI targets additionally require their declared source feature to be enabled
        SourceFeatureEnabled = sourceFeatureEnabled;
    }
}
