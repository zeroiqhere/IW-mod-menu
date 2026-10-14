using System.Text.Json;
using Apparition.Backend.Controller;
using Apparition.Backend.Ipc;
using Apparition.Backend.OpenControls;
using syndishanx_iw_tool.Overlay;

namespace Backend.Tests;

// This rig drives the actual controller against the existing offline session boundary
internal sealed class OpeningControlsRig {
    internal SessionBoundary Session { get; } = new();
    internal ManualTimeProvider Clock { get; } = new();
    internal BackendController Controller { get; }
    internal ulong Sequence { get; set; } = 1;

    internal OpeningControlsRig(VerifiedOpeningActions? actions = null, bool configured = true) {
        Session.State = Session.State with { Preview = true };
        Controller = new(42, Session, choiceControlsAvailable: true, clock: Clock,
            openingActions: configured ? actions ?? MainOpeningChordTests.AllActions() : null);
    }

    internal StateDocument Current() => Controller.Snapshot();
    internal void Send(OverlayInput input) {
        var frame = Current();
        // Mirror the native command scope instead of attaching policy epochs to movement
        ulong? policyRevision = input is OverlayInput.Select or OverlayInput.PreviousChoice or OverlayInput.NextChoice
            ? Revision(frame) : null;
        Controller.Consume(new(Sequence++, 1, 42, input, true, frame.NavigationRevision, policyRevision), () => true);
    }
    internal void Open() {
        Send(OverlayInput.Toggle);
        MenuOpenTestHarness.Settle(Controller, Clock);
        Select("Menu Customization");
        Select("Open Controls");
    }
    internal void Highlight(string label) {
        int target = Current().Rows.ToList().FindIndex(row => row.Label == label);
        TestAssert.True(target >= 0, "Required row missing: " + label);
        for (int count = 0; count < 128 && Current().SelectedIndex != target; count++) Send(OverlayInput.Down);
        TestAssert.Equal(target, Current().SelectedIndex);
    }
    internal void Select(string label) { Highlight(label); Send(OverlayInput.Select); }
    internal void Wait(int milliseconds) { Clock.Advance(TimeSpan.FromMilliseconds(milliseconds)); Current(); }
    internal void SetSlot(int slot) {
        Highlight("Bind Slot");
        while (Current().Rows[1].Value != slot.ToString(System.Globalization.CultureInfo.InvariantCulture)) Send(OverlayInput.NextChoice);
        Send(OverlayInput.Select);
        Wait(250);
    }
    internal void NoGameWork() {
        Controller.StartQueuedAction();
        TestAssert.Equal(0, Session.Executed.Count);
        TestAssert.Equal(0, Session.ShutdownCalls);
    }
    internal static ulong? Revision(StateDocument frame) {
        using var json = JsonDocument.Parse(frame.Serialize());
        // A configured pair has one admission epoch, separate from each tuple revision
        if (json.RootElement.TryGetProperty("openingPolicies", out var pair)) return pair.GetProperty("revision").GetUInt64();
        return json.RootElement.TryGetProperty("mainOpeningControls", out var value) ? value.GetProperty("revision").GetUInt64() : null;
    }
    internal static int[] Actions(StateDocument frame) {
        using var json = JsonDocument.Parse(frame.Serialize());
        return json.RootElement.GetProperty("mainOpeningControls").GetProperty("actions").EnumerateArray().Select(item => item.GetInt32()).ToArray();
    }
}
