# Windows shared performance acceptance

The shared Windows host must be measured before stable 0.7.0. A different UI
framework does not establish lower CPU or memory use. The isolated harness below
is diagnostic evidence, not installed-product or long-running acceptance.

## Paired UI harness

Build the native application with `Build-Native.ps1`, compile and run
`Test-Native.ps1`, and build `scripts/DesktopTests/Pulse.Desktop.Tests.csproj`
in Release using the repository's .NET SDK. Run `Measure-NativeUi.ps1` twice
with the same arguments except `-HostKind Wpf` / `-HostKind Shared`:

```powershell
./scripts/Measure-NativeUi.ps1 -AppDirectory ./build/native/app -HostKind Shared -Scene monitor -Seconds 60 -Width 440 -Height 640
```

Supported scenes are `monitor`, `tray`, `desktop`, `desktop-contrast`,
`desktop-dynamic`, and `desktop-dynamic-contrast`. Run them sequentially on
the same display without concurrent UI tests. Each child uses its own fixture
directory, a 10-second warmup, two-second readings and external process counters.
Both consume `NativeTests.Snapshot()` through their existing reading adapters.
The driver changes the same synthetic temperature and sequence during each run.
No real collector, driver, account credentials, quota requests, FPS capture,
installed settings, startup tasks, or updater transport participates.

The script rejects a mismatched scene or actual DIP dimensions. Results retain
actual DIP and physical dimensions, assembly/harness hashes, processor count,
sample duration, average working set/private bytes, and CPU time normalized
across all logical processors. Compare physical dimensions as well as DIP.
The state directory contains `result.json`, raw `samples.json`, `ready.json`,
`completed.json`, and captured stderr. A successful result requires a clean
child exit and, for dynamic scenes, observed backdrop updates.

Desktop fixtures use matching font size, spacing, one column, transparent
background, topmost state, and a black/white/gray gradient. Dynamic gradients
advance every 33 ms with an eight-second period. The shared fixture includes
its normal window input/layer adapters; the isolated WPF shell excludes desktop
layer integration. UI content and rendering implementations differ. This is not
a controlled experiment of framework overhead alone. Shared production currently
polls once per second; this harness deliberately uses WPF's two-second cadence
to isolate presentation. Do not describe it as production sampling acceptance.

## Local baseline, 2026-09-17

Production source at `f338b0414157a134f7183032d15a1b9cf7552f21`, with the
paired harness introduced alongside this document. Each run completed 30 samples
over at least 60 seconds after warmup, on 16 logical processors, at 440 x 640 DIP
and 660 x 960 physical pixels. CPU percentages represent the whole machine.

| Scene | Host | CPU % | Working set MiB | Private MiB | Local evidence directory under vendor/ |
| --- | --- | ---: | ---: | ---: | --- |
| Monitor | WPF | 0.092 | 119.5 | 96.9 | ui-measure-b54be959d3994e0cb4f16761fba3fdd3 |
| Monitor | Shared | 0.095 | 147.4 | 123.3 | ui-measure-d6a867cb26ec46aa9cae3c9a4d646299 |
| Tray | WPF | 0.021 | 119.0 | 96.1 | ui-measure-c997be059faf465ca43cd563f060ba88 |
| Tray | Shared | 0.063 | 144.9 | 120.8 | ui-measure-0fb2d6f67d734bf6acae5e5c7c9b4479 |
| Desktop, Local Contrast | WPF | 0.495 | 140.7 | 167.1 | ui-measure-f507d164a9cb483db1e3a26b26eb89ad |
| Desktop, Local Contrast | Shared | 0.513 | 149.8 | 171.3 | ui-measure-43d7385a76784448924cab659ec19a4f |

These single runs do not establish statistical equivalence. Shared uses more
memory in these scenes; there is no measured basis to market it as inherently
lighter than WPF. Monitor and Local Contrast CPU are close in absolute terms.
The higher shared Tray CPU warranted checking its hidden Monitor rendering.
At this baseline, `MonitorWindow.Present` always called `Render`, whereas WPF
`Shell.UpdatePanel` skips hidden/minimized Monitor rendering while retaining
readings and Desktop updates. This is a code-grounded optimization candidate,
not a measured causal attribution or permission to stop background sampling.

