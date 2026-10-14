using System.Globalization;

namespace Apparition.Backend.Runtime;

public sealed record BackendOptions(int ProcessId, string SessionDirectory, bool MenuPositionControlsAvailable = false) {
    public static BackendOptions Parse(string[] arguments) {
        ArgumentNullException.ThrowIfNull(arguments);
        // The optional flag is a request; the runner still requires a complete private binding packet
        if (arguments.Length is not (4 or 5)
            || (arguments.Length == 5 && arguments[4] != "--menu-position-controls-available")
            || arguments[0] != "--game-pid"
            || arguments[2] != "--session-directory"
            || !int.TryParse(arguments[1], NumberStyles.None, CultureInfo.InvariantCulture, out int pid) || pid <= 0
            || !Path.IsPathFullyQualified(arguments[3])) {
            throw new ArgumentException("Expected --game-pid <Windows PID> --session-directory <absolute private directory> [--menu-position-controls-available]");
        }
        return new(pid, Path.GetFullPath(arguments[3]), arguments.Length == 5);
    }
}
