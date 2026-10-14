namespace Apparition.Backend.Runtime;

// Separate tools cannot share ownership of the same game memory changes
public static class OtherToolPolicy {
    public static IReadOnlyList<string> ProcessNames { get; } = Array.AsReadOnly(new[] {
        "syndishanx_iw_tool", "iw_apparition_overlay", "iw_apparition_backend"
    });

    public static bool IsConflict(string processName, int processId, int currentProcessId) {
        if (processId == currentProcessId) return false;
        return ProcessNames.Contains(processName, StringComparer.OrdinalIgnoreCase);
    }
}
