using Apparition.Backend.Controller;
using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

// This exporter links the menu model but never the game session or memory dependency
internal static class MenuFixtureExporter {
    public const int TotalFixtureCount = QuickControlsFixtureExporter.TotalFixtureCount + FamilyNavigationFixtureProfiles.ProfileCount + QuickPageFixtureProfiles.ProfileCount + ScopedOpeningFixtureProfiles.ProfileCount;
    public static int Run(string[] arguments) {
        try {
            if (arguments.Length != 2 || arguments[0] != "--export-menu-fixtures") {
                throw new ArgumentException("Expected --export-menu-fixtures followed by a new absolute directory");
            }
            int count = Export(arguments[1]);
            Console.WriteLine($"Exported {count} offline menu snapshots");
            return 0;
        } catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException) {
            Console.Error.WriteLine("Menu fixture export failed: " + exception.Message);
            return 1;
        }
    }

    public static int Export(string directory) {
        if (!Path.IsPathFullyQualified(directory)) {
            throw new ArgumentException("Menu fixture directory must be absolute", nameof(directory));
        }
        string destination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        if (Directory.Exists(destination) || File.Exists(destination)) {
            throw new IOException("Menu fixture directory must not already exist");
        }
        string parent = Path.GetDirectoryName(destination) ?? throw new ArgumentException("A parent directory is required");
        if (!Directory.Exists(parent)) throw new IOException("Menu fixture parent directory must exist");

        // Stage beside the destination so publication cannot merge into an existing directory
        string staging = Path.Combine(parent, ".apparition-menu-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try {
            var fixtures = Build();
            foreach (var fixture in fixtures) {
                using var output = new FileStream(Path.Combine(staging, fixture.Key), FileMode.CreateNew, FileAccess.Write);
                output.Write(fixture.Value.Serialize());
            }
            Directory.Move(staging, destination);
            return fixtures.Count;
        } finally {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        }
    }

    public static IReadOnlyDictionary<string, StateDocument> Build() {
        var boundary = new SessionBoundary();
        boundary.State = boundary.State with { Preview = true };
        // Offline fixtures explicitly supply verified test controls rather than guessing keys
        var controllerClock = new ManualTimeProvider();
        var controller = new BackendController(42, boundary, choiceControlsAvailable: true, widthEditorControlsAvailable: true, clock: controllerClock);
        ulong sequence = 1;
        void Send(OverlayInput input) => controller.Consume(new(sequence++, 1, 42, input, true), () => true);
        void SelectRow(BackendController owner, Action<OverlayInput> send, string label) {
            var snapshot = owner.Snapshot();
            int index = snapshot.Rows.ToList().FindIndex(row => row.Label == label);
            if (index < 0) throw new InvalidOperationException("Required preview row is unavailable: " + label);
            for (int moved = 0; moved < snapshot.Rows.Count && owner.Snapshot().SelectedIndex != index; moved++) send(OverlayInput.Down);
            send(OverlayInput.Select);
        }
        var fixtures = new Dictionary<string, StateDocument>();

        Send(OverlayInput.Toggle); MenuOpenTestHarness.Settle(controller, controllerClock);
        fixtures.Add("main.json", controller.Snapshot());
        SelectRow(controller, Send, "Basic Scripts");
        fixtures.Add("basic-scripts.json", controller.Snapshot());
        Send(OverlayInput.Back);
        SelectRow(controller, Send, "All Players Menu");
        fixtures.Add("all-players.json", controller.Snapshot());
        Send(OverlayInput.Back);
        SelectRow(controller, Send, "Menu Customization");
        fixtures.Add("customization.json", controller.Snapshot());
        SelectRow(controller, Send, "Main Design Color");
        fixtures.Add("theme-colors-default.json", controller.Snapshot());

        // Select Mint through the same input route used by the production controller
        int mintIndex = controller.Snapshot().Rows.ToList().FindIndex(row => row.Label == "Mint");
        if (mintIndex < 0) throw new InvalidOperationException("The source palette requires Mint");
        for (int index = 0; index < mintIndex; index++) Send(OverlayInput.Down);
        Send(OverlayInput.Select);
        fixtures.Add("theme-colors-mint.json", controller.Snapshot());
        Send(OverlayInput.Back);
        SelectRow(controller, Send, "Menu Preferences");
        Send(OverlayInput.Down);
        fixtures.Add("preferences-default.json", controller.Snapshot());
        Send(OverlayInput.NextChoice);
        fixtures.Add("preferences-draft.json", controller.Snapshot());
        // Choice movement stages the candidate and fresh Use commits it
        foreach (string label in new[] { "Bool Display", "Bool Box Location", "Spotlight Cursor",
            "Colored Cursor", "Large Cursor", "Option Counter" }) {
            // Preserve the original committed profile while the new numeric row stays at default
            int target = controller.Snapshot().Rows.ToList().FindIndex(row => row.Label == label);
            for (int moved = 0; moved < controller.Snapshot().Rows.Count && controller.Snapshot().SelectedIndex != target; moved++) Send(OverlayInput.Down);
            if (label == "Bool Box Location") Send(OverlayInput.NextChoice);
            Send(OverlayInput.Select);
            if (label is "Spotlight Cursor" or "Colored Cursor" or "Large Cursor") controllerClock.Advance(TimeSpan.FromMilliseconds(430));
            if (label == "Option Counter") controllerClock.Advance(TimeSpan.FromMilliseconds(250));
        }
        // Visibility is edited only on its dedicated source submenu
        Send(OverlayInput.Back);
        SelectRow(controller, Send, "Menu Instructions");
        SelectRow(controller, Send, "Disable");
        Send(OverlayInput.Back);
        SelectRow(controller, Send, "Menu Preferences");
        int counterIndex = controller.Snapshot().Rows.ToList().FindIndex(row => row.Label == "Option Counter");
        for (int moved = 0; moved < controller.Snapshot().Rows.Count && controller.Snapshot().SelectedIndex != counterIndex; moved++) Send(OverlayInput.Down);
        fixtures.Add("preferences-text-left.json", controller.Snapshot());
        // Keep the original snapshots unchanged and append committed hold-close variants
        SelectRow(controller, Send, "Quick Exit");
        controllerClock.Advance(TimeSpan.FromMilliseconds(430));
        fixtures.Add("preferences-quick-exit.json", controller.Snapshot());
        Send(OverlayInput.Back);
        Send(OverlayInput.Back);
        fixtures.Add("root-quick-exit.json", controller.Snapshot());
        // A fresh local controller keeps the previous eleven appearance profiles intact
        var widthBoundary = new SessionBoundary();
        widthBoundary.State = widthBoundary.State with { Preview = true };
        var widthClock = new ManualTimeProvider();
        var widthController = new BackendController(42, widthBoundary, true, true, widthClock);
        ulong widthSequence = 1;
        void SendWidth(OverlayInput input) => widthController.Consume(new(widthSequence++, 1, 42, input, true,
            input >= OverlayInput.Close ? widthController.Snapshot().NavigationRevision : null), () => true);
        SendWidth(OverlayInput.Toggle); MenuOpenTestHarness.Settle(widthController, widthClock);
        SelectRow(widthController, SendWidth, "Menu Customization");
        SelectRow(widthController, SendWidth, "Width Editor");
        fixtures.Add("width-editor-default.json", widthController.Snapshot());
        for (int index = 0; index < 9; index++) SendWidth(OverlayInput.IncreaseWidthStep);
        for (int index = 0; index < 24; index++) SendWidth(OverlayInput.IncreaseWidth);
        fixtures.Add("width-editor-wide.json", widthController.Snapshot());
        SendWidth(OverlayInput.FinishWidthEditor);
        fixtures.Add("customization-unlock-rebuild.json", widthController.Snapshot());
        widthClock.Advance(TimeSpan.FromMilliseconds(50));
        fixtures.Add("customization-unlock-pause.json", widthController.Snapshot());
        widthClock.Advance(TimeSpan.FromMilliseconds(100));
        fixtures.Add("customization-wide.json", widthController.Snapshot());
        // These profiles use a fresh controller so the previous appearance examples stay unchanged
        var instructionBoundary = new SessionBoundary();
        instructionBoundary.State = instructionBoundary.State with { Preview = true };
        var instructionClock = new ManualTimeProvider();
        var instructionController = new BackendController(42, instructionBoundary, clock: instructionClock);
        ulong instructionSequence = 1;
        void SendInstruction(OverlayInput input) => instructionController.Consume(
            new(instructionSequence++, 1, 42, input, true), () => true);
        void SelectInstruction(string label) {
            var current = instructionController.Snapshot();
            int target = current.Rows.ToList().FindIndex(row => row.Label == label);
            if (target < 0) throw new InvalidOperationException("Required instruction preview row is unavailable");
            for (int move = 0; move < current.Rows.Count && instructionController.Snapshot().SelectedIndex != target; move++)
                SendInstruction(OverlayInput.Down);
            SendInstruction(OverlayInput.Select);
        }
        SendInstruction(OverlayInput.Toggle); MenuOpenTestHarness.Settle(instructionController, instructionClock);
        SelectInstruction("Menu Customization");
        SelectInstruction("Menu Instructions");
        fixtures.Add("instructions-default.json", instructionController.Snapshot());
        SelectInstruction("Alternate Style");
        SelectInstruction("Disable Background");
        fixtures.Add("instructions-alternate-no-background.json", instructionController.Snapshot());
        SelectInstruction("Alternate Style");
        SelectInstruction("Disable Background");
        SelectInstruction("Adaptive Position");
        fixtures.Add("instructions-adaptive.json", instructionController.Snapshot());
        SelectInstruction("Adaptive Position");
        SelectInstruction("Disable");
        fixtures.Add("instructions-hidden.json", instructionController.Snapshot());
        // Position profiles share the same real editor and deterministic clock as the controller tests
        var position = InstructionPositionEditorTests.Open();
        position.Enter();
        fixtures.Add("instructions-position-entering.json", position.Controller.Snapshot());
        position.Clock.Advance(TimeSpan.FromMilliseconds(100));
        fixtures.Add("instructions-position-editing.json", position.Controller.Snapshot());
        position.Send(OverlayInput.MoveInstructionsRight);
        position.Send(OverlayInput.MoveInstructionsDown);
        fixtures.Add("instructions-position-moved.json", position.Controller.Snapshot());
        position.Send(OverlayInput.FinishInstructionPositionEditor);
        fixtures.Add("instructions-position-exiting.json", position.Controller.Snapshot());
        position.Clock.Advance(TimeSpan.FromMilliseconds(100)); position.Controller.Snapshot();
        position.Clock.Advance(TimeSpan.FromMilliseconds(150)); position.Controller.Snapshot();
        position.Select("Reset Position");
        fixtures.Add("instructions-position-reset.json", position.Controller.Snapshot());
        position.Select("Disable"); position.Select("Reset Position");
        fixtures.Add("instructions-hidden-reset.json", position.Controller.Snapshot());
        position.AssertNoGameWork();
        // Rainbow profiles use fresh source actions and retain the fixed fallback accent
        var rainbow = new SmoothRainbowThemeTests.Rig(preview: true);
        rainbow.OpenColors(); rainbow.Select("Smooth Rainbow");
        fixtures.Add("theme-colors-rainbow.json", rainbow.Controller.Snapshot());
        rainbow.Send(OverlayInput.Select);
        fixtures.Add("theme-colors-rainbow-no-op.json", rainbow.Controller.Snapshot());
        rainbow.Select("Mint");
        fixtures.Add("theme-colors-rainbow-stopped.json", rainbow.Controller.Snapshot());
        rainbow.AssertNoGameWork();
        // Numeric snapshots separate source candidates from actual committed milliseconds
        var scroll = new ScrollAnimationSelectionTests.Rig();
        scroll.OpenPreferences(); scroll.Highlight("Scroll Animation Time (ms)");
        scroll.Send(OverlayInput.NextChoice);
        fixtures.Add("preferences-scroll-draft.json", scroll.Controller.Snapshot());
        scroll.Send(OverlayInput.Select);
        fixtures.Add("preferences-scroll-committed.json", scroll.Controller.Snapshot());
        for (int step = 0; step < 3; step++) scroll.Send(OverlayInput.PreviousChoice);
        scroll.Send(OverlayInput.Select);
        fixtures.Add("preferences-scroll-min.json", scroll.Controller.Snapshot());
        scroll.Send(OverlayInput.PreviousChoice); scroll.Send(OverlayInput.Select);
        fixtures.Add("preferences-scroll-max.json", scroll.Controller.Snapshot());
        scroll.AssertNoGameWork();
        var motion = new ScrollAnimationSelectionTests.Rig();
        motion.Send(OverlayInput.Toggle); MenuOpenTestHarness.Settle(motion.Controller, motion.Clock); MenuOpenTestHarness.Settle(controller, controllerClock);
        fixtures.Add("scroll-motion-root-start.json", motion.Controller.Snapshot());
        motion.Send(OverlayInput.Down);
        fixtures.Add("scroll-motion-root-down.json", motion.Controller.Snapshot());
        motion.Send(OverlayInput.Toggle); MenuOpenTestHarness.Settle(motion.Controller, motion.Clock); MenuOpenTestHarness.Settle(controller, controllerClock); motion.Send(OverlayInput.Toggle); MenuOpenTestHarness.Settle(motion.Controller, motion.Clock); MenuOpenTestHarness.Settle(controller, controllerClock);
        fixtures.Add("scroll-motion-root-reopened.json", motion.Controller.Snapshot());
        motion.Select("Menu Customization"); motion.Select("Main Design Color");
        for (int row = 0; row < 4; row++) motion.Send(OverlayInput.Down);
        fixtures.Add("scroll-motion-colors-start.json", motion.Controller.Snapshot());
        motion.Send(OverlayInput.Down);
        fixtures.Add("scroll-motion-colors-window.json", motion.Controller.Snapshot());
        motion.Send(OverlayInput.Back);
        fixtures.Add("scroll-motion-customization.json", motion.Controller.Snapshot());
        motion.AssertNoGameWork();
        // Menu movement profiles use only the live local editor and its owned clock
        var menuPosition = new MenuPositionEditorTests.Rig();
        menuPosition.Enter();
        fixtures.Add("menu-position-editor-default.json", menuPosition.Current());
        menuPosition.Send(OverlayInput.MoveMenuRight); menuPosition.Send(OverlayInput.MoveMenuDown);
        fixtures.Add("menu-position-editor-moved.json", menuPosition.Current());
        for (int step = 0; step < 9; step++) menuPosition.Send(OverlayInput.IncreaseMenuPositionStep);
        menuPosition.Send(OverlayInput.MoveMenuRight); menuPosition.Send(OverlayInput.MoveMenuDown);
        fixtures.Add("menu-position-editor-step-max.json", menuPosition.Current());
        menuPosition.Send(OverlayInput.FinishMenuPositionEditor);
        fixtures.Add("menu-position-unlock-rebuild.json", menuPosition.Current());
        menuPosition.Clock.Advance(TimeSpan.FromMilliseconds(50));
        fixtures.Add("menu-position-unlock-pause.json", menuPosition.Current());
        menuPosition.Clock.Advance(TimeSpan.FromMilliseconds(100));
        fixtures.Add("customization-positioned.json", menuPosition.Current());
        menuPosition.NoGameWork();
        // Credits use the real controller clock and never connect a feature session
        var credits = new MenuCreditsTests.Rig(); credits.Enter();
        fixtures.Add("menu-credits-enter.json", credits.Current());
        credits.Clock.Advance(TimeSpan.FromMilliseconds(500));
        fixtures.Add("menu-credits-fade-in.json", credits.Current());
        credits.Clock.Advance(TimeSpan.FromMilliseconds(500));
        fixtures.Add("menu-credits-overlap.json", credits.Current());
        credits.Clock.Advance(TimeSpan.FromMilliseconds(38999));
        fixtures.Add("menu-credits-late.json", credits.Current());
        credits.Clock.Advance(TimeSpan.FromMilliseconds(1));
        fixtures.Add("menu-credits-unlock-rebuild.json", credits.Current());
        credits.Clock.Advance(TimeSpan.FromMilliseconds(50));
        fixtures.Add("menu-credits-unlock-pause.json", credits.Current());
        credits.Clock.Advance(TimeSpan.FromMilliseconds(100));
        fixtures.Add("customization-after-credits.json", credits.Current());
        credits.NoGameWork();
        // Each refresh profile uses one real preference commit and a separate owned clock
        string[] refreshNames = { "spotlight", "colored", "large", "quick-exit" };
        for (int flag = 0; flag < refreshNames.Length; flag++) {
            var refresh = new MenuPreferenceRefreshTests.Rig(); refresh.Open();
            refresh.Select(MenuPreferenceRefreshTests.Flags[flag].Label);
            string prefix = "preferences-refresh-" + refreshNames[flag];
            fixtures.Add(prefix + "-pending.json", refresh.Current());
            refresh.Clock.Advance(TimeSpan.FromMilliseconds(179));
            fixtures.Add(prefix + "-pre-refresh.json", refresh.Current());
            refresh.Clock.Advance(TimeSpan.FromMilliseconds(1));
            fixtures.Add(prefix + "-cooldown.json", refresh.Current());
            refresh.Clock.Advance(TimeSpan.FromMilliseconds(249));
            fixtures.Add(prefix + "-pre-unlock.json", refresh.Current());
            refresh.Clock.Advance(TimeSpan.FromMilliseconds(1));
            fixtures.Add(prefix + "-unlocked.json", refresh.Current());
            refresh.NoGameWork();
        }
        // Each design profile commits through the real staged source slider and owned clock
        foreach (string name in MenuDesignTests.Names.Skip(1))
        {
            var design = new MenuDesignTests.Rig(widthControls: true); design.Open();
            design.Select("Option Counter");
            design.Clock.Advance(TimeSpan.FromMilliseconds(250));
            design.Current();
            design.Stage(name); design.Send(OverlayInput.Select); string prefix = "preferences-design-" + name.ToLowerInvariant();
            fixtures.Add(prefix + "-rebuild.json", design.Current());
            design.Clock.Advance(TimeSpan.FromMilliseconds(49)); fixtures.Add(prefix + "-pre-rebuild.json", design.Current());
            design.Clock.Advance(TimeSpan.FromMilliseconds(1)); fixtures.Add(prefix + "-cooldown.json", design.Current());
            design.Clock.Advance(TimeSpan.FromMilliseconds(199)); fixtures.Add(prefix + "-pre-unlock.json", design.Current());
            design.Clock.Advance(TimeSpan.FromMilliseconds(1)); fixtures.Add(prefix + "-unlocked.json", design.Current());
            if (name == "Native")
            {
                design.Send(OverlayInput.Back); design.Send(OverlayInput.Back); fixtures.Add("main-design-native.json", design.Current());
            }
            else if (name == "Classic")
            {
                design.Send(OverlayInput.Back); design.Select("Width Editor"); fixtures.Add("width-editor-design-classic.json", design.Current());
            }
            design.NoGameWork();
        }
        var draftDesign = new MenuDesignTests.Rig(); draftDesign.Open(); draftDesign.Stage("Classic");
        fixtures.Add("preferences-design-draft.json", draftDesign.Current()); draftDesign.NoGameWork();
        var sameDesign = new MenuDesignTests.Rig(); sameDesign.Open(); sameDesign.Send(OverlayInput.Select);
        fixtures.Add("preferences-design-same-cooldown.json", sameDesign.Current());
        sameDesign.Clock.Advance(TimeSpan.FromMilliseconds(249)); fixtures.Add("preferences-design-same-pre-unlock.json", sameDesign.Current());
        sameDesign.Clock.Advance(TimeSpan.FromMilliseconds(1)); fixtures.Add("preferences-design-same-unlocked.json", sameDesign.Current()); sameDesign.NoGameWork();
        // Opening profiles retain real row models and advance only their owned local clock
        var opening = MenuOpeningTests.Start(); opening.Clock.Advance(TimeSpan.FromMilliseconds(50));
        fixtures.Add("menu-opening-start.json", opening.Current());
        opening.Clock.Advance(TimeSpan.FromMilliseconds(100)); fixtures.Add("menu-opening-progress.json", opening.Current());
        opening.Clock.Advance(TimeSpan.FromMilliseconds(100)); fixtures.Add("menu-opening-input-ready.json", opening.Current());
        opening.Clock.Advance(TimeSpan.FromMilliseconds(699)); fixtures.Add("menu-opening-last.json", opening.Current());
        opening.Clock.Advance(TimeSpan.FromMilliseconds(1)); fixtures.Add("menu-opening-complete.json", opening.Current()); opening.NoGameWork();
        var openingDraft = MenuOpeningTests.Start(); MenuOpeningTests.AdvanceToReady(openingDraft); openingDraft.Send(OverlayInput.NextChoice);
        fixtures.Add("menu-opening-draft.json", openingDraft.Current()); openingDraft.NoGameWork();
        var openingSame = MenuOpeningTests.Start(); MenuOpeningTests.AdvanceToReady(openingSame); openingSame.Send(OverlayInput.Select);
        fixtures.Add("menu-opening-same.json", openingSame.Current()); openingSame.NoGameWork();
        var openingScroll = MenuOpeningTests.Start(); MenuOpeningTests.AdvanceToReady(openingScroll); openingScroll.Send(OverlayInput.Down);
        fixtures.Add("menu-opening-scroll.json", openingScroll.Current()); openingScroll.NoGameWork();
        var openingLate = MenuOpeningTests.Start(); openingLate.Clock.Advance(TimeSpan.FromMilliseconds(1000));
        fixtures.Add("menu-opening-late.json", openingLate.Current()); openingLate.NoGameWork();
        var openingEight = new MenuDesignTests.Rig(); openingEight.Open();
        openingEight.Select("Option Counter");
        openingEight.Clock.Advance(TimeSpan.FromMilliseconds(250));
        openingEight.Current();
        openingEight.Stage("Native"); openingEight.Send(OverlayInput.Select);
        openingEight.Clock.Advance(TimeSpan.FromMilliseconds(50)); fixtures.Add("menu-opening-eight-start.json", openingEight.Current());
        openingEight.Clock.Advance(TimeSpan.FromMilliseconds(699)); fixtures.Add("menu-opening-eight-last.json", openingEight.Current());
        openingEight.Clock.Advance(TimeSpan.FromMilliseconds(1)); fixtures.Add("menu-opening-eight-complete.json", openingEight.Current()); openingEight.NoGameWork();
        instructionController.StartQueuedAction();
        controller.StartQueuedAction();
        widthController.StartQueuedAction();
        if (boundary.Executed.Count != 0 || boundary.ShutdownCalls != 0 || controller.ExitRequested
            || instructionBoundary.Executed.Count != 0 || instructionBoundary.ShutdownCalls != 0 || instructionController.ExitRequested
            || widthBoundary.Executed.Count != 0 || widthBoundary.ShutdownCalls != 0 || widthController.ExitRequested) {
            throw new InvalidOperationException("Offline export cannot dispatch game actions or session cleanup");
        }
        MenuInitialOpenFixtureExporter.Add(fixtures);
        MenuDesignFooterFixtureExporter.Add(fixtures);
        StealthFixtureExporter.Add(fixtures);
        OpeningControlsFixtureExporter.Add(fixtures);
        QuickControlsFixtureExporter.Add(fixtures);
        FamilyNavigationFixtureProfiles.Add(fixtures);
        QuickPageFixtureProfiles.Add(fixtures);
        ScopedOpeningFixtureProfiles.Add(fixtures);
        return fixtures;
    }
}
