using System.Collections.Generic;
using Apparition.Backend.OpenControls;
using System.Text.Json.Serialization;
using syndishanx_iw_tool.Overlay.Players;

namespace syndishanx_iw_tool.Overlay;

// Pages describe navigation and do not grant access to a game operation
public enum MenuPage {
    Main, Personal, Players, SelectedPlayer, AllPlayers, Customization, History, Support, ThemeColors, Preferences, WidthEditor, Instructions, InstructionPositionEditor, MenuPositionEditor, MenuCredits, OpenControls, QuickMenu, SelectedBasicScripts
}

// Actions carry intent for a separate backend to validate and handle
public enum MenuActionKind {
    None, OpenPage, Connect, RefreshPlayers, SelectPlayer, TogglePersonalGodmode,
    ToggleLobbyAmmo, ToggleTeammateGodmode, ToggleHotkeys, ChangeAccent, Exit, ChangePreference, ResetInstructionPosition, EnableSmoothRainbowTheme, ChangeOpeningSelector, ChangeMainOpeningAction,
    // Append new actions so existing serialized ordinals remain stable
    RestorePersonalGodmode, ToggleAllPlayersGodmode
}

// A selected name is a display hint, not a stable player or spawn identity
public sealed record MenuAction(MenuActionKind Kind, MenuPage Page = MenuPage.Main,
    int Slot = -1, string? ExpectedName = null) {
    // These owned expectations stay private when an action is copied or serialized
    [JsonIgnore] public PlayerMenuSelectionScope? SelectionScope { get; init; }
    [JsonIgnore] public PlayerMenuTarget? ExpectedTarget { get; init; }
    [JsonIgnore] public CohortGodmodeTarget? ExpectedCohort { get; init; }
}

// Disabled rows remain visible so their unavailable state can be explained
public sealed record MenuRow(string Label, string Value, bool Enabled,
    MenuAction Action, string Detail = "", bool IsSpacer = false, OpeningAction? OpeningAction = null) {
    // The private source declaration has no value, permission or native address
    [JsonIgnore] public MenuSourceChoiceRole SourceChoiceRole { get; init; }
}

// The window renders this snapshot without reading the game or dispatching actions
public sealed record MenuSnapshot(string Title, string Context,
    IReadOnlyList<MenuRow> Rows, string Notice);
