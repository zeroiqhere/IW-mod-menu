using Apparition.Backend.Ipc;
using Apparition.Backend.OpenControls;
using Apparition.Backend.Session;
using Apparition.Backend.Preferences;
using syndishanx_iw_tool.Overlay;

namespace Apparition.Backend.Controller;

// Only the owner loop advances navigation and observes completed action tasks
public sealed partial class BackendController {
    private readonly int processId;
    private readonly CommandGate commands;
    private readonly IBackendSession session;
    private readonly OverlayNavigation navigation = new();
    private Task? operation;
    private MenuAction? runningAction;
    private OverlayMenuState? retainedState;
    private string? error;
    private string? appearanceError;
    private string accent = MenuThemes.DefaultAccent;
    // Keep the fixed accent as a fallback while the renderer owns rainbow animation
    private bool smoothRainbowTheme;
    private MenuDesignDraft designDraft = new();
    private LocalMenuDesignTransitionState? designTransition;
    private string? designTransitionPhase;
    private ulong openingRevision;
    private LocalMenuOpeningState? menuOpening;
    private LocalMenuOpenTransitionState? openTransition;
    private string? openTransitionPhase;
    private bool openReadyToDraw;
    private MenuOpening? openingFrame;
    private MenuAppearance appearance = MenuAppearance.Default;
    private ScrollAnimationDraft scrollAnimationDraft = ScrollAnimationDraft.FromAppearance(MenuAppearance.Default);
    private MenuChoiceDrafts choices = MenuChoiceDrafts.FromAppearance(MenuAppearance.Default);
    private readonly bool choiceControlsAvailable;
    private readonly bool widthEditorControlsAvailable;
    private LocalWidthEditorState? widthEditor;
    private readonly bool menuPositionEditorControlsAvailable;
    private LocalMenuPositionEditorState? menuPositionEditor;
    private LocalMenuCreditsState? menuCredits;
    private MenuCredits? creditsFrame;
    private LocalMenuPreferenceRefreshState? preferenceRefresh;
    private string? preferenceRefreshPhase;
    private LocalMenuPreferenceRebuildState? preferenceRebuild;
    private string? preferenceRebuildPhase;
    private readonly bool instructionPositionEditorControlsAvailable;
    private LocalInstructionPositionEditorState? instructionPositionEditor;
    private readonly TimeProvider clock;
    private LocalMenuUnlockState menuUnlock = LocalMenuUnlockState.None;
    private bool finished;
    public bool ExitRequested { get; private set; }
    public bool Busy => !finished && (operation is not null || queuedAction is not null || queuedOpening is not null || openingValidation is not null || ExitRequested);
    public ulong LastSequence => commands.LastSequence;
    public void RequestShutdown() {
        preferenceSavingClosed = true;
        pendingPreferenceSave = null;
        // Keep a coherent frame while cleanup changes the live session references
        retainedState ??= session.ReadMenuState();
        monitorWait = null;
        CancelMonitorContinuation();
        menuUnlock = LocalMenuUnlockState.None;
        CancelPreferenceRefresh();
        if (navigation.Page is MenuPage.WidthEditor or MenuPage.InstructionPositionEditor or MenuPage.MenuPositionEditor or MenuPage.MenuCredits) {
            LeaveWidthEditor();
            LeaveInstructionPositionEditor();
            LeaveMenuPositionEditor();
            LeaveMenuCredits();
            navigation.Close();
        }
        queuedOpening = null;
        cleanupVisit = null;
        ExitRequested = true;
        SynchronizeCohortObservation();
    }

    public BackendController(int processId, IBackendSession session, bool choiceControlsAvailable = false,
        bool widthEditorControlsAvailable = false, TimeProvider? clock = null,
        bool instructionPositionEditorControlsAvailable = false, bool menuPositionEditorControlsAvailable = false,
        VerifiedOpeningActions? openingActions = null, ICommittedMenuPreferencesStore? preferenceStore = null) {
        this.processId = processId;
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        commands = new(processId);
        this.choiceControlsAvailable = choiceControlsAvailable;
        this.widthEditorControlsAvailable = choiceControlsAvailable && widthEditorControlsAvailable;
        this.clock = clock ?? TimeProvider.System;
        this.instructionPositionEditorControlsAvailable = instructionPositionEditorControlsAvailable;
        this.menuPositionEditorControlsAvailable = menuPositionEditorControlsAvailable;
        ConfigureOpeningControls(openingActions);
        this.preferenceStore = preferenceStore ?? processPreferenceStore;
        SynchronizeCommittedPreferences();
    }

    // Owned lifecycle tests can supply a navigation context at its counter boundary
    internal BackendController(int processId, IBackendSession session, OverlayNavigation navigation,
        bool menuPositionEditorControlsAvailable, bool choiceControlsAvailable = false, TimeProvider? clock = null, ulong openingRevision = 0, VerifiedOpeningActions? openingActions = null, ulong booleanPaintRevision = 0, ulong menuChromeRevision = 0, ulong instructionCreationRevision = 0, ulong instructionThemeRevision = 0, ulong chromeThemeRevision = 0) : this(processId, session,
            choiceControlsAvailable: choiceControlsAvailable, clock: clock,
            menuPositionEditorControlsAvailable: menuPositionEditorControlsAvailable, openingActions: openingActions) {
        this.navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        this.openingRevision = openingRevision;
        this.booleanPaintRevision = booleanPaintRevision;
        menuChrome = new(menuChromeRevision);
        instructionHud = new(instructionCreationRevision);
        instructionTheme = new(Counter: instructionThemeRevision);
        chromeTheme = new(Counter: chromeThemeRevision);
    }

    public void ObserveCompletion() {
        ObserveOpeningCompletion();
        ObservePlayerSelectionReceipt();
        if (operation?.IsCompleted != true) return;
        bool succeeded = operation.IsCompletedSuccessfully;
        try {
            operation.GetAwaiter().GetResult();
        } catch (Exception exception) {
            error = "The operation failed: " + exception.Message;
        }
        operation = null;
        if (!ExitRequested) retainedState = null;
        if (succeeded && !ExitRequested && runningAction?.Kind == MenuActionKind.Connect) {
            var state = session.ReadMenuState();
            if (!state.Connected) {
                // A guarded backend return is not a verified successful connection
                error = state.CanConnect
                    ? "Connection did not complete. Return to the game and select Connect after spawning again in Apparition"
                    : "Connection did not complete. Check Messages for the current session status";
            }
        }
        runningAction = null;
        if (selectionAfterCompletion is not null) {
            // Back, cursor movement, or close and reopen cancel the old navigation intent
            if (!ExitRequested && succeeded && navigation.IsOpen && selectionRevision == navigation.Revision
                && selectionForegroundAllowed?.Invoke() == true && AllowsQueuedActionScope(selectionCompletionScope)) {
                var state = session.ReadMenuState();
                if (state.Connected && !state.Busy && state.SelectedSlot == selectionAfterCompletion.Slot
                    && state.SelectedName == selectionAfterCompletion.ExpectedName
                    && CompletePlayerSelection(selectionAfterCompletion, state)) {
                    RebindPlainCallbackAfterSelection();
                    CancelOpening();
                }
            }
            selectionAfterCompletion = null;
            selectionRevision = null;
            selectionForegroundAllowed = null;
            selectionCompletionScope = null;
            selectionCapturedTarget = null;
            selectionReceiptRejected = false;
        }
        // Automatic cleanup can revoke actor admission before the completed action is published
        ObserveSessionRecovery();
    }

