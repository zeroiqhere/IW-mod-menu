using syndishanx_iw_tool.Players;

namespace syndishanx_iw_tool.Features.Godmode;

// Slot numbers alone cannot identify a player or a match
internal static class CapturedGodmodeRoster {
    // Copy before waiting so later caller edits cannot retarget a queued batch
    internal static PlayerIdentity[] Copy(IReadOnlyList<PlayerIdentity>? identities) =>
        identities is null || identities.Count is < 1 or > 4 ? Array.Empty<PlayerIdentity>() : identities.ToArray();

    // Distinct slots must also resolve to distinct player storage and GUIDs
    internal static bool Valid(PlayerIdentity[] captured) => captured.Length is >= 1 and <= 4
        && captured.All(identity => identity is not null && identity.Slot is >= 0 and <= 3
            && identity.Entity > 0 && identity.Client > 0 && identity.ServerClients > 0 && identity.ServerCount == 4
            && !string.IsNullOrEmpty(identity.Name) && !string.IsNullOrEmpty(identity.Guid))
        && captured.Select(identity => identity.Slot).Distinct().Count() == captured.Length
        && captured.Select(identity => identity.Entity).Distinct().Count() == captured.Length
        && captured.Select(identity => identity.Client).Distinct().Count() == captured.Length
        && captured.Select(identity => identity.Guid).Distinct(StringComparer.OrdinalIgnoreCase).Count() == captured.Length
        && captured.Select(identity => identity.ServerClients).Distinct().Count() == 1;
}
