using Apparition.Backend.QuickMenu;
using syndishanx_iw_tool.Players;
using syndishanx_iw_tool.Players.LocalObservation;

namespace syndishanx_iw_tool.Overlay;

// One copied context keeps the supplied life, actor and authority facts together
// This record does not read memory or grant a gameplay operation
public sealed record QuickMenuViewContext(LocalPlayerObservation Observation,
    QuickMenuEligibility Eligibility, PlayerIdentity? SelectedLocalIdentity, int ExpectedProcessId, bool OpeningAvailable = false);
