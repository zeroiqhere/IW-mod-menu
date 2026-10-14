namespace Apparition.Backend.Ipc;

// Fixed protocol names and operations identify failures without exposing private paths
public sealed class SessionFileException : IOException {
    public string FileName { get; }
    public string Operation { get; }
    public int NativeHResult { get; }

    public SessionFileException(string fileName, string operation, Exception cause)
        : base($"{fileName}: {operation} failed ({cause.GetType().Name}, HRESULT 0x{cause.HResult:X8})", cause) {
        FileName = fileName;
        Operation = operation;
        NativeHResult = cause.HResult;
        HResult = cause.HResult;
    }
}
