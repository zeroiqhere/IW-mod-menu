using Apparition.Backend.Session;
using Backend.Tests;
using syndishanx_iw_tool.Features.Godmode;
using syndishanx_iw_tool.Players;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Session.Tests.Session.Cohort;

// These groups test the owned service through real registry and flag primitives
internal static class CohortGodmodeSessionTests {
    public static async Task FreshHostCaptureUsesFourConfirmedActorsAndReadViewDoesNoNativeReads() {
        await using var fixture = new CohortGodmodeFixture();
        TestAssert.Equal(0, fixture.ActorReads);
        await fixture.Service.RefreshAsync();
        var view = fixture.Service.ReadView();
        TestAssert.True(view.CanToggle && view.Target is not null, "A complete current host cohort must be admitted");
        TestAssert.Equal(GodmodeValue.Off, view.Value);
        int actors = fixture.ActorReads, cohort = fixture.CohortReads, flags = fixture.Reads;
        for (int index = 0; index < 20; index++) TestAssert.True(fixture.Service.ReadView().CanToggle, "The cached view remains usable");
        TestAssert.Equal(actors, fixture.ActorReads);
        TestAssert.Equal(cohort, fixture.CohortReads);
        TestAssert.Equal(flags, fixture.Reads);
        TestAssert.Equal(0, fixture.Writes);
    }

    public static async Task FullBindingHostAndPartialRosterFailuresRetireTargetWithoutWrites() {
        foreach (string fault in new[] { "host", "binding", "partial", "process", "between" }) {
            await using var fixture = new CohortGodmodeFixture();
            await fixture.Service.RefreshAsync();
            var old = fixture.Service.ReadView().Target;
            TestAssert.True(old is not null, "The original target is captured before the fault");
            if (fault == "host") fixture.Host = false;
            if (fault == "binding") fixture.Binding = fixture.Binding with { AccountId = 124 };
            if (fault == "process") fixture.Connected = false;
            if (fault == "partial") fixture.OnCohort = () => new(fixture.Observation().Session,
                fixture.Observation().Slots.Select(slot => slot.Slot == 3
                    ? new PlayerSlotObservation(3, PlayerSlotPresence.Unknown) : slot).ToArray());
            if (fault == "between") {
                int first = fixture.ActorReads + 1;
                fixture.OnActor = count => new(fixture.Binding, 100, 0, count == first);
            }
            var result = await fixture.Service.ToggleAsync(old);
            TestAssert.True(!result.Succeeded, "A changed proof must reject the action");
            TestAssert.True(!fixture.Service.ReadView().CanToggle && fixture.Service.ReadView().Target is null,
                "A rejected proof retires its menu receipt");
            TestAssert.Equal(0, fixture.Writes);
        }
    }

    public static async Task SharedRegistryChangesMaskCachedViewAndFreshTargetsRejectOldRequests() {
        await using var fixture = new CohortGodmodeFixture();
        await fixture.Service.RefreshAsync();
        var old = fixture.Service.ReadView().Target;
        TestAssert.True(old is not null, "Capture precedes shared owner changes");
        TestAssert.True((await fixture.Registry.SetBatchAsync([fixture.Identities[1]], true)).Succeeded,
            "The selected-peer path uses the same registry");
        TestAssert.True(!fixture.Service.ReadView().CanToggle, "A shared operation invalidates cached capability");
        await fixture.Service.RefreshAsync();
        var current = fixture.Service.ReadView();
        TestAssert.Equal(GodmodeValue.Mixed, current.Value);
        TestAssert.True(current.CanToggle && !ReferenceEquals(old, current.Target), "A new receipt follows the changed shared state");
        int writes = fixture.Writes;
        TestAssert.True(!(await fixture.Service.ToggleAsync(old)).Succeeded, "The former target cannot replay");
        TestAssert.True(ReferenceEquals(current.Target, fixture.Service.ReadView().Target), "Rejected old intent cannot retire the valid current target");
        await using var other = new CohortGodmodeFixture();
        await other.Service.RefreshAsync();
        TestAssert.True(!(await fixture.Service.ToggleAsync(other.Service.ReadView().Target)).Succeeded, "A foreign owner receipt is rejected");
        TestAssert.True(ReferenceEquals(current.Target, fixture.Service.ReadView().Target), "Foreign rejection leaves the original target valid");
        TestAssert.Equal(writes, fixture.Writes);
    }