    public string? ReadMenuUnlockPhase() {
        // Owner-loop publication checks only elapsed clock state, never session work
        var unlock = menuUnlock.Read(clock);
        menuUnlock = unlock.State;
        if (unlock.ClockRecovered && !menuUnlock.ChromeRecreated) menuChrome = menuChrome.ClearReadiness();
        if (unlock.RecreateChrome) menuChrome = menuChrome.MarkReady();
        return navigation.IsOpen ? unlock.Phase : null;
    }

    public string? ReadMenuPublicationPhase() {
        AdvanceMonitorWait();
        AdvanceOptionCallback();
        AdvanceQuickOpening();
        // Only local clock and navigation state advance here, never pending session work
        if (openTransition is not null || designTransition is not null || preferenceRebuild is not null || menuOpening is not null) {
            // One sample covers both timelines before committing counters or presentation
            long timestamp = clock.GetTimestamp();
            if (openTransition is not null) {
                var transition = openTransition.Read(clock, timestamp);
                if (transition.ClockRecovered && openTransition.Drawn) CancelOpening();
                openTransition = transition.State;
                openTransitionPhase = transition.Phase;
                openReadyToDraw = transition.ReadyToDraw;
            }
            // Counter and Stealth rebuilds own their captured panel and independent row fade
            if (preferenceRebuild is not null) {
                var transition = preferenceRebuild.Read(clock, timestamp);
                var nextOpening = menuOpening;
                if (transition.ClockRecovered && nextOpening?.StartedTimestamp is not null) nextOpening = null;
                if (transition.RefreshPresentation && nextOpening is not null) {
                    nextOpening = nextOpening.Activate(timestamp, checked(navigation.MenuRevision + 1));
                }
                var next = nextOpening?.Read(clock, timestamp);
                // The revision must commit before the captured display can be released
                if (transition.RefreshPresentation) {
                    navigation.InvalidatePresentation();
                    menuChrome = menuChrome.MarkReady();
                    if (pendingPreferenceSave?.Boundary == PreferenceSaveBoundary.Rebuild) preferenceSaveReady = true;
                }
                preferenceRebuild = transition.State is { } refreshed && transition.RefreshPresentation
                    ? refreshed with { PresentationApplied = true } : transition.State;
                preferenceRebuildPhase = transition.Phase;
                menuOpening = next?.State;
                openingFrame = next?.Frame;
            } else if (designTransition is not null) {
                var transition = designTransition.Read(clock, timestamp);
                var nextOpening = menuOpening;
                if (transition.ClockRecovered && nextOpening?.StartedTimestamp is not null) nextOpening = null;
                if (transition.RefreshPresentation && nextOpening is not null) {
                    nextOpening = nextOpening.Activate(timestamp, checked(navigation.MenuRevision + 1));
                }
                var next = nextOpening?.Read(clock, timestamp);
                // A rejected redraw cannot advance either retained timeline
                if (transition.RefreshPresentation) {
                    navigation.InvalidatePresentation();
                    menuChrome = menuChrome.MarkReady();
                    instructionHud = instructionHud.MarkReady();
                    if (pendingPreferenceSave?.Boundary == PreferenceSaveBoundary.Rebuild) preferenceSaveReady = true;
                }
                // Only normal completion of the linked design clock can release its original caller
                if (transition.State is null && designTransition.CallerRevision is { } callerRevision)
                    MarkUseInvocationReady(callerRevision);
                designTransition = transition.State is { } refreshed && transition.RefreshPresentation
                    ? refreshed with { PresentationApplied = true } : transition.State;
                designTransitionPhase = transition.Phase;
                menuOpening = next?.State;
                openingFrame = next?.Frame;
            } else if (menuOpening is not null) {
                var next = menuOpening.Read(clock, timestamp);
                menuOpening = next.State;
                openingFrame = next.Frame;
            }
        }
        if (preferenceRefresh is not null) {
            var transition = preferenceRefresh.Read(clock);
            if (transition.RefreshPresentation) {
                // Commit the counter before dropping the captured style or marking it applied
                navigation.InvalidatePresentation();
                CancelOpening();
            }
            preferenceRefresh = transition.State is { } refreshed && transition.RefreshPresentation
                ? refreshed with { PresentationApplied = true } : transition.State;
            preferenceRefreshPhase = transition.Phase;
        }
        if (menuCredits is not null) {
            var transition = menuCredits.Read(clock);
            if (transition.Frame is null) {
                BeginMenuUnlock(LeaveMenuCredits);
            } else {
                menuCredits = transition.State;
                creditsFrame = transition.Frame;
            }
        }
        if (instructionPositionEditor is not null) {
            var transition = instructionPositionEditor.Read(clock);
            if (transition.Finished) {
                BeginMenuUnlock(LeaveInstructionPositionEditor);
                DeferPreferenceSave(PreferenceSaveBoundary.SoftUnlock);
            }
            else instructionPositionEditor = transition.State;
        }
        string? primary = navigation.IsOpen && openTransitionPhase is not null ? (openReadyToDraw ? "open:ready" : "open:" + openTransitionPhase)
            : navigation.IsOpen && designTransitionPhase is not null ? "design:" + designTransitionPhase
            : navigation.IsOpen && preferenceRebuildPhase is not null ? "preference-rebuild:" + preferenceRebuildPhase
            : navigation.IsOpen && preferenceRefreshPhase is not null ? "preference:" + preferenceRefreshPhase
            : navigation.IsOpen && optionCallbackFrame is not null ? MenuPublicationPhase.Callback(optionCallbackFrame)
            : navigation.IsOpen && menuCredits is not null ? "credits"
            : navigation.IsOpen && instructionPositionEditor is not null ? "position:" + instructionPositionEditor.Phase : ReadMenuUnlockPhase();
        return MenuPublicationPhase.ComposeMechanical(MenuPublicationPhase.ComposeUse(
            MenuPublicationPhase.ComposeMonitor(MenuPublicationPhase.Compose(primary, navigation.IsOpen ? openingFrame : null), monitorWait?.Frame),
            useInvocation is null ? null : ReadUseContinuation(BindPlayerMenuState(retainedState ?? session.ReadMenuState()))),
            monitorInvocation is null ? null : ReadMonitorContinuation(retainedState ?? session.ReadMenuState()));
    }

