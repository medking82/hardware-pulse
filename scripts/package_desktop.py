"""Build and verify a self-contained Desktop preview archive. No installation."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import platform
import plistlib
import shutil
import subprocess
import tarfile
import tempfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
RIDS = ("win-x64", "win-arm64", "linux-x64", "linux-arm64", "osx-x64", "osx-arm64")
REQUIRED_NOTICES = ("Avalonia-MIT.txt", "Avalonia-NOTICE.md", "Avalonia-ANGLE-LICENSE.txt", "DotNet-MIT.txt", "DotNet-NOTICES.txt",
                    "MicroCom-MIT.txt", "SkiaSharp-MIT.txt", "HarfBuzzSharp-MIT.txt",
                    "SkiaSharp-HarfBuzzSharp-NOTICES.txt", "LobeIcons-MIT.txt", "TokenMonitor.txt", "SOURCES.md")


def app_version():
    value = ET.parse(ROOT / "src/Hosts/Desktop/Pulse.Desktop.csproj").findtext("./PropertyGroup/Version")
    assert value, "Missing shared App version"
    return value


def run(args, **kwargs):
    return subprocess.run([str(x) for x in args], check=True, text=True,
                          creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0, **kwargs)


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def executable_path(rid):
    if rid.startswith("win-"):
        return Path("Pulse.Desktop.exe")
    return Path("Pulse Preview.app/Contents/MacOS/Pulse.Desktop") if rid.startswith("osx-") else Path("Pulse.Desktop")


def resources_path(rid):
    return Path("Pulse Preview.app/Contents/Resources") if rid.startswith("osx-") else Path(".")


def verify_notices(folder, rid):
    resources = folder / resources_path(rid)
    for name in ("LICENSE", *("licenses/" + name for name in REQUIRED_NOTICES)):
        notice = resources / name
        assert notice.is_file() and notice.stat().st_size > 0, "Missing distribution notice: " + name


def verify(folder, rid):
    manifest = json.loads((folder / "manifest.json").read_text())
    assert manifest["rid"] == rid and manifest["kind"] == "development-preview"
    assert manifest["version"] == app_version(), "Wrong shared App version"
    expected = manifest["files"]
    actual = {p.relative_to(folder).as_posix() for p in folder.rglob("*") if p.is_file()} - {"manifest.json"}
    assert actual == set(expected), "Package file inventory changed"
    for name, value in expected.items():
        assert digest(folder / name) == value, "Package digest mismatch: " + name
    verify_notices(folder, rid)
    exe = folder / executable_path(rid)
    assert exe.is_file(), "Missing executable"
    if os.name != "nt" and not rid.startswith("win-"):
        assert exe.stat().st_mode & 0o111, "Missing executable permission"
    data = exe.read_bytes()
    header = data[:32]
    if rid.startswith("linux-"):
        assert header[:4] == b"\x7fELF" and header[4:6] == b"\x02\x01", "Expected little-endian ELF64"
        assert int.from_bytes(header[18:20], "little") == (183 if rid.endswith("arm64") else 62)
    elif rid.startswith("win-"):
        assert len(data) >= 64 and header[:2] == b"MZ", "Expected PE executable"
        offset = int.from_bytes(data[60:64], "little")
        assert 64 <= offset <= len(data) - 24 and data[offset:offset + 4] == b"PE\0\0", "Invalid PE header"
        assert int.from_bytes(data[offset + 4:offset + 6], "little") == (0xaa64 if rid.endswith("arm64") else 0x8664), "Wrong PE architecture"
    else:
        assert header[:4] == b"\xcf\xfa\xed\xfe", "Expected Mach-O 64"
        assert int.from_bytes(header[4:8], "little") == (0x100000c if rid.endswith("arm64") else 0x1000007)
        info = plistlib.loads((exe.parent.parent / "Info.plist").read_bytes())
        assert info["CFBundleExecutable"] == exe.name and info["CFBundlePackageType"] == "APPL"
        assert info["CFBundleShortVersionString"] == app_version().split("-")[0]
    options = json.loads((exe.parent / "Pulse.Desktop.runtimeconfig.json").read_text())["runtimeOptions"]
    assert "framework" not in options and "frameworks" not in options
    assert options["includedFrameworks"] == [{"name": "Microsoft.NETCore.App", "version": "10.0.12"}]
    suffix = ".dylib" if rid.startswith("osx-") else ".so"
    native_files = ["hostfxr.dll", "coreclr.dll", "libSkiaSharp.dll", "libHarfBuzzSharp.dll"] if rid.startswith("win-") else ["libhostfxr" + suffix, "libcoreclr" + suffix, "libSkiaSharp" + suffix, "libHarfBuzzSharp" + suffix]
    for file in native_files:
        assert (exe.parent / file).is_file(), "Missing self-contained dependency: " + file
    forbidden = [p for p in expected if p.endswith((".pdb", "auth.json", ".ps1"))]
    assert not forbidden, "Unexpected development/private files: " + ", ".join(forbidden)
    return exe


def build(rid, dotnet, allow_dirty=False):
    commit = run(["git", "rev-parse", "HEAD"], cwd=ROOT, capture_output=True).stdout.strip()
    dirty = bool(run(["git", "diff", "HEAD", "--name-only"], cwd=ROOT, capture_output=True).stdout.strip())
    if dirty and not allow_dirty:
        raise ValueError("Tracked changes present; commit first or use --allow-dirty for local validation only")
    dest = (ROOT / "dist" / "desktop-preview").resolve()
    assert dest.is_relative_to(ROOT.resolve()), "Package workspace escapes repository"
    dest.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="package-", dir=dest) as temp:
        temp = Path(temp)
        package = temp / ("Pulse-Preview-" + rid)
        binary = package / executable_path(rid)
        binary.parent.mkdir(parents=True)
        run([dotnet, "restore", ROOT / "src/Hosts/Desktop/Pulse.Desktop.csproj", "--locked-mode"], cwd=ROOT)
        run([dotnet, "publish", ROOT / "src/Hosts/Desktop/Pulse.Desktop.csproj", "-c", "Release", "-r", rid,
             "--self-contained", "true", "--no-restore", "--disable-build-servers", "-p:UseSharedCompilation=false",
             "-p:DebugType=None", "-p:DebugSymbols=false", "-o", binary.parent], cwd=ROOT)
        # Native NuGet symbols are copied independently of managed DebugSymbols.
        # Remove only these known symbols from this owned temporary publish output.
        for name in ("libSkiaSharp.pdb", "libHarfBuzzSharp.pdb"):
            symbol = binary.parent / name
            if symbol.is_file():
                symbol.unlink()
        binary.chmod(0o755)
        if rid.startswith("osx-"):
            (binary.parent.parent / "Info.plist").write_bytes(plistlib.dumps({
                "CFBundleName": "Pulse Preview", "CFBundleDisplayName": "Pulse Preview",
                "CFBundleIdentifier": "io.github.medking82.pulse.preview", "CFBundleExecutable": "Pulse.Desktop",
                "CFBundlePackageType": "APPL", "CFBundleVersion": app_version().split("-")[0],
                "CFBundleShortVersionString": app_version().split("-")[0], "NSHighResolutionCapable": True}))
        resources = package / resources_path(rid)
        resources.mkdir(parents=True, exist_ok=True)
        shutil.copy2(ROOT / "LICENSE", resources / "LICENSE")
        shutil.copytree(ROOT / "licenses", resources / "licenses")
        shutil.copy2(ROOT / "src/Hosts/Desktop/packages.lock.json", resources / "packages.lock.json")
        (package / "README.txt").write_text(
            "Pulse Desktop development preview\n\n"
            "Windows: extract the entire archive, then run Pulse.Desktop.exe. This does not replace the WPF installer.\n"
            "Linux: run ./Pulse.Desktop in a graphical desktop (X11 tested).\n"
            "macOS: Pulse Preview.app is a development bundle, not Developer ID signed or notarized.\n"
            "The .NET runtime is included; native OS graphics/font dependencies are still required.\n"
            "Live CPU/RAM, selected network and opt-in Codex file-login quota are available.\n"
            "Settings remember window size, theme, network choice and explicit Codex opt-in.\n"
            "Tray/menu bar offers Open Pulse and Quit Pulse where the desktop supports it. Closing the window quits.\n"
            "Desktop overlay, FPS and other quota providers are not connected.\n"
            "No installation, startup registration or automatic updates are performed.\n"
            "Upstream notices are in licenses/ on Windows/Linux, or inside the macOS App's Contents/Resources/licenses/.\n"
            "Experimental preview for evaluation; this is not the stable Windows product.\n", encoding="utf-8")
        files = {p.relative_to(package).as_posix(): digest(p) for p in sorted(package.rglob("*")) if p.is_file()}
        (package / "manifest.json").write_text(json.dumps({"schema": 1, "kind": "development-preview",
            "version": app_version(), "commit": commit, "dirty": dirty, "rid": rid, "files": files}, indent=2) + "\n", encoding="utf-8")
        verify(package, rid)
        archive = dest / f"Pulse-Preview-{rid}-{commit[:12]}{'-dirty' if dirty else ''}.tar.gz"
        def permissions(info):
            info.mode = 0o755 if info.isdir() or info.name == package.name + "/" + executable_path(rid).as_posix() else 0o644
            return info
        with tarfile.open(archive, "w:gz") as tar:
            tar.add(package, arcname=package.name, filter=permissions)
        (Path(str(archive) + ".sha256")).write_text(digest(archive) + "  " + archive.name + "\n", encoding="ascii")
    print("PACKAGE " + str(archive))
    return archive


def inspect_archive(archive, rid, smoke=False, measure=False):
    archive = archive.resolve()
    workspace = (ROOT / "dist" / "desktop-preview").resolve()
    assert workspace.is_relative_to(ROOT.resolve()), "Package workspace escapes repository"
    assert archive.is_relative_to(workspace), "Inspect artifacts in the repository package workspace"
    assert Path(str(archive) + ".sha256").read_text().split()[0] == digest(archive), "Archive digest mismatch"
    with tempfile.TemporaryDirectory(prefix="verify-", dir=archive.parent) as temp:
        with tarfile.open(archive) as tar:
            members = tar.getmembers()
            assert sum(m.size for m in members) < 1024 * 1024 * 1024, "Package too large"
            assert all(m.isfile() or m.isdir() for m in members), "Links are not package content"
            entry = "Pulse-Preview-" + rid + "/" + executable_path(rid).as_posix()
            assert tar.getmember(entry).mode & 0o111, "Archive lost executable permission"
            tar.extractall(temp, filter="data")
        package = Path(temp) / ("Pulse-Preview-" + rid)
        assert {p.name for p in Path(temp).iterdir()} == {package.name}, "Unexpected archive root"
        exe = verify(package, rid)
        if smoke or measure:
            os_name = "osx" if platform.system() == "Darwin" else "linux" if platform.system() == "Linux" else "win" if platform.system() == "Windows" else "unsupported"
            machine = os.environ.get("PROCESSOR_ARCHITEW6432", platform.machine()).lower() if os_name == "win" else platform.machine().lower()
            arch = "arm64" if machine in ("arm64", "aarch64") else "x64"
            assert rid == os_name + "-" + arch, "Native smoke requires matching OS and architecture"
            env = dict(os.environ)
            for key in list(env):
                if key.startswith("DOTNET_ROOT"):
                    del env[key]
            env["DOTNET_ROOT"] = str(Path(temp) / "no-system-dotnet")
            env["DOTNET_MULTILEVEL_LOOKUP"] = "0"
            result = run([exe, "--measure-session" if measure else "--smoke-test"], cwd=exe.parent, env=env, capture_output=True, timeout=120 if measure else 30)
            assert "PASS native Desktop UI and live CPU/RAM" in result.stdout, result.stdout + result.stderr
            if measure:
                rows=[json.loads(line.removeprefix("BENCH_DESKTOP ")) for line in result.stdout.splitlines() if line.startswith("BENCH_DESKTOP ")]
                assert len(rows)==1 and rows[0]["Seconds"]>=60 and not rows[0]["Demo"], "Missing live steady-state measurement"
            print(result.stdout.strip())
    print("PASS extracted self-contained package: " + rid)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--rid", required=True, choices=RIDS)
    parser.add_argument("--dotnet", default="dotnet")
    parser.add_argument("--allow-dirty", action="store_true")
    parser.add_argument("--archive", type=Path)
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--smoke-test", action="store_true")
    mode.add_argument("--measure-session", action="store_true")
    args = parser.parse_args()
    artifact = args.archive or build(args.rid, args.dotnet, args.allow_dirty)
    inspect_archive(artifact, args.rid, args.smoke_test, args.measure_session)
