using System;
using System.Collections.Generic;
using Apparition.Backend.QuickMenu;
using syndishanx_iw_tool.Players.LocalObservation;
using syndishanx_iw_tool.Players;
using syndishanx_iw_tool.Overlay.Players;
using System.Text.Json.Serialization;

namespace syndishanx_iw_tool.Overlay;

// Player labels are copied from the current roster and remain display hints
public sealed record OverlayPlayer(int Slot, string Name);

// Capabilities and status values come from the backend instead of menu assumptions
public sealed record OverlayMenuState(bool Preview, bool Connected, bool Busy, bool CanConnect,
    bool CanPersonalGodmode, string PersonalGodmodeValue, string PersonalGodmodeStatus,
    bool CanLobbyAmmo, string LobbyAmmoValue, string LobbyAmmoStatus,
    bool CanRefreshPlayers, bool CanSelectPlayers, IReadOnlyList<OverlayPlayer> Players,
    int SelectedSlot, string? SelectedName, bool CanTeammateGodmode,
    string TeammateGodmodeValue, string TeammateGodmodeStatus,
    bool FeatureHotkeys, string RecentMessages) {
    // Recovery is private session metadata and grants no actor or gameplay authority
    [JsonIgnore] public bool NeedsSessionRecovery { get; init; }
    // Aggregate state comes from a complete owned cohort, not individual capability flags
    public bool CanAllPlayersGodmode { get; init; }
    public string AllPlayersGodmodeValue { get; init; } = "UNKNOWN";
    public string AllPlayersGodmodeStatus { get; init; } = "A complete verified lobby observation is unavailable";
    // Whole-lobby authority stays private with the exact cached observation
    [JsonIgnore] public CohortGodmodeTarget? AllPlayersGodmodeTarget { get; init; }
    // The selected receipt comes from strict session capture, not a roster label
    [JsonIgnore] public PlayerMenuTarget? SelectedPlayerTarget { get; init; }
    // This route only reopens an existing original owner that still needs cleanup
    [JsonIgnore] public bool CanRecoverSelectedTeammate { get; init; }
    // The personal feature owner supplies this target; a roster selection cannot fill it
    public PlayerIdentity? PersonalGodmodeIdentity { get; init; }
    // An owned OFF retry stays separate from permission to enable a new change
    public bool CanRestorePersonalGodmode { get; init; }
    // Production owners require a verified target even when capture is unavailable
    public bool PersonalGodmodeRequiresVerifiedTarget { get; init; }
}

public static class OverlayMenuBuilder {
    private const int MaximumHistoryCharacters = 1024;
    private const string SharedAmmoWarning = "Infinite Ammo affects everyone in the lobby, including the host";
    private const string PrivateAmmoUnavailable = "Personal infinite ammo is unavailable";
    private const string ConnectGuidance = "Spawn first, then select Connect after spawning in the root Apparition menu to unlock game features";
    private const string BlockedConnectionGuidance = "Connection is unavailable. Check Messages for the current session status";