    public StateDocument Snapshot() {
        SynchronizeCommittedPreferences();
        ObserveCompletion();
        ObserveOwnedCleanupVisit();
        ReadMenuPublicationPhase();
        ObserveAdmittedMenuOwner();
        SynchronizeCohortObservation();
        string? unlockPhase = ReadMenuUnlockPhase();
        // Pending work uses its captured state, not partially changed session fields
        var state = BindPlayerMenuState(retainedState ?? session.ReadMenuState());
        if (cleanupRecovery) {
            // The administrative visit provides local navigation and Exit, never gameplay admission
            state = state with { CanConnect = false, CanPersonalGodmode = false, CanRestorePersonalGodmode = false,
                CanLobbyAmmo = false, CanSelectPlayers = false, CanRefreshPlayers = false, CanTeammateGodmode = false, CanAllPlayersGodmode = false };
        }
        // Session-owned cleanup remains busy even when no controller command is pending
        bool frameBusy = Busy || state.Busy;
        state = state with { Busy = frameBusy };
        if (frameBusy) {
            // Other workers can stop while a long scan runs, so cached values stay unknown
            state = state with { PersonalGodmodeValue = "UNKNOWN", LobbyAmmoValue = "UNKNOWN",
                TeammateGodmodeValue = "UNKNOWN", AllPlayersGodmodeValue = "UNKNOWN" };
        }
        var rowAppearance = preferenceRebuild?.DisplayAppearance(appearance)
            ?? preferenceRefresh?.RowsAppearance(appearance) ?? CallbackRowsAppearance(appearance);
        // Quick Exit remains committed for native control policy while only cursor styling is retained
        var displayedAppearance = designTransition?.DisplayAppearance(rowAppearance with { QuickExit = appearance.QuickExit })
            ?? rowAppearance with { QuickExit = appearance.QuickExit };
        var snapshot = cleanupVisit is OwnedCleanupVisit ? BuildOwnedCleanupMenu() : OverlayMenuBuilder.Build(navigation.Page, state, accent, rowAppearance, choices, choiceControlsAvailable,
            widthEditorControlsAvailable, instructionPositionEditorControlsAvailable, smoothRainbowTheme, scrollAnimationDraft, menuPositionEditorControlsAvailable, designDraft, ReadOpeningControlsView(), ReadQuickMenuViewContext());
        if (frameBusy) snapshot = snapshot with { Notice = "An operation is in progress" };
        if (error is not null) snapshot = snapshot with {
            Notice = string.IsNullOrEmpty(snapshot.Notice) ? error : error + "\n" + snapshot.Notice
        };
        if (appearanceError is not null) snapshot = snapshot with {
            Notice = string.IsNullOrEmpty(snapshot.Notice) ? appearanceError : appearanceError + "\n" + snapshot.Notice
        };
        if (preferenceStorageError is not null || preferenceOpeningError is not null) snapshot = snapshot with {
            Notice = string.Join("\n", new[] { preferenceStorageError, preferenceOpeningError, snapshot.Notice }
                .Where(detail => !string.IsNullOrEmpty(detail)))
        };
        if (openingControlsError is not null) snapshot = snapshot with {
            Notice = string.IsNullOrEmpty(snapshot.Notice) ? openingControlsError : openingControlsError + "\n" + snapshot.Notice
        };
        if (unlockPhase is not null) snapshot = snapshot with {
            Notice = string.IsNullOrEmpty(snapshot.Notice) ? "Returning to menu; controls paused"
                : snapshot.Notice + "\nReturning to menu; controls paused"
        };
        if (instructionPositionEditor is not null) {
            string detail = instructionPositionEditor.Phase == "editing"
                ? "Saved action slots move instructions; melee returns to the menu"
                : "Instruction position editor controls paused";
            snapshot = snapshot with { Notice = string.IsNullOrEmpty(snapshot.Notice) ? detail : snapshot.Notice + "\n" + detail };
        }
        if (openTransitionPhase is not null) snapshot = snapshot with {
            Notice = string.IsNullOrEmpty(snapshot.Notice) ? "Opening menu; controls paused"
                : snapshot.Notice + "\nOpening menu; controls paused"
        };
        if (designTransitionPhase is not null) snapshot = snapshot with
        {
            Notice = string.IsNullOrEmpty(snapshot.Notice) ? "Updating menu design; controls paused"
                : snapshot.Notice + "\nUpdating menu design; controls paused"
        };
        if (preferenceRebuildPhase is not null) snapshot = snapshot with {
            Notice = string.IsNullOrEmpty(snapshot.Notice) ? "Updating menu; controls paused"
                : snapshot.Notice + "\nUpdating menu; controls paused"
        };
        if (preferenceRefreshPhase is not null) snapshot = snapshot with {
            Notice = string.IsNullOrEmpty(snapshot.Notice) ? "Updating menu; controls paused"
                : snapshot.Notice + "\nUpdating menu; controls paused"
        };
        if (optionCallbackFrame is not null) snapshot = snapshot with {
            Notice = string.IsNullOrEmpty(snapshot.Notice) ? "Updating option; controls paused"
                : snapshot.Notice + "\nUpdating option; controls paused"
        };
        if (menuCredits is not null) {
            const string attribution = "Apparition BO3 reference: CF4_99; IW backend: SyndiShanX; dependencies: Memory.dll, Dear ImGui, MinHook, nlohmann/json";
            snapshot = snapshot with { Notice = string.IsNullOrEmpty(snapshot.Notice) ? attribution : snapshot.Notice + "\n" + attribution };
        }
        if (menuPositionEditor is not null) {
            var position = MenuPosition.Resolve(appearance);
            // The independent notice remains useful when the menu moves outside the viewport
            string detail = System.FormattableString.Invariant($"Position X: {position.X} Y: {position.Y}; movement increment: {menuPositionEditor.Step}");
            snapshot = snapshot with { Notice = string.IsNullOrEmpty(snapshot.Notice) ? detail : snapshot.Notice + "\n" + detail };
        }
        // Prepare the actual redraw with one clock sample before committing the fresh rows
        LocalMenuOpenTransitionState? preparedOpen = null;
        LocalMenuOpeningState? preparedOpening = null;
        LocalMenuOpenTransitionState? preparedPendingOpen = null;
        string? preparedPhase = null;
        bool preparedReady = false;
        if (openTransition is { Drawn: false } pendingOpen) {
            long timestamp = clock.GetTimestamp();
            var transition = pendingOpen.Read(clock, timestamp);
            // A failed row commit cannot retain the later prepared sample
            preparedPendingOpen = transition.State;
            preparedPhase = transition.Phase;
            preparedReady = transition.ReadyToDraw;
            if (transition.ReadyToDraw) {
                int duration = MenuOptionCapacity.OpeningDurationMilliseconds(snapshot.Rows.Count, displayedAppearance);
                preparedOpen = pendingOpen.Draw(timestamp);
                if (duration > 0) preparedOpening = new(pendingOpen.OpeningRevision, duration, timestamp, timestamp);
            }
        }
        PreflightPendingBooleanRows(snapshot.Rows);
        var preparedBooleanPaint = PrepareBooleanPaint(snapshot.Rows, preparedOpen is not null, !state.Preview && state.Connected);
        var preparedChrome = preparedOpen is not null ? menuChrome.MarkReady().Commit() : menuChrome.Commit();
        var proof = ReadInstructionAdmission();
        var stagedInstructions = instructionHud.Prepare(proof, appearance, state.Preview,
            session.RequiresLocalMenuAdmission, finished || ExitRequested || cleanupRecovery, accent, smoothRainbowTheme);
        // Qualified withdrawal keeps private timing without sampling a clock or publishing footer authority
        var stagedTheme = stagedInstructions.Withdrawn
            ? (State: instructionTheme, Frame: (InstructionThemeState?)null)
            : instructionTheme.Project(proof?.OwnerToken, stagedInstructions.Frame, clock);
        var stagedChromeTheme = PrepareChromeTheme(preparedChrome, proof, state.Preview);
        var preparedMonitor = PrepareMonitorPublication(state);
        var preparedMonitorWait = monitorWait;
        ulong monitorRowsRevision = navigation.Revision;
        navigation.SetRows(snapshot.Rows);
        var stagedMonitor = BindMonitorPublication(preparedMonitor, state);
        // A fresh own-parent cursor clamp remains part of this exact sleeping branch
        var stagedMonitorWait = preparedMonitorWait?.NavigationRevision == monitorRowsRevision
            ? preparedMonitorWait with { NavigationRevision = navigation.Revision } : preparedMonitorWait;
        // Creation-dependent opening state remains local until the complete DTO validates
        var preparedUnlock = menuUnlock.CommitChromeCreation();
        BindQuickOpeningRows(snapshot.Rows.Count, displayedAppearance);
        var nextOpen = preparedOpen ?? preparedPendingOpen ?? openTransition;
        string? nextOpenPhase = preparedOpen is not null ? "cooldown"
            : preparedPendingOpen is not null ? preparedPhase : openTransitionPhase;
        bool nextOpenReady = preparedOpen is not null ? false
            : preparedPendingOpen is not null ? preparedReady : openReadyToDraw;
        var nextOpening = menuOpening;
        var nextOpeningFrame = openingFrame;
        if (preparedOpen is not null) {
            // This candidate binds actual rows without another clock or counter sample
            nextOpening = preparedOpening?.Activate(preparedOpen.StartedTimestamp, navigation.MenuRevision);
            nextOpeningFrame = nextOpening is null ? null : new(nextOpening.Revision, 0);
        }
        if (nextOpening?.StartedTimestamp is not null && nextOpening.PresentationRevision != navigation.MenuRevision) {
            nextOpening = null;
            nextOpeningFrame = null;
        }
        // Rebuild retains existing paint while placement can use the committed design
        MenuDesignTransition? designFrame = navigation.IsOpen && designTransitionPhase is not null
            ? new(designTransitionPhase, designTransitionPhase == "rebuild" ? appearance.Design : null) : null;
        // Read cancellation before projecting the callback so a hidden owner cannot publish half a pair
        var useReceipt = ReadUseContinuation(state);
        var useCallback = navigation.IsOpen ? ReadUseCallbackFrame(useReceipt) : null;
        StateDocument result;
        try {
            result = StateDocument.Create(processId, LastSequence, navigation.IsOpen, snapshot, navigation.SelectedIndex,
                accent, displayedAppearance, navigation.Revision,
                navigation.Surface == MenuSurface.Main ? navigation.Page == MenuPage.Main : navigation.Page == MenuPage.QuickMenu,
                navigation.IsOpen && navigation.Page == MenuPage.WidthEditor ? widthEditor : null,
                unlockPhase, navigation.IsOpen && navigation.Page == MenuPage.InstructionPositionEditor
                    && instructionPositionEditor is not null ? new InstructionPositionEditor(instructionPositionEditor.Phase) : null,
                smoothRainbowTheme, navigation.MenuRevision,
                navigation.IsOpen && navigation.Page == MenuPage.MenuPositionEditor ? menuPositionEditor : null,
                navigation.IsOpen && navigation.Page == MenuPage.MenuCredits ? creditsFrame : null,
                navigation.IsOpen && preferenceRefreshPhase is not null ? new MenuPreferenceRefresh(preferenceRefreshPhase) : null,
                designFrame, navigation.IsOpen ? nextOpeningFrame : null, navigation.IsOpen && nextOpenPhase is not null ? new MenuOpenTransition(nextOpenPhase) : quickInputsLocked && navigation.IsOpen ? new MenuOpenTransition("cooldown") : null,
                navigation.IsOpen && preferenceRebuildPhase is not null ? new MenuPreferenceRebuild(preferenceRebuildPhase) : null,
                CurrentMainOpeningChord is { } main ? MainOpeningControlsState.FromChord(main) : null,
                openingPolicies is null ? null : OpeningPoliciesState.FromPolicies(openingPolicies),
                navigation.Surface == MenuSurface.Main ? "main" : "quick", ReadLocalMenuEligibility(proof, frameBusy, MenuControlsDisabled(preparedUnlock)), useCallback, preparedBooleanPaint?.Frame,
                preparedChrome.Read(navigation.IsOpen, state.Preview, admittedMenuOwner), stagedInstructions.Frame, stagedTheme.Frame, stagedChromeTheme.Frame, stagedMonitorWait?.Frame, useReceipt, stagedMonitor?.Frame);
        } catch {
            // Failed factory validation may retain an exact retry guard, never a committed receipt
            if (stagedMonitor is not null && preparedMonitor is { Bound: false }) {
                monitorInvocation = stagedMonitor with { Bound = false };
                monitorWait = stagedMonitorWait;
            }
            throw;
        }
        monitorInvocation = stagedMonitor;
        monitorWait = stagedMonitorWait;
        // Schema failure cannot spend chrome, footer, opening or paint creation state
        menuChrome = preparedChrome;
        chromeTheme = stagedChromeTheme.State;
        menuUnlock = preparedUnlock;
        openTransition = nextOpen;
        openTransitionPhase = nextOpenPhase;
        openReadyToDraw = nextOpenReady;
        menuOpening = nextOpening;
        openingFrame = nextOpeningFrame;
        CommitBooleanPaint(preparedBooleanPaint);
        instructionHud = stagedInstructions.State;
        instructionTheme = stagedTheme.State;
        // A schema or creation failure above cannot replace the previously saved record
        CommitDeferredPreferenceSave();
        return result;
    }

