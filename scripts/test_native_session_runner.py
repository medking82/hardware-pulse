"""Regress target-only diagnostics, output progress and the unchanged native deadline."""
import contextlib
import io
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile
import time
import unittest
from run_native_desktop_session import run_session


class NativeSessionRunnerTests(unittest.TestCase):
    def child(self, code):
        return [sys.executable, "-u", "-c", code]

    def test_exit_status_is_preserved(self):
        with contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(run_session(self.child("raise SystemExit(7)"), budget=2), 7)

    def test_live_progress_does_not_collect(self):
        child = "import time;\nfor i in range(6):\n print('FPS_PHASE progress',flush=True);time.sleep(.1)"
        report = self.child("raise SystemExit('unexpected report')")
        output = io.StringIO()
        with contextlib.redirect_stdout(output):
            self.assertEqual(run_session(self.child(child), report, budget=3, stall=.3), 0)
        self.assertNotIn("NATIVE_STACK_BEGIN", output.getvalue())

    def test_stalled_report_targets_only_the_owned_pid_and_deadline_holds(self):
        with tempfile.TemporaryDirectory() as directory:
            pid_file = Path(directory)/"report-pid.txt"
            child = "import os,time;print('CHILD_PID='+str(os.getpid()),flush=True);time.sleep(20)"
            reporter = "from pathlib import Path;import sys;Path(sys.argv[1]).write_text(sys.argv[-1]);print('managed fixture stack')"
            report = [*self.child(reporter), str(pid_file)]
            flags = subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0
            peer = subprocess.Popen(self.child("import time;time.sleep(20)"), creationflags=flags)
            try:
                output = io.StringIO();started = time.monotonic()
                with contextlib.redirect_stdout(output):
                    result = run_session(self.child(child), report, budget=1.5, stall=.3, stack_budget=.4)
                self.assertEqual(result, 124)
                self.assertLess(time.monotonic()-started, 3)
                child_pid = re.search(r"CHILD_PID=(\d+)", output.getvalue()).group(1)
                self.assertEqual(pid_file.read_text(), child_pid)
                self.assertNotEqual(child_pid, str(peer.pid))
                self.assertIsNone(peer.poll())
                self.assertEqual(output.getvalue().count("NATIVE_STACK_BEGIN"), 1)
            finally:
                peer.kill();peer.wait()

    def test_reporter_hang_cannot_extend_child_deadline(self):
        output = io.StringIO();started = time.monotonic()
        with contextlib.redirect_stdout(output):
            result = run_session(self.child("import time;time.sleep(20)"), self.child("import time;time.sleep(20)"),
                                 budget=1.5, stall=.3, stack_budget=.2)
        self.assertEqual(result, 124)
        self.assertLess(time.monotonic()-started, 3)
        self.assertIn("reporter-timeout", output.getvalue())


if __name__ == "__main__":
    unittest.main()
