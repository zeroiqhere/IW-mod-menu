#!/usr/bin/env python3
"""Check real CPU preview stability across fixture order and location"""

import argparse
import hashlib
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest


PROFILE_COUNT = 218
PREVIEW_COUNT = 278


def profile_names(directory: Path) -> set[str]:
    # Non-JSON files do not participate in the preview export
    return {entry.name for entry in directory.iterdir() if entry.suffix == ".json"}


def validate_profiles(directory: Path, expected: set[str]) -> None:
    actual = profile_names(directory)
    if actual != expected:
        missing = sorted(expected - actual)
        extra = sorted(actual - expected)
        raise ValueError(f"Profile set differs: missing={missing}, extra={extra}")
    for name in expected:
        path = directory / name
        if path.is_symlink() or not path.is_file():
            raise ValueError(f"Profile is not a regular owned file: {name}")


def preview_hashes(directory: Path, expected: set[str]) -> dict[str, str]:
    paths = sorted(directory.glob("*.ppm"))
    actual = {path.name for path in paths}
    if actual != expected:
        missing = sorted(expected - actual)
        extra = sorted(actual - expected)
        raise ValueError(f"Preview set differs: missing={missing}, extra={extra}")
    for path in paths:
        if path.is_symlink() or not path.is_file():
            raise ValueError(f"Preview is not a regular owned file: {path.name}")
    # Compare complete artifacts rather than relaxing glyph coverage differences
    return {path.name: hashlib.sha256(path.read_bytes()).hexdigest() for path in paths}


def run_export(executable: Path, fixtures: Path, output: Path, log: Path) -> None:
    output.mkdir(mode=0o700)
    # Preserve both streams and fail before accepting any partial image set
    with log.open("xb") as stream:
        result = subprocess.run(
            [str(executable), str(output), str(fixtures)],
            stdout=stream,
            stderr=subprocess.STDOUT,
            timeout=180,
            check=False,
        )
    if result.returncode != 0:
        raise RuntimeError(f"Preview exporter exited {result.returncode}; see {log.name}")