    public void Consume(CommandEnvelope command, Func<bool> foregroundAllowed) {
        SynchronizeCommittedPreferences();
        try {
            ConsumeCore(command, foregroundAllowed);
        } finally {
            // Navigation changes disarm the worker hint before another owner-loop frame
            SynchronizeCohortObservation();
        }
    }

    private void ConsumeCore(CommandEnvelope command, Func<bool> foregroundAllowed) {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(foregroundAllowed);
        // Even a lost-focus command is acknowledged and can never execute later
        bool freshCommand = command.Sequence > LastSequence;
        bool focused = foregroundAllowed();
        if (!commands.Consume(command, focused) || ExitRequested) {
            if (freshCommand && !focused && !ExitRequested && navigation.IsOpen && command.Valid
                && command.Version == 1 && command.ProcessId == processId && command.Input == OverlayInput.Select) {
                error = "The game focus changed. Return to the game and select this option again";
            }
            return;
        }
        var current = Snapshot();
        // Administrative input is consumed before any actor, editor, or appearance branch
        if (HandleOwnedCleanupInput(command, foregroundAllowed)) return;
        // Mechanical waits consume ordinary edges without changing source controls-disabled ownership
        if (MonitorWaitBlocksInput) return;
        if (!AllowsMechanicalScope(command, foregroundAllowed)) return;
        if (HandleScopedMonitorInput(command, foregroundAllowed)) return;
        // A stale editor capture cannot become an unrelated parent-page selection
        if (!AllowsCapturedOpeningScope(command)) return;
        if (quickInputsLocked || current.MenuOptionCallback is not null) return;
        // All monitor input is acknowledged and dropped until SoftUnlock completes
        if (current.MenuUnlock is not null || current.MenuPreferenceRefresh is not null || current.MenuDesignTransition is not null || current.MenuOpenTransition is not null || current.MenuPreferenceRebuild is not null) return;
        // An independent accepted branch cannot reuse a completed mechanical invocation
        if (command.Input is not (OverlayInput.NextChoice or OverlayInput.PreviousChoice or OverlayInput.Back
            or OverlayInput.Close or OverlayInput.JumpBack)) CancelMonitorContinuation();
        if (HandleOpeningControlsCommand(command, foregroundAllowed)) return;
        if (command.Input == OverlayInput.FinishMenuCredits) {
            if (!navigation.IsOpen || navigation.Page != MenuPage.MenuCredits || menuCredits is null
                || command.NavigationRevision != navigation.Revision || !foregroundAllowed()) return;
            BeginMenuUnlock(LeaveMenuCredits);
            return;
        }
        if (command.Input is OverlayInput.MoveMenuUp or OverlayInput.MoveMenuDown or OverlayInput.MoveMenuLeft or OverlayInput.MoveMenuRight
            or OverlayInput.IncreaseMenuPositionStep or OverlayInput.DecreaseMenuPositionStep or OverlayInput.FinishMenuPositionEditor) {
            if (!navigation.IsOpen || navigation.Page != MenuPage.MenuPositionEditor || !menuPositionEditorControlsAvailable
                || menuPositionEditor is null || command.NavigationRevision != navigation.Revision || !foregroundAllowed()) return;
            if (command.Input == OverlayInput.FinishMenuPositionEditor) {
                BeginMenuUnlock(LeaveMenuPositionEditor);
                DeferPreferenceSave(PreferenceSaveBoundary.SoftUnlock);
            } else if (command.Input is OverlayInput.IncreaseMenuPositionStep or OverlayInput.DecreaseMenuPositionStep) {
                if (menuPositionEditor.TryAdjustStep(command.Input == OverlayInput.IncreaseMenuPositionStep ? 1 : -1, out var changed)) {
                    menuPositionEditor = changed;
                }
            } else if (MenuPosition.Resolve(appearance).TryMove(command.Input.Value, menuPositionEditor.Step, out var moved)) {
                appearance = appearance with { MenuPosition = moved };
            }
            return;
        }
        if (command.Input is OverlayInput.MoveInstructionsUp or OverlayInput.MoveInstructionsDown
            or OverlayInput.MoveInstructionsLeft or OverlayInput.MoveInstructionsRight or OverlayInput.FinishInstructionPositionEditor) {
            if (!navigation.IsOpen || navigation.Page != MenuPage.InstructionPositionEditor
                || !instructionPositionEditorControlsAvailable || !appearance.InstructionsVisible
                || instructionPositionEditor?.Phase != "editing" || command.NavigationRevision != navigation.Revision
                || !foregroundAllowed()) return;
            if (command.Input == OverlayInput.FinishInstructionPositionEditor) {
                instructionPositionEditor = instructionPositionEditor.Exit(clock);
            } else if (InstructionPosition.Resolve(appearance).TryMove(command.Input.Value, out var moved)) {
                appearance = appearance with { InstructionPosition = moved };
            }
            return;
        }
        if (command.Input is OverlayInput.IncreaseWidth or OverlayInput.DecreaseWidth
            or OverlayInput.IncreaseWidthStep or OverlayInput.DecreaseWidthStep or OverlayInput.FinishWidthEditor) {
            // Dedicated commands cannot act outside their currently observed editor visit
            if (!navigation.IsOpen || navigation.Page != MenuPage.WidthEditor || !widthEditorControlsAvailable
                || widthEditor is null || command.NavigationRevision != navigation.Revision || !foregroundAllowed()) return;
            if (command.Input == OverlayInput.FinishWidthEditor) {
                BeginMenuUnlock(LeaveWidthEditor);
                DeferPreferenceSave(PreferenceSaveBoundary.SoftUnlock);
            }
            else if (command.Input is OverlayInput.IncreaseWidth or OverlayInput.DecreaseWidth) {
                if (widthEditor.TryAdjustWidth(appearance.MenuWidth,
                    command.Input == OverlayInput.IncreaseWidth ? 1 : -1, out int width)) {
                    appearance = appearance with { MenuWidth = width };
                }
            } else if (widthEditor.TryAdjustStep(command.Input == OverlayInput.IncreaseWidthStep ? 1 : -1, out var updated)) {
                widthEditor = updated;
            }
            return;
        }
        if (command.Input == OverlayInput.Close) {
            // A local hold-close never enters the Exit or session cleanup path
            // Snapshot may have completed a selection, so compare against its current epoch
            if (navigation.IsOpen && appearance.QuickExit && command.NavigationRevision is ulong observed
                && observed == navigation.Revision && foregroundAllowed()) {
                var wait = PrepareMonitorWait("back", command, foregroundAllowed);
                if (command.MenuRevision is not null && wait is null) return;
                LeaveWidthEditor();
                LeaveInstructionPositionEditor();
                LeaveMenuPositionEditor();
                LeaveMenuCredits();
                navigation.Close();
                CommitMonitorWait(wait);
                CancelOpening();
            }
            return;
        }
        if (navigation.Page == MenuPage.MenuCredits) {
            // Melee owns the explicit finish command while normal menu controls stay disarmed
            if (command.Input == OverlayInput.Toggle && foregroundAllowed()) {
                LeaveMenuCredits();
                navigation.Close();
            }
            return;
        }
        if (navigation.Page == MenuPage.MenuPositionEditor) {
            // Normal navigation and Quick Exit gestures never own the reposition controls
            if (command.Input == OverlayInput.Toggle) {
                LeaveMenuPositionEditor();
                navigation.Close();
            }
            return;
        }
        if (navigation.Page == MenuPage.InstructionPositionEditor) {
            // Toggle aborts the local visit without beginning a timed finish or session exit
            if (command.Input == OverlayInput.Toggle) {
                LeaveInstructionPositionEditor();
                LeaveMenuPositionEditor();
                navigation.Close();
            }
            return;
        }
        if (navigation.Page == MenuPage.WidthEditor) {
            // Normal row inputs remain disarmed until melee finishes this special panel
            if (command.Input == OverlayInput.Toggle) {
                LeaveWidthEditor();
                navigation.Close();
            }
            return;
        }
        if (command.Input is OverlayInput.PreviousChoice or OverlayInput.NextChoice) {
            HandleOrdinaryChoiceInput(command, foregroundAllowed);
            return;
        }
        if (command.Input == OverlayInput.Toggle && !navigation.IsOpen) {
            // Actorless recovery only reopens the administrative root and grants no game capability
            if (cleanupRecovery) {
                if (foregroundAllowed()) navigation.OpenCleanupRoot();
                return;
            }
            // Reserve every visit before opening, including pages with no animated rows yet
            if (session.ReadLocalMenuAdmission() is not null
                || session.RequiresLocalMenuAdmission && session.ReadMenuState().Connected || !foregroundAllowed()) return;
            ulong revision = checked(openingRevision + 1);
            long timestamp = clock.GetTimestamp();
            var prepared = new LocalMenuOpenTransitionState(revision, timestamp, timestamp);
            var chrome = menuChrome.Prepare(admittedMenuOwner);
            navigation.Open();
            menuChrome = chrome;
            openingRevision = revision;
            openTransition = prepared;
            openTransitionPhase = "rebuild";
            openReadyToDraw = false;
            return;
        }
        var backWait = command.Input == OverlayInput.Back && navigation.IsOpen ? PrepareMonitorWait("back", command, foregroundAllowed) : null;
        if (command.Input == OverlayInput.Back && command.MenuRevision is not null && backWait is null) return;
        ulong previousRevision = navigation.Revision;
        var action = navigation.Handle(command.Input!.Value);
        var useIntent = action is null ? null : CaptureUseIntent(command, foregroundAllowed,
            BindPlayerMenuState(retainedState ?? session.ReadMenuState()));
        CommitMonitorWait(backWait);
        // Scrolling supersedes opacity without changing the presentation context
        if (navigation.Revision != previousRevision) CancelOpening();
        if (action is null) return;
        if (action.Kind == MenuActionKind.OpenPage) {
            if (action.Page is MenuPage.SelectedPlayer or MenuPage.SelectedBasicScripts
                && !AllowsPlayerMenuAction(action, foregroundAllowed)) return;
            if (action.Page == MenuPage.MenuCredits) {
                if (menuCredits is not null || !AllowsLocalAppearance(action, foregroundAllowed)) return;
                // A counter failure cannot leave a timer running on the old parent page
                var started = LocalMenuCreditsState.Start(clock);
                navigation.OpenPage(action.Page);
                CancelOpening();
                menuCredits = started;
                creditsFrame = new(0);
                return;
            }
            if (action.Page == MenuPage.MenuPositionEditor) {
                if (!AllowsLocalAppearance(action, foregroundAllowed)) return;
                // A rejected navigation counter cannot leave a latent editor on the parent
                navigation.OpenPage(action.Page);
                CancelOpening();
                menuPositionEditor = new();
                return;
            }
            if (action.Page == MenuPage.WidthEditor) {
                if (!AllowsLocalAppearance(action, foregroundAllowed)) return;
                // Offset resets on entry while committed width remains with appearance
                widthEditor = new();
            }
            if (action.Page == MenuPage.InstructionPositionEditor) {
                if (!AllowsLocalAppearance(action, foregroundAllowed)) return;
                instructionPositionEditor = LocalInstructionPositionEditorState.Enter(clock);
            }
            navigation.OpenPage(action.Page);
            CancelOpening();
            return;
        }
        if (action.Kind == MenuActionKind.Exit) {
            RequestShutdown();
            return;
        }
        if (action.Kind == MenuActionKind.ResetInstructionPosition) {
            if (navigation.Page == MenuPage.Instructions && AllowsLocalAppearance(action, foregroundAllowed)) {
                var prepared = PrepareLocalOptionCallback("plain");
                appearance = appearance with { InstructionPosition = null };
                SaveCommittedPreferences();
                CommitOptionCallback(prepared, useIntent);
            }
            return;
        }
        if (action.Kind is MenuActionKind.ChangeAccent or MenuActionKind.ChangePreference or MenuActionKind.EnableSmoothRainbowTheme) {
            if (!AllowsLocalAppearance(action, foregroundAllowed)) return;
            if (TryHandleRejectedPreference(action, useIntent)) return;
            string? localKind = LocalOptionCallbackKind(action, navigation.Page);
            bool immediateInstructionRedraw = navigation.Page == MenuPage.Instructions
                && action.Kind == MenuActionKind.ChangePreference && action.Slot == (int)MenuPreference.InstructionsVisible;
            var localCallback = localKind is null ? null : PrepareLocalOptionCallback(localKind, immediateInstructionRedraw);
            // This local preference belongs to the policy pair rather than duplicated appearance state
            if (action.Kind == MenuActionKind.ChangePreference && navigation.Page == MenuPage.Preferences
                && action.Slot == (int)MenuPreference.DisableQuickMenu) {
                // New policy mutations require the worker's capture, including outside the editor
                if (command.OpeningControlsRevision is not { } captured
                    || command.NavigationRevision != navigation.Revision) return;
                ApplyQuickMenuDisabled(captured, useIntent);
                return;
            }
            // Appearance actions cannot dispatch to the session or accept a guessed color index
            bool saveCommittedPreference = false;
            if (action.Kind == MenuActionKind.ChangeAccent && navigation.Page == MenuPage.ThemeColors
                && MenuThemes.Find(action.Slot) is { } theme) {
                var themeInvocation = PrepareInstructionThemeInvocation(current, theme.Hex, localCallback);
                var chromeInvocation = PrepareChromeThemeInvocation(current, theme.Hex, localCallback);
                accent = theme.Hex;
                smoothRainbowTheme = false;
                instructionTheme = themeInvocation;
                chromeTheme = chromeInvocation;
                saveCommittedPreference = true;
            } else if (action.Kind == MenuActionKind.EnableSmoothRainbowTheme && navigation.Page == MenuPage.ThemeColors) {
                // Selecting the active source mode is a no-op rather than a toggle or restart
                saveCommittedPreference = !smoothRainbowTheme;
                smoothRainbowTheme = true;
            } else if (action.Kind == MenuActionKind.ChangePreference
                && navigation.Page == MenuPage.Preferences
                && action.Slot == (int)MenuPreference.Design && designDraft.TryCommit(appearance, out var designed))
            {
                bool changed = designed.Design != appearance.Design;
                var caller = PrepareLocalOptionCallback("plain");
                if (!changed && caller is not null) {
                    // The source slider keeps its caller wait and held Use even when its handler returns early
                    CommitOptionCallback(caller, useIntent);
                    return;
                }
                // Clock and future redraw admission are transactional with the actual local commit
                var started = caller is null ? LocalMenuDesignTransitionState.Start(appearance, changed, clock)
                    : new(appearance.Design, changed, caller.StartedTimestamp, caller.LastTimestamp, CallerRevision: caller.Revision);
                ulong revision = openingRevision;
                LocalMenuOpeningState? armed = null;
                if (changed) {
                    _ = checked(navigation.MenuRevision + 1);
                    revision = checked(openingRevision + 1);
                    int duration = MenuOptionCapacity.OpeningDurationMilliseconds(current.Rows.Count, designed);
                    if (duration > 0) armed = new(revision, duration, LastTimestamp: started.LastTimestamp);
                }
                // Arming uses the existing sample and cannot introduce a postcommit clock failure
                var chrome = changed ? menuChrome.Prepare(admittedMenuOwner) : menuChrome;
                var footer = changed ? instructionHud.Request(ReadInstructionAdmission()?.OwnerToken, false) : instructionHud;
                appearance = designed;
                instructionHud = footer;
                menuChrome = chrome;
                if (changed) { openingRevision = revision; menuOpening = armed; openingFrame = null; }
                designTransition = started;
                designTransitionPhase = changed ? "rebuild" : "cooldown";
                if (changed) DeferPreferenceSave(PreferenceSaveBoundary.Rebuild);
                if (caller is not null) {
                    // The design timeline replaces the plain clock while keeping its one admitted receipt
                    CommitUseInvocation(useIntent, caller);
                    return;
                }
            }
            else if (action.Kind == MenuActionKind.ChangePreference
                && (navigation.Page is MenuPage.Preferences or MenuPage.Instructions)
                && (action.Slot == (int)MenuPreference.ScrollAnimationTime
                    ? scrollAnimationDraft.TryCommit(appearance, out var updated)
                    : MenuChoiceDrafts.IsChoice(action.Slot)
                        ? choices.TryCommit(action.Slot, appearance, out updated)
                        : appearance.TryChange(action.Slot, out updated))) {
                var footer = PrepareInstructionPreference(action.Slot, appearance, updated);
                // Source choices return before Save when the applied value is reselected
                saveCommittedPreference = !MenuChoiceDrafts.IsChoice(action.Slot) || updated != appearance;
                if (updated != appearance && navigation.Page == MenuPage.Preferences
                    && (action.Slot == (int)MenuPreference.OptionCounter
                        || action.Slot == (int)MenuPreference.StealthUI && appearance.OptionCounter)) {
                    // Validate clock and both counters before changing any local preference
                    var started = LocalMenuPreferenceRebuildState.Start(appearance,
                        action.Slot == (int)MenuPreference.OptionCounter ? MenuPreferenceRebuildCause.OptionCounter : MenuPreferenceRebuildCause.StealthUI, clock);
                    _ = checked(navigation.MenuRevision + 1);
                    ulong revision = checked(openingRevision + 1);
                    int duration = MenuOptionCapacity.OpeningDurationMilliseconds(current.Rows.Count, updated);
                    LocalMenuOpeningState? armed = duration > 0 ? new(revision, duration, LastTimestamp: started.LastTimestamp) : null;
                    var chrome = menuChrome.Prepare(admittedMenuOwner);
                    appearance = updated;
                    menuChrome = chrome;
                    // A successful commit replaces only the previous local-choice feedback
                    appearanceError = null;
                    openingRevision = revision;
                    menuOpening = armed;
                    openingFrame = null;
                    preferenceRebuild = started;
                    preferenceRebuildPhase = "rebuild";
                    DeferPreferenceSave(PreferenceSaveBoundary.Rebuild);
                    return;
                }
                if (updated != appearance && LocalMenuPreferenceRefreshState.AppliesTo(navigation.Page, action.Slot)) {
                    // One owned callback supplies both the source deadline and held-Use receipt
                    var previous = appearance;
                    var prepared = PrepareLocalOptionCallback("boolean");
                    var legacyRefresh = prepared is null ? LocalMenuPreferenceRefreshState.Start(previous, clock) : null;
                    // Actorless callers retain their existing clock without borrowing source admission
                    if (legacyRefresh is not null) _ = checked(navigation.MenuRevision + 1);
                    appearance = updated;
                    appearanceError = null;
                    if (prepared is not null) {
                        CommitOptionCallback(prepared, useIntent, previous);
                    } else {
                        preferenceRefresh = legacyRefresh;
                        preferenceRefreshPhase = "pending";
                    }
                    SaveCommittedPreferences();
                    return;
                }
                // Immediate handlers and choices retain their existing source-specific refresh path
                bool delayedInstructionRedraw = localCallback is { Kind: "boolean" }
                    && navigation.Page == MenuPage.Instructions && !immediateInstructionRedraw;
                if (updated != appearance && action.Slot != (int)MenuPreference.ScrollAnimationTime && !delayedInstructionRedraw) {
                    navigation.InvalidatePresentation();
                    CancelOpening();
                }
                appearance = updated;
                instructionHud = footer;
                appearanceError = null;
            } else if (action.Kind == MenuActionKind.ChangePreference
                && action.Slot == (int)MenuPreference.BooleanDisplay && appearance.StealthUI
                && choices.Candidate(action.Slot) == "Boxes") {
                appearanceError = "Boxes cannot be applied while Stealth UI is enabled. Choose Text or Text Color, or disable Stealth UI";
            } else if (navigation.Page == MenuPage.Instructions && action.Kind == MenuActionKind.ChangePreference
                && action.Slot == (int)MenuPreference.AdaptiveInstructions && appearance.AlternateInstructions) {
                // The source keeps this BOOL selectable and rejects inside its callback
                appearanceError = "Adaptive Position can't be used with Alternate Instructions enabled";
            }
            // Rejected and unchanged source slider selections still own their finite caller wait
            CommitOptionCallback(localCallback, useIntent);
            if (localCallback is null) CancelUseContinuation();
            if (saveCommittedPreference && designTransition is null && preferenceRebuild is null) SaveCommittedPreferences();
            return;
        }
        if (Busy || action.Kind == MenuActionKind.None) return;
        // A fresh capability snapshot and focus read precede every backend action
        var state = BindPlayerMenuState(session.ReadMenuState());
        var fresh = OverlayMenuBuilder.Build(navigation.Page, state, accent, appearance, choices, choiceControlsAvailable,
            widthEditorControlsAvailable, instructionPositionEditorControlsAvailable, smoothRainbowTheme, scrollAnimationDraft, menuPositionEditorControlsAvailable, designDraft, ReadOpeningControlsView(), ReadQuickMenuViewContext());
        if (navigation.SelectedIndex >= fresh.Rows.Count
            || !fresh.Rows[navigation.SelectedIndex].Enabled
            || fresh.Rows[navigation.SelectedIndex].Action != action) return;
        if (!foregroundAllowed()) {
            // A rejected selection stays acknowledged and requires a new deliberate input
            error = "The game focus changed. Return to the game and select this option again";
            return;
        }
        var acceptedScope = CaptureQueuedActionScope(foregroundAllowed);
        retainedState = action.Kind switch {
            MenuActionKind.TogglePersonalGodmode or MenuActionKind.RestorePersonalGodmode => state with {
                PersonalGodmodeValue = "UNKNOWN", PersonalGodmodeStatus = "Changing personal Godmode"
            },
            MenuActionKind.ToggleAllPlayersGodmode => state with {
                AllPlayersGodmodeValue = "UNKNOWN", AllPlayersGodmodeStatus = "Changing all-player God Mode"
            },
            MenuActionKind.ToggleLobbyAmmo => state with {
                LobbyAmmoValue = "UNKNOWN", LobbyAmmoStatus = "Changing lobby Infinite Ammo"
            },
            MenuActionKind.ToggleTeammateGodmode => state with {
                TeammateGodmodeValue = "UNKNOWN", TeammateGodmodeStatus = "Changing selected teammate Godmode"
            },
            _ => state
        };
        error = null;
        // Defer actual execution until the runner has published the pending state
        if (action.Kind == MenuActionKind.SelectPlayer) {
            var scope = session.ReadPlayerSelectionScope();
            if (scope is null || scope.Revision == ulong.MaxValue) {
                retainedState = null;
                error = "A current session selection scope is required";
                return;
            }
            action = action with { SelectionScope = scope };
        }
        CancelUseContinuation();
        queuedUseIntent = useIntent;
        queuedAction = action;
        queuedActionScope = acceptedScope;
        selectionRevision = action.Kind == MenuActionKind.SelectPlayer ? navigation.Revision : null;
        selectionCapturedTarget = null;
        selectionReceiptRejected = false;
        // Completion uses the originating focus and actor scope rather than a later view
        selectionForegroundAllowed = action.Kind == MenuActionKind.SelectPlayer ? foregroundAllowed : null;
        selectionCompletionScope = action.Kind == MenuActionKind.SelectPlayer ? acceptedScope : null;
    }

