from collections import OrderedDict, deque
import hashlib
import re

from .models import Limits, require_windows_pid

# Wine's process and thread prefixes are hexadecimal, unlike the CLI's decimal PID
PREFIX = re.compile(r"^(?:(?P<time>[0-9]{1,12}\.[0-9]{1,9}):)?"
                    r"(?P<pid>[0-9a-fA-F]{1,8}):(?P<tid>[0-9a-fA-F]{1,8}):"
                    r"(?P<level>trace|warn|err|fixme):(?P<channel>[a-zA-Z0-9_]+):"
                    r"(?P<function>[a-zA-Z0-9_]+) (?P<message>.*)$")
FATAL = re.compile(r"^Unhandled exception code (?P<code>[0-9a-fA-F]{8}) "
                   r"flags (?P<flags>[0-9a-fA-F]{1,8}) addr (?P<address>(?:0x)?[0-9a-fA-F]{1,16})$")
DISPATCH = re.compile(r"^code=(?P<code>[0-9a-fA-F]{8})(?: \([^\r\n]*\))? "
                      r"flags=(?P<flags>[0-9a-fA-F]{1,8}) addr=(?P<address>(?:0x)?[0-9a-fA-F]{1,16})$")
INFO = re.compile(r"^\s*info\[(?P<index>[01])\]=(?P<value>(?:0x)?[0-9a-fA-F]{1,16})$")
HIGHER_INFO = re.compile(r"^\s*info\[(?:[2-9]|1[0-4])\]=(?:0x)?[0-9a-fA-F]{1,16}$")
MODULE = re.compile(r'^Loaded L"(?P<path>[^"\r\n]{1,4096})" at '
                    r'(?P<base>(?:0x)?[0-9a-fA-F]{1,16}): (?:native|builtin)$')


def _hex(value):
    return f"0x{int(value, 16):x}"


def _exception(match, line_number):
    return {"line": line_number, "code": _hex(match["code"]),
            "flags": _hex(match["flags"]), "address": _hex(match["address"])}


def _recent(dispatch, line_number, wine_time, limits):
    if dispatch is None or line_number - dispatch["line"] > limits.correlation_lines:
        return None
    if dispatch["wine_time"] is not None and wine_time is not None:
        # Timestamps constrain correlation; rollback cannot link a later dispatch to an earlier fatal
        def milliseconds(value):
            seconds, fraction = value.split(".")
            return int(seconds) * 1000 + int((fraction + "000")[:3])
        delta = milliseconds(wine_time) - milliseconds(dispatch["wine_time"])
        if not 0 <= delta <= limits.correlation_milliseconds:
            return None
    return dispatch


