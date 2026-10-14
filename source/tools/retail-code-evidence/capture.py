import argparse
import hashlib
import os
from pathlib import Path

from bounds import mapped_image, process_token, readable_span
from pe_image import PeImage
from profile import IMAGE_SHA256
from segments import capture_segments
from evidence import write_capture


def capture_from_process(image_path, process, output):
    # Rejected destinations do not spend any game reads
    if output.exists() or output.is_symlink():
        raise FileExistsError('The output directory already exists')
    if not output.parent.is_dir():
        raise FileNotFoundError('The output parent directory must exist')
    image_path = image_path.resolve(strict=True)
    disk = image_path.read_bytes()
    if hashlib.sha256(disk).hexdigest() != IMAGE_SHA256:
        raise ValueError('The installed image is outside the reviewed fingerprint')
    image = PeImage(disk)
    stat = image_path.stat()
    token = process_token((process / 'stat').read_text())
    if process.name != str(token[0]) or (process / 'comm').read_text().strip() != 'iw7_ship.exe':
        raise ValueError('The explicit process is not the original game')
    maps = (process / 'maps').read_text()
    if not mapped_image(maps, image_path, stat, image.base):
        raise ValueError('The loaded image mapping does not match the installed image')
    readable_span(maps, image.base, 4096)
    # This is the only process-memory handle and it cannot perform writes
    handle = os.open(process / 'mem', os.O_RDONLY | os.O_CLOEXEC)
    try:
        header = os.pread(handle, 4096, image.base)
        if len(header) != 4096 or PeImage(header).identity != image.identity:
            raise ValueError('Loaded PE metadata does not match the reviewed image')
        segments = capture_segments(image, lambda address, count: os.pread(handle, count, address), maps)
        repeated_header = os.pread(handle, 4096, image.base)
        if repeated_header != header:
            raise ValueError('Loaded PE headers changed during capture')
    finally:
        os.close(handle)
    current = image_path.stat()
    if ((current.st_dev, current.st_ino) != (stat.st_dev, stat.st_ino)
            or hashlib.sha256(image_path.read_bytes()).hexdigest() != IMAGE_SHA256):
        raise ValueError('The installed image changed during capture')
    # Sample context last so a change during the final header or disk check cannot pass
    if (process_token((process / 'stat').read_text()) != token
            or (process / 'comm').read_text().strip() != 'iw7_ship.exe'
            or (process / 'maps').read_text() != maps):
        raise ValueError('The process identity or mappings changed during capture')
    write_capture(output, segments, {
        'pid': token[0], 'processStartTicks': token[1], 'installedSha256': IMAGE_SHA256,
        'loadedPeMetadataMatched': True, 'repeatedReadsMatched': True,
        'stableProcessAndMappingsObserved': True,
    })


def positive_pid(raw):
    value = int(raw)
    if value <= 0:
        raise argparse.ArgumentTypeError('PID must be positive')
    return value


def main():
    parser = argparse.ArgumentParser(description='Capture reviewed IW ammo code candidates without invoking them')
    parser.add_argument('--image', required=True, type=Path)
    parser.add_argument('--pid', required=True, type=positive_pid)
    parser.add_argument('--output', required=True, type=Path)
    args = parser.parse_args()
    try:
        capture_from_process(args.image, Path('/proc') / str(args.pid), args.output)
    except (OSError, ValueError) as error:
        parser.exit(1, f'Capture rejected: {error}\n')
    print(f'Read-only candidate segments saved to {args.output}')


# Importing the helper cannot discover or attach to a process
if __name__ == '__main__':
    main()
