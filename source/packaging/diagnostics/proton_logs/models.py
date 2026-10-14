from dataclasses import dataclass


@dataclass(frozen=True)
class Limits:
    # Retained records and read sizes stay bounded even when the source is large
    max_line_bytes: int = 65536
    max_lines: int = 20_000_000
    max_source_bytes: int = 2_147_483_648
    max_events: int = 64
    max_modules: int = 256
    max_threads: int = 64
    context_lines: int = 32
    correlation_lines: int = 256
    correlation_milliseconds: int = 1000
    context_characters: int = 512

    def __post_init__(self):
        for name, maximum in {
            "max_line_bytes": 1_048_576, "max_lines": 100_000_000,
            "max_source_bytes": 8_589_934_592, "max_events": 1024,
            "max_modules": 2048, "max_threads": 256, "context_lines": 64,
            "correlation_lines": 4096, "correlation_milliseconds": 60_000, "context_characters": 1024,
        }.items():
            value = getattr(self, name)
            if type(value) is not int or not 1 <= value <= maximum:
                raise ValueError(f"Invalid log limit: {name}")


def require_windows_pid(value):
    # A decimal Windows PID is supplied explicitly; no Linux PID or session is inferred
    if type(value) is not int or not 1 <= value <= 0xFFFFFFFF:
        raise ValueError("An exact positive Windows process ID is required")
    return value