    public static async Task DeadOwnedOnCanTurnOffButDeadOffCannotEnable() {
        await using var fixture = new CohortGodmodeFixture();
        await fixture.Service.RefreshAsync();
        TestAssert.True((await fixture.Service.ToggleAsync(fixture.Service.ReadView().Target)).Succeeded, "The original cohort enables");
        fixture.Health = 0;
        fixture.Eligible = false;
        await fixture.Service.RefreshAsync();
        TestAssert.True(fixture.Service.ReadView().CanToggle, "Dead original ON remains cleanup-capable");
        TestAssert.True((await fixture.Service.ToggleAsync(fixture.Service.ReadView().Target)).Succeeded, "OFF uses retained life-independent ownership");
        TestAssert.True(fixture.Flags.Values.All(flags => flags.Flags == 0x200), "Every original word is restored");
        await fixture.Service.RefreshAsync();
        TestAssert.True(!fixture.Service.ReadView().CanToggle, "Dead OFF cannot advertise new ON");
        int writes = fixture.Writes;
        TestAssert.True(!(await fixture.Service.ToggleAsync(fixture.Service.ReadView().Target)).Succeeded, "Dead admission never enables");
        TestAssert.Equal(writes, fixture.Writes);
    }

    public static async Task CommandTimeRollbackAfterRefreshRejectsAndRetiresTarget() {
        await using var fixture = new CohortGodmodeFixture();
        await fixture.Service.RefreshAsync();
        var original = fixture.Service.ReadView().Target;
        fixture.CommandTime = 200;
        await fixture.Service.RefreshAsync();
        TestAssert.True(ReferenceEquals(original, fixture.Service.ReadView().Target), "Same cohort keeps its receipt while time advances");
        fixture.CommandTime = 150;
        TestAssert.True(!(await fixture.Service.ToggleAsync(original)).Succeeded, "The latest observed time cannot roll back");
        TestAssert.True(fixture.Service.ReadView().Target is null, "Rollback permanently retires the old receipt");
        TestAssert.Equal(0, fixture.Writes);
        await using var withdrawn = new CohortGodmodeFixture();
        await withdrawn.Service.RefreshAsync();
        withdrawn.Host = false;
        await withdrawn.Service.RefreshAsync();
        withdrawn.Host = true;
        withdrawn.CommandTime = 50;
        await withdrawn.Service.RefreshAsync();
        TestAssert.True(!withdrawn.Service.ReadView().CanToggle && withdrawn.Service.ReadView().Target is null,
            "Host recovery cannot erase a previously observed original command-time watermark");
        TestAssert.Equal(0, withdrawn.Writes);
    }

    public static async Task SameOwnerToggleReusesTargetAndExactSharedFeatures() {
        await using var fixture = new CohortGodmodeFixture();
        await fixture.Service.RefreshAsync();
        var target = fixture.Service.ReadView().Target;
        TestAssert.True((await fixture.Service.ToggleAsync(target)).Succeeded, "First toggle enables all actual features");
        TestAssert.True(ReferenceEquals(target, fixture.Service.ReadView().Target), "An owned toggle keeps the same target");
        TestAssert.True(fixture.Features.Values.All(feature => feature.Active), "No duplicate owner substitutes for the registry features");
        TestAssert.True((await fixture.Service.ToggleAsync(target)).Succeeded, "Second toggle restores all original words");
        TestAssert.True(fixture.Features.Values.All(feature => !feature.Active && !feature.HasPendingRestore), "Original owners finish restoration");
        TestAssert.Equal(8, fixture.Writes);
    }

    public static async Task CallerCancellationAndCloseJoinHeldSourceWithoutDetachedWork() {
        await using var fixture = new CohortGodmodeFixture(stopTimeout: TimeSpan.FromMilliseconds(30));
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        fixture.OnActor = _ => { entered.Set(); release.Wait(); return new(fixture.Binding, 100, 0, true); };
        using var caller = new CancellationTokenSource();
        Task refresh = fixture.Service.RefreshAsync(caller.Token);
        TestAssert.True(entered.Wait(TimeSpan.FromSeconds(3)), "The retained read reaches its source barrier");
        caller.Cancel();
        try { await refresh; } catch (OperationCanceledException) { }
        fixture.Service.RequestClose();
        bool timedOut = false;
        try { await fixture.Service.StopAsync(); } catch (IOException) { timedOut = true; }
        TestAssert.True(timedOut, "A held source is retained instead of reported stopped");
        release.Set();
        await fixture.Service.StopAsync();
        TestAssert.True(!fixture.Service.ReadView().CanToggle && fixture.Service.ReadView().Target is null,
            "A late canceled source cannot publish after close");
        TestAssert.Equal(0, fixture.Writes);
    }

