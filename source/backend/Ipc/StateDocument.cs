using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Encodings.Web;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Overlay.Themes;
using Apparition.Backend.OpenControls;

namespace Apparition.Backend.Ipc;

public sealed record StateRow(string Label, string Value, bool Enabled,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool IsSpacer = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] OpeningAction? OpeningAction = null);
public sealed partial record StateDocument(int Version, int ProcessId, ulong Generation, ulong LastSequence, bool IsOpen,
    string Accent, string Title, string Context, string Notice, int SelectedIndex, IReadOnlyList<StateRow> Rows,
    MenuAppearance? Appearance = null, ulong NavigationRevision = 0, bool IsRootMenu = true,
    LocalWidthEditorState? WidthEditor = null, string? MenuUnlock = null,
    InstructionPositionEditor? InstructionPositionEditor = null, bool SmoothRainbowTheme = false,
    // Older factory callers omit the context field rather than publishing an invalid null
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ulong? MenuRevision = null, LocalMenuPositionEditorState? MenuPositionEditor = null, MenuCredits? MenuCredits = null, MenuPreferenceRefresh? MenuPreferenceRefresh = null, MenuDesignTransition? MenuDesignTransition = null, MenuOpening? MenuOpening = null, MenuOpenTransition? MenuOpenTransition = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] MenuPreferenceRebuild? MenuPreferenceRebuild = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] MainOpeningControlsState? MainOpeningControls = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] OpeningPoliciesState? OpeningPolicies = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? MenuSurface = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] LocalMenuEligibilityState? LocalMenuEligibility = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] MenuOptionCallback? MenuOptionCallback = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] MenuBooleanPaint? MenuBooleanPaint = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ulong? MenuChromeRevision = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] InstructionHudState? InstructionHud = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] InstructionThemeState? InstructionTheme = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ChromeThemeState? ChromeTheme = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] MenuMonitorWait? MenuMonitorWait = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] MenuUseContinuation? MenuUseContinuation = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] MenuMonitorContinuation? MenuMonitorContinuation = null) {
    public const int MaximumBytes = 32768;
    private static readonly JsonSerializerOptions Options = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static StateDocument Create(int processId, ulong sequence, bool isOpen,
        MenuSnapshot snapshot, int selectedIndex, string accent = "#3998FE", MenuAppearance? appearance = null,
        ulong navigationRevision = 0, bool isRootMenu = true, LocalWidthEditorState? widthEditor = null,
        string? menuUnlock = null, InstructionPositionEditor? instructionPositionEditor = null, bool smoothRainbowTheme = false, ulong? menuRevision = null, LocalMenuPositionEditorState? menuPositionEditor = null, MenuCredits? menuCredits = null, MenuPreferenceRefresh? menuPreferenceRefresh = null, MenuDesignTransition? menuDesignTransition = null, MenuOpening? menuOpening = null, MenuOpenTransition? menuOpenTransition = null, MenuPreferenceRebuild? menuPreferenceRebuild = null, MainOpeningControlsState? mainOpeningControls = null, OpeningPoliciesState? openingPolicies = null, string? menuSurface = null, LocalMenuEligibilityState? localMenuEligibility = null, MenuOptionCallback? menuOptionCallback = null, MenuBooleanPaint? menuBooleanPaint = null, ulong? menuChromeRevision = null, InstructionHudState? instructionHud = null, InstructionThemeState? instructionTheme = null, ChromeThemeState? chromeTheme = null, MenuMonitorWait? menuMonitorWait = null, MenuUseContinuation? menuUseContinuation = null, MenuMonitorContinuation? menuMonitorContinuation = null) {
        if (menuChromeRevision == 0) throw new InvalidOperationException("Chrome creation revision must be positive");
        ValidateInstructionHud(instructionHud, localMenuEligibility, appearance);
        ValidateInstructionTheme(instructionTheme, instructionHud);
        ValidateOpeningMirror(openingPolicies, mainOpeningControls);
        if (appearance is not null && !appearance.IsSupported()) throw new InvalidOperationException("Unsupported menu appearance");
        ValidateLocalMonitor(menuSurface, localMenuEligibility);
        if (menuPreferenceRebuild is not null && (!menuPreferenceRebuild.IsSupported() || !isOpen || isRootMenu
            || snapshot.Rows.Count == 0 || selectedIndex < 0 || selectedIndex >= snapshot.Rows.Count
            || widthEditor is not null || instructionPositionEditor is not null || menuPositionEditor is not null
            || menuCredits is not null || menuUnlock is not null || menuPreferenceRefresh is not null
            || menuDesignTransition is not null || menuOpenTransition is not null)) {
            throw new InvalidOperationException("Preference rebuild requires an open populated exclusive child menu");
        }
        if (menuOpening is not null && menuPreferenceRebuild is { Phase: "rebuild" }) {
            throw new InvalidOperationException("Hidden preference rebuild cannot retain opening rows");
        }
        if (menuOpenTransition is not null && (!menuOpenTransition.IsSupported() || !isOpen
            || (snapshot.Rows.Count == 0 && selectedIndex != 0)
            || (snapshot.Rows.Count > 0 && (selectedIndex < 0 || selectedIndex >= snapshot.Rows.Count))
            || widthEditor is not null || instructionPositionEditor is not null || menuPositionEditor is not null
            || menuCredits is not null || menuUnlock is not null || menuPreferenceRefresh is not null || menuDesignTransition is not null)) {
            throw new InvalidOperationException("Normal opening requires an open ordinary exclusive menu with a valid retained cursor");
        }
        if (menuOpening is not null && (!isOpen || !menuOpening.IsSupported(snapshot.Rows.Count, appearance ?? MenuAppearance.Default)
            || selectedIndex < 0 || selectedIndex >= snapshot.Rows.Count || widthEditor is not null || instructionPositionEditor is not null
            || menuPositionEditor is not null || menuCredits is not null || menuUnlock is not null || menuPreferenceRefresh is { Phase: "cooldown" }
            || menuDesignTransition is { Phase: "rebuild" } || menuOpenTransition is { Phase: "rebuild" })) {
            throw new InvalidOperationException("Opening metadata requires an open ordinary menu with active visible row durations");
        }
        if (menuDesignTransition is not null && (!menuDesignTransition.IsSupported(appearance ?? MenuAppearance.Default) || !isOpen || isRootMenu
            || snapshot.Rows.Count == 0 || selectedIndex < 0 || selectedIndex >= snapshot.Rows.Count
            || widthEditor is not null || instructionPositionEditor is not null || menuPositionEditor is not null
            || menuCredits is not null || menuUnlock is not null || menuPreferenceRefresh is not null))
        {
            throw new InvalidOperationException("Design transition metadata requires an open populated exclusive child menu");
        }
        if (menuPreferenceRefresh is not null && (!menuPreferenceRefresh.IsSupported() || !isOpen || isRootMenu
            || snapshot.Rows.Count == 0 || selectedIndex < 0 || selectedIndex >= snapshot.Rows.Count
            || widthEditor is not null || instructionPositionEditor is not null || menuPositionEditor is not null
            || menuCredits is not null || menuUnlock is not null)) {
            throw new InvalidOperationException("Preference refresh metadata requires an open populated exclusive child menu");
        }
        if (menuPreferenceRefresh is { Phase: "selector" }) {
            // Only the source Menu selector can wait without replacing its existing row HUD
            if (mainOpeningControls is null || selectedIndex != 0 || !HasSelectorTopology(snapshot.Rows)) {
                throw new InvalidOperationException("Selector wait requires the captured Open Controls Menu selector");
            }
        }
        if (menuCredits is not null && (!menuCredits.IsSupported() || !isOpen || isRootMenu
            || snapshot.Rows.Count != 0 || selectedIndex != 0 || widthEditor is not null || instructionPositionEditor is not null
            || menuPositionEditor is not null || menuUnlock is not null)) {
            throw new InvalidOperationException("Credits metadata requires an open empty exclusive presentation");
        }
        if (menuPositionEditor is not null && (!menuPositionEditor.IsSupported() || !isOpen || isRootMenu
            || snapshot.Rows.Count != 0 || selectedIndex != 0 || widthEditor is not null
            || instructionPositionEditor is not null || menuUnlock is not null)) {
            throw new InvalidOperationException("Menu position editor metadata requires an open empty exclusive editor");
        }
        if (instructionPositionEditor is not null && (!instructionPositionEditor.IsSupported() || !isOpen || isRootMenu
            || snapshot.Rows.Count != 0 || selectedIndex != 0 || appearance?.InstructionsVisible != true
            || widthEditor is not null || menuUnlock is not null)) {
            throw new InvalidOperationException("Instruction position editor metadata requires an open empty editor with visible instructions");
        }
        if (snapshot.Rows.Count > 128) throw new InvalidOperationException("The menu exceeds 128 rows");
        foreach (var row in snapshot.Rows) {
            if (row.IsSpacer && (row.Label.Length != 0 || row.Value.Length != 0 || row.Enabled
                || row.Action.Kind != MenuActionKind.None || row.OpeningAction is not null)) {
                throw new InvalidOperationException("A spacer must be an empty disabled non-action row");
            }
            if (row.OpeningAction is { } action && !OpeningActionCatalog.TryFind(action, out _)) {
                throw new InvalidOperationException("Unknown opening row action");
            }
        }
        // A source blank occupies space but never owns the selected cursor
        if (selectedIndex >= 0 && selectedIndex < snapshot.Rows.Count && snapshot.Rows[selectedIndex].IsSpacer) {
            throw new InvalidOperationException("A spacer cannot be selected");
        }
        // Transiently paused controls cannot advertise an active selected-row action
        string detail = menuOptionCallback is null && menuUnlock is null && menuPreferenceRefresh is null && menuDesignTransition is null && menuOpenTransition is null && menuPreferenceRebuild is null && selectedIndex >= 0 && selectedIndex < snapshot.Rows.Count
            ? snapshot.Rows[selectedIndex].Detail : "";
        // Compare exactly the sanitized text that can reach the native renderer
        string notice = MenuNoticeComposer.Compose(Limit(snapshot.Notice, 1500, 6000, true),
            Limit(detail, 1500, 6000, true));
        // Bounds apply to encoded content as well as visible character counts
        var result = new StateDocument(1, processId, 0, sequence, isOpen, accent, Limit(snapshot.Title, 100, 200),
            Limit(snapshot.Context, 120, 300), Limit(notice, 1500, 6000, true), selectedIndex,
            snapshot.Rows.Select(row => new StateRow(Limit(row.Label, 64, 96),
                Limit(row.Value, 16, 32), row.Enabled, row.IsSpacer, row.OpeningAction)).ToArray(), appearance, navigationRevision, isRootMenu, widthEditor, menuUnlock, instructionPositionEditor, smoothRainbowTheme, menuRevision, menuPositionEditor, menuCredits, menuPreferenceRefresh, menuDesignTransition, menuOpening, menuOpenTransition, menuPreferenceRebuild, mainOpeningControls, openingPolicies, menuSurface, localMenuEligibility, menuOptionCallback, menuBooleanPaint, menuChromeRevision, instructionHud, instructionTheme, chromeTheme, menuMonitorWait, menuUseContinuation, menuMonitorContinuation);
        result.ValidateChromeTheme();
        result.ValidateUseContinuation();
        result.ValidateMonitorWait();
        result.ValidateMonitorContinuation();
        result.ValidateOptionCallback();
        result.ValidateBooleanPaint();
        // Explain a retained indicator when a newer confirmed state differs
        string paintNotice = result.BooleanPaintNotice();
        return paintNotice.Length == 0 ? result : result with {
            Notice = Limit(MenuNoticeComposer.Compose(paintNotice, result.Notice), 1500, 6000, true)
        };
    }

    public byte[] Serialize() {
        if (MenuChromeRevision == 0) throw new InvalidOperationException("Chrome creation revision must be positive");
        // Record copies must retain the same strict public monitor shape
        ValidateLocalMonitor(MenuSurface, LocalMenuEligibility);
        ValidateUseContinuation();
        ValidateMonitorWait();
        ValidateMonitorContinuation();
        ValidateOptionCallback();
        ValidateBooleanPaint();
        // Direct record copies cannot publish a competing Main mirror
        ValidateInstructionHud(InstructionHud, LocalMenuEligibility, Appearance);
        ValidateInstructionTheme(InstructionTheme, InstructionHud);
        ValidateChromeTheme();
        ValidateOpeningMirror(OpeningPolicies, MainOpeningControls);
        if (MenuPreferenceRefresh is { Phase: "selector" }) {
            // Record copies retain the same wire topology and exclusive selector scope
            if (!IsOpen || IsRootMenu || MainOpeningControls is null || SelectedIndex != 0
                || !HasSelectorWireTopology(Rows)
                || WidthEditor is not null || InstructionPositionEditor is not null || MenuPositionEditor is not null
                || MenuCredits is not null || MenuUnlock is not null || MenuDesignTransition is not null
                || MenuOpenTransition is not null || MenuPreferenceRebuild is not null) {
                throw new InvalidOperationException("Selector wait requires the captured Open Controls Menu selector");
            }
        }
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(this, Options);
        if (bytes.Length > MaximumBytes) throw new InvalidOperationException("The menu state exceeds 32 KiB");
        return bytes;
    }

    private void ValidateOptionCallback() {
        if (MenuOptionCallback is not { } callback) return;
        // A callback lock belongs to an ordinary visible menu, never an editor or a closed frame
        if (!callback.IsSupported() || !IsOpen || Rows is null || Rows.Count == 0
            || WidthEditor is not null || InstructionPositionEditor is not null || MenuPositionEditor is not null
            || MenuCredits is not null || MenuUnlock is not null || MenuPreferenceRefresh is not null
            || MenuDesignTransition is not null && callback.Kind != "plain" || MenuOpenTransition is not null || MenuPreferenceRebuild is not null
            || SelectedIndex < 0 || SelectedIndex >= Rows.Count || Rows[SelectedIndex].IsSpacer
            || callback.Kind == "boolean" && callback.Phase == "cooldown" && MenuOpening is not null) {
            throw new InvalidOperationException("Callback wait requires an exclusive open populated ordinary menu");
        }
    }

    private static void ValidateLocalMonitor(string? surface, LocalMenuEligibilityState? eligibility) {
        if (surface is not null && surface is not ("main" or "quick"))
            throw new InvalidOperationException("Unsupported menu family");
        if (eligibility is not null && !eligibility.IsSupported())
            throw new InvalidOperationException("Unsupported local menu eligibility");
    }

    private static void ValidateOpeningMirror(OpeningPoliciesState? policies, MainOpeningControlsState? main) {
        if (policies is not null && !policies.MatchesMain(main)) {
            throw new InvalidOperationException("Opening policies require their exact Main tuple mirror");
        }
    }

    private static bool HasSelectorTopology(IReadOnlyList<MenuRow> rows) {
        int offset = SelectorActionOffset(rows.Count);
        if (offset < 0 || rows[0].Label != "Menu" || rows[1].Label != "Bind Slot"
            || rows[0].IsSpacer || rows[1].IsSpacer || rows[0].OpeningAction is not null || rows[1].OpeningAction is not null
            || rows[0].Action.Kind != MenuActionKind.ChangeOpeningSelector || rows[0].Action.Slot != 0
            || rows[1].Action.Kind != MenuActionKind.ChangeOpeningSelector || rows[1].Action.Slot != 1
            || !rows[2].IsSpacer || rows[2].Label.Length != 0 || rows[2].Value.Length != 0 || rows[2].Enabled
            || rows[2].OpeningAction is not null || rows[2].Action.Kind != MenuActionKind.None) return false;
        // None follows the applied slot; its presence cannot be inferred from the staged slot caption
        if (offset == 4 && (rows[3].Label != "None" || rows[3].IsSpacer || rows[3].OpeningAction is not null
            || rows[3].Action.Kind != MenuActionKind.ChangeMainOpeningAction || rows[3].Action.Slot != -1)) return false;
        for (int index = offset; index < rows.Count; index++) {
            var expected = (OpeningAction)(index - offset);
            if (rows[index].IsSpacer || rows[index].OpeningAction != expected
                || rows[index].Action.Kind != MenuActionKind.ChangeMainOpeningAction
                || rows[index].Action.Slot != (int)expected) return false;
        }
        return true;
    }

    private static bool HasSelectorWireTopology(IReadOnlyList<StateRow> rows) {
        int offset = SelectorActionOffset(rows.Count);
        if (offset < 0 || rows[0].Label != "Menu" || rows[1].Label != "Bind Slot"
            || rows[0].IsSpacer || rows[1].IsSpacer || rows[0].OpeningAction is not null || rows[1].OpeningAction is not null
            || !rows[2].IsSpacer || rows[2].Label.Length != 0 || rows[2].Value.Length != 0 || rows[2].Enabled
            || rows[2].OpeningAction is not null) return false;
        if (offset == 4 && (rows[3].Label != "None" || rows[3].IsSpacer || rows[3].OpeningAction is not null)) return false;
        // Physical key captions may change, while typed IDs keep the source catalog order exact
        for (int index = offset; index < rows.Count; index++) {
            if (rows[index].IsSpacer || rows[index].OpeningAction != (OpeningAction)(index - offset)) return false;
        }
        return true;
    }

    private static int SelectorActionOffset(int count) => count switch {
        16 => 3,
        17 => 4,
        _ => -1
    };

    private static string Limit(string? text, int maximumCharacters, int maximumBytes, bool allowNewline = false) {
        var result = new StringBuilder();
        int bytes = 0;
        foreach (var rune in (text ?? "").EnumerateRunes()) {
            if (Rune.IsControl(rune) && !(allowNewline && rune.Value == '\n')) continue;
            // Quotes and slashes can double in JSON, so include that cost in the budget
            int width = JsonEncodedText.Encode(rune.ToString(), Options.Encoder).EncodedUtf8Bytes.Length;
            if (result.Length + rune.Utf16SequenceLength > maximumCharacters || bytes + width > maximumBytes) break;
            result.Append(rune.ToString());
            bytes += width;
        }
        return result.ToString();
    }
}
