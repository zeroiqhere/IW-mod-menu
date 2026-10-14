using Apparition.Backend.Ipc;
using System.Text;

// This fixture writes only its explicitly supplied private test directory
if (args.Length is < 1 or > 3 || !Path.IsPathFullyQualified(args[0]) || !Directory.Exists(args[0])) return 2;
string directory = args[0];
int frameCount = args.Length > 1 && int.TryParse(args[1], out int frames) && frames is > 0 and <= 20000 ? frames : 3000;
bool waitForReader = args.Length < 3 || args[2] != "no-reader";
int generation = 1;
using var log = new StreamWriter(Path.Combine(directory, "publisher.log")) { AutoFlush = true };
try {
    if (args.Length == 3 && args[2] == "held-reader-tests") {
        await HeldReaderChecks.RunAsync(directory, log);
        return 0;
    }
    AtomicFilePublisher.Write(directory, "state.json", Encoding.UTF8.GetBytes("{\"version\":1,\"generation\":1}"));
    File.WriteAllText(Path.Combine(directory, "publisher-ready.txt"), "ready");
    // The reader begins before publication so the original overlap is exercised
    for (int wait = 0; waitForReader && wait < 600 && !File.Exists(Path.Combine(directory, "reader-ready.txt")); wait++) {
        await Task.Delay(25);
    }
    if (waitForReader && !File.Exists(Path.Combine(directory, "reader-ready.txt"))) throw new IOException("The fixture reader did not become ready");
    for (generation = 2; generation <= frameCount + 1; generation++) {
        AtomicFilePublisher.Write(directory, "state.json", Encoding.UTF8.GetBytes($"{{\"version\":1,\"generation\":{generation}}}"));
        await Task.Delay(5);
    }
    log.WriteLine($"PASS {frameCount} atomic publications");
    return 0;
} catch (Exception exception) {
    log.WriteLine($"Generation {generation}: " + exception.GetType().Name + ": " + exception.Message);
    if (exception.InnerException is not null) log.WriteLine("Inner HRESULT 0x" + exception.InnerException.HResult.ToString("X8"));
    return 1;
} finally {
    File.WriteAllText(Path.Combine(directory, "publisher-done.txt"), "done");
}
