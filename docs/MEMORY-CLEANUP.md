# Resources retired

[简体中文](MEMORY-CLEANUP.zh-CN.md)

Resources and process cleanup are retired from Windows Pulse 0.6.52. The process
list, grouping, RAM/GPU close suggestions, normal-close actions and their adapter
are removed. Pulse has no RAM/VRAM cleanup or zombie-process detection feature.

The experiment did not reliably complete the intended job: a normal window-close
request cannot guarantee an app exits or its RAM/VRAM is released, and process
usage cannot establish that an app is unused. Repeated UI readability failures
also made the workflow unsuitable. The feature is removed rather than expanded
into force termination or application-specific tray control.

CPU/GPU temperatures, RAM/VRAM usage, fans, network, Desktop, FPS and quota remain
read-only monitoring features. Existing settings and installer identity are retained;
no process is closed and no system settings are changed by this retirement.

Older implementation and release notes remain in Git history and
[v0.6.51](https://github.com/medking82/hardware-pulse/releases/tag/v0.6.51).
