from dataclasses import dataclass

from bounds import readable_span
from profile import AMMO_CANDIDATES


@dataclass(frozen=True)
class CapturedSegment:
    name: str
    rva: int
    code: bytes


def capture_segments(image, read, maps):
    spans = []
    # Admit every candidate before reading any code, with no guessed fallback size
    for name, rva in AMMO_CANDIDATES:
        length = image.function_size(rva)
        if length is None:
            raise ValueError(f'{name}: exact unwind entry is missing')
        readable_span(maps, image.base + rva, length)
        spans.append((name, rva, length))
    captured = []
    for name, rva, length in spans:
        first = read(image.base + rva, length)
        # ReadProcessMemory style callbacks must return complete immutable bytes
        if not isinstance(first, bytes) or len(first) != length:
            raise ValueError(f'{name}: incomplete first read')
        second = read(image.base + rva, length)
        if not isinstance(second, bytes) or second != first:
            raise ValueError(f'{name}: repeated code read changed or was incomplete')
        captured.append(CapturedSegment(name, rva, first))
    return captured
