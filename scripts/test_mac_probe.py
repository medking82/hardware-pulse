"""End-to-end macOS probe process contracts, without saving host snapshots."""
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
for arguments in [["--unknown"], ["--interface", "en0"], ["--help", "extra"]]:
    result = run(arguments, 2)
    assert not result.stdout and "Usage:" in result.stderr
if sys.platform != "darwin":
    result = run([], 4)
    assert not result.stdout and "requires macOS" in result.stderr
    print("PASS macOS probe help, invalid arguments and unsupported OS")
    sys.exit(0)

result = run([], 0)
assert not result.stderr
data = json.loads(result.stdout)
assert data["schema"] == 1 and data["platform"] == "macos" and data["complete"]
expected = os.environ.get("PULSE_TEST_ARCH")
assert not expected or data["architecture"] == expected
assert data["units"] == {"cpuLoad": "percent", "ram": "GiB"}
cpu, memory = data["cpu"], data["memory"]
assert cpu["state"] == memory["state"] == "LIVE"
assert cpu["error"] is None and memory["error"] is None
assert 0 <= cpu["values"]["cpuLoad"] <= 100
ram = memory["usage"]["ram"]
assert 0 <= ram["used"] <= ram["total"] and ram["total"] > 0
assert 0 <= ram["percent"] <= 100 and "estimate" in ram["label"]
assert "cpu" not in cpu["values"] and "network" not in data
print("PASS macOS probe live CPU/RAM estimate, units and JSON contracts")