    private MenuAction? queuedAction;
    private MenuAction? selectionAfterCompletion;
    private Func<bool>? selectionForegroundAllowed;
    private QueuedActionScope? selectionCompletionScope;
    private ulong? selectionRevision;

    private void CancelOpening() {
        CancelQuickFade();
        menuOpening = null;
        openingFrame = null;
    }

    private void CancelPreferenceRefresh() {
        monitorWait = null;
        CancelMonitorContinuation();
        // Retiring a caller cancels its save even when the same account remains authenticated
        pendingPreferenceSave = null;
        preferenceSaveReady = false;
        // A canceled caller cannot refresh a later actor, while the existing eligible footer can survive
        instructionHud = instructionHud.CancelPendingRecreation();
        CancelOptionCallback();
        menuChrome = menuChrome.Cancel();
        CancelOpening();
        quickOpening = null;
        quickWaitReplacedByCallback = false;
        quickInputsLocked = false;
        openTransition = null;
        openTransitionPhase = null;
        openReadyToDraw = false;
        // Explicit owner cleanup keeps committed preferences and cancels only their delayed display
        designTransition = null;
        designTransitionPhase = null;
        preferenceRefresh = null;
        preferenceRefreshPhase = null;
        preferenceRebuild = null;
        preferenceRebuildPhase = null;
    }

