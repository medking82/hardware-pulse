"""Bound one owned native test and collect one managed stack after a progress stall."""
import argparse
import os
from pathlib import Path
import subprocess
import sys
import threading
import time

NATIVE_BUDGET = 60
STALL_SECONDS = 10
STACK_BUDGET = 8
PROGRESS = ("NATIVE_TEST_", "SAMPLING_REOPEN_PHASE", "FPS_PHASE")


def run_session(command, stack_command=None, *, budget=NATIVE_BUDGET,
                stall=STALL_SECONDS, stack_budget=STACK_BUDGET):
    started = time.monotonic()
    deadline = started + budget
    last_progress = [None]  # Arm only after native test progress, not slow runtime/font startup.
    lock = threading.Lock()
    flags = subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0
    process = subprocess.Popen(command, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                               text=True, encoding="utf-8", errors="replace", creationflags=flags)

    def forward():
        for line in process.stdout:
            if line.startswith(PROGRESS):
                with lock:
                    last_progress[0] = time.monotonic()
            print(line, end="", flush=True)

    reader = threading.Thread(target=forward, daemon=True)
    reader.start()
    captured = False
    try:
        while process.poll() is None:
            now = time.monotonic()
            if now >= deadline:
                print(f"NATIVE_SESSION_TIMEOUT target_pid={process.pid} budget={budget}", flush=True)
                process.kill()  # Kill only the child we created; SIGTERM can be ignored.
                process.wait()
                return 124
            with lock:
                idle = None if last_progress[0] is None else now - last_progress[0]
            if stack_command and not captured and idle is not None and idle >= stall:
                captured = True
                print(f"NATIVE_STACK_BEGIN target_pid={process.pid} idleSeconds={idle:.3f}", flush=True)
                report_env = os.environ.copy()
                report_env["DOTNET_ROLL_FORWARD"] = "LatestMajor"
                try:
                    report = subprocess.run([*stack_command, "report", "--process-id", str(process.pid)],
                                            stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                                            text=True, encoding="utf-8", errors="replace",
                                            timeout=min(stack_budget, max(.05, deadline-now-.1)),
                                            creationflags=flags, env=report_env)
                    print(report.stdout[:262144], end="", flush=True)
                    print(f"NATIVE_STACK_END exit={report.returncode}", flush=True)
                except subprocess.TimeoutExpired as error:
                    output = error.output or b""
                    if isinstance(output, bytes):
                        output = output.decode("utf-8", errors="replace")
                    print(output[:262144], end="", flush=True)
                    print("NATIVE_STACK_END reporter-timeout", flush=True)
                except OSError as error:
                    print(f"NATIVE_STACK_END unavailable={type(error).__name__}", flush=True)
            else:
                try:
                    process.wait(timeout=min(.05, max(.001, deadline-now)))
                except subprocess.TimeoutExpired:
                    pass
        return process.returncode if process.returncode >= 0 else 128-process.returncode
    finally:
        if process.poll() is None:
            process.kill()
            process.wait()
        reader.join(timeout=1)
        if not reader.is_alive():
            process.stdout.close()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--stack-tool", required=True)
    args = parser.parse_args()
    if not Path(args.stack_tool).is_file():
        parser.error("Pinned dotnet-stack executable is missing")
    return run_session(["dotnet", "scripts/DesktopTests/bin/Release/net10.0/Pulse.Desktop.Tests.dll", "--native-session"],
                       [args.stack_tool])


if __name__ == "__main__":
    raise SystemExit(main())
