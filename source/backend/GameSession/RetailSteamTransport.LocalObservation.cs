using System.IO;
using System.Security.Cryptography;
using syndishanx_iw_tool.Players.LocalObservation;

namespace syndishanx_iw_tool.GameSession;

internal sealed partial class RetailSteamTransport {
    private const string InspectedRetailFingerprint = "A883EE28EE9F552C63D8ED1E352F60501B2E256D8857CA178C6DD3AAE622723B";
    private ReadOnlyProcessMemory? localObservationMemory;
    private long localObservationModuleBase;
    private byte[]? localObservationHeader;
    private bool localObservationBuildRejected;
    private readonly LocalExecutableHeaderCache localObservationExecutable = new();

    public LocalActorSnapshot ReadLocalActorSnapshot(CancellationToken cancellationToken) =>
        ReadVerifiedLocalSnapshot(LocalActorSnapshotReader.Read, cancellationToken);

    // Observation and feature admission borrow the same pinned read-only connection
    private T ReadVerifiedLocalSnapshot<T>(Func<long, Func<long, int, byte[]?>, T> readSnapshot,
        CancellationToken cancellationToken) {
        lock (accessLock) {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Connected) throw new IOException("The original Steam connection is unavailable");
            if (localObservationBuildRejected) throw new IOException("Local observation is unavailable for this unverified game build");
            var process = memory.mProc.Process;
            long moduleBase = process.MainModule?.BaseAddress.ToInt64() ?? 0;
            if (localObservationMemory is null) {
                byte[] installed = localObservationExecutable.GetOrRead(moduleBase, () => {
                    string executable = process.MainModule?.FileName
                        ?? throw new IOException("The local observation executable could not be identified");
                    using var file = File.OpenRead(executable);
                    // A native read retry reuses the already fingerprinted file for this connection
                    if (Convert.ToHexString(SHA256.HashData(file)) != InspectedRetailFingerprint) {
                        localObservationBuildRejected = true;
                        throw new IOException("Local observation is unavailable for this unverified game build");
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                    file.Position = 0;
                    var header = new byte[4096];
                    file.ReadExactly(header);
                    return header;
                });
                cancellationToken.ThrowIfCancellationRequested();
                var candidate = new ReadOnlyProcessMemory(process.Id);
                try {
                    byte[] loaded = candidate.Read(moduleBase, 4096);
                    try { LocalExecutableHeader.Verify(loaded, installed); }
                    catch (IOException) {
                        // An unsupported mapped image is permanent for this original connection
                        localObservationBuildRejected = true;
                        throw;
                    }
                    if (!Connected || memory.mProc.Process != process) throw new IOException("The original connection changed during validation");
                    cancellationToken.ThrowIfCancellationRequested();
                    // Ownership transfers only after complete identity and header validation
                    localObservationHeader = loaded;
                    localObservationModuleBase = moduleBase;
                    localObservationMemory = candidate;
                } catch {
                    candidate.Dispose();
                    throw;
                }
            }
            if (moduleBase != localObservationModuleBase) throw new IOException("The original game module changed");
            // Each read remains bounded, exact and attached to the already owned process
            byte[] Read(long address, int count) {
                cancellationToken.ThrowIfCancellationRequested();
                // The owned read handle cannot retarget a recycled PID; lifetime is rechecked around the snapshot
                return localObservationMemory.Read(address, count);
            }
            LocalExecutableHeader.Verify(Read(moduleBase, 4096), localObservationHeader!);
            var snapshot = readSnapshot(moduleBase, Read);
            cancellationToken.ThrowIfCancellationRequested();
            if (!Connected || memory.mProc.Process != process) throw new IOException("The original connection changed during observation");
            return snapshot;
        }
    }
}
