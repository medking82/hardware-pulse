"""Fetch pinned, unmodified CJK fonts for embedding; never install system fonts."""
import hashlib
import json
from pathlib import Path
import tempfile
import urllib.request

ROOT = Path(__file__).resolve().parents[1]


def ensure():
    folder = ROOT / "vendor/desktop-fonts"
    folder.mkdir(parents=True, exist_ok=True)
    lock = json.loads((ROOT / "scripts/desktop-fonts.lock.json").read_text(encoding="utf-8"))
    for font in lock["fonts"]:
        path = folder / font["name"]
        def valid(candidate):
            if not candidate.is_file() or candidate.stat().st_size != font["size"]:
                return False
            with candidate.open("rb") as stream:
                return hashlib.file_digest(stream, "sha256").hexdigest() == font["sha256"]
        if valid(path):
            continue
        temporary = None
        try:
            with tempfile.NamedTemporaryFile(dir=folder, delete=False) as stream:
                temporary = Path(stream.name)
                with urllib.request.urlopen(font["url"], timeout=60) as response:
                    remaining = font["size"] + 1
                    while remaining:
                        block = response.read(min(1024 * 1024, remaining))
                        if not block:
                            break
                        stream.write(block)
                        remaining -= len(block)
            if not valid(temporary):
                raise ValueError("Pinned font size/hash mismatch: " + font["name"])
            temporary.replace(path)
        finally:
            if temporary is not None:
                temporary.unlink(missing_ok=True)
    print("PASS pinned Desktop CJK fonts")


if __name__ == "__main__":
    ensure()
