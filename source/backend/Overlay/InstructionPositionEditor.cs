namespace syndishanx_iw_tool.Overlay;

// Only the phase leaves the owner; timestamps remain inside the controller
public sealed record InstructionPositionEditor(string Phase) {
    public bool IsSupported() => Phase is "entering" or "editing" or "exiting";
}
