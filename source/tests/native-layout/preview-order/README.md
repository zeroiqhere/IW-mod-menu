# Preview order regression

This CPU-only integration suite runs the real `ApparitionPanelTests` exporter three times. It copies the same 214 producer profiles into fresh directories in lexical order, reverse order, and a relocated lexical copy. It verifies the copied bytes, confirms that the filesystem exposes different enumeration orders, and compares the complete filename sets and SHA-256 hashes of all 274 PPMs.

The exporter uses one persistent ImGui atlas. Sorting profile filenames gives that atlas a consistent glyph warmup order. This makes exported images reproducible without changing production rendering, font policy, or the existing per-profile glyph coverage checks.

Run the four registered checks with a built CPU executable and a fresh private work directory:

```sh
python3 tests/native-layout/preview-order/test_preview_order.py \
  --executable "$CPU_BUILD/ApparitionPanelTests" \
  --fixtures "$PRODUCER_FIXTURES" \
  --workroot "$HOME/.cache/iw-apparition-preview-order-run"
```

The other three checks reject missing or extra input profiles, a failed owned exporter subprocess, and incomplete, swapped, extra, or symlinked output files. Export logs and each directory's observed enumeration order remain in the work directory. No graphics backend, game, Wine prefix, or installed package is used.

`profiles.txt` lists the exact producer profile inventory in sorted order. `previews.txt` lists all expected output filenames, including the 60 helper images, including 30 spacer clock images generated from six actual controller pairs. When the production controller adds profiles, regenerate the profile manifest from the authoritative offline export and the preview manifest from the reviewed exporter outputs. Review every added or removed name, then update `PROFILE_COUNT` and `PREVIEW_COUNT`. Do not reduce the required set to make a missing-profile failure pass.

Individual image comparisons and exporter assertions are evidence within the integration case. They are not additional registered tests.