    private void LeaveMenuCredits() {
        if (navigation.Page != MenuPage.MenuCredits) return;
        navigation.Handle(OverlayInput.Back);
        menuCredits = null;
        creditsFrame = null;
    }

    private void LeaveMenuPositionEditor() {
        if (navigation.Page != MenuPage.MenuPositionEditor) return;
        // Return to the saved parent highlight before a caller finishes or hides the visit
        navigation.Handle(OverlayInput.Back);
        menuPositionEditor = null;
        if (!deferEditorPreferenceExit) SaveCommittedPreferences();
    }

    private void LeaveWidthEditor() {
        if (navigation.Page != MenuPage.WidthEditor) return;
        // The parent highlight is restored before any caller hides or reopens the menu
        navigation.Handle(OverlayInput.Back);
        widthEditor = null;
        if (!deferEditorPreferenceExit) SaveCommittedPreferences();
    }

    private void LeaveInstructionPositionEditor() {
        if (navigation.Page != MenuPage.InstructionPositionEditor) return;
        navigation.Handle(OverlayInput.Back);
        instructionPositionEditor = null;
    }

    private bool AllowsLocalAppearance(MenuAction action, Func<bool> foregroundAllowed) {
        // Rebuild the selected row so saved or arbitrary choices cannot bypass its capability
        var fresh = OverlayMenuBuilder.Build(navigation.Page, retainedState ?? session.ReadMenuState(), accent, appearance,
            choices, choiceControlsAvailable, widthEditorControlsAvailable, instructionPositionEditorControlsAvailable, smoothRainbowTheme, scrollAnimationDraft, menuPositionEditorControlsAvailable, designDraft, ReadOpeningControlsView(), ReadQuickMenuViewContext());
        if (navigation.SelectedIndex >= fresh.Rows.Count || !fresh.Rows[navigation.SelectedIndex].Enabled
            || fresh.Rows[navigation.SelectedIndex].Action != action) return false;
        if (foregroundAllowed()) return true;
        error = "The game focus changed. Return to the game and select this option again";
        return false;
    }

