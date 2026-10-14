# Atomic publication fixture

This standalone Windows .NET 8 fixture links the production atomic publisher. It writes only a supplied test directory and never opens a game process or loads a payload.

Build with `dotnet publish AtomicPublisher.csproj -c Release --no-self-contained -o <test-output>`.

Run `AtomicPublisher.exe <absolute-test-directory> [frame-count] [mode]`. The default mode creates the initial frame, writes `publisher-ready.txt`, and waits up to 15 seconds for `reader-ready.txt`. A reader must run against `state.json` before that marker is created. Publication then writes 3,000 complete frames by default. `publisher.log` reports a fixed operation and HRESULT on failure; `publisher-done.txt` marks completion.

The `no-reader` mode bypasses the reader marker for a control run. The `held-reader-tests` mode exercises real Windows file handles: a brief reader release permits a complete publication, a permanent reader produces a failure within 100 ms while preserving both frames, publication recovers after release, and a read-only destination remains unchanged.

The original regression was reproduced with the native `SessionFiles.ReadState` reader alone. Wine rejects replacement of an opened destination inode even when the reader shares delete access. The production fix retries only Windows replacement errors 5, 32, and 33, with six attempts and an 80 ms deadline. It does not retry temporary-file creation, writing, flushing, destination validation, or other errors.
