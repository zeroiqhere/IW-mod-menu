using Apparition.Backend.Ipc;
using Apparition.Backend.Runtime;

namespace Apparition.Backend;

// A WinExe entry point never opens a console or a separate application window
internal static class Program {
    public static async Task<int> Main(string[] arguments) {
        // The retained memory library can emit address errors, so diagnostics stay inside IPC
        Console.SetOut(TextWriter.Null);
        Console.SetError(TextWriter.Null);
        SessionFiles? files = null;
        try {
            var options = BackendOptions.Parse(arguments);
            files = new SessionFiles(options.SessionDirectory);
            return await BackendRunner.RunAsync(options, files);
        } catch (Exception exception) {
            try {
                files?.WriteStatus("Backend startup or cleanup failed: " + exception.Message);
            } catch (IOException) {
                // Failure to report an error must not show a window or expose a console
            } catch (UnauthorizedAccessException) {
                // A rejected directory remains untouched after the failed write
            }
            return 2;
        }
    }
}
