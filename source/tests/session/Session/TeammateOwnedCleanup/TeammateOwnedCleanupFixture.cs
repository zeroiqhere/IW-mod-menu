using Apparition.Backend.Session;
using syndishanx_iw_tool.Features.Godmode;
using syndishanx_iw_tool.GameSession;
using syndishanx_iw_tool.Players;

namespace Session.Tests;

// Real flag ownership uses only this fixture's copied player state
internal sealed class TeammateOwnedCleanupFixture {
    private readonly object gate = new();
    public PlayerState State = new(new(1, 0x101000, 0x201000, 0x300000, 4, "Friend1", "0000000000000001") {
        ServerClient = 0x300000 + 715416L
    }, 100, 100, 0x200);
    public bool Connected = true;
    public bool RejectRestore = false;
    public bool ReadFails = false;
    public Func<PlayerState, PlayerState>? StrictTransform = null;
    public Func<OwnedPlayerFlagsState, OwnedPlayerFlagsState>? OwnedTransform = null;
    public Action? BeforeBoundRead = null;
    public int Writes;
    public int Binds;
    public TeammateGodmodeSession Session { get; }

    public TeammateOwnedCleanupFixture(bool owned = true) {
        Session = new(ReadStrict, identity => {
            Interlocked.Increment(ref Binds);
            var bound = new BoundPlayerFlagsTransport(identity, ReadStrict(identity.Slot), () => Connected,
                owned ? () => { BeforeBoundRead?.Invoke();return ReadOwned(identity); } : () => ToOwned(ReadStrict(identity.Slot)), (address, expected, desired) => {
                    lock (gate) {
                        if (State.Identity != identity || address != identity.Entity + 0x1C8 || State.Flags != expected
                            || RejectRestore && (desired & 1U) == 0) return false;
                        State = State with { Flags = desired };Writes++;return true;
                    }
                });
            // A long interval removes scheduler timing from admission and cleanup assertions
            var session = Session ?? throw new InvalidOperationException("The fixture owner is not initialized");
            return new FlagsPlayerGodmodeFeature(session.GuardOwnedTransport(identity, bound), TimeSpan.FromSeconds(30));
        }, owned ? ReadOwned : null);
    }

    public OwnedPlayerFlagsState ReadOwned(PlayerIdentity identity) {
        PlayerState state = ReadCurrent();
        if (!Connected || state.Identity != identity) throw new IOException("The original owner changed");
        var copied = ToOwned(state);
        return OwnedTransform?.Invoke(copied) ?? copied;
    }

    private static OwnedPlayerFlagsState ToOwned(PlayerState state) => new(state.Identity, state.CommandTime,
        state.Flags, state.Health is > 1 and <= 10000);

    public PlayerState ReadCurrent() {
        lock (gate) {
            if (ReadFails) throw new IOException("The owned state cannot be read");
            return State;
        }
    }

    public PlayerState ReadStrict(int slot) {
        PlayerState state = ReadCurrent();
        state = StrictTransform?.Invoke(state) ?? state;
        if (state.Identity.Slot != slot || state.Health is <= 1 or > 10000)
            throw new IOException("The selected teammate is not living");
        return state;
    }

    public void Replace(Func<PlayerState, PlayerState> change) {
        lock (gate) State = change(State);
    }

    public async Task Enable() {
        RetailBackendSessionTests.Check(await Session.SelectAsync(1, "Friend1"), "Initial teammate capture failed");
        var result = await Session.ToggleAsync(1, "Friend1");
        RetailBackendSessionTests.Check(result?.Succeeded == true && ReadCurrent().Flags == 0x201,
            "The actual retained feature did not enable its owned bit");
    }
}
