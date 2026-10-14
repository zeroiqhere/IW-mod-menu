namespace Apparition.Backend.Ipc;

// The sequence advances before dispatch so denied or failed actions never replay
public sealed class CommandGate {
    private readonly int processId;
    public ulong LastSequence { get; private set; }

    public CommandGate(int processId) {
        if (processId <= 0) throw new ArgumentOutOfRangeException(nameof(processId));
        this.processId = processId;
    }

    public bool Consume(CommandEnvelope command, bool foregroundAllowed) {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Sequence <= LastSequence) return false;
        LastSequence = command.Sequence;
        return command.Valid && command.Version == 1 && command.ProcessId == processId && foregroundAllowed;
    }
}
