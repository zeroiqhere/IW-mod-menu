namespace Apparition.Backend.Ipc;

// Atomic replacement lets the injected reader see only complete JSON frames
public sealed class SessionFiles {
    private readonly string directory;
    private ulong generation;
    public SessionFiles(string directory) {
        if (!Path.IsPathFullyQualified(directory)) throw new ArgumentException("The session directory must be absolute", nameof(directory));
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException("The private session directory does not exist");
        this.directory = Path.GetFullPath(directory);
    }

    public CommandEnvelope? ReadCommand() {
        string path = Path.Combine(directory, "command.json");
        try {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            if (file.Length is <= 0 or > CommandEnvelope.MaximumBytes) return null;
            byte[] bytes = new byte[(int)file.Length];
            file.ReadExactly(bytes);
            return CommandEnvelope.Parse(bytes);
        } catch (FileNotFoundException) {
            return null;
        } catch (IOException) {
            // A concurrently replaced command is retried on the next frame
            return null;
        } catch (UnauthorizedAccessException exception) {
            throw new SessionFileException("command.json", "read command", exception);
        }
    }

    public bool ReadChoiceControlsAvailable() => ReadSavedBindingCapabilities().ChoiceControlsAvailable;

    internal SavedBindingCapabilities ReadSavedBindingCapabilities() => ReadSavedControlCapabilities().Menu;

    internal SavedControlCapabilities ReadSavedControlCapabilities() {
        string path = Path.Combine(directory, "bindings.json");
        try {
            VerifyBindingPath(path);
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (file.Length is <= 0 or > SavedBindingCapabilities.MaximumBytes) return SavedControlCapabilities.Unavailable;
            byte[] bytes = new byte[(int)file.Length];
            file.ReadExactly(bytes);
            // Confirm the path remains ordinary and the opened packet retains its bounded length
            VerifyBindingPath(path);
            return file.Length == bytes.Length ? SavedControlCapabilities.Parse(bytes) : SavedControlCapabilities.Unavailable;
        } catch (FileNotFoundException) { return SavedControlCapabilities.Unavailable; }
        catch (IOException) { return SavedControlCapabilities.Unavailable; }
        catch (UnauthorizedAccessException exception) {
            throw new SessionFileException("bindings.json", "read saved controls", exception);
        }
    }

    private void VerifyBindingPath(string path) {
        var parent = File.GetAttributes(directory);
        var attributes = File.GetAttributes(path);
        if ((parent & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != FileAttributes.Directory
            || (attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0) {
            throw new UnauthorizedAccessException("Bindings must remain an ordinary file in the private session directory");
        }
    }

    public void WriteState(StateDocument state) {
        // Every committed frame carries a heartbeat independent of command acknowledgments
        ulong next = checked(generation + 1);
        WriteAtomic("state.json", (state with { Generation = next }).Serialize());
        generation = next;
    }

    public bool HasStopRequest(int expectedProcessId) {
        try {
            using var file = new FileStream(Path.Combine(directory, "stop-request.json"),
                FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (file.Length is <= 0 or > 1024) return false;
            byte[] bytes = new byte[(int)file.Length];
            file.ReadExactly(bytes);
            using var document = System.Text.Json.JsonDocument.Parse(bytes);
            var root = document.RootElement;
            if (root.ValueKind != System.Text.Json.JsonValueKind.Object) return false;
            var fields = root.EnumerateObject().ToArray();
            // A malformed or foreign stop file cannot terminate this process's session
            return fields.Length == 2 && fields.Count(field => field.Name == "version") == 1
                && fields.Count(field => field.Name == "processId") == 1
                && root.GetProperty("version").ValueKind == System.Text.Json.JsonValueKind.Number
                && root.GetProperty("version").TryGetInt32(out int version) && version == 1
                && root.GetProperty("processId").ValueKind == System.Text.Json.JsonValueKind.Number
                && root.GetProperty("processId").TryGetInt32(out int pid) && pid == expectedProcessId;
        } catch (IOException) {
            return false;
        } catch (System.Text.Json.JsonException) {
            return false;
        } catch (UnauthorizedAccessException exception) {
            throw new SessionFileException("stop-request.json", "read stop request", exception);
        }
    }

    public void WriteStatus(string message) {
        // Diagnostic errors are bounded and never include credentials or memory contents
        string bounded = message.Length <= 1500 ? message : message[..1500];
        WriteAtomic("status.json", System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new { version = 1, message = bounded }));
    }

    private void WriteAtomic(string name, byte[] bytes) {
        AtomicFilePublisher.Write(directory, name, bytes);
    }
}
