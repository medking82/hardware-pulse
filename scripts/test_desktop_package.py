"""Negative package contract tests; native launch is verified separately in CI."""
import json
import shutil
from pathlib import Path
import tarfile
import tempfile
import unittest
import subprocess
from unittest.mock import patch
from io import BytesIO
import package_desktop as package
import prepare_desktop_fonts as fonts


class FontPreparationTests(unittest.TestCase):
    def test_pinned_download_cache_and_corrupt_response(self):
        with tempfile.TemporaryDirectory(dir=package.ROOT / "vendor") as temp:
            root = Path(temp)
            (root / "scripts").mkdir()
            good = b"font fixture"
            (root / "scripts/desktop-fonts.lock.json").write_text(json.dumps({"fonts": [{
                "name": "fixture.otf", "size": len(good),
                "sha256": package.hashlib.sha256(good).hexdigest(), "url": "https://example.invalid/font"
            }]}))
            with patch.object(fonts, "ROOT", root), patch.object(fonts.urllib.request, "urlopen", return_value=BytesIO(good)) as request:
                fonts.ensure()
                fonts.ensure()
                self.assertEqual(request.call_count, 1, "Valid cache should avoid network")
            target = root / "vendor/desktop-fonts/fixture.otf"
            target.write_bytes(b"invalid cache")
            with patch.object(fonts, "ROOT", root), patch.object(fonts.urllib.request, "urlopen", return_value=BytesIO(b"x" * 100)):
                with self.assertRaisesRegex(ValueError, "size/hash mismatch"):
                    fonts.ensure()
            self.assertEqual(target.read_bytes(), b"invalid cache", "Rejected download must not replace cache")
            self.assertEqual(list(target.parent.iterdir()), [target], "Failed download cleans temporary data")


class BundleLaunchTests(unittest.TestCase):
    def test_open_exit_alone_does_not_prove_app_success(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            exe = root / "Pulse Preview.app/Contents/MacOS/Pulse.Desktop"
            with patch.object(package, "run", return_value=subprocess.CompletedProcess([], 0, "", "")):
                with self.assertRaisesRegex(AssertionError, "did not complete App smoke"):
                    package.verify_bundle_launch(exe, {}, root)

    def test_app_failure_is_not_hidden_by_success_marker(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            exe = root / "Pulse Preview.app/Contents/MacOS/Pulse.Desktop"
            def launch(*args, **kwargs):
                (root / "bundle-stdout.log").write_text("PASS native Desktop UI and live CPU/RAM")
                (root / "bundle-stderr.log").write_text("FAIL font coverage")
                return subprocess.CompletedProcess([], 0, "", "")
            with patch.object(package, "run", side_effect=launch):
                with self.assertRaisesRegex(AssertionError, "FAIL font coverage"):
                    package.verify_bundle_launch(exe, {}, root)


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
        for name in ("README.txt", "README.zh-CN.txt"):
            shutil.copy2(package.ROOT / "docs/distribution" / name, self.folder / name)
        self.manifest()

    def test_missing_launch_guides_even_with_matching_manifest(self):
        for name in ("README.txt", "README.zh-CN.txt"):
            with self.subTest(name=name):
                guide = self.folder / name
                original = guide.read_bytes()
                guide.unlink()
                self.manifest()
                with self.assertRaisesRegex(AssertionError, "Missing launch guide"):
                    package.verify(self.folder, "linux-x64")
                guide.write_text("", encoding="utf-8")
                self.manifest()
                with self.assertRaisesRegex(AssertionError, "Missing launch guide"):
                    package.verify(self.folder, "linux-x64")
                guide.write_bytes(original)
                self.manifest()

    def test_mac_guides_inside_bundle(self):
        with self.assertRaisesRegex(AssertionError, "Missing launch guide"):
            package.verify_guides(self.folder, "osx-arm64")
        resources = self.folder / package.resources_path("osx-arm64")
        resources.mkdir(parents=True)
        for name in ("README.txt", "README.zh-CN.txt"):
            shutil.copy2(self.folder / name, resources / name)
        package.verify_guides(self.folder, "osx-arm64")
        (resources / "README.zh-CN.txt").unlink()
        with self.assertRaisesRegex(AssertionError, "Missing launch guide"):
            package.verify_guides(self.folder, "osx-arm64")

    def manifest(self, rid="linux-x64"):
        (self.folder / "manifest.json").write_text(json.dumps({"kind": "development-preview", "rid": rid,"version": package.app_version(),
            "files": {p.relative_to(self.folder).as_posix(): package.digest(p)
                      for p in self.folder.rglob("*") if p.is_file() and p.name != "manifest.json"}}))

    def test_windows_machine_and_native_dependencies(self):
        (self.folder / "Pulse.Desktop").unlink()
        header = bytearray(96)
        header[:2] = b"MZ"
        header[60:64] = (64).to_bytes(4, "little")
        header[64:68] = b"PE\0\0"
        header[68:70] = (0x8664).to_bytes(2, "little")
        exe = self.folder / "Pulse.Desktop.exe"
        exe.write_bytes(header)
        for old, new in zip(("libhostfxr.so", "libcoreclr.so", "libSkiaSharp.so", "libHarfBuzzSharp.so"),
                            ("hostfxr.dll", "coreclr.dll", "libSkiaSharp.dll", "libHarfBuzzSharp.dll")):
            (self.folder / old).rename(self.folder / new)
        self.manifest("win-x64")
        package.verify(self.folder, "win-x64")
        self.manifest("win-arm64")
        with self.assertRaisesRegex(AssertionError, "Wrong PE architecture"):
            package.verify(self.folder, "win-arm64")
        header[68:70] = (0xaa64).to_bytes(2, "little")
        exe.write_bytes(header)
        self.manifest("win-arm64")
        package.verify(self.folder, "win-arm64")
        (self.folder / "coreclr.dll").unlink()
        self.manifest("win-arm64")
        with self.assertRaisesRegex(AssertionError, "Missing self-contained dependency"):
            package.verify(self.folder, "win-arm64")

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

    def test_version_mismatch(self):
        path = self.folder / "manifest.json"
        manifest = json.loads(path.read_text())
        manifest["version"] = "0.0.0-wrong"
        path.write_text(json.dumps(manifest))
        with self.assertRaisesRegex(AssertionError, "Wrong shared App version"):
            package.verify(self.folder, "linux-x64")

    def test_mac_notices_travel_with_app(self):
        # Outer archive notices alone cannot satisfy the movable .app bundle.
        with self.assertRaisesRegex(AssertionError, "Missing distribution notice"):
            package.verify_notices(self.folder, "osx-arm64")
        resources = self.folder / "Pulse Preview.app/Contents/Resources"
        resources.mkdir(parents=True)
        shutil.copy2(self.folder / "LICENSE", resources / "LICENSE")
        shutil.copytree(self.folder / "licenses", resources / "licenses")
        package.verify_notices(self.folder, "osx-arm64")
        moved = Path(self.temp.name) / "relocated"
        moved.mkdir()
        shutil.copytree(self.folder / "Pulse Preview.app", moved / "Pulse Preview.app")
        package.verify_notices(moved, "osx-arm64")
        (moved / "Pulse Preview.app/Contents/Resources/licenses/SkiaSharp-HarfBuzzSharp-NOTICES.txt").unlink()
        with self.assertRaisesRegex(AssertionError, "Missing distribution notice"):
            package.verify_notices(moved, "osx-arm64")

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
