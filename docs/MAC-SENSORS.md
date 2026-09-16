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
Current development is not included in the published preview.2 package.
