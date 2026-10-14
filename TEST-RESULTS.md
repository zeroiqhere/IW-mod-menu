# Controlled game test: 1757-530c4372

Test date: October 14, 2026. Environment: Arch Linux, Proton, retail Steam Infinite Warfare, hosted solo Zombies in Spaceland.

All 21 runtime artifacts matched the saved package's sealed checksums before the test. The menu appeared with Aim + Melee, and the user confirmed Connect succeeded. The controller independently reported CONNECTED.

The user tested ordinary zombie damage with personal Godmode ON and OFF: ON prevented damage, and OFF restored damage. Infinite Ammo ON preserved ammo while firing; OFF restored normal consumption. These are gameplay observations rather than inferences from the menu labels.

After both features were OFF, the user selected the menu's Exit action and left the Zombies match normally. The game returned to the lobby without crashing. The controller exited with code zero.

The renderer reported 4,871 drawn frames and zero renderer faults. That count describes the renderer's own reported faults; it does not establish general crash immunity.

The trial did not verify teammates, all-player Godmode, all maps, active-feature match exit, arbitrary deaths or repeated transitions. Money changes, perks, door opening and Easter Egg actions remain disabled. This repository does not claim completed main or Super Easter Egg rewards.

Original package metadata and checksums remain unchanged. Private process identifiers, session credentials and local logs are omitted from this public-facing test summary.
