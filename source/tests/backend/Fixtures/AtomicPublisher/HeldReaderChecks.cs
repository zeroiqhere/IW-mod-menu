using Apparition.Backend.Ipc;
using System.Diagnostics;
using System.Text;

// Windows sharing tests use real handles inside an isolated directory
internal static class HeldReaderChecks {
    public static async Task RunAsync(string directory, TextWriter log) {
        string target = Path.Combine(directory, "state.json");
        byte[] before = Encoding.UTF8.GetBytes("{\"generation\":1}");
        byte[] after = Encoding.UTF8.GetBytes("{\"generation\":2}");
        AtomicFilePublisher.Write(directory, "state.json", before);

        var releasedReader = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read);
        // The real blocking reader releases while the publisher is retrying
        var release = Task.Run(async () => {
            await Task.Delay(20);
            releasedReader.Dispose();
        });
        AtomicFilePublisher.Write(directory, "state.json", after);
        await release;
        if (!File.ReadAllBytes(target).SequenceEqual(after)) throw new IOException("The released reader did not permit the complete new frame");
        log.WriteLine("PASS A briefly held reader releases and the complete new frame commits");

        using (var permanentReader = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read)) {
            var clock = Stopwatch.StartNew();
            try {
                AtomicFilePublisher.Write(directory, "state.json", before);
                throw new InvalidOperationException("A permanently held reader must not be replaced");
            } catch (SessionFileException exception) {
                if (exception.Operation != "replace destination" || clock.ElapsedMilliseconds > 100) {
                    throw new InvalidOperationException("A persistent reader must produce a bounded replacement failure");
                }
            }
            log.WriteLine($"INFO Permanently held reader failed after {clock.ElapsedMilliseconds} ms");
            if (!File.ReadAllBytes(target).SequenceEqual(after)
                || !File.ReadAllBytes(target + ".tmp").SequenceEqual(before)) {
                throw new IOException("A timed out replacement must preserve its old frame and complete temporary frame");
            }
        }
        log.WriteLine("PASS A permanently held reader fails within the retry bound and preserves both frames");
        AtomicFilePublisher.Write(directory, "state.json", before);
        log.WriteLine("PASS Publication succeeds after a previously timed out reader closes");

        File.SetAttributes(target, File.GetAttributes(target) | FileAttributes.ReadOnly);
        try {
            try {
                AtomicFilePublisher.Write(directory, "state.json", after);
                throw new InvalidOperationException("A read-only destination must not be replaced");
            } catch (SessionFileException exception) {
                if (exception.Operation != "replace destination") throw;
            }
            if (!File.ReadAllBytes(target).SequenceEqual(before)) throw new IOException("A read-only destination must retain its frame");
        } finally {
            File.SetAttributes(target, File.GetAttributes(target) & ~FileAttributes.ReadOnly);
        }
        log.WriteLine("PASS A read-only permission failure remains visible and the destination is unchanged");
    }
}
