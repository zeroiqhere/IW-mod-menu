using Apparition.Backend.Session;
using syndishanx_iw_tool.Features.Godmode;
using syndishanx_iw_tool.Overlay;
using static Session.Tests.RetailBackendSessionTests;

namespace Session.Tests;

// Captured sets exercise owner reuse, not a claim of complete retail lobby detection
internal static class SharedGodmodeOwnerTests {
    public static async Task SharedCaptureReusesPersonalAndSelectedPeerOwnersWithoutChangingSelection() {
        await using var fixture = new SharedGodmodeFixture();
        await fixture.CaptureLocalAsync();
        Check(await fixture.Owner.Teammates.SelectAsync(0, "Host"), "Peer selection failed");
        await fixture.Owner.Teammates.ToggleAsync(0, "Host");
        Check((await fixture.Owner.GodmodeOwners.SetAsync(fixture.Identity(3), true)).Succeeded,
            "The personal registry enable failed");
        int writes = fixture.Transport.FlagWrites;
        var captured = await fixture.Owner.GodmodeOwners.CaptureAsync(new[] { fixture.Identity(3), fixture.Identity(0) });
        Check(captured.Succeeded && captured.Snapshot.Value == GodmodeValue.On
            && fixture.Transport.FlagWrites == writes && fixture.Transport.LocalBindCalls == 1
            && fixture.Transport.TeammateBindCalls == 1 && fixture.Owner.Teammates.ReadView().Identity == fixture.Identity(0),
            "Shared capture created a writer, changed flags, or moved the selected player");
        Check((await fixture.Owner.GodmodeOwners.SetBatchAsync(new[] { fixture.Identity(3), fixture.Identity(0) }, false)).Succeeded,
            "Shared OFF failed");
        await fixture.Owner.Teammates.RefreshAsync();
        Check(fixture.Owner.LocalGodmode!.ReadView().Value == GodmodeValue.Off
            && fixture.Owner.Teammates.ReadView().Value == GodmodeValue.Off,
            "The existing individual owners did not observe shared restoration");
    }

    public static async Task SharedCapturedSetsUseOneWriterPerActorForEveryVerifiedLocalSlot() {
        foreach (int local in Enumerable.Range(0, 4)) {
            await using var fixture = new SharedGodmodeFixture(local);
            await fixture.CaptureLocalAsync();
            int peer = (local + 1) % 4;
            var identities = new[] { fixture.Identity(local), fixture.Identity(peer) };
            foreach (bool desired in new[] { true, false, true }) {
                Check((await fixture.Owner.GodmodeOwners.SetBatchAsync(identities, desired)).Succeeded,
                    "A captured shared desired-state change failed");
                Check(fixture.Transport.Flags(local) == (desired ? 0x201 : 0x200)
                    && fixture.Transport.Flags(peer) == (desired ? 0x201 : 0x200),
                    "A captured shared change missed an original actor");
            }
            Check(fixture.Transport.LocalBindCalls == 1 && fixture.Transport.TeammateBindCalls == 1
                && fixture.Transport.HealthWrites == 0
                && Enumerable.Range(0, 4).Except(new[] { local, peer }).All(slot => fixture.Transport.Flags(slot) == 0x200),
                "Shared changes rebound an actor or changed an uncaptured player");
        }
    }

    public static async Task MixedSharedEnablePreservesEarlierOnAndIndividualChangesUpdateTheAggregate() {
        await using var fixture = new SharedGodmodeFixture();
        await fixture.CaptureLocalAsync();
        var identities = Enumerable.Range(0, 4).Select(fixture.Identity).ToArray();
        Check((await fixture.Owner.GodmodeOwners.SetAsync(fixture.Identity(3), true)).Succeeded, "Personal enable failed");
        Check((await fixture.Owner.GodmodeOwners.SetAsync(fixture.Identity(0), true)).Succeeded, "Peer enable failed");
        var mixed = await fixture.Owner.GodmodeOwners.CaptureAsync(identities);
        Check(mixed.Succeeded && mixed.Snapshot.Value == GodmodeValue.Mixed, "Mixed state was not observed");
        Check((await fixture.Owner.GodmodeOwners.SetBatchAsync(identities, true)).Succeeded
            && fixture.Transport.LocalFlagWrites == 1 && fixture.Transport.FlagWrites == 4,
            "Shared enable toggled an earlier ON or repeated its write");
        Check((await fixture.Owner.GodmodeOwners.SetBatchAsync(identities, false)).Succeeded
            && fixture.Transport.FlagWrites == 8, "Shared OFF did not restore all captured owners once");
        Check((await fixture.Owner.GodmodeOwners.ToggleAsync(fixture.Identity(3))).Succeeded
            && fixture.Owner.GodmodeOwners.Snapshot(identities).Value == GodmodeValue.Mixed,
            "An individual toggle did not change the same shared aggregate");
        Check((await fixture.Owner.GodmodeOwners.SetBatchAsync(identities, true)).Succeeded
            && fixture.Transport.FlagWrites == 12 && fixture.Transport.LocalFlagWrites == 3,
            "A later mixed enable used an independent group latch or toggled the personal player");
    }

