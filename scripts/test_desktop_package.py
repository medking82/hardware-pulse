"""Negative package contract tests; native launch is verified separately in CI."""
import json
import shutil
from pathlib import Path
import tarfile
import tempfile
import unittest
import package_desktop as package


class PackageTests(unittest.TestCase):
    def setUp(self):
        parent = (package.ROOT / "dist/desktop-preview").resolve()
        self.assertTrue(parent.is_relative_to(package.ROOT.resolve()))
        parent.mkdir(parents=True, exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(dir=parent, prefix="contract-")
        self.addCleanup(self.temp.cleanup)
        self.folder = Path(self.temp.name) / "Pulse-Preview-linux-x64"
        self.folder.mkdir()
        header = bytearray(32)
        header[:6] = b"\x7fELF\x02\x01"
        header[18:20] = (62).to_bytes(2, "little")
        (self.folder / "Pulse.Desktop").write_bytes(header)
        (self.folder / "Pulse.Desktop").chmod(0o755)
        for file in ["libhostfxr.so", "libcoreclr.so", "libSkiaSharp.so", "libHarfBuzzSharp.so"]:
            (self.folder / file).write_bytes(b"fixture")
        (self.folder / "Pulse.Desktop.runtimeconfig.json").write_text(json.dumps({"runtimeOptions": {
            "includedFrameworks": [{"name": "Microsoft.NETCore.App", "version": "10.0.12"}]}}))
        shutil.copy2(package.ROOT / "LICENSE", self.folder / "LICENSE")
        (self.folder / "licenses").mkdir()
        for name in package.REQUIRED_NOTICES:
            shutil.copy2(package.ROOT / "licenses" / name, self.folder / "licenses" / name)
        self.manifest()

    def manifest(self):
        (self.folder / "manifest.json").write_text(json.dumps({"kind": "development-preview", "rid": "linux-x64",
            "files": {p.relative_to(self.folder).as_posix(): package.digest(p)
                      for p in self.folder.rglob("*") if p.is_file() and p.name != "manifest.json"}}))

    def test_required_notice_even_with_matching_manifest(self):
        notice = self.folder / "licenses/SkiaSharp-HarfBuzzSharp-NOTICES.txt"
        notice.unlink()
        self.manifest()
        with self.assertRaisesRegex(AssertionError, "Missing distribution notice"):
            package.verify(self.folder, "linux-x64")
        notice.write_text("")
        self.manifest()
        with self.assertRaisesRegex(AssertionError, "Missing distribution notice"):
            package.verify(self.folder, "linux-x64")

    def test_inventory_and_digest(self):
        package.verify(self.folder, "linux-x64")
        (self.folder / "unexpected.txt").write_text("extra")
        with self.assertRaisesRegex(AssertionError, "inventory"):
            package.verify(self.folder, "linux-x64")
        (self.folder / "unexpected.txt").unlink()
        (self.folder / "libcoreclr.so").write_bytes(b"changed")
        with self.assertRaisesRegex(AssertionError, "digest"):
            package.verify(self.folder, "linux-x64")

    def test_runtime_dependency(self):
        (self.folder / "Pulse.Desktop.runtimeconfig.json").write_text(json.dumps({"runtimeOptions": {"framework": {"name": "Microsoft.NETCore.App"}}}))
        self.manifest()
        with self.assertRaises(AssertionError):
            package.verify(self.folder, "linux-x64")

    def test_archive_permission(self):
        archive = Path(self.temp.name) / "fixture.tar.gz"
        def remove_exec(info):
            if info.isfile():
                info.mode = 0o644
            return info
        with tarfile.open(archive, "w:gz") as tar:
            tar.add(self.folder, arcname=self.folder.name, filter=remove_exec)
        Path(str(archive) + ".sha256").write_text(package.digest(archive) + "  " + archive.name)
        with self.assertRaisesRegex(AssertionError, "executable permission"):
            package.inspect_archive(archive, "linux-x64")
        Path(str(archive) + ".sha256").write_text("0" * 64 + "  " + archive.name)
        with self.assertRaisesRegex(AssertionError, "Archive digest"):
            package.inspect_archive(archive, "linux-x64")


if __name__ == "__main__":
    unittest.main()
