using Apparition.Backend.Session;
using Features.Tests;
using syndishanx_iw_tool.GameSession;
using syndishanx_iw_tool.Features;
using syndishanx_iw_tool.Players;
using syndishanx_iw_tool.Players.LocalBinding;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Session.Tests;

// All fields and flag writes stay inside this owned fixture, with no process access
internal sealed class LocalGodmodeFixture : IAsyncDisposable {
    public RecordingFlagsTransport Flags { get; } = new();
    public LocalAccountSessionBinding Binding { get; set; }
    public int Health { get; set; } = 100;
    public int SessionState { get; set; }
    public int CommandTime { get; set; } = 1000;
    public int MovementType { get; set; }
    public bool ReadFailure { get; set; }
    public bool BindFailure { get; set; }
    public Action? BeforeCurrentRead { get; set; }
    public Action? BeforeBind { get; set; }
    public int CurrentReads { get; private set; }
    public int BindCalls { get; private set; }
    public LocalGodmodeSession Service { get; }

    public LocalGodmodeFixture(int slot = 0, TimeSpan? interval = null) {
        long server = 0x30000;
        var identity = new PlayerIdentity(slot, 0x10000 + slot * 0x3F8, 0x20000,
            server, 4, "Offline local", "1234567890ABCDEF") { ServerClient = server + slot * 715416L };
        Binding = new(0, 123, 0x60000, 1, 0x70000, slot, identity);
        Service = new(ReadCurrent, Bind, interval ?? TimeSpan.FromHours(1));
    }

    private LocalActorFlagsSnapshot ReadCurrent() {
        CurrentReads++;
        BeforeCurrentRead?.Invoke();
        if (ReadFailure || !Flags.Connected) throw new IOException("The strict local source is unavailable");
        return new(new(Binding, Health, SessionState, true), CommandTime, MovementType, Flags.Flags);
    }

    private ITeammateFlagsTransport Bind(LocalActorFlagsSnapshot snapshot) {
        BindCalls++;
        BeforeBind?.Invoke();
        if (BindFailure) throw new IOException("The owned binding factory failed");
        return new BoundLocalActorFlagsTransport(snapshot, () => Flags.Connected,
            () => new LocalOwnedActorFlagsSnapshot(new(Binding, Health, SessionState, true),
                CommandTime, MovementType, Flags.ReadFlags()),
            (address, expected, desired) => {
                Check(address == snapshot.Binding.Identity.Entity + 0x1C8,
                    "The writer must retain the original actor flags address");
                return Flags.WriteFlags(expected, desired);
            });
    }

    // Every fixture joins its real feature monitor even when an assertion fails
    public async ValueTask DisposeAsync() {
        await Service.CloseAsync();
    }

    public static void Check(bool value, string reason) {
        if (!value) throw new InvalidOperationException(reason);
    }
}