The WPF application SHA-256 was
`4DB45A9712BA894F4FFE926A4C3FA0D893C9B582A67BB4241FFCBFE3B39456EC`;
the shared application SHA-256 was
`F3CCEEDBAD16155E4D1EF49A71406949DAB095A6CF6BDE4DF8373DED8C9903B6`.
Full adapter/Core/harness hashes and counter samples remain in the local evidence.

Validation passed: native regression (`vendor/test-benchmark-native.log`),
shared headless suite (`vendor/test-benchmark-shared.log`), and repository
`Validate.ps1 -ModernCore` (`vendor/validate-ui-benchmark.log`). A separate
two-second shared dynamic-contrast smoke completed 356 backdrop updates after
warmup (`vendor/benchmark-shared-dynamic-smoke.log`); it verifies execution and
cleanup, not sustained dynamic-background performance.

## Hidden Monitor rendering

`MonitorWindow` now retains each incoming snapshot and updates the floating
consumer before skipping its own controls when hidden or minimized. Visibility
and window-state restoration render the latest snapshot immediately. The
sampling loop, session peaks, provider lifecycles, cadence and settings remain
unchanged; no background collection is suspended to improve benchmark numbers.

`SamplingRecoveryTests.Visibility` checks pre-show data, hidden/minimized control
deferral, current floating readings, Session Max, unavailable values and restore.
The existing reopen check now also waits through background polling while hidden
and verifies the same sampling worker survives reopening. These checks run in
both headless and native suites.

The same 60-second Tray harness after this change measured 0.029% CPU,
145.0 MiB working set and 120.9 MiB private bytes (previously 0.063%,
144.9 MiB and 120.8 MiB). This is a single-run UI comparison, not a promise
of a proportional improvement in the installed application's total load.
Local evidence: `vendor/ui-measure-70fb639ea17f4eb0a972c60bfc79eae3`,
shared application SHA-256
`C8E7BEC3F7E7DD4CABE6F6A90F432A650FDEC854DC7E7D1C56F238D040ACF271`.

The first native suite passed the affected visibility and sampling checks but
failed the independent input fixture's red-pixel assertion. The final build's
full native suite passed without changing that assertion or its adapter.
Both logs are retained (`vendor/test-hidden-monitor-native.log` and
`vendor/test-hidden-monitor-native-final.log`). The intermittent pixel failure's
cause remains unproven; a passing rerun does not establish that it was fixed.
Final shared headless and `Validate.ps1 -ModernCore` passed as recorded in
`vendor/test-hidden-monitor-headless-final.log` and `vendor/validate-hidden-monitor.log`.

## Five-minute dynamic Local Contrast comparison

Source: `36531437695e998dd88c0183180b70cc3343a334`, Windows x64, 16 logical
processors. Release harness build passed without warnings. Both runs requested
440 x 640 DIP, a 2-second synthetic reading cadence, 33 ms animated-gradient
timer and enabled Local Contrast. They ran serially with 10 seconds warmup and
150 measured samples each. No builds or UI tests ran concurrently. Both
completed their cooperative shutdown successfully; no forced GC or trimming
was used.

| Observation | WPF | Shared |
| --- | ---: | ---: |
| Measured seconds | 302.96 | 302.89 |
| Whole-machine CPU average | 0.556% | 0.812% |
| Working set average | 143.38 MiB | 175.28 MiB |
| Private bytes average | 127.45 MiB | 203.83 MiB |
| Private bytes, first 30 s average | 124.62 MiB | 197.40 MiB |
| Private bytes, last 30 s average | 127.66 MiB | 201.99 MiB |
| Private bytes peak | 134.29 MiB | 212.03 MiB |
| Background updates, entire harness lifetime | 6688 | 7761 |

The shared run consumed more CPU and memory in this scene. It also delivered
more background updates despite the same requested timer interval, so this is
not a fixed-throughput comparison or a causal attribution of the difference.
First-to-last private-byte averages rose by about 3.04 MiB and 4.59 MiB. Five
minutes and these process counters do not prove leak freedom, a leak, or
long-term stability. Do not describe shared as inherently lighter than WPF.
The extra memory and dynamic-scene CPU still need attribution before claiming
final resource acceptance. Live collectors, quota and FPS remain excluded.

Local raw evidence:

- WPF: `vendor/ui-measure-7935d48d0aec4c7a99fb78ae75973199/`;
  app SHA-256 `628F8433260483CA5F57E15011AA7260EC9469C0C34C175F354CFB124C162C34`.
