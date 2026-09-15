"""End-to-end CLI contracts; only Linux cases read live procfs."""
import json
import os
import subprocess
import sys

dotnet, assembly = sys.argv[1:]


def run(arguments, code):
    result = subprocess.run([dotnet, assembly, *arguments], capture_output=True,
                            text=True, encoding="utf-8", timeout=15)
    assert result.returncode == code, (arguments, result.returncode, result.stderr)
    return result


assert "Usage:" in run(["--help"], 0).stdout
for arguments in [["--unknown"], ["--interface"], ["--interface", ""],
                  ["--interface", "bad name"], ["--interface", "lo", "extra"]]:
    result = run(arguments, 2)
    assert not result.stdout and "Usage:" in result.stderr

if sys.platform != "linux":
    result = run([], 4)
    assert not result.stdout and "requires Linux" in result.stderr
    print("PASS probe help, invalid arguments and unsupported OS")
    sys.exit(0)


def snapshot(arguments, code):
    result = run(arguments, code)
    assert not result.stderr, result.stderr
    data = json.loads(result.stdout)
    assert data["schema"] == 1 and data["platform"] == "linux"
    expected = os.environ.get("PULSE_TEST_ARCH")
    assert not expected or data["architecture"] == expected
    cpu = data["cpuRam"]
    assert cpu["state"] == "LIVE" and cpu["error"] is None
    assert 0 <= cpu["values"]["cpuLoad"] <= 100
    ram = cpu["usage"]["ram"]
    assert 0 <= ram["used"] <= ram["total"] and ram["total"] > 0
    assert 0 <= ram["percent"] <= 100
    assert "cpu" not in cpu["values"]
    return data


basic = snapshot([], 0)
assert basic["complete"] and basic["network"] is None
loopback = snapshot(["--interface", "lo"], 0)
assert loopback["complete"]
assert loopback["network"]["names"]["Network"] == "lo"
assert loopback["network"]["values"]["netDown"] >= 0
assert loopback["network"]["values"]["netUp"] >= 0
# More than Linux's maximum interface-name length guarantees no matching device.
missing = snapshot(["--interface", "pulse-missing-interface-for-probe"], 3)
assert not missing["complete"] and missing["network"]["state"] == "OFFLINE"
assert missing["network"]["error"] and not missing["network"]["values"]
print("PASS probe live CPU/RAM, optional network, partial failure and JSON contracts")