    public static MenuSnapshot Build(MenuPage page, OverlayMenuState state, string accent = MenuThemes.DefaultAccent,
        MenuAppearance? appearance = null, MenuChoiceDrafts? choices = null, bool choiceControlsAvailable = false,
        bool widthEditorControlsAvailable = false, bool instructionPositionEditorControlsAvailable = false, bool smoothRainbowTheme = false, ScrollAnimationDraft? scrollAnimationDraft = null, bool menuPositionEditorControlsAvailable = false, MenuDesignDraft? menuDesignDraft = null, OpeningControlsView? openingControls = null, QuickMenuViewContext? quickContext = null) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(state.Players);
        appearance ??= MenuAppearance.Default;
        if (!appearance.IsSupported()) throw new ArgumentException("Menu appearance contains an unsupported choice", nameof(appearance));
        choices ??= MenuChoiceDrafts.FromAppearance(appearance);
        menuDesignDraft ??= new();
        if (!menuDesignDraft.IsSupported()) throw new System.ArgumentException("Unsupported design candidate", nameof(menuDesignDraft));
        scrollAnimationDraft ??= ScrollAnimationDraft.FromAppearance(appearance);
        if (!scrollAnimationDraft.IsSupported()) throw new ArgumentException("Scroll animation draft contains an unsupported value", nameof(scrollAnimationDraft));
        if (!choices.IsSupported()) throw new ArgumentException("Menu choice contains an unsupported index", nameof(choices));
        if (!Enum.IsDefined(page)) {
            throw new ArgumentOutOfRangeException(nameof(page));
        }
        bool backendAllowed = !state.Preview && !state.Busy;
        bool connectedAllowed = backendAllowed && state.Connected;
        string context = state.Preview ? "PREVIEW" : state.Connected ? "CONNECTED" : "DISCONNECTED";
        if (state.Busy) {
            context += " · WORKING";
        }
        string notice = state.Preview ? "Preview mode: game actions are disabled"
            : state.Busy ? "An operation is in progress"
            : !state.Connected ? state.CanConnect ? ConnectGuidance : BlockedConnectionGuidance : "";
        string title;
        var rows = new List<MenuRow>();

