#!/usr/bin/env bash
# CI-only X11 session. Xvfb supplies a display, not minimize/maximize behavior.
set -euo pipefail
openbox --sm-disable >"${RUNNER_TEMP:-/tmp}/pulse-openbox.log" 2>&1 &
wm_pid=$!
cleanup() { kill "$wm_pid" 2>/dev/null || true; wait "$wm_pid" 2>/dev/null || true; }
trap cleanup EXIT
ready=false
for attempt in {1..100}; do
    if xprop -root _NET_SUPPORTING_WM_CHECK | grep -q 'window id #'; then
        ready=true
        break
    fi
    kill -0 "$wm_pid"
    sleep 0.05
done
if [[ "$ready" != true ]]; then
    echo 'FAIL native session: X11 window manager did not become ready' >&2
    exit 1
fi
# Same 60s test budget; SIGKILL also enforces it when a blocked native loop ignores TERM.
# The supervisor samples only its own child after 10s without a test progress marker.
PULSE_TEST_PRIVATE_X11=1 timeout --signal=KILL 60s python3 scripts/run_native_desktop_session.py --stack-tool "${PULSE_TEST_STACK_TOOL:?Pinned stack tool path is required}"
