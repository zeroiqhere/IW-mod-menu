from .cli import main

# Module execution uses the same read-only path as the command-line wrapper
raise SystemExit(main())