def analyze_stream(stream, windows_pid, *, limits=None):
    pid = require_windows_pid(windows_pid)
    limits = Limits() if limits is None else limits
    if not isinstance(limits, Limits):
        raise ValueError("Validated log limits are required")
    digest = hashlib.sha256()
    threads = OrderedDict()
    modules, events = [], []
    module_keys = set()
    counts = {"lines": 0, "bytes": 0, "selected_pid_lines": 0,
              "unparsed_lines": 0, "fatal_events": 0, "malformed_fatal_records": 0,
              "module_loads": 0, "omitted_events": 0, "omitted_modules": 0,
              "evicted_threads": 0, "malformed_dispatch_records": 0,
              "invalidated_information_records": 0, "ignored_higher_information_records": 0}
    while True:
        # readline never allocates an unbounded line before the size check
        raw = stream.readline(limits.max_line_bytes + 1)
        if not raw:
            break
        if not isinstance(raw, bytes):
            raise ValueError("A binary log stream is required")
        if len(raw) > limits.max_line_bytes:
            raise ValueError("A source log line exceeds the configured limit")
        counts["lines"] += 1
        counts["bytes"] += len(raw)
        if counts["lines"] > limits.max_lines or counts["bytes"] > limits.max_source_bytes:
            raise ValueError("The source log exceeds the configured scan limit")
        digest.update(raw)
        try:
            line = raw.decode("utf-8", "strict").rstrip("\r\n")
        except UnicodeError as error:
            raise ValueError("The source log is not valid UTF-8") from error
        record = PREFIX.fullmatch(line)
        if not record:
            counts["unparsed_lines"] += 1
            continue
        if int(record["pid"], 16) != pid:
            continue
        counts["selected_pid_lines"] += 1
        number = counts["lines"]
        channel, function, message = record["channel"], record["function"], record["message"]
        if channel == "loaddll":
            loaded = MODULE.fullmatch(message)
            if loaded:
                # Retain only the logged basename and base, with no inferred module size or range
                name = loaded["path"].replace("\\", "/").rsplit("/", 1)[-1]
                if not name or len(name) > 256 or any(ord(character) < 32 for character in name):
                    continue
                key = (name, _hex(loaded["base"]))
                counts["module_loads"] += 1
                if key not in module_keys:
                    if len(modules) < limits.max_modules:
                        module_keys.add(key)
                        modules.append({"name": key[0], "base": key[1], "line": number})
                    else:
                        counts["omitted_modules"] += 1
            continue
        if channel != "seh":
            continue
        tid = int(record["tid"], 16)
        if tid not in threads:
            if len(threads) == limits.max_threads:
                threads.popitem(last=False)
                counts["evicted_threads"] += 1
            threads[tid] = {"dispatch": None, "context": deque(maxlen=limits.context_lines)}
        threads.move_to_end(tid)
        thread = threads[tid]
        dispatch = DISPATCH.fullmatch(message) if function == "dispatch_exception" else None
        if dispatch:
            # A new dispatch replaces the prior exception and its parameters for this thread
            thread["dispatch"] = _exception(dispatch, number) | {"information": {}, "wine_time": record["time"]}
        elif function == "dispatch_exception":
            info = INFO.fullmatch(message)
            recent = _recent(thread["dispatch"], number, record["time"], limits)
            if message.startswith("code="):
                thread["dispatch"] = None
                counts["malformed_dispatch_records"] += 1
            elif info:
                # Valid orphan or stale parameters are ignored without calling their grammar invalid
                if recent:
                    if info["index"] in recent["information"]:
                        # Repeated parameters make the dispatch ambiguous instead of overwriting evidence
                        thread["dispatch"] = None
                        counts["invalidated_information_records"] += 1
                    else:
                        recent["information"][info["index"]] = _hex(info["value"])
            elif message.lstrip().startswith("info["):
                if HIGHER_INFO.fullmatch(message):
                    # Valid parameters 2 through 14 are ignored; only the access-operation pair is interpreted
                    counts["ignored_higher_information_records"] += 1
                else:
                    # Malformed parameters cannot leave earlier values looking complete or trustworthy
                    thread["dispatch"] = None
                    counts["invalidated_information_records"] += 1
        fatal = FATAL.fullmatch(message) if function == "NtRaiseException" else None
        if function == "NtRaiseException" and message.startswith("Unhandled exception") and not fatal:
            counts["malformed_fatal_records"] += 1
        if fatal:
            counts["fatal_events"] += 1
            if len(events) < limits.max_events:
                recent = _recent(thread["dispatch"], number, record["time"], limits)
                # Copy the observed records so later dispatches cannot change a saved event
                correlated = None if recent is None else dict(recent) | {"information": dict(recent["information"])}
                if correlated is not None:
                    # Only access-violation parameters have this documented interpretation
                    operation = {"0x0": "read", "0x1": "write", "0x8": "execute"}
                    is_access = correlated["code"] == "0xc0000005"
                    correlated["access_operation"] = operation.get(correlated["information"].get("0")) if is_access else None
                    correlated["access_address"] = correlated["information"].get("1") if is_access else None
                events.append(_exception(fatal, number) | {
                    "thread_hex": f"{tid:04x}", "wine_time": record["time"],
                    "recent_same_thread_dispatch": correlated,
                    "context": list(thread["context"]),
                    "allocation_owner": None, "causal_module": None, "cause": None,
                    "correlation_is_causation": False,
                })
            else:
                counts["omitted_events"] += 1
        thread["context"].append({"line": number, "function": function,
                                   "message": message[:limits.context_characters]})
    return {"version": 1, "selection": {"windows_pid": pid, "wine_pid_hex": f"{pid:04x}",
            "process_or_session_identity_verified": False},
            "source": {"sha256": digest.hexdigest(), "bytes": counts["bytes"]},
            "counts": counts, "limits": vars(limits), "modules": modules, "fatal_events": events,
            "interpretation": {"allocation_owner": None, "cause": None,
                "module_address_ranges_known": False, "stack_corruption_established": False,
                "pid_reuse_or_multiple_sessions_excluded": False,
                "note": "Records match the supplied PID only; nearby records and module loads do not establish a cause or session identity"}}
