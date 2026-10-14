# Memory.dll provenance

`Memory.dll` was extracted from the original [SyndiShanX IW-RTM-Tool v1.3 release](https://github.com/SyndiShanX/IW-RTM-Tool/releases/tag/v1.3), published June 2, 2025. This local test build retains that binary without modifying it.

SHA-256:

```text
2d183ad700eb3903fd2ff8b58c39135dba058fffb157d363f1af4904cf5b0e3a
```

The binary's ProductVersion identifies source commit [`fa6187dec24bb8c75f0cfa7882c8c8b2af4c120a`](https://github.com/erfg12/memory.dll/tree/fa6187dec24bb8c75f0cfa7882c8c8b2af4c120a) in erfg12/Memory.dll. This records the reported source revision; a reproducible binary match has not been established.

The corresponding upstream GNU LGPL 2.1 license is supplied as [Memory.LICENSE](Memory.LICENSE). The application itself retains its separate [GNU GPLv3 license](../LICENSE.md).

From the source folder, the retained binary can be checked with:

```bash
sha256sum dependencies/Memory.dll
```
