using System.Text.Json.Serialization;
using syndishanx_iw_tool.Features.Godmode;
using syndishanx_iw_tool.Overlay.Players;

namespace Apparition.Backend.Session;

// Menu publication contains cached facts, never a fresh authorization for game writes
public sealed record CohortGodmodeView(GodmodeValue Value, bool CanToggle, string Status,
    [property: JsonIgnore] CohortGodmodeTarget? Target);
