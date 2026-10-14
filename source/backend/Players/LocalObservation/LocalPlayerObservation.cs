using System.Text;

namespace syndishanx_iw_tool.Players.LocalObservation;

// Player life is separate from process lifetime and feature-writable health
public enum LocalPlayerLife { Unknown, Alive, Dead }

// A provider must justify these facts before reporting a complete local binding
// A cached observation is a snapshot, not fresh engine state or spawn authorization
public sealed record LocalPlayerObservation {
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    public int ProcessId { get; init; }
    public PlayerIdentity? Identity { get; init; }
    public LocalPlayerLife Life { get; init; }
    public bool? HasMenu { get; init; }
    public bool? ControlsDisabled { get; init; }
    // This is the named native session-host result, not the menu verification level
    public bool? SessionHostRole { get; init; }
    public string Status { get; init; }

    public LocalPlayerObservation(int processId, PlayerIdentity? identity, LocalPlayerLife life,
        bool? hasMenu, bool? controlsDisabled, string status, bool? sessionHostRole = null) {
        ProcessId = processId;
        Identity = identity;
        Life = life;
        HasMenu = hasMenu;
        ControlsDisabled = controlsDisabled;
        SessionHostRole = sessionHostRole;
        Status = status;
        if (!IsSupported()) throw new ArgumentException("The local observation has an unsupported shape");
    }

    public static LocalPlayerObservation BindingUnavailable { get; } =
        Unknown("A verified local-player binding is unavailable");

    public static LocalPlayerObservation Unknown(string status) =>
        new(0, null, LocalPlayerLife.Unknown, null, null, status);

    // Shape checks cannot authenticate an account, entity binding or engine life predicate
    public bool IsSupported() {
        if (ProcessId < 0 || !Enum.IsDefined(Life) || !ValidText(Status, 500, 2000)) return false;
        if (Identity is null) {
            return Life == LocalPlayerLife.Unknown && HasMenu != true && ControlsDisabled != false
                && SessionHostRole is null;
        }
        return ProcessId > 0 && Identity.Slot is >= 0 and <= 3 && Identity.ServerCount is >= 1 and <= 4
            && Identity.Slot < Identity.ServerCount
            && ValidPointer(Identity.Entity) && ValidPointer(Identity.Client) && ValidPointer(Identity.ServerClients)
            && ValidText(Identity.Name, 31, 31) && Identity.Guid is { Length: 16 }
            && Identity.Guid.All(character => character is >= '0' and <= '9' or >= 'A' and <= 'F' or >= 'a' and <= 'f');
    }

    private static bool ValidPointer(long value) => value is >= 0x10000 and <= 0x7FFFFFFF0000 && value % 8 == 0;

    private static bool ValidText(string? text, int characters, int bytes) {
        if (string.IsNullOrWhiteSpace(text) || text.Length > characters || text.Any(char.IsControl)) return false;
        try { return StrictUtf8.GetByteCount(text) <= bytes; }
        catch (EncoderFallbackException) { return false; }
    }
}
