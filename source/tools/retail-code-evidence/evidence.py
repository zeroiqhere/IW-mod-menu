import hashlib
import json

from profile import SOURCE_COMMIT


def write_capture(output, segments, metadata):
    # A new directory preserves previous evidence and refuses symlink destinations
    output.mkdir(mode=0o700, parents=False, exist_ok=False)
    manifest = dict(metadata)
    manifest.update({
        'sourceCommit': SOURCE_COMMIT,
        'callableBindingsVerified': False,
        'completeLogicalFunctionsVerified': False,
        'writesToGame': 0,
        'injections': 0,
        'engineOrSteamCalls': 0,
        'segments': [],
    })
    for segment in segments:
        (output / (segment.name + '.bin')).write_bytes(segment.code)
        manifest['segments'].append({
            'name': segment.name, 'candidateRva': hex(segment.rva),
            'byteCount': len(segment.code), 'sha256': hashlib.sha256(segment.code).hexdigest(),
            'bound': 'exact-runtime-function-entry',
        })
    # The manifest is the completion marker; failed output has no success marker
    pending = output / '.capture.json.tmp'
    with pending.open('x', encoding='utf-8') as stream:
        json.dump(manifest, stream, indent=2)
        stream.write('\n')
    pending.replace(output / 'capture.json')

