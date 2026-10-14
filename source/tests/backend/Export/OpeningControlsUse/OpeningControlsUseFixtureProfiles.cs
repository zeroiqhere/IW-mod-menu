using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

// Every recorded row and command comes from one admitted controller visit
internal sealed record OpeningControlsUseFixtureProfile(string Name, string Graph, string Event,
    long ClockMilliseconds, long RelativeMilliseconds, MenuPage Page, int SelectedSourceIndex,
    StateDocument State, CommandEnvelope? Command, StateDocument? CapturedFrame, string? CapturedFrameName,
    int SessionActionCount);

internal static class OpeningControlsUseFixtureProfiles {
    private const string BindingGraph = "binding";

    public static IReadOnlyList<OpeningControlsUseFixtureProfile> Build() {
        var profiles = new List<OpeningControlsUseFixtureProfile>();
        var binding = OpeningControlsUseFixture.Ready(selectedLabel: "Use");
        long bindingOrigin = Now(binding);
        AddFrame(profiles, "binding-before0", BindingGraph, "before", binding, bindingOrigin);
        var first = binding.Capture(OverlayInput.Select);
        AddCommand(profiles, "binding-select0", BindingGraph, "first-select", binding, bindingOrigin,
            first, "binding-before0");
        AddWait(profiles, "binding-wait179", BindingGraph, "first-wait-179", binding, bindingOrigin, 179);
        AddWait(profiles, "binding-redraw180", BindingGraph, "first-redraw-180", binding, bindingOrigin, 1);
        AddWait(profiles, "binding-wait429", BindingGraph, "first-wait-429", binding, bindingOrigin, 249);
        AddWait(profiles, "binding-ready430", BindingGraph, "first-ready-430", binding, bindingOrigin, 1);

        var second = binding.Capture(OverlayInput.Select);
        AddCommand(profiles, "binding-second-select430", BindingGraph, "second-select", binding, bindingOrigin,
            second, "binding-ready430");
        AddWait(profiles, "binding-second-redraw610", BindingGraph, "second-redraw-610", binding, bindingOrigin, 180);
        AddWait(profiles, "binding-second-ready860", BindingGraph, "second-ready-860", binding, bindingOrigin, 250);
        var third = binding.Capture(OverlayInput.Select);
        AddCommand(profiles, "binding-third-select860", BindingGraph, "third-select", binding, bindingOrigin,
            third, "binding-second-ready860");

        var melee = OpeningControlsUseFixture.Ready(selectedLabel: "Melee");
        long meleeOrigin = Now(melee);
        AddFrame(profiles, "duplicate-melee-before0", "duplicate-melee", "before", melee, meleeOrigin);
        var rejectedMelee = melee.Capture(OverlayInput.Select);
        AddCommand(profiles, "duplicate-melee-select0", "duplicate-melee", "duplicate-select", melee,
            meleeOrigin, rejectedMelee, "duplicate-melee-before0");
        AddWait(profiles, "duplicate-melee-wait179", "duplicate-melee", "wait-179", melee, meleeOrigin, 179);
        AddWait(profiles, "duplicate-melee-redraw180", "duplicate-melee", "redraw-180", melee, meleeOrigin, 1);
        AddWait(profiles, "duplicate-melee-wait429", "duplicate-melee", "wait-429", melee, meleeOrigin, 249);
        AddWait(profiles, "duplicate-melee-ready430", "duplicate-melee", "ready-430", melee, meleeOrigin, 1);

        var disableQuick = OpeningControlsUseFixture.Ready(MenuPage.Preferences, "Disable Quick Menu");
        long disableQuickOrigin = Now(disableQuick);
        AddFrame(profiles, "disable-quick-before0", "disable-quick", "before", disableQuick, disableQuickOrigin);
        var quickToggle = disableQuick.Capture(OverlayInput.Select);
        AddCommand(profiles, "disable-quick-select0", "disable-quick", "toggle-select", disableQuick,
            disableQuickOrigin, quickToggle, "disable-quick-before0");
        AddWait(profiles, "disable-quick-wait179", "disable-quick", "wait-179", disableQuick,
            disableQuickOrigin, 179);
        AddWait(profiles, "disable-quick-redraw180", "disable-quick", "redraw-180", disableQuick,
            disableQuickOrigin, 1);
        AddWait(profiles, "disable-quick-wait429", "disable-quick", "wait-429", disableQuick,
            disableQuickOrigin, 249);
        AddWait(profiles, "disable-quick-ready430", "disable-quick", "ready-430", disableQuick,
            disableQuickOrigin, 1);

        var menuSelector = OpeningControlsUseFixture.Ready(selectedLabel: "Menu");
        long menuOrigin = Now(menuSelector);
        AddFrame(profiles, "menu-selector-before0", "menu-selector", "before", menuSelector, menuOrigin);
        var sameMenu = menuSelector.Capture(OverlayInput.Select);
        AddCommand(profiles, "menu-selector-select0", "menu-selector", "same-main-select", menuSelector,
            menuOrigin, sameMenu, "menu-selector-before0");
        AddWait(profiles, "menu-selector-wait249", "menu-selector", "wait-249", menuSelector, menuOrigin, 249);
        AddWait(profiles, "menu-selector-ready250", "menu-selector", "ready-250", menuSelector, menuOrigin, 1);

        var slotSelector = OpeningControlsUseFixture.Ready(selectedLabel: "Bind Slot");
        long slotOrigin = Now(slotSelector);
        AddFrame(profiles, "slot-selector-before0", "slot-selector", "before", slotSelector, slotOrigin);
        var sameSlot = slotSelector.Capture(OverlayInput.Select);
        AddCommand(profiles, "slot-selector-select0", "slot-selector", "same-slot-select", slotSelector,
            slotOrigin, sameSlot, "slot-selector-before0");
        AddWait(profiles, "slot-selector-wait249", "slot-selector", "wait-249", slotSelector, slotOrigin, 249);
        AddWait(profiles, "slot-selector-ready250", "slot-selector", "ready-250", slotSelector, slotOrigin, 1);

        var changedSlot = OpeningControlsUseFixture.Ready(selectedLabel: "Bind Slot");
        long changedSlotOrigin = Now(changedSlot);
        AddFrame(profiles, "changed-slot-before0", "changed-slot", "before", changedSlot, changedSlotOrigin);
        var nextSlot = changedSlot.Capture(OverlayInput.NextChoice);
        AddCommand(profiles, "changed-slot-next-choice0", "changed-slot", "stage-slot-2", changedSlot,
            changedSlotOrigin, nextSlot, "changed-slot-before0");
        AddWait(profiles, "changed-slot-choice-ready180", "changed-slot", "choice-ready-180", changedSlot,
            changedSlotOrigin, 180);
        var applySlot = changedSlot.Capture(OverlayInput.Select);
        AddCommand(profiles, "changed-slot-select180", "changed-slot", "apply-slot-2", changedSlot,
            changedSlotOrigin, applySlot, "changed-slot-choice-ready180");
        AddWait(profiles, "changed-slot-wait249", "changed-slot", "callback-wait-249", changedSlot,
            changedSlotOrigin, 249);
        AddWait(profiles, "changed-slot-ready250", "changed-slot", "callback-ready-250", changedSlot,
            changedSlotOrigin, 1);

        return profiles.AsReadOnly();
    }

