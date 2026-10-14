using syndishanx_iw_tool.Features.Godmode;

namespace Session.Tests;

internal static class LocalGodmodeBindingAdmissionTests {
    public static async Task CloseRequestedDuringBindingRetainsTheAdapterWithoutFirstEnable() {
        await using var fixture = new LocalGodmodeFixture(2);
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.BeforeBind = () => {
            entered.TrySetResult();
            release.Wait();
        };
        Task<bool> enabling = Task.Run(() => fixture.Service.ToggleAsync());
        Task<Apparition.Backend.Session.LocalGodmodeCloseResult>? closing = null;
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            // Admission closes while the actual production bound adapter is still being constructed
            fixture.Service.RequestClose();
            closing = fixture.Service.CloseAsync();
            LocalGodmodeFixture.Check(!closing.IsCompleted && fixture.Flags.Writes == 0,
                "Cleanup cannot pass a held binding factory or enable its flag");
            release.Set();
            LocalGodmodeFixture.Check(!await enabling.WaitAsync(TimeSpan.FromSeconds(5)),
                "A binding completed after close admission and still enabled its first flag");
            var outcome = await closing.WaitAsync(TimeSpan.FromSeconds(5));
            var view = fixture.Service.ReadView();
            LocalGodmodeFixture.Check(outcome.Stopped && outcome.Restored && fixture.BindCalls == 1
                && fixture.Flags.Writes == 0 && fixture.Flags.Flags == 0x200
                && view.HasBoundTarget && view.Binding == fixture.Binding && view.Value == GodmodeValue.Unknown,
                "The constructed original adapter must be retained and joined without a first write");
        } finally {
            fixture.BeforeBind = null;
            release.Set();
            await enabling.WaitAsync(TimeSpan.FromSeconds(5));
            if (closing is not null) await closing.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }
    public static async Task CloseRequestedDuringNativeBaselinePreventsTheLaterOnWriteEntry() {
        await using var fixture = new LocalGodmodeFixture();
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int first = 0;
        fixture.Flags.OnRead = _ => {
            if (Interlocked.Exchange(ref first, 1) == 0) {
                entered.TrySetResult();
                release.Wait();
            }
            return fixture.Flags.Flags;
        };
        Task<bool> enabling = Task.Run(() => fixture.Service.ToggleAsync());
        Task<Apparition.Backend.Session.LocalGodmodeCloseResult>? closing = null;
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            // The primitive has entered its real baseline read but not its ON writer
            fixture.Service.RequestClose();
            closing = fixture.Service.CloseAsync();
            release.Set();
            LocalGodmodeFixture.Check(!await enabling.WaitAsync(TimeSpan.FromSeconds(5)),
                "A held baseline read entered the ON writer after close admission");
            var outcome = await closing.WaitAsync(TimeSpan.FromSeconds(5));
            LocalGodmodeFixture.Check(outcome.Stopped && outcome.Restored && fixture.Flags.Writes == 0
                && fixture.Flags.Flags == 0x200 && fixture.BindCalls == 1,
                "Rejected late ON must join its original adapter without a flag write");
        } finally {
            fixture.Flags.OnRead = null;
            release.Set();
            await enabling.WaitAsync(TimeSpan.FromSeconds(5));
            if (closing is not null) await closing.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

}
