namespace Apparition.Backend.Session;

internal sealed partial class RetailBackendSession {
    // Connected retail input needs an owned receipt even when the latest read is unavailable
    public bool RequiresLocalMenuAdmission => true;
}