- Shared: `vendor/ui-measure-498c7ab04d04427cac56d7f62e014a0e/`;
  app SHA-256 `12E9B0C0B000A905B8A5F692EFF682AF47C174F50E3E81F51830AB8998B20482`.

Each directory retains result, sample, readiness, completion and stderr files,
including Core/adapter/harness hashes. Launch logs are
`vendor/soak-wpf-dynamic-300.log` and `vendor/soak-shared-dynamic-300.log`.

## Dynamic background attribution: contrast off/on

Four serial runs on the same Windows x64 machine used 440 x 640 DIP
(660 x 960 physical pixels), 16 logical processors, 10 seconds warmup and
120 requested measured seconds (60 samples). Each host used identical binary
hashes for its off/on pair. No builds or UI tests ran concurrently; all four
processes exited cooperatively. These are existing UI harness binaries, not
the final installed release.

| Host | Local Contrast | CPU % | Working set MiB | Private MiB | Background updates |
| --- | --- | ---: | ---: | ---: | ---: |
| WPF | Off | 0.079 | 124.04 | 108.54 | 2812 |
| WPF | On | 0.533 | 143.00 | 125.85 | 2813 |
| Shared | Off | 0.459 | 151.98 | 182.52 | 3243 |
| Shared | On | 0.866 | 172.70 | 202.74 | 3304 |

The observed CPU increment is 0.454 percentage points for WPF and 0.406 for
shared; working-set increments are 18.96 and 20.72 MiB. Shared's higher total
CPU is already present with Local Contrast disabled. This contradicts an
explanation attributing the entire gap to contrast capture/analysis. It does
not isolate rendering, allocation or collector costs and is not statistical
evidence that shared contrast is cheaper. The two frameworks deliver different
background update counts despite the same requested interval.

Both harnesses render the animated gradient inside the measured process, so
their counters include test-background rendering as well as Pulse. A common
background in a separately measured process is the next isolation boundary;
do not optimize production sampling from these combined counters. Live
collectors, FPS and quota requests remain excluded.

Raw evidence directories under `vendor/`:

- WPF off: `ui-measure-10a6f457bdc74d6d881f148c2e8f3aac`.
- WPF on: `ui-measure-079072e163514f638cc4254973420773`.
- Shared off: `ui-measure-9bb07045b2f94100b381f41a20d3305f`.
- Shared on: `ui-measure-19c58cc33c3746658918836855c6397a`.

WPF app SHA-256:
`05C2C949EA60FCE0DB502A4F8D93E4B11330EA43A411D5F3DE5A44D3EABE2049`.
Shared app SHA-256:
`12E9B0C0B000A905B8A5F692EFF682AF47C174F50E3E81F51830AB8998B20482`.
Each directory includes all assembly hashes, raw samples, readiness/completion
and stderr. Launch logs are `vendor/attribution-{wpf,shared}-dynamic-{off,on}.log`.
The WPF binary was rebuilt by validation since the five-minute comparison;
only its new matched pair is used for this increment calculation.

## Separately measured common background

`Measure-NativeUi.ps1 -ExternalBackground` now runs the same WPF animated
gradient helper for either host. `NativeTests --benchmark-backdrop` owns only
that window and its cooperative stop marker. The UI harnesses suppress their
internal gradients; production code and sampling are unchanged. The driver
requires matching physical bounds, explicit external-background admission and
zero UI-owned animation updates. It records background CPU/private/working-set
samples separately on the same measurement interval. Startup, early exit and
shutdown failures invalidate the run; cleanup targets only process objects
created by this measurement. Existing embedded-background mode remains available.

Allowed scope: the two UI harnesses, measurement driver and this evidence.
No installed files, personal settings, collectors, privilege policy or capture
implementation change. Source rollback is a revert of this harness change.
Deterministic checks are both builds, repository validation, headless regression,
paired execution, geometry checks and an injected early helper exit.

Four serial 60-second runs used the same 440 x 640 DIP / 660 x 960 pixel bounds,
2-second readings, 10-second warmup and 16 logical processors. All completed
normally, each with 30 measured samples. No builds or UI tests ran concurrently.

