"""Convert production drawlist PPM previews to portable PNG files"""

import argparse
import binascii
from pathlib import Path
import struct
import zlib


def chunk(kind: bytes, data: bytes) -> bytes:
    # Each PNG chunk carries its length and checksum
    return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", binascii.crc32(kind + data))


def convert(source: Path) -> Path:
    # The test rasterizer writes a fixed P6 header without metadata or comments
    with source.open("rb") as stream:
        if stream.readline() != b"P6\n":
            raise ValueError("Expected a production preview P6 image")
        width, height = map(int, stream.readline().split())
        if stream.readline() != b"255\n" or not (0 < width <= 4096 and 0 < height <= 4096):
            raise ValueError("Unsupported preview dimensions or color range")
        pixels = stream.read()
    if len(pixels) != width * height * 3:
        raise ValueError("Preview pixel count does not match its dimensions")
    # PNG filter zero preserves the exact RGB samples from the CPU drawlist
    rows = b"".join(b"\0" + pixels[y * width * 3:(y + 1) * width * 3] for y in range(height))
    header = struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0)
    target = source.with_suffix(".png")
    target.write_bytes(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", header) + chunk(b"IDAT", zlib.compress(rows)) + chunk(b"IEND", b""))
    return target


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("previews", nargs="+", type=Path)
    for preview in parser.parse_args().previews:
        print(convert(preview))
