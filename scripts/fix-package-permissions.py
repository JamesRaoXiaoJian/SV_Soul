"""Normalize release ZIP permissions so macOS/Linux can read the mod files."""
import sys
from pathlib import Path
from zipfile import ZipFile

path = Path(sys.argv[1])
temporary = path.with_suffix('.normalized.zip')
with ZipFile(path) as source, ZipFile(temporary, 'w') as target:
    for entry in source.infolist():
        entry.create_system = 3
        entry.external_attr = ((0o40755 if entry.is_dir() else 0o100644) << 16)
        target.writestr(entry, source.read(entry.filename))
temporary.replace(path)
