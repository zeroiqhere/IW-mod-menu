namespace syndishanx_iw_tool.SessionLifecycle;

// Keep shutdown completion separate from restoration confirmation
public sealed record SessionShutdownResult(bool Stopped, bool Restored, string? ErrorMessage);
