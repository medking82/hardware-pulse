# macOS temperature and fan adapter

`MacSmcReadings` owns AppleSMC read access. MonitorSource calls it on the existing
serial sampling worker; Core ReadingSession retains peaks, and both Monitor and
floating monitor consume the resulting snapshots. No new process, timer, driver,
privileged helper or hardware write is introduced.

The native connection admits only read-key (5), key-index (8) and key-info (9)
commands under selector 2. It is opened and disposed within each polling call.
Discovery is bounded to 16,384 keys and 256 temperature/fan channels. Successful
metadata is cached for the App session; unavailable discovery backs off 30 seconds.
The two 80-byte wire buffers are reused. Normal polling does not enumerate keys.

Temperature keys beginning with T and actual fan keys F?Ac are recognized when
their payload is sp78, fpe2 or flt with the exact expected size. Unknown formats,
firmware errors, non-finite values and implausible sensor sentinels are unavailable.
Zero RPM is valid; zero temperature is treated as an inactive sensor. Labels retain
the raw key (for example SMC · TC0P); no universal CPU/GPU identity is claimed.

AppleSMC is not a stable public sensor API. Protocol references consulted:

- [SMCKit wire layout and read protocol](https://github.com/beltex/SMCKit/blob/master/SMCKit/SMC.swift)
- [Stats SMC reader and Apple Silicon float decoding](https://github.com/exelban/stats/blob/master/SMC/smc.swift)
- [IOKit public device access API](https://developer.apple.com/documentation/iokit)

Pulse independently implements the read-only protocol; it does not include fan
control code. In particular it never writes target speed, fan mode or unlock keys.

Fixtures check byte order, firmware-error handling, no stale live value, genuine
stopped fans, discovery bounds, caching and connection disposal. The native C check
verifies compiler alignment and IOKit argument widths; it cannot prove firmware
compatibility. macOS CI prints actual channel/read counts. A VM without AppleSMC
reports unavailable and does not establish physical Intel/Apple Silicon coverage.
The adapter is included in the published preview.3 package.

## CPU identity and GPU statistics

CPU model comes from `machdep.cpu.brand_string`; installed physical/logical core
counts come from `hw.physicalcpu_max` and `hw.logicalcpu_max`. The three sysctl
queries are cached per MonitorSource and do not run on each sample. Values reflect
what the OS exposes (including VM limits), not a model-name lookup table.
See [XNU CPU topology definitions](https://github.com/apple-oss-distributions/xnu/blob/main/bsd/kern/kern_mib.c).

`MacGpuReadings` enumerates at most 32 IOAccelerator registry entries, with stable
registry IDs for independent device history. It reads the driver's
PerformanceStatistics `Device Utilization %` or `GPU Activity(%)` property.
Missing, non-finite or out-of-range values are unavailable, while a real zero is
idle. See [the driver-property usage in Stats](https://github.com/exelban/stats/blob/master/Modules/GPU/reader.swift).

GPU core count is the device's numeric `gpu-core-count` property, cached by registry
ID and dropped when that device disappears. See [AppleGPUInfo](https://github.com/philipturner/applegpuinfo).
Unknown core counts stay unavailable; Intel/AMD execution units are not relabeled
as Apple GPU cores. No core-count lookup by chip name, synthetic VRAM accounting,
or display-refresh-rate-as-game-FPS is used.

Monitor and floating monitor share the same GPU snapshots and Session Max state.
The CPU card shows model and physical/logical counts; GPU rows show device name,
core count and utilization. Models and device identities are not translated.
CF objects and IOKit handles are released before each native poll returns.
