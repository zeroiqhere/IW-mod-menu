namespace Apparition.Backend.Controller;

public sealed partial class BackendController {
    private LocalMenuChromeState menuChrome = new();
    // Boundary tests inspect only the committed scalar, never the actor owner
    internal ulong CommittedChromeRevision => menuChrome.Revision;

    private void BeginMenuUnlock(Action leaveEditor) {
        // Clock and the future creation counter are checked before changing the parent path
        var prepared = LocalMenuUnlockState.Start(clock);
        var chrome = menuChrome.Prepare(admittedMenuOwner);
        // Normal editor exits save after SoftUnlock; a separate close can return before that wait
        deferEditorPreferenceExit = true;
        try { leaveEditor(); }
        finally { deferEditorPreferenceExit = false; }
        menuUnlock = prepared;
        menuChrome = chrome;
    }
}