class PreviewOrderTests(unittest.TestCase):
    executable: Path
    fixtures: Path
    workroot: Path
    expected: set[str]
    previews: set[str]

    def test_missing_and_extra_profiles_are_rejected(self) -> None:
        with tempfile.TemporaryDirectory(dir=self.workroot) as temporary:
            directory = Path(temporary)
            (directory / "first.json").write_text("{}", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "missing"):
                validate_profiles(directory, {"first.json", "second.json"})
            with self.assertRaisesRegex(ValueError, "extra"):
                validate_profiles(directory, set())

    def test_exporter_failure_is_rejected_before_image_comparison(self) -> None:
        with tempfile.TemporaryDirectory(dir=self.workroot) as temporary:
            directory = Path(temporary)
            executable = directory / "failed-exporter"
            # This owned subprocess exercises the same checked exit path without rendering
            executable.write_text(f"#!{sys.executable}\nraise SystemExit(7)\n", encoding="utf-8")
            executable.chmod(0o700)
            with self.assertRaisesRegex(RuntimeError, "exited 7"):
                run_export(executable, directory, directory / "images", directory / "failure.log")

    def test_incomplete_and_extra_preview_sets_are_rejected(self) -> None:
        with tempfile.TemporaryDirectory(dir=self.workroot) as temporary:
            directory = Path(temporary)
            expected = {f"image-{index}.ppm" for index in range(PREVIEW_COUNT)}
            for index in range(PREVIEW_COUNT):
                (directory / f"image-{index}.ppm").write_bytes(b"owned test image")
            (directory / "image-0.ppm").unlink()
            with self.assertRaises(ValueError):
                preview_hashes(directory, expected)
            # A renamed helper must fail even when the total count stays correct
            (directory / "unknown.ppm").write_bytes(b"owned test image")
            with self.assertRaises(ValueError):
                preview_hashes(directory, expected)
            (directory / "unknown.ppm").unlink()
            (directory / "image-0.ppm").write_bytes(b"owned test image")
            (directory / "extra.ppm").write_bytes(b"owned test image")
            with self.assertRaises(ValueError):
                preview_hashes(directory, expected)
            (directory / "extra.ppm").unlink()
            (directory / "image-0.ppm").unlink()
            (directory / "image-0.ppm").symlink_to(directory / "image-1.ppm")
            with self.assertRaisesRegex(ValueError, "regular owned file"):
                preview_hashes(directory, expected)

    def test_all_previews_match_across_opposite_creation_orders_and_relocation(self) -> None:
        names = sorted(self.expected)
        runs = []
        for label, order in (("forward", names), ("reverse", list(reversed(names))), ("relocated", names)):
            parent = self.workroot / label
            parent.mkdir(mode=0o700)
            fixtures = parent / "fixtures"
            fixtures.mkdir(mode=0o700)
            for name in order:
                shutil.copyfile(self.fixtures / name, fixtures / name)
            validate_profiles(fixtures, self.expected)
            # Record the actual enumeration order so the integration cannot be a false comparison
            observed = [entry.name for entry in os.scandir(fixtures) if entry.name.endswith(".json")]
            (parent / "enumeration.txt").write_text("\n".join(observed) + "\n", encoding="utf-8")
            for name in names:
                self.assertEqual((fixtures / name).read_bytes(), (self.fixtures / name).read_bytes())
            output = parent / "images"
            run_export(self.executable, fixtures, output, parent / "export.log")
            runs.append((observed, preview_hashes(output, self.previews)))

        self.assertNotEqual(runs[0][0], runs[1][0], "Filesystem did not expose different fixture orders")
        for label, (_, hashes) in zip(("reverse", "relocated"), runs[1:]):
            differences = sorted(name for name in runs[0][1] if runs[0][1][name] != hashes.get(name))
            self.assertEqual(differences, [], f"{label} preview hashes differ")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--executable", type=Path, required=True)
    parser.add_argument("--fixtures", type=Path, required=True)
    parser.add_argument("--workroot", type=Path, required=True)
    arguments = parser.parse_args()

    manifest = Path(__file__).with_name("profiles.txt").read_text(encoding="utf-8").splitlines()
    expected = set(manifest)
    if len(manifest) != PROFILE_COUNT or len(expected) != PROFILE_COUNT:
        parser.error(f"Profile manifest must contain {PROFILE_COUNT} unique names")
    preview_manifest = Path(__file__).with_name("previews.txt").read_text(encoding="utf-8").splitlines()
    previews = set(preview_manifest)
    if len(preview_manifest) != PREVIEW_COUNT or len(previews) != PREVIEW_COUNT:
        parser.error(f"Preview manifest must contain {PREVIEW_COUNT} unique names")
    if any(Path(name).name != name or not name.endswith(".json") for name in expected):
        parser.error("Profile manifest must contain JSON basenames")
    if any(Path(name).name != name or not name.endswith(".ppm") for name in previews):
        parser.error("Preview manifest must contain PPM basenames")
    if not {Path(name).with_suffix(".ppm").name for name in expected} <= previews:
        parser.error("Preview manifest must include every profile image")
    if not arguments.executable.is_file() or not os.access(arguments.executable, os.X_OK):
        parser.error("Executable must be an existing executable file")
    try:
        validate_profiles(arguments.fixtures, expected)
        # A fresh directory prevents accidental overwrites and stale successful artifacts
        arguments.workroot.mkdir(mode=0o700, parents=True, exist_ok=False)
    except (OSError, ValueError) as error:
        parser.error(str(error))

    PreviewOrderTests.executable = arguments.executable.resolve()
    PreviewOrderTests.fixtures = arguments.fixtures.resolve()
    PreviewOrderTests.workroot = arguments.workroot.resolve()
    PreviewOrderTests.expected = expected
    PreviewOrderTests.previews = previews
    suite = unittest.defaultTestLoader.loadTestsFromTestCase(PreviewOrderTests)
    result = unittest.TextTestRunner(verbosity=2).run(suite)
    return 0 if result.wasSuccessful() else 1


if __name__ == "__main__":
    raise SystemExit(main())
