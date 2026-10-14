#pragma once
#include "../../../../native/shared/MenuOptionCallback.h"
#include <array>
#include <optional>

namespace iw::overlay::tests::playerMenus {
struct Expected {
    const char* file;
    const char* title;
    std::size_t rows, selected;
    std::uint64_t navigation, menu, sequence;
    const char* current;
    bool enabled;
    std::uint64_t paintRevision;
    const char* cached;
    std::optional<MenuOptionCallback> callback;
    bool quick = false;
};
inline constexpr MenuOptionCallback Plain{1, MenuOptionCallbackKind::Plain, MenuOptionCallbackPhase::Cooldown};
inline constexpr MenuOptionCallback Pending{2, MenuOptionCallbackKind::Boolean, MenuOptionCallbackPhase::Pending};
inline constexpr MenuOptionCallback Cooldown{2, MenuOptionCallbackKind::Boolean, MenuOptionCallbackPhase::Cooldown};
// Literal source inventories are independent of either reader and the managed row builders
inline constexpr std::array<const char*, 14> Options{{"Verification", "Basic Scripts", "Teleport Menu",
    "Profile Management", "Weaponry", "Bullet Menu", "Fun Scripts", "Model Manipulation", "Aimbot Menu",
    "Model Attachment", "Malicious Options", "Send Message", "Freeze", "Kick"}};
inline constexpr std::array<const char*, 31> Basic{{"God Mode", "Demi-God", "Noclip", "Bind Noclip To Lethal Action",
    "UFO Mode", "Unlimited Ammo", "Unlimited Equipment", "Unlimited Special Weapon", "Modify Score", "Perk Menu",
    "Gobblegum Menu", "Movement Speed", "Third Person", "Invisibility", "Clone", "No Target", "Reduced Spread",
    "Multi-Jump", "Disable HUD", "Visual Effects", "Set Vision", "Zombie Charms", "Custom Crosshairs",
    "No Explosive Damage", "Character Model Index", "Random Character Model Index", "Shoot While Sprinting",
    "Unlimited Sprint", "Respawn", "Revive", "Death"}};
inline constexpr std::array<const char*, 12> AllPlayers{{"Verification", "Teleport", "Profile Management", "Model Manipulation",
    "Malicious Options", "God Mode", "Send Message", "Kick", "Down", "Revive", "Respawn", "Infinite Ammo (IW lobby)"}};
// These captured graph epochs are fixed expectations, never inferred from packet contents
inline const std::array<Expected, 20> Profiles{{
    {"player-navigation-peer-options.json", "Friend", 14, 0, 9, 6, 7, "UNAVAILABLE", false, 0, "", Plain},
    {"player-navigation-peer-basic-before.json", "Basic Scripts", 31, 0, 12, 8, 9, "ON", true, 1, "ON", {}},
    {"player-navigation-peer-boolean0.json", "Basic Scripts", 31, 0, 12, 8, 10, "OFF", true, 1, "ON", Pending},
    {"player-navigation-peer-boolean179.json", "Basic Scripts", 31, 0, 12, 8, 10, "OFF", true, 1, "ON", Pending},
    {"player-navigation-peer-boolean180.json", "Basic Scripts", 31, 0, 12, 9, 10, "OFF", true, 2, "OFF", Cooldown},
    {"player-navigation-peer-boolean429.json", "Basic Scripts", 31, 0, 12, 9, 10, "OFF", true, 2, "OFF", Cooldown},
    {"player-navigation-peer-boolean430.json", "Basic Scripts", 31, 0, 12, 9, 10, "OFF", true, 2, "OFF", {}},
    {"player-navigation-peer-unsupported-ammo.json", "Basic Scripts", 31, 5, 17, 9, 16, "OFF", true, 2, "OFF", {}},
    {"player-navigation-peer-parent.json", "Friend", 14, 1, 19, 11, 17, "UNAVAILABLE", false, 0, "", {}},
    {"player-navigation-quick-forced-self.json", "Quick Menu", 14, 0, 22, 14, 19, "OFF", true, 3, "OFF", {}, true},
    {"player-navigation-main-peer-return.json", "Friend", 14, 1, 25, 17, 21, "UNAVAILABLE", false, 0, "", {}},
    {"player-navigation-stale-peer.json", "Basic Scripts", 31, 5, 27, 19, 22, "UNKNOWN", false, 0, "", {}},
    {"player-navigation-self-options.json", "Local player", 14, 0, 10, 6, 8, "UNAVAILABLE", false, 0, "", Plain},
    {"player-navigation-self-basic-before.json", "Basic Scripts", 31, 0, 13, 8, 10, "OFF", true, 1, "OFF", {}},
    {"player-navigation-self-boolean0.json", "Basic Scripts", 31, 0, 13, 8, 11, "ON", true, 1, "OFF", Pending},
    {"player-navigation-self-boolean180.json", "Basic Scripts", 31, 0, 13, 9, 11, "ON", true, 2, "ON", Cooldown},
    {"player-navigation-self-boolean430.json", "Basic Scripts", 31, 0, 13, 9, 11, "ON", true, 2, "ON", {}},
    {"player-navigation-self-metadata-options.json", "Local player", 14, 1, 15, 11, 12, "UNAVAILABLE", false, 0, "", {}},
    {"player-navigation-self-metadata-basic.json", "Basic Scripts", 31, 0, 17, 13, 13, "UNKNOWN", false, 0, "", {}},
    {"player-navigation-allplayers-source.json", "All Players Menu", 12, 0, 7, 4, 6, "UNAVAILABLE", false, 0, "", {}}
}};
}