| Host | Contrast | UI CPU % | UI working set MiB | UI private MiB | Background CPU % | Background updates |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| WPF | Off | 0.035 | 125.03 | 106.60 | 0.066 | 1561 |
| WPF | On | 0.477 | 143.05 | 128.06 | 0.043 | 1559 |
| Shared | Off | 0.098 | 144.14 | 152.50 | 0.043 | 1581 |
| Shared | On | 0.524 | 160.99 | 164.80 | 0.059 | 1583 |

Background working set was 74.4–74.8 MiB and private bytes 96.9–98.5 MiB;
this extra test helper is not shipped with Pulse. Removing framework-specific
animation from the measured UI narrows the observed CPU gap. This supports
background rendering as a confounder in the earlier combined-process results;
it does not prove that all remaining overhead is understood. Shared still uses
more memory, and one-minute samples cannot establish equivalence or stability.
The WPF/shared Desktop integration difference, synthetic inputs and exclusion
of real collectors/FPS/quota remain. These are not final installed measurements.

Local evidence directories under `vendor/`, ordered as the table:
`ui-measure-9d906ab2eea442469aff894534e3ce33`,
`ui-measure-9f62171c68c14da2859db16f4d37c68a`,
`ui-measure-3eb0faf7e35d44c680b2aaf9dbdee98b`,
`ui-measure-97aabd3352674befaaf2ad89091e88d2`.
Each retains assembly hashes, separate counters, geometry, completion and stderr.
WPF app SHA-256: `438608BEE35FA5E8D0A4BC80CAF7D3CE3AB614C7510034A88843BA04581AB2D2`;
shared app SHA-256: `90445A1CA55CF74FA556ACDFCBED8098787A5D5AB6DEC361D943CDC727D1F829`.
Common helper SHA-256: `C7E05C3C03C4C097F877411BF825F4E1176FF2DAF5E62EB7930E4DE85B4B478B`.

Repository validation passed (`vendor/validate-external-background.log`), and
the shared Release build had no warnings/errors (`vendor/build-external-background.log`).
The two-second shared contrast smoke passed geometry and both normal exits.
An early cooperative helper exit failed with `External background exited during
sampling`, recorded incomplete evidence, emitted no successful result and left
neither owned process running (`vendor/external-background-early-exit.log` and
`vendor/ui-measure-c8fdc36233e341b2b072a37ed3836b30`).
The full shared headless suite passed (`vendor/test-external-background-headless.log`).
Its two `FAIL native Desktop monitoring` lines are intentional injected failures
in `SamplingRecoveryTests.Run`, which requires exit code 3 and closed windows
for smoke/measurement mode before restoring the prior test exit code.
Both original embedded-background dynamic smokes also passed, retaining their
own animation and normal shutdown (`vendor/embedded-background-shared-smoke.log`
and `vendor/embedded-background-wpf-smoke.log`).

## Remaining release evidence

- Measure the actual installed UI plus collector and all helper processes,
  with the same sensor availability and display conditions as the WPF baseline.
- Cover Monitor, Desktop and Tray, quota/FPS/Local Contrast off and on, and
  dynamic backgrounds. Record unavailable features rather than counting them
  as a cheap successful run.
- Verify reading accuracy and staleness with real inputs; the synthetic fixture
  only verifies a repeatable benchmark source.
- Run a sustained soak and inspect resource trends, hide/show, display changes,
  screenshot-mode restoration and disposal. A 60-second average cannot prove
  leak freedom or long-term stability.
- Keep final release evidence bound to the actual packaged binaries and source
  revision. Rebuilt local UI test assemblies are not the downloadable product.

The harness change is reversible and limited to test entry points, the measurement
driver and this document. Production sampling, privileges and settings are
unchanged. Relevant validation is both harness builds, native regression tests,
paired benchmark runs, shared headless tests and `Validate.ps1 -ModernCore`.

<!-- sop-risk-classification: {"facts":{"blast_radius":"isolated","change_kind":"implementation","data_boundary":"ordinary","destructive":"no","failure_cost":"low","irreversibility":"reversible","operational_controls":"not_applicable","privilege_boundary":"unchanged","project_policy":"default","rollback":"easy","scope_knowledge":"known","uncertainty":"low","verification":"deterministic"},"formal_review":"not_required","kind":"risk-classification-assessment","reasons":{"formal_review":["routine_no_review"],"risk":["no_high_risk_signal"]},"risk":"routine","schema_version":2} -->