    private static void AddWait(ICollection<OpeningControlsUseFixtureProfile> profiles, string name,
        string graph, string eventName, OpeningControlsUseFixture rig, long origin, int elapsed) {
        StateDocument state = rig.Wait(elapsed);
        AddFrame(profiles, name, graph, eventName, rig, origin, state: state);
    }

    private static void AddCommand(ICollection<OpeningControlsUseFixtureProfile> profiles, string name,
        string graph, string eventName, OpeningControlsUseFixture rig, long origin,
        OpeningControlsUseFixture.CommandCapture capture, string captureFrameName, StateDocument? state = null) {
        AddFrame(profiles, name, graph, eventName, rig, origin, state ?? capture.ImmediateFrame,
            capture.WireCommand, capture.ObservedFrame, captureFrameName + ".json");
    }

    private static void AddFrame(ICollection<OpeningControlsUseFixtureProfile> profiles, string name,
        string graph, string eventName, OpeningControlsUseFixture rig, long origin, StateDocument? state = null,
        CommandEnvelope? command = null, StateDocument? capturedFrame = null, string? capturedFrameName = null) {
        StateDocument frame = state ?? rig.Frame;
        TestAssert.Equal(0, rig.SessionActionCount);
        profiles.Add(new(name + ".json", graph, eventName, Now(rig), Now(rig) - origin,
            rig.Navigation.Page, frame.SelectedIndex, frame, command, capturedFrame, capturedFrameName,
            rig.SessionActionCount));
    }

    private static long Now(OpeningControlsUseFixture rig) =>
        rig.Clock.GetTimestamp() / TimeSpan.TicksPerMillisecond;
}
