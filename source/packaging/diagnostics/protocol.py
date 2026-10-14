"""Parse bounded diagnostic commands without accepting duplicate meaning."""

from dataclasses import dataclass
import json

MAXIMUM_SEQUENCE = (1 << 64) - 1
INPUTS = frozenset({"toggle", "up", "down", "select", "back"})


class ObjectFields(list):
    """Preserve object fields so duplicate sequences cannot be hidden."""


def reject_nonfinite(_value: str):
    raise ValueError("Nonfinite JSON numbers are not supported")


def fields_from_bytes(data: bytes, maximum: int) -> ObjectFields | None:
    if not data or len(data) > maximum:
        return None
    try:
        value = json.loads(data.decode("utf-8", "strict"), object_pairs_hook=ObjectFields,
                           parse_constant=reject_nonfinite)
    except (ValueError, RecursionError, UnicodeError):
        return None
    return value if isinstance(value, ObjectFields) else None


@dataclass(frozen=True)
class Command:
    sequence: int
    valid: bool
    input: str | None


def parse_command(data: bytes, process_id: int) -> Command | None:
    fields = fields_from_bytes(data, 4096)
    if fields is None:
        return None
    sequences = [value for name, value in fields if name == "sequence"]
    # Python booleans are integers by inheritance, so require the exact integer type
    if len(sequences) != 1 or type(sequences[0]) is not int or not 0 <= sequences[0] <= MAXIMUM_SEQUENCE:
        return None
    document = dict(fields)
    unique = len(document) == len(fields)
    # A trusted fresh sequence can be acknowledged even when its payload is rejected
    valid = (unique and set(document) == {"version", "sequence", "processId", "input"}
             and type(document.get("version")) is int and document["version"] == 1
             and type(document.get("processId")) is int and document["processId"] == process_id
             and type(document.get("input")) is str and document["input"] in INPUTS)
    return Command(sequences[0], valid, document.get("input") if valid else None)


def valid_stop(data: bytes, process_id: int) -> bool:
    fields = fields_from_bytes(data, 1024)
    if fields is None or len(fields) != 2:
        return False
    document = dict(fields)
    # Two unique fields are required before a stop can match the captured process
    return (set(document) == {"version", "processId"}
            and type(document.get("version")) is int and document["version"] == 1
            and type(document.get("processId")) is int and document["processId"] == process_id)
