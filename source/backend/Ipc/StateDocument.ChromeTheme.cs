namespace Apparition.Backend.Ipc;

public sealed partial record StateDocument {
    private void ValidateChromeTheme() {
        // Omitted metadata preserves legacy rendering without claiming a source chrome owner
        if (ChromeTheme is null) return;
        if (!ChromeTheme.IsSupported()) throw new InvalidOperationException("Unsupported chrome theme descriptor");
        if (!ChromeTheme.Exists) return;
        // SoftUnlock rollback can retain actual chrome even when its monitor phase returns to rebuild
        if (!IsOpen || (Appearance ?? syndishanx_iw_tool.Overlay.MenuAppearance.Default).Design != ChromeTheme.CreationDesign
            || MenuChromeRevision != ChromeTheme.CreationRevision
            || LocalMenuEligibility is not { Life: "alive" or "dead", HasMenu: true }
            || LocalMenuEligibility.Revision != ChromeTheme.AdmissionRevision
            || MenuOpenTransition is { Phase: "rebuild" } || MenuDesignTransition is { Phase: "rebuild" }
            || MenuPreferenceRebuild is { Phase: "rebuild" })
            throw new InvalidOperationException("Chrome theme requires the matching existing actor-owned chrome");
    }
}
