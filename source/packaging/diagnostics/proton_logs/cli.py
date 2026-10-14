import argparse
import sys

from .files import analyze_file, write_new_report


def main(argv=None):
    parser = argparse.ArgumentParser(description="Read a saved Proton log for one explicitly supplied Windows PID")
    parser.add_argument("source")
    parser.add_argument("--windows-pid", required=True, type=int, help="Exact decimal Windows game PID")
    parser.add_argument("--output-dir", required=True, help="A new private report directory")
    parser.add_argument("--session-label", help="Optional supplied provenance; never verified by the analyzer")
    args = parser.parse_args(argv)
    try:
        # Finish source validation before creating any output directory
        report = analyze_file(args.source, args.windows_pid, session_label=args.session_label)
        destination = write_new_report(report, args.output_dir)
    except (OSError, ValueError) as error:
        message = str(error) if isinstance(error, ValueError) else error.strerror
        print(f"Log analysis failed: {message}", file=sys.stderr)
        return 1
    print(destination)
    return 0