    public void StartQueuedAction() {
        if (ExitRequested || finished) {
            // An accepted intent cannot cross the shutdown boundary into a new gameplay task
            queuedAction = null;
            queuedActionScope = null;
            queuedUseIntent = null;
            DropPendingActionState();
            queuedOpening = null;
            return;
        }
        if (StartQueuedOpening()) return;
        if (queuedAction is null) return;
        MenuAction action = queuedAction;
        var acceptedScope = queuedActionScope;
        var originalUseIntent = queuedUseIntent;
        queuedUseIntent = null;
        queuedAction = null;
        queuedActionScope = null;
        menuUnlock = LocalMenuUnlockState.None;
        try {
            if (!AllowsQueuedActionScope(acceptedScope)
                || action.Page == MenuPage.SelectedBasicScripts && !HasCurrentPlayerTarget(action)
                || action.Kind == MenuActionKind.ToggleAllPlayersGodmode && !HasCurrentCohortTarget(action)) {
                DropPendingActionState();
                error = "The game or menu changed before this option started. Select it again";
                return;
            }
            BeginOptionCallback(action, originalUseIntent);
        } catch (Exception exception) {
            // A failed clock or exhausted epoch cannot start work or replay an accepted intent
            DropPendingActionState();
            error = "The option could not start: " + exception.Message;
            return;
        }
        runningAction = action;
        try {
            operation = session.ExecuteAsync(action);
        } catch (Exception exception) {
            operation = Task.FromException(exception);
        }
        // Selection opens a page only after the stable identity check has completed
        if (action.Kind == MenuActionKind.SelectPlayer) {
            selectionAfterCompletion = action;
            ObservePlayerSelectionReceipt();
        }
    }

    public async Task DrainOperationAsync() {
        if (openingValidation is { } validation) {
            try { await validation; } catch (Exception exception) { error = "Menu opening stopped: " + exception.Message; }
            ObserveOpeningCompletion();
        }
        if (operation is null) return;
        try {
            // Cleanup cancels and joins the feature workers before this continuation is drained
            await operation;
        } catch (Exception exception) {
            error = "The operation stopped: " + exception.Message;
        }
        ObserveCompletion();
    }

    public StateDocument ClosedState() {
        finished = true;
        CancelPreferenceRefresh();
        retainedState = null;
        queuedAction = null;
        queuedActionScope = null;
        LeaveWidthEditor();
        LeaveInstructionPositionEditor();
        LeaveMenuPositionEditor();
        LeaveMenuCredits();
        navigation.Close();
        return Snapshot();
    }
}
