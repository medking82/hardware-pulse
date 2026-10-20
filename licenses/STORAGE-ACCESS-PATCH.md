# Local Windows sensor dependency changes

Hardware Pulse modifies two permission constants in the hash-pinned upstream
libraries. These are locally modified builds, not unmodified upstream binaries.
Neither change writes disk sectors, changes Defender policy, or removes sensors.

LibreHardwareMonitor also has a guarded AMD ADL PMLog read. ADL's shared-memory
mapping can become inaccessible after the GPU has been running. Both
`AmdGpu.Update` and `AmdGpu.GetReport` now call
`HardwarePulse.AmdPmLogMemory.Read` in the shipped Windows adapter, instead of
passing that driver-owned pointer directly to `Marshal.PtrToStructure`.
The helper uses `ReadProcessMemory` on the current process, then marshals an
owned, pinned copy. Failed or partial reads have a cleared timestamp so LHM
rejects that PMLog sample and retains its existing queried OD8 fallback.
No GPU is disabled and no hardware settings are changed.

- LibreHardwareMonitor 0.9.6, MPL-2.0: `StorageDevice.UpdatePerformanceSensors`
  opens its disk-performance query handle with desired access **0**, instead of
  casting `System.IO.FileAccess.ReadWrite` (3) to a Win32 mask.
- BlackSharp.Core 1.0.7, MPL-2.0, source commit
  `c70b735c6cec123ee8a046ac4a0bc6c606f52cf0`:
  `SafeFileHandler.OpenHandle` requests **GENERIC_READ**, not
  `GENERIC_READ | GENERIC_WRITE`. DiskInfoToolkit uses this helper for storage
  enumeration and SMART queries. Protocols requiring write access can fail rather
  than regain write capability; compatibility must be verified on the target
  hardware. This does not promise every controller supports read-only SMART.

The exact upstream source archives are included beside this notice:
`LibreHardwareMonitor-source-0.9.6.zip` and `BlackSharp-source-1.0.7.zip`.
`StorageAccess.patch` provides the corresponding preferred-source changes. Apply
its first file to the LHM source root and its second file to the BlackSharp source
root, then use each upstream project's build instructions and original licenses.
The included LibreHardwareMonitor-MPL-2.0.txt supplies the license text for both.

`AmdPmLog.patch` supplies the additional LHM source changes. Its MIT-licensed
helper source is included as `AmdPmLogMemory.cs.txt`; compile it into
`Pulse.Adapters.Windows.dll` and reference that assembly when building the
patched LHM source. The project build below performs those steps and the
corresponding IL replacements without rebuilding unrelated upstream code.

The project build uses `scripts/Patch-StorageAccess.ps1` with hash-pinned
Mono.Cecil 0.11.6 instead of rebuilding unrelated upstream code. It changes only
the two IL access constants and two AMD PMLog calls, adds the Windows adapter
reference, and checks both original and resulting DLL SHA256 values against
`scripts/storage-access-patch.json`. Unknown versions fail closed. Repeated
builds must reproduce the same output hashes. Mono.Cecil is a build tool only;
it is not bundled with or loaded by Hardware Pulse.

Reproduce from the Hardware Pulse source tree with `scripts/Build.ps1` or
`scripts/Build-WindowsCollector.ps1`; no installer invocation is required.
`scripts/Test-StorageAccess.ps1` verifies the resulting access sites;
`scripts/Test-AmdPmLogMemory.ps1` checks both AMD call sites and the actual
reader against valid, invalid and guard-page memory. The patch
script only accepts outputs below that checkout's build directory, never an
installed client. Roll back a development build by restoring its two original
libraries from the verified upstream archive; existing installations are not
modified by these build scripts.
