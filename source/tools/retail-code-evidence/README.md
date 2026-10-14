# Read-only private-ammo code evidence

This developer diagnostic prepares the missing retail ammo binding investigation. It does not enable personal ammo, call an engine function, inject a DLL, patch code, change counters, or update an installed menu. It has not been run against the game in this checkpoint.

The six candidate addresses come from the [pinned gsc-tool IW7 method map](https://github.com/xensik/gsc-tool/blob/05d212f5059f7e2b08d95be3e083eb1d6bb83ffc/src/gsc/engine/iw7_meth.cpp). A fresh comparison against [current source at 8f1d6bd](https://github.com/xensik/gsc-tool/blob/8f1d6bd4d51efdc619629b9254fb439427a6d306/src/gsc/engine/iw7_meth.cpp) on 2026-10-13 found the same entries. The comments supply research addresses, not proof of retail method attribution or a callable ABI.

The supported installed-image SHA-256 is `a883ee28ee9f552c63d8ed1e352f60501b2e256d8857ca178c6dd3aae622723b`. The image path and process ID must be supplied explicitly. The helper does not search for a running game. Its process-memory descriptor is opened read-only with close-on-exec, using [Python's documented file-descriptor operations](https://docs.python.org/3.14/library/os.html#os.open).

Each candidate requires an exact PE exception-directory entry and a complete readable executable span of at most 4096 bytes. Missing, ambiguous, oversized, or non-executable bounds reject the capture. Every span is read twice and must match. The helper verifies the image's disk fingerprint, mapped device/inode/path and zero-offset base, loaded PE metadata, process creation token, name and mappings. Changes observed during the capture reject the entire bundle. Repeated matching samples are not an atomic snapshot and cannot rule out changes that occur between the observations.

An exception-directory entry may describe only one segment of a larger logical function. The manifest explicitly marks function completeness and callable bindings as unverified. Captures still need offline disassembly, builtin-dispatch attribution, helper/weapon-pool analysis, argument and default-hand semantics, and event/lifetime proof. No source label grants permission to call a candidate or write its data.

For a future separately scheduled read-only diagnostic, from the project root:

```bash
python tools/retail-code-evidence/capture.py \
  --image "$IW_GAME_IMAGE" \
  --pid "$IW_GAME_PID" \
  --output "$IW_AMMO_EVIDENCE_DIRECTORY"
```

The variables must name the original Steam executable, its specific live process, and a new output directory whose parent exists. The helper does not request privileges or bypass denied process-memory access. Existing output is rejected before process-memory reads. A failed capture creates no output; a failure while saving can leave a partial new directory without the completed `capture.json` marker. The marker is published only after its JSON has been completely written.

Run the offline regressions from the project root:

```bash
python -m unittest discover -s tests/diagnostics/retail-code-evidence -v
```

Tests use generated PE bytes, bounded byte callbacks, and owned sparse files that model process records. They never inspect a live process. The synthetic end-to-end capture substitutes only its fixture image fingerprint; it does not validate the retail executable. Coverage includes all six segment reads, changed/short reads, unwind and mapping failures, wrong process/image, changing context/headers, descriptor release, output refusal, incomplete manifest rejection, argument handling and inert imports. These checks establish the diagnostic's behavior, not private ammo or gameplay readiness.

The PE and read-bound helpers were adapted from the archived entity-binding diagnostic. This directory is the maintained helper for the ammo investigation; historical evidence and its scripts remain unchanged.
