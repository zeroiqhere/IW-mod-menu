def process_token(raw):
    # The command name may contain spaces or parentheses
    end = raw.rfind(')')
    fields = raw[end + 1:].split()
    if end < 0 or len(fields) < 20 or fields[0] in ('Z', 'X', 'x'):
        raise ValueError('A live process creation token is required')
    pid, start = int(raw.split(' ', 1)[0]), int(fields[19])
    if pid <= 0 or start <= 0:
        raise ValueError('A positive process identity is required')
    return pid, start


def readable_span(raw_maps, address, count):
    # Small reads must fit inside one readable mapping
    if not 0 < count <= 4096 or address < 0 or address + count >= 2**64:
        raise ValueError('The requested span exceeds the read bounds')
    for line in raw_maps.splitlines():
        fields = line.split()
        start, end = (int(value, 16) for value in fields[0].split('-'))
        if fields[1].startswith('r') and start <= address and address + count <= end:
            return start, end
    raise ValueError('The complete span is not readable in one mapping')


def mapped_image(raw_maps, image_path, image_stat, base):
    import os
    from pathlib import Path
    # A same-named file cannot stand in for the mapped disk device and inode
    matches = 0
    for line in raw_maps.splitlines():
        fields = line.split(None, 5)
        if len(fields) != 6:
            continue
        start, end = (int(value, 16) for value in fields[0].split('-'))
        major, minor = (int(value, 16) for value in fields[3].split(':'))
        if (start == base and end >= base + 4096 and fields[1].startswith('r')
                and int(fields[2], 16) == 0 and int(fields[4]) == image_stat.st_ino
                and (major, minor) == (os.major(image_stat.st_dev), os.minor(image_stat.st_dev))
                and Path(fields[5]) == image_path):
            matches += 1
    return matches == 1
