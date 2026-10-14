import os
import tempfile
import unittest
from pathlib import Path
from fixtures import image_bytes
from bounds import mapped_image


class MappedImageTests(unittest.TestCase):
    def test_mapping_requires_image_path_inode_device_zero_offset_and_base(self):
        with tempfile.TemporaryDirectory() as directory:
            image = Path(directory) / 'image with spaces.exe'
            image.write_bytes(image_bytes())
            stat = image.stat()
            device = f'{os.major(stat.st_dev):02x}:{os.minor(stat.st_dev):02x}'
            valid = f'140000000-140001000 r--p 00000000 {device} {stat.st_ino} {image}\n'
            self.assertTrue(mapped_image(valid, image, stat, 0x140000000))
            for maps in (valid.replace('00000000', '00000001'), valid.replace(str(image), str(image) + '.other'),
                         valid.replace(f' {stat.st_ino} ', f' {stat.st_ino + 1} '), valid.replace(device, 'ff:ff'),
                         valid.replace('r--p', '---p'), valid.replace('140000000-', '13ffff000-'), valid + valid):
                with self.subTest(maps=maps):
                    self.assertFalse(mapped_image(maps, image, stat, 0x140000000))
