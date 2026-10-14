import struct
import sys
from pathlib import Path

# Test discovery imports only the standalone diagnostic source
SOURCE_DIRECTORY = Path(__file__).resolve().parents[3] / 'tools/retail-code-evidence'
sys.path.insert(0, str(SOURCE_DIRECTORY))
from profile import AMMO_CANDIDATES


def image_bytes():
    data = bytearray(4096)
    data[:2] = b'MZ'
    struct.pack_into('<I', data, 60, 128)
    data[128:132] = b'PE\0\0'
    struct.pack_into('<HHI', data, 132, 0x8664, 2, 123)
    struct.pack_into('<H', data, 148, 240)
    struct.pack_into('<H', data, 152, 0x20B)
    struct.pack_into('<Q', data, 176, 0x140000000)
    struct.pack_into('<I', data, 208, 0xE00000)
    struct.pack_into('<II', data, 288, 0xD00000, len(AMMO_CANDIDATES) * 12)
    struct.pack_into('<IIII', data, 400, 0x10000, 0xB00000, 512, 512)
    struct.pack_into('<I', data, 428, 0x20000000)
    struct.pack_into('<IIII', data, 440, 0x1000, 0xD00000, 512, 1024)
    for index, (_, rva) in enumerate(AMMO_CANDIDATES):
        struct.pack_into('<III', data, 1024 + index * 12, rva, rva + 32 + index, 0)
    return data


def process_stat(pid=42, start=12345, state='R'):
    return f'{pid} (iw7_ship.exe) {state} ' + ' '.join(['0'] * 18 + [str(start)])


CODE_MAPS = '140b00000-140b10000 r-xp 00000000 00:00 0\n'
