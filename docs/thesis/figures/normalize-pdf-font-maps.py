"""Make PDF's implicit CID-to-glyph identity mapping explicit for PDF/UA-2.

Cairo can omit CIDToGIDMap in embedded TrueType CID fonts. Ordinary PDF
readers use Identity in that case; PDF/UA-2 requires the entry to be present.
Existing mappings and all content streams are preserved. Run after SVG export.
Requires pypdf (tested with 6.21.2).
"""

from pathlib import Path
import sys

from pypdf import PdfReader, PdfWriter
from pypdf.generic import NameObject


def normalize(path: Path) -> int:
    writer = PdfWriter(clone_from=PdfReader(path))
    changed = 0
    # Inspect every object, including fonts in nested form resources.
    for reference in writer.list_objects_in_increment():
        obj = reference.get_object()
        if not isinstance(obj, dict) or obj.get("/Subtype") != "/CIDFontType2":
            continue
        descriptor = obj.get("/FontDescriptor")
        if descriptor is None or "/FontFile2" not in descriptor.get_object():
            continue
        if "/CIDToGIDMap" not in obj:
            obj[NameObject("/CIDToGIDMap")] = NameObject("/Identity")
            changed += 1
    if changed:
        temporary = path.with_suffix(".normalized.pdf")
        writer.write(temporary)
        temporary.replace(path)
    writer.close()
    return changed


if __name__ == "__main__":
    for filename in sys.argv[1:]:
        path = Path(filename)
        print(f"{path.name}: {normalize(path)} explicit identity mappings added")
