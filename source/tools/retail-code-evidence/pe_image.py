import struct


class PeImage:
    def __init__(self, data):
        # A complete installed image provides the static section and unwind bounds
        self.data = data
        if len(data) < 64 or data[:2] != b'MZ':
            raise ValueError('DOS header mismatch')
        pe = struct.unpack_from('<I', data, 60)[0]
        if pe + 24 > len(data) or data[pe:pe + 4] != b'PE\0\0':
            raise ValueError('PE header mismatch')
        machine, count, timestamp = struct.unpack_from('<HHI', data, pe + 4)
        optional_size = struct.unpack_from('<H', data, pe + 20)[0]
        optional = pe + 24
        end = optional + optional_size + count * 40
        if machine != 0x8664 or optional_size < 144 or end > len(data):
            raise ValueError('A complete x64 PE header is required')
        if struct.unpack_from('<H', data, optional)[0] != 0x20b:
            raise ValueError('PE optional header mismatch')
        self.base = struct.unpack_from('<Q', data, optional + 24)[0]
        self.size = struct.unpack_from('<I', data, optional + 56)[0]
        self.exception = struct.unpack_from('<II', data, optional + 136)
        self.sections = []
        for index in range(count):
            offset = optional + optional_size + index * 40
            virtual_size, rva, raw_size, raw = struct.unpack_from('<IIII', data, offset + 8)
            flags = struct.unpack_from('<I', data, offset + 36)[0]
            self.sections.append((rva, virtual_size, raw, raw_size, flags))
        self.identity = (machine, timestamp, self.base, self.size, tuple(self.sections), self.exception)

    def raw_offset(self, rva, length):
        # Virtual padding is not readable from the installed file
        if length <= 0:
            raise ValueError('A nonempty span is required')
        for start, _, raw, raw_size, _ in self.sections:
            relative = rva - start
            if relative >= 0 and relative + length <= raw_size and raw + relative + length <= len(self.data):
                return raw + relative
        raise ValueError('The complete span has no raw file backing')

    def executable_span(self, rva, length):
        if not 0 < length <= 4096:
            raise ValueError('Code capture must stay within its read budget')
        if not any(start <= rva and rva + length <= start + size and flags & 0x20000000
                   for start, size, _, _, flags in self.sections):
            raise ValueError('Code capture is outside an executable section')

    def function_size(self, rva):
        table, length = self.exception
        if not table and not length:
            return None
        if length % 12:
            raise ValueError('Unwind directory has an incomplete entry')
        start = self.raw_offset(table, length)
        matches = []
        for offset in range(start, start + length, 12):
            begin, end, _ = struct.unpack_from('<III', self.data, offset)
            if begin == rva:
                self.executable_span(begin, end - begin)
                matches.append(end - begin)
        if len(matches) > 1:
            raise ValueError('Unwind entry is ambiguous')
        return matches[0] if matches else None