        // Page links stay available while game operations are disabled
        switch (page) {
            case MenuPage.QuickMenu:
                title = "Quick Menu";
                // One context supplies the copied actor facts for both presentation and action checks
                // Missing context keeps the legacy inert view and cannot authorize a source callback
                rows.AddRange(QuickMenuRows.Build(state, quickContext?.Eligibility ?? new(QuickPlayerLife.Unknown, null, null),
                    quickContext?.Observation ?? LocalPlayerObservation.BindingUnavailable,
                    quickContext?.SelectedLocalIdentity, quickContext?.ExpectedProcessId ?? 0));
                notice = Combine(notice, quickContext is null || quickContext.Observation.Identity is null
                    ? "Quick opening is unavailable in this build"
                    : quickContext.OpeningAvailable ? "" : "This view uses the current player state; Quick opening is not enabled yet");
                if (quickContext?.Observation.IsSupported() == true) notice = Combine(notice, quickContext.Observation.Status);
                if (quickContext?.Observation.ControlsDisabled == true) notice = Combine(notice, "Quick controls paused");
                break;
            case MenuPage.Main:
                title = appearance.Design == "Native" ? "Main Menu" : "Apparition";
                // Connection guidance follows the actual session rather than the selected page
                string connectDetail = state.Preview ? "Preview mode: game actions are disabled"
                    : state.Connected ? "Connected. Choose Basic Scripts for your Godmode or All Players Menu for lobby Infinite Ammo"
                    : state.Busy ? "Wait for the current operation to finish"
                    : state.CanConnect ? "Only connect after spawning in the host's Steam custom Zombies match"
                    : BlockedConnectionGuidance;
                rows.Add(new MenuRow("Connect after spawning", state.Preview ? "UNAVAILABLE" : state.Connected ? "CONNECTED" : "",
                    backendAllowed && !state.Connected && state.CanConnect, new MenuAction(MenuActionKind.Connect), connectDetail));
                // Keep the represented Apparition pages in source declaration order
                rows.Add(PageRow("Basic Scripts", MenuPage.Personal));
                rows.Add(PageRow("Menu Customization", MenuPage.Customization));
                rows.Add(PageRow("Players Menu", MenuPage.Players));
                rows.Add(PageRow("All Players Menu", MenuPage.AllPlayers));
                rows.Add(PageRow("Messages", MenuPage.History));
                rows.Add(PageRow("Feature Support", MenuPage.Support));
                // Exit is always available so a separate handler can perform cleanup
                rows.Add(new MenuRow("Exit", "", true, new MenuAction(MenuActionKind.Exit)));
                break;
            case MenuPage.Personal:
                title = "Basic Scripts";
                // The source inventory preserves the one supported personal action and marks every other callback unavailable
                rows.AddRange(BasicScriptsMenuRows.Build(state));
                break;
            case MenuPage.Players:
                title = "Players Menu";
                rows.Add(new MenuRow("Refresh players", "", connectedAllowed && state.CanRefreshPlayers,
                    new MenuAction(MenuActionKind.RefreshPlayers)));
                if (!state.Preview) {
                    if (state.CanRecoverSelectedTeammate
                        && state.SelectedPlayerTarget is { Route: PlayerMenuRoute.Teammate } cleanupTarget) {
                        // Navigation returns to an existing owned actor and never creates a new selection
                        rows.Add(new MenuRow("Return to cleanup: " + cleanupTarget.Identity.Name, ">",
                            connectedAllowed, new MenuAction(MenuActionKind.OpenPage, MenuPage.SelectedPlayer) {
                                ExpectedTarget = cleanupTarget
                            }, "Open the original teammate's Basic Scripts to turn off its owned change"));
                    }
                    AddPlayers(rows, state, connectedAllowed);
                }
                if (rows.Count == 1) {
                    notice = Combine(notice, "No players detected");
                }
                break;
            case MenuPage.SelectedPlayer:
                title = PlayerOptionsMenuRows.HasMatchingTarget(state, state.SelectedPlayerTarget)
                    ? state.SelectedPlayerTarget!.Identity.Name : "Selected Player";
                rows.AddRange(PlayerOptionsMenuRows.Build(state));
                if (!PlayerOptionsMenuRows.HasMatchingTarget(state, state.SelectedPlayerTarget))
                    notice = Combine(notice, "Select a current player from the Players Menu");
                break;
            case MenuPage.SelectedBasicScripts:
                title = "Basic Scripts";
                rows.AddRange(SelectedBasicScriptsMenuRows.Build(state));
                break;
            case MenuPage.AllPlayers:
                title = "All Players Menu";
                rows.AddRange(AllPlayersMenuRows.Build(state));
                notice = Combine(notice, SharedAmmoWarning);
                break;
            case MenuPage.Customization:
                title = "Menu Customization";
                rows.Add(PageRow("Menu Credits", MenuPage.MenuCredits));
                rows.Add(openingControls?.CanEdit == true
                    ? PageRow("Open Controls", MenuPage.OpenControls)
                    : UnavailableRow("Open Controls", openingControls is null ? "Aim + melee opens; melee goes back or closes; aim / fire scroll; use selects"
                        : "Opening controls are unavailable without verified saved action and choice bindings"));
                rows.Add(new MenuRow("Width Editor", ">", choiceControlsAvailable && widthEditorControlsAvailable,
                    new MenuAction(MenuActionKind.OpenPage, MenuPage.WidthEditor),
                    choiceControlsAvailable && widthEditorControlsAvailable ? "Select to open Width Editor. Edits apply immediately; melee returns here"
                        : "Width controls unavailable. Save +attack, +speed_throw, +actionslot 3 and +actionslot 4 bindings in IW controls, then restart the menu"));
                rows.Add(new MenuRow("Reposition Menu", menuPositionEditorControlsAvailable ? ">" : "UNAVAILABLE",
                    menuPositionEditorControlsAvailable, new MenuAction(MenuActionKind.OpenPage, MenuPage.MenuPositionEditor),
                    menuPositionEditorControlsAvailable ? "Select to reposition the menu. Edits apply immediately; melee returns here"
                        : "Reposition controls unavailable. Save action slots 1 / 2 / 3 / 4, frag, smoke and melee bindings, then restart the menu"));
                rows.Add(PageRow("Menu Instructions", MenuPage.Instructions));
                rows.Add(PageRow("Main Design Color", MenuPage.ThemeColors));
                rows.Add(PageRow("Menu Preferences", MenuPage.Preferences));
                break;
            case MenuPage.OpenControls:
                title = "Open Controls";
                rows.AddRange(OpeningControlsMenuRows.Build(openingControls));
                if (openingControls is not null) notice = Combine(notice, OpeningControlsMenuRows.AppliedSummary(openingControls));
                break;
            case MenuPage.MenuCredits:
                title = "Menu Customization";
                // Dedicated credits metadata replaces ordinary options while retaining the title
                break;
            case MenuPage.WidthEditor:
                title = "Width Editor";
                // Dedicated metadata drives the editor panel, so no ordinary rows are fabricated
                break;
            case MenuPage.MenuPositionEditor:
                title = "Menu Customization";
                // Source soft lock retains the parent title and removes ordinary options
                break;
            case MenuPage.Instructions:
                title = "Menu Instructions";
                rows.AddRange(MenuInstructionRows.Build(appearance, instructionPositionEditorControlsAvailable));
                break;
            case MenuPage.InstructionPositionEditor:
                title = "Menu Instructions";
                // The local editor uses phase metadata and no ordinary option rows
                break;
            case MenuPage.Preferences:
                title = "Menu Preferences";
                rows.Add(ChoiceRow("Design", menuDesignDraft.Candidate, appearance.Design, (int)MenuPreference.Design, choiceControlsAvailable));
                rows.Add(ChoiceRow("Bool Display", choices.Candidate(0), appearance.BooleanDisplay, 0, choiceControlsAvailable));
                rows.Add(ChoiceRow("Bool Box Location", choices.Candidate(1), appearance.BoolBoxLocation, 1, choiceControlsAvailable));
                // The source caption says milliseconds but its stored candidate is ten times smaller
                rows.Add(ChoiceRow("Scroll Animation Time (ms)", scrollAnimationDraft.DisplayValue,
                    appearance.ScrollAnimationMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture) + " ms",
                    (int)MenuPreference.ScrollAnimationTime, choiceControlsAvailable));
                rows.Add(new MenuRow("Quick Exit", appearance.QuickExit ? "ON" : "OFF", true,
                    new MenuAction(MenuActionKind.ChangePreference, Slot: (int)MenuPreference.QuickExit),
                    "Tap melee goes back; hold melee closes this menu"));
                // Configuration can be edited independently of unverified Quick opening eligibility
                rows.Add(OpeningControlsMenuRows.DisableQuickPreference(openingControls));
                rows.Add(PreferenceRow("Spotlight Cursor", appearance.SpotlightCursor ? "ON" : "OFF", MenuPreference.SpotlightCursor));
                // Source BOOL handlers report design conflicts after an ordinary Select
                rows.Add(PreferenceRow("Colored Cursor", appearance.ColoredCursor ? "ON" : "OFF", MenuPreference.ColoredCursor));
                rows.Add(PreferenceRow("Large Cursor", appearance.LargeCursor ? "ON" : "OFF", MenuPreference.LargeCursor));
                rows.Add(PreferenceRow("Option Counter", appearance.OptionCounter ? "ON" : "OFF", MenuPreference.OptionCounter));
                // This local setting also changes display and capacity, so explain its lasting effects
                rows.Add(new MenuRow("Stealth UI", appearance.StealthUI ? "ON" : "OFF", true,
                    new MenuAction(MenuActionKind.ChangePreference, Slot: (int)MenuPreference.StealthUI),
                    "Show five rows; replace Boxes with Text and clear the counter. Turning this off keeps those settings"));
                rows.Add(UnavailableRow("No Target While In Menu", "AI targeting control has not been validated for this IW backend"));
                break;
            case MenuPage.ThemeColors:
                title = "Main Design Color";
                for (int index = 0; index < MenuThemes.All.Count; index++) {
                    MenuTheme theme = MenuThemes.All[index];
                    // Rainbow owns the checked box until a fixed source color is selected
                    rows.Add(new MenuRow(theme.Name,
                        !smoothRainbowTheme && string.Equals(theme.Hex, accent, StringComparison.OrdinalIgnoreCase) ? "ON" : "OFF",
                        true, new MenuAction(MenuActionKind.ChangeAccent, Slot: index)));
                }
                // This source mode is separate from the thirty-six fixed RGB entries
                rows.Add(new MenuRow("Smooth Rainbow", smoothRainbowTheme ? "ON" : "OFF", true,
                    new MenuAction(MenuActionKind.EnableSmoothRainbowTheme),
                    "Select to start the color cycle. Selecting it again keeps it running. Choose a fixed color to stop it"));
                break;
            case MenuPage.History:
                title = "Messages";
                // Retain the newest bounded text without fabricating feature confirmations
                string history = state.RecentMessages ?? "";
                if (history.Length > MaximumHistoryCharacters) {
                    history = history.Substring(history.Length - MaximumHistoryCharacters);
                }
                notice = Combine(notice, string.IsNullOrWhiteSpace(history) ? "No recent messages" : history);
                break;
            case MenuPage.Support:
                title = "Feature Support";
                rows.Add(UnavailableRow("Personal Infinite Ammo", PrivateAmmoUnavailable));
                rows.Add(UnavailableRow("BO3 map and Easter Egg commands",
                    "BO3 map and Easter Egg commands are not available in Infinite Warfare"));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(page));
        }

        // An owned read-only collection prevents later roster changes from altering this menu
        // Known source roles stay private and cannot turn an unavailable row into an action
        for (int index = 0; index < rows.Count; index++) {
            if (rows[index].SourceChoiceRole == MenuSourceChoiceRole.Unknown)
                rows[index] = rows[index] with { SourceChoiceRole = MenuSourceChoiceRoles.ForPageRow(page, index, rows[index].Action) };
        }
        return new MenuSnapshot(title, context, Array.AsReadOnly(rows.ToArray()), notice);
    }

    private static void AddPlayers(List<MenuRow> rows, OverlayMenuState state, bool connectedAllowed) {
        var slots = new HashSet<int>();
        foreach (OverlayPlayer player in state.Players) {
            // Reject ambiguous labels instead of producing a guessed selection payload
            if (player is null || player.Slot is < 0 or > 3
                || string.IsNullOrWhiteSpace(player.Name) || !slots.Add(player.Slot)) {
                throw new ArgumentException("The roster contains an invalid or duplicate player slot", nameof(state));
            }
            string label = player.Name;
            // Strict session selection chooses the owner before every actor reaches source Options
            rows.Add(new MenuRow(label, ">", connectedAllowed && state.CanSelectPlayers,
                new MenuAction(MenuActionKind.SelectPlayer, MenuPage.SelectedPlayer,
                    player.Slot, player.Name)));
        }
    }

    private static MenuRow PageRow(string label, MenuPage page) =>
        new(label, ">", true, new MenuAction(MenuActionKind.OpenPage, page));

    private static MenuRow ChoiceRow(string label, string candidate, string committed, int preference, bool available) =>
        new(label, candidate, available, new MenuAction(MenuActionKind.ChangePreference, Slot: preference),
            available ? "Candidate: " + candidate + "; Applied: " + committed + ". Action slots 3 / 4 choose; Use applies"
                : "Choice controls unavailable. Save bindings for +actionslot 3 and +actionslot 4 in IW controls, then restart the menu");

    // The Select hint describes the supported input without advertising source sliders
    private static MenuRow PreferenceRow(string label, string value, MenuPreference preference) =>
        new(label, value, true, new MenuAction(MenuActionKind.ChangePreference, Slot: (int)preference), "Use selects the next choice");

    private static MenuRow UnavailableRow(string label, string detail) =>
        new(label, "UNAVAILABLE", false, new MenuAction(MenuActionKind.None), detail);

    // A disabled capability may retain a confirmed value, but an absent session cannot
    private static string ConfirmedValue(OverlayMenuState state, string value) =>
        state.Preview || !state.Connected || state.Busy ? "UNKNOWN" : value;

    private static string Combine(string first, string second) =>
        string.IsNullOrEmpty(first) ? second : string.IsNullOrEmpty(second) ? first : first + "\n" + second;
}
