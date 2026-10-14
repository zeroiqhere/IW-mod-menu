namespace Apparition.Backend.Ipc;

// The completed temporary frame replaces the destination in one filesystem operation
internal static class AtomicFilePublisher {
    public static void Write(string directory, string name, byte[] bytes) {
        string target = Path.Combine(directory, name);
        string temporary = target + ".tmp";
        string operation = "create temporary";
        try {
            using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None)) {
                operation = "write temporary";
                file.Write(bytes);
                operation = "flush temporary";
                file.Flush(true);
                operation = "close temporary";
            }
            operation = "replace destination";
            ReplaceDestination(temporary, target);
        } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) {
            throw new SessionFileException(name, operation, exception);
        }
    }

    private static void ReplaceDestination(string temporary, string target) {
        const int MaximumAttempts = 6;
        const int RetryDeadlineMilliseconds = 80;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        Exception? lastFailure = null;
        for (int attempt = 0; ; attempt++) {
            // Permission and path-type checks precede every attempted destination change
            EnsureOrdinaryWritableDestination(target);
            if (lastFailure is not null && clock.ElapsedMilliseconds >= RetryDeadlineMilliseconds) {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(lastFailure).Throw();
            }
            try {
                File.Move(temporary, target, true);
                return;
            } catch (Exception exception) when (attempt < MaximumAttempts - 1
                && clock.ElapsedMilliseconds < RetryDeadlineMilliseconds
                && IsReaderOverlap(exception) && IsOrdinaryWritableDestination(target)) {
                // Wine denies replacement while any native reader owns the destination inode
                // The same complete temporary frame survives at most 62 ms of bounded retries
                lastFailure = exception;
                int delay = 2 << attempt;
                if (clock.ElapsedMilliseconds + delay >= RetryDeadlineMilliseconds) throw;
                Thread.Sleep(delay);
                if (clock.ElapsedMilliseconds >= RetryDeadlineMilliseconds) throw;
            }
        }
    }

    private static void EnsureOrdinaryWritableDestination(string target) {
        FileAttributes attributes;
        try {
            attributes = File.GetAttributes(target);
        } catch (FileNotFoundException) {
            return;
        }
        if ((attributes & (FileAttributes.ReadOnly | FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0) {
            throw new UnauthorizedAccessException("The protocol destination must be an ordinary writable file");
        }
    }

    private static bool IsReaderOverlap(Exception exception) {
        // Access denied was reproduced only at replace time with Wine's native reader
        if (!OperatingSystem.IsWindows() || exception is not (IOException or UnauthorizedAccessException)) return false;
        return exception.HResult is unchecked((int)0x80070005)
            or unchecked((int)0x80070020) or unchecked((int)0x80070021);
    }

    private static bool IsOrdinaryWritableDestination(string target) {
        try {
            var attributes = File.GetAttributes(target);
            return (attributes & (FileAttributes.ReadOnly | FileAttributes.Directory | FileAttributes.ReparsePoint)) == 0;
        } catch (FileNotFoundException) {
            return true;
        } catch (IOException) {
            return false;
        } catch (UnauthorizedAccessException) {
            // An uninspectable destination is a permission failure rather than a retry candidate
            return false;
        }
    }
}
