namespace Session.Tests;

internal static class LocalGodmodeObservationAdmissionTests {
    public static async Task ObservationRecoveryBusyOwnerRejectsTheReceiptWithoutQueuingAnotherCapture() {
        await using var fixture = new LocalGodmodeFixture();
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.BeforeCurrentRead = () => {
            entered.TrySetResult();
            release.Wait();
        };
        // Keep the real explicit operation inside its strict read while polling submits a receipt
        Task<bool> explicitCapture = Task.Run(() => fixture.Service.CaptureAsync());
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            bool recovered = await fixture.Service.CaptureAsync(expectedBinding: fixture.Binding)
                .WaitAsync(TimeSpan.FromSeconds(2));
            LocalGodmodeFixture.Check(!recovered && !explicitCapture.IsCompleted
                && fixture.CurrentReads == 1 && fixture.BindCalls == 0 && fixture.Flags.Writes == 0,
                "Background recovery waited behind or entered the occupied explicit owner");
        } finally {
            fixture.BeforeCurrentRead = null;
            release.Set();
            await explicitCapture.WaitAsync(TimeSpan.FromSeconds(5));
        }
        // Releasing the explicit read must not leave a stale background capture to run afterward
        LocalGodmodeFixture.Check(explicitCapture.Result && fixture.CurrentReads == 1
            && fixture.BindCalls == 1 && fixture.Flags.Writes == 0,
            "A rejected receipt remained queued after the explicit owner completed");
    }
}