    public static async Task FailedCaptureAndForeignFlagsRemainUnknownWithoutNewWrites() {
        await using var fixture = new CohortGodmodeFixture();
        fixture.Flags[fixture.Identities[2]].Flags = 0x201;
        await fixture.Service.RefreshAsync();
        TestAssert.Equal(GodmodeValue.Unknown, fixture.Service.ReadView().Value);
        TestAssert.True(!fixture.Service.ReadView().CanToggle && fixture.Service.ReadView().Target is null,
            "Foreign ON is not a captured group toggle");
        TestAssert.Equal(0, fixture.Writes);
    }

    public static async Task RollbackOnRequiresFreshHostButOwnedOffCleanupSurvivesLoss() {
        await using var fixture = new CohortGodmodeFixture();
        await fixture.Service.RefreshAsync();
        TestAssert.True((await fixture.Service.ToggleAsync(fixture.Service.ReadView().Target)).Succeeded, "Initial ON establishes actual owned words");
        fixture.Flags[fixture.Identities[0]].OnWrite = (_, _, desired) => {
            if ((desired & 1U) == 0) fixture.Host = false;
            return true;
        };
        var result = await fixture.Service.ToggleAsync(fixture.Service.ReadView().Target);
        TestAssert.True(!result.Succeeded, "Lost host proof interrupts the batch");
        TestAssert.Equal(0x200U, fixture.Flags[fixture.Identities[0]].Flags);
        TestAssert.Equal(2, fixture.Flags[fixture.Identities[0]].Writes);
        // The service owns no second cleanup handle and cannot block shared owner close
        fixture.Flags[fixture.Identities[0]].OnWrite = null;
        var cleanup = await fixture.Registry.CloseAsync();
        TestAssert.True(cleanup.SafeToRelease, "Original owned OFF cleanup survives host loss");
        TestAssert.True(fixture.Flags.Values.All(flags => flags.Flags == 0x200), "All retained words restore without a new cohort");
    }

    public static async Task UnavailableSourcesAndUnregisteredSelfNeverReadOrBind() {
        foreach (bool available in new[] { false, true }) {
            await using var fixture = new CohortGodmodeFixture(available);
            fixture.Registered = false;
            await fixture.Service.RefreshAsync();
            TestAssert.True(!fixture.Service.ReadView().CanToggle, "Unregistered self cannot resolve as a peer");
            TestAssert.Equal(0, fixture.ActorReads);
            TestAssert.Equal(0, fixture.CohortReads);
            TestAssert.Equal(0, fixture.Reads);
            TestAssert.Equal(0, fixture.Writes);
        }
    }
    public static async Task SnapshotAndMetadataMustDescribeTheSameOwnedFlagsAtPublication() {
        await using var fixture = new CohortGodmodeFixture();
        var cached = new CachedStateGodmodeFeature(fixture.Features[fixture.Identities[0]]);
        fixture.RegistryFeatures[fixture.Identities[0]] = cached;
        await fixture.Service.RefreshAsync();
        int allOnGuards = 0;
        fixture.OnCohort = () => {
            // The first all-ON guard precedes the copied snapshot; the second follows it
            if (fixture.Flags.Values.All(flags => (flags.Flags & 1U) != 0) && ++allOnGuards == 2) {
                cached.ForceInactive = true;
                fixture.Flags[fixture.Identities[0]].Flags = 0x200;
            }
            return fixture.Observation();
        };
        var result = await fixture.Service.ToggleAsync(fixture.Service.ReadView().Target);
        TestAssert.True(allOnGuards >= 2 && cached.ForceInactive, "The actual completion boundary was reached");
        TestAssert.True(!result.Succeeded, "An old ON snapshot cannot be paired with inactive owner metadata");
        var view = fixture.Service.ReadView();
        TestAssert.Equal(GodmodeValue.Unknown, view.Value);
        TestAssert.True(!view.CanToggle && view.Target is null, "Publication withdraws the contradictory receipt");
        TestAssert.True(fixture.Features.Values.Any(feature => feature.HasPendingRestore),
            "Failure keeps actual original cleanup owners instead of creating replacement handles");
    }

}
