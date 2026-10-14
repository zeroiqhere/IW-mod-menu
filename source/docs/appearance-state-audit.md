# Appearance state lifetime

The pinned Apparition source (`307e94c1b1d6537ba7a2a71b32a22d563d3ea25c`) calls `LoadMenuVars` during the player's first-spawn setup (`main.gsc:189–213`, `main.gsc:332`). `SaveMenuTheme` stores 25 values, including its saved marker, as strings in engine dvars named with the setting and player XUID (`Menu/menu_customization.gsc:700–742`). It loads those values when the marker exists and saves defaults otherwise (`Menu/menu_customization.gsc:745–806`). Fixed color and ordinary preference changes save at their handlers; rebuild and editor changes use the source commit boundaries detailed in the committed-preferences audit. Rainbow uses a `MainTheme` sentinel rather than a fixed RGB value.

This is engine-variable storage scoped by player identity. The source contains no file-writing or config-flush implementation for these settings. Its README says open controls save between games, but the code alone does not prove persistence after a full game process restart. Local disk persistence would therefore be an explicit application feature, not verified behavior copied from this source.

Page, cursor, parent path, and selected player are absent from the saved list. Closing the menu preserves those player fields (`Menu/base.gsc:203–256`); cursor values are keyed by page (`Menu/base.gsc:848–864`). Changing verification clears page and cursor state (`Menu/verification.gsc:53–56`). These are separate from saved appearance settings.

The [committed preferences candidate](committed-preferences-reference-audit.md) now
retains all 23 supported settings in an account-keyed memory repository above
controller and connection recreation. Reload requires the current verified
account scope and creates fresh input policies and editor drafts. The repository
ends with the backend process, which remains shorter than source engine-variable
availability across a backend restart within the same running game. Each launcher
run creates fresh private session IPC, so session JSON files are not a settings
store. Appearance changes do not rewrite saved game keys or their bindings.

Disk persistence is deferred. If added explicitly later, use a separate versioned UI-only settings document outside the cache and installation directory. On this Linux launcher, an appropriate location is `$XDG_CONFIG_HOME/iw-apparition-injected/preferences.json`, falling back to `$HOME/.config` and ignoring a relative XDG path, as specified by the [XDG Base Directory Specification](https://specifications.freedesktop.org/basedir/latest/). Preserve unsupported or damaged documents instead of replacing them with defaults. Do not persist connection, feature activation, process IDs, selected-player identity, or live navigation. Per-OS-user storage would differ from the source's per-XUID scope and must be described accurately.

Any later disk store should use strict bounded schema validation, same-directory atomic replacement, a single writer, private directory/file checks, and visible save-failure status. Tests should cover reload, absent settings, unsupported versions and values, duplicate properties, partial writes, concurrent readers, permission failure, links, and unchanged game key files. The current memory repository performs no disk storage or game experiment.