    public static async Task FailedPersonalRestorationCanRetryThroughTheSameSharedRegistryAndMenu() {
        var transport = new RecordingRetailTransport { LocalSlot = 2 };
        var session = Create(transport);
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            await session.ExecuteAsync(new(MenuActionKind.TogglePersonalGodmode));
            transport.RejectLocalFlagRestore = true;
            await session.ExecuteAsync(new(MenuActionKind.RestorePersonalGodmode));
            Check(session.ReadMenuState().CanRestorePersonalGodmode && transport.Flags(2) == 0x201,
                "Failed restoration lost the original personal cleanup route");
            transport.RejectLocalFlagRestore = false;
            await session.ExecuteAsync(new(MenuActionKind.RestorePersonalGodmode));
            await session.ExecuteAsync(new(MenuActionKind.RefreshPlayers));
            var row = OverlayMenuBuilder.Build(MenuPage.Personal, session.ReadMenuState()).Rows[0];
            Check(row.Enabled && row.Value == "OFF", "Successful retry left stale shared cleanup metadata");
            var group = OverlayMenuBuilder.Build(MenuPage.AllPlayers, session.ReadMenuState()).Rows.Single(row => row.Label == "God Mode");
            Check(!group.Enabled && group.Action.Kind == MenuActionKind.None,
                "Shared owner integration exposed an unproved All Players callback");
            await session.ExecuteAsync(row.Action);
            Check(transport.Flags(2) == 0x201 && transport.LocalBindCalls == 1 && transport.TeammateBindCalls == 0,
                "The menu did not reuse the recovered original personal owner");
        } finally {
            transport.RejectLocalFlagRestore = false;
            await session.ShutdownAsync();
        }
    }

    public static async Task SharedRegistryCannotBindAnAuthenticatedSelfThroughThePeerFactory() {
        await using var fixture = new SharedGodmodeFixture();
        fixture.Transport.SetFlags(3, 0x201);
        Check(!await fixture.Owner.CapturePersonalGodmodeAsync(), "Foreign flag was captured as owned");
        var result = await fixture.Owner.GodmodeOwners.SetAsync(fixture.Identity(3), true);
        Check(!result.Succeeded && fixture.Transport.LocalBindCalls == 0 && fixture.Transport.TeammateBindCalls == 0
            && fixture.Transport.FlagWrites == 0, "A known self without a usable local adapter fell through to a peer writer");
    }

    public static async Task PendingPeerCleanupDisablesANewPersonalEnableUntilTheOriginalPeerIsRestored() {
        var transport = new RecordingRetailTransport { LocalSlot = 3 };
        var session = Create(transport);
        try {
            await session.ExecuteAsync(new(MenuActionKind.Connect));
            await session.ExecuteAsync(Select(1));
            await session.ExecuteAsync(Toggle(1));
            Check(transport.Flags(1) == 0x201 && transport.Flags(3) == 0x200,
                "The pending-cleanup test did not establish the independent peer ON state");
            transport.RejectFlagRestore = true;
            await session.ExecuteAsync(Toggle(1));
            Check(transport.Flags(1) == 0x201 && transport.FlagWrites == 1,
                "The original peer restoration was not rejected");
            var blocked = OverlayMenuBuilder.Build(MenuPage.Personal, session.ReadMenuState()).Rows[0];
            Check(!blocked.Enabled && blocked.Value == "OFF" && blocked.Detail.Contains("cleanup", StringComparison.OrdinalIgnoreCase),
                "The personal row advertised a new enable blocked by the shared registry's pending peer cleanup");
            transport.RejectFlagRestore = false;
            await session.ExecuteAsync(Toggle(1));
            var recovered = OverlayMenuBuilder.Build(MenuPage.Personal, session.ReadMenuState()).Rows[0];
            Check(recovered.Enabled && recovered.Value == "OFF", "Peer recovery left personal enable unavailable");
            await session.ExecuteAsync(recovered.Action);
            Check(transport.Flags(3) == 0x201 && transport.Flags(1) == 0x200
                && transport.LocalBindCalls == 1 && transport.TeammateBindCalls == 1,
                "Recovered personal enable did not use the original independent local owner");
        } finally {
            transport.RejectFlagRestore = false;
            await session.ShutdownAsync();
        }
    }

    public static async Task SharedCloseJoinsBothOwnersOnceAndPreservesCleanupForRetry() {
        await using var fixture = new SharedGodmodeFixture();
        await fixture.CaptureLocalAsync();
        var identities = new[] { fixture.Identity(3), fixture.Identity(0) };
        Check((await fixture.Owner.GodmodeOwners.SetBatchAsync(identities, true)).Succeeded, "Shared enable failed");
        fixture.Transport.RejectLocalFlagRestore = true;
        var failed = await fixture.Owner.CloseGodmodeOwnersAsync();
        Check(!failed.SafeToRelease && !failed.Restored && fixture.Transport.Flags(3) == 0x201
            && fixture.Transport.Flags(0) == 0x200 && fixture.Transport.LocalFlagWrites == 1,
            "Shared close released unconfirmed local cleanup or skipped an independent peer");
        fixture.Transport.RejectLocalFlagRestore = false;
        var retried = await fixture.Owner.CloseGodmodeOwnersAsync();
        Check(retried.SafeToRelease && fixture.Transport.FlagWrites == 4 && fixture.Transport.LocalFlagWrites == 2
            && fixture.Transport.LocalBindCalls == 1 && fixture.Transport.TeammateBindCalls == 1,
            "Close retry duplicated a restoration or replaced an original adapter");
    }
}
