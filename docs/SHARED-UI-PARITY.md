# Shared host migration acceptance

The product reference is Windows WPF v0.6.27, commit
`ccf2a1e411767e16981091e164f27b7cb130fb9a`. The destination is the shared
host, not a replacement WPF maintenance release. The withdrawn v0.7.0 must
not be reused or have its assets replaced. A new version is selected only
after acceptance. Existing published preview packages are not stable evidence.

## Protected reference and ownership

Keep `src/Panel.xaml`, `src/Native`, original SVG assets and installed user
profiles intact while porting. Work starts on `codex/iterate-0.6.27`.
The separate dirty main checkout is preserved and is not the migration input.
Read `docs/RUNTIME-BOUNDARIES.md` for Core/platform boundaries.

WPF dynamically changes the XAML tree: `LayoutSettings.BuildSettingsLayout`
creates the six Settings categories and responsive sections; `Cards.ApplyDensity`
changes compact/detail layout. The initial XAML alone is not the UI reference.
Use the running fixture and these runtime owners together.

| Required behavior | Reference owner | Acceptance evidence still needed in shared host |
| --- | --- | --- |
| Compact custom window, original controls and navigation | Panel.xaml, Shell, LayoutSettings | Matched-size renders; drag, resize, minimize, close/tray and keyboard behavior |
| Real backdrop, independent readable foreground, adjustable opacity, solid fallback | Controls.ApplyMaterial, MaterialPolicy, PulseBackdrop | Native composition on Windows; 0/30/100 opacity, solid/high contrast, lock/unlock and Settings transitions |
| CPU/GPU/Memory/NVMe/Airflow/Network device cards | Cards.cs, ResponsivePanel | Same hierarchy, labels, original SVGs, compact/Details, visibility/order and 1–3 columns; no generic sensor dump |
| General, App Appearance, Desktop, App Cards, FPS, AI Quota | LayoutSettings.BuildSettingsLayout | Sticky Back/category navigation, category contents and all existing controls; narrow and wide, light/dark, EN/SC/TC |
| Desktop presentation and editing | DesktopView and LayoutSettings | App-to-Desktop transition, transparent background, row/grid layout, text/icon colors, independent opacity, lock, topmost, Local Contrast and restore |
| Shared readings and quota | ReadingSession, QuotaSession and adapters | One sampler/session per owner; both views receive same snapshot; unavailable is not zero; provider cancellation and transient/auth failures |
| Installation and profile compatibility | Startup, Settings, installer, UpdateCheck | Isolated migration fixtures then actual upgrade acceptance; preserve settings and geometry; recovery from withdrawn shared installer |
| Resource use | Measurement fixtures | Comparable WPF/shared builds, same hardware/data/rate/views, warm-up plus CPU/RAM/soak measurements; no inferred improvement |

macOS remains RC until its platform gates in RELEASE-READINESS are satisfied.
Native materials, menu bar, login startup, Keychain, sleep/wake and physical
hardware acceptance cannot be inferred from Windows/headless results.

## First implementation boundary: App material

Port background opacity and solid fallback through the existing MaterialPolicy;
keep foreground opacity independent. Allow Desktop host presentation/settings
and corresponding tests only. Do not alter collectors, credentials, scheduled
tasks, installer identity, WPF UI, existing personal profiles or release assets.
Use platform-reported transparency support; requesting blur is not proof that
blur is active. Actual native appearance remains a separate acceptance gate.

Validate preference roundtrip, unknown-field preservation, unsupported/solid
fallback, zero/full opacity and readable text. Then render the affected App
and Settings before integrating card layout. Headless green is not visual parity.
Rollback is reverting this source change; no installed profile is migrated here.

## Current evidence

The WPF reference fixture renders have been inspected locally (compact App and
General Settings). They establish the original hierarchy; they do not prove
shared-host parity. No complete parity row above is accepted yet.

App material implementation now uses the existing Core MaterialPolicy. New
preview profiles start dark with 85% background opacity, following the user's
explicit preference for a deeper readable glass surface; existing explicit
Light/System/Dark preferences remain supported. Solid mode and an unsupported
platform produce an opaque background without overwriting the preference or
fading foreground text. Settings content remains opaque. The native request
now reuses `Native/Backdrop.cs` through the Windows adapter on an HWND surface;
other platforms request Blur without silently accepting plain transparency.
Zero opacity explicitly allows clear transparency. High contrast requests an
opaque surface; native high-contrast appearance still requires acceptance.

Build, headless Desktop regression, and Windows native-session regression pass.
The repository's complete `scripts/Validate.ps1` also passes on this change;
the WPF presentation, Panel.xaml and original assets still match v0.6.27.
AppMaterialTests cover saved preferences, zero/full opacity, solid fallback,
foreground independence, opaque Settings and unknown-field preservation. An
isolated Windows demo was visually inspected: the backdrop is active, but the
old preview card hierarchy and navigation visibly remain. Do not label this
as WPF visual parity. The demo was closed without changing installed profiles.

### Readability regression: bounded Windows correction

The later native reproduction showed sharp underlying text through the window:
Avalonia's Blur hint had fallen back to ordinary transparency. The original
`PulseBackdrop.ApplyStable` owner restores visible blur in the Windows demo.
The original radial viewport gradient is also restored. A failed native backdrop
application uses an opaque tint, and headless handles never call the HWND API.
No original WPF source, installed settings, collector or release is modified.

White text at 30% opacity had insufficient contrast over bright content. The user
chose deeper default glass while retaining light text and the opacity slider.
New profiles now use 85%; explicit saved values remain unchanged. Small device
values inherit the main text color; large hero readings and original icons retain
hardware accents. Secondary device text uses #DDE9F0, and status/reset text is no
longer faded independently. The black/white backdrop compositing regression reports
4.96:1 minimum for main and secondary text across the viewport gradient stops.
This is palette evidence, not a claim about every control or arbitrary saved opacity.

The Windows demo was inspected focused and unfocused, plus opaque Settings. Blur
remains active on focus changes and light text is visibly separated from the
background. Low explicit opacity remains user-controlled and can reduce contrast.
Installed WPF settings were not migrated. Full Settings/Desktop parity and other
platform material acceptance remain outstanding.

### Device-card migration boundary

The shared Monitor now groups readings into CPU, GPU, Memory, NVMe, Airflow and
Network cards using the original SVGs. WindowsSnapshotReadings reads the existing
per-user collector snapshot with bounded input and the original SensorProfile
parser. It starts no collector, driver or elevated process. MonitorSource owns
the existing polling worker; cards do not poll. Missing/stale readings are shown
as unavailable while capability layout and Session Max survive. The selected
traffic interface, rather than the collector's default interface, labels rates.

Synthetic checks cover parser parity/freshness, capability layout, Details,
one-to-three columns, stable controls and peak/current behavior. Allowed surfaces
are the modern Windows adapter, shared host and their tests; original WPF sources,
assets, personal profiles, installer and release remain protected. Height-driven compact density,
card reordering/visibility, shell navigation and full Settings/Desktop presentation
still require migration. This partial card port is not release acceptance.

### Compact card flow

DeviceCard now follows Native/Cards.ApplyDensity for width-driven flow: compact
metrics share two columns when their measured text fits, otherwise they use one;
Airflow and Details retain full-width rows. A hero reading stacks below its title
when the header cannot fit on one line. Existing controls and fixed grid definitions
are reused across sampling, resize and Details changes. Memory/NVMe pairs retain
their separate layout. This does not yet port the original height-driven density
levels or font-size setting.

A regression assertion failed on the previous shared layout because Load and
Vcore always occupied different rows. It now passes, together with a narrow-card
single-column/header-stacking check, Details restoration and text-bound checks at
360/800/1200 widths. Compact and Details renders were inspected. The full headless
suite and Windows native-session regression pass; these checks do not establish
full App/Desktop or Settings parity.

The first repository validation attempt in this slice failed the unchanged WPF
Desktop native hit-test at 30% opacity after its fixed 300 ms wait. An isolated
diagnostic rerun kept every assertion and reported the expected HWND at both
sample points for 0% and 30%, then passed. The cause of the first observation is
not established; neither the WPF implementation nor that test was changed.
The subsequent complete `scripts/Validate.ps1` run passed, including that fixture.

The Windows input fixture passed once and later failed its immediate red-pixel
assertion after pass-through hit testing succeeded. The fixture now waits up
to its existing five-second deadline for that exact red pixel and rechecks
pass-through afterwards. The subsequent native suite passed. This addresses
the compositor observation timing, without relaxing the pixel or ownership
assertion; it is not evidence for other machines or platforms.

<!-- sop-risk-classification: {"facts":{"blast_radius":"isolated","change_kind":"implementation","data_boundary":"ordinary","destructive":"no","failure_cost":"low","irreversibility":"reversible","operational_controls":"not_applicable","privilege_boundary":"unchanged","project_policy":"default","rollback":"easy","scope_knowledge":"known","uncertainty":"low","verification":"semantic"},"formal_review":"not_required","kind":"risk-classification-assessment","reasons":{"formal_review":["routine_no_review"],"risk":["no_high_risk_signal"]},"risk":"routine","schema_version":2} -->

### Original direct mode controls

Monitor uses Live and Session Max buttons instead of the preview's mode dropdown,
and Details is a toggle button. The wrapping control row preserves access at narrow
widths; mode selection is mutually exclusive even when the active button is pressed
again. Selection only re-renders the current snapshot and its history, including an
open floating monitor; it does not add polling or change the reading owner.

Allowed scope is Monitor presentation and corresponding regression tests. Installed
profiles, original WPF, adapters, credentials and releases remain unchanged. Source
reversion is the rollback boundary. Headless keyboard activation, repeated selection,
immediate current/peak switching, Details and sampling recovery pass. The Windows
native regression passes, and the compact render was inspected. The large preview
heading, main tabs, complete original Settings and Desktop still need migration;
this step is not full UI or release acceptance.
Repository scripts/Validate.ps1 also passed for this control change.

### Compact heading and original page navigation

The shared Monitor no longer uses top-level Monitor/Settings tabs. It has a 22 DIP
Pulse heading, fixed monitoring controls/status, and a bottom-right Settings button
using the original settings.svg. Settings has a fixed Back/title toolbar above its
scrolling content. Page changes restore keyboard focus to Back or Settings and retain
the existing controls, selected category and ongoing reading updates. The same shared
presentation serves all platforms; no Windows-only presentation fork was introduced.

The boundary remains shared presentation, embedded original asset references,
localization and corresponding tests. Native/WPF sources, original asset files,
installed profiles, data owners, credentials and releases are protected. Reverting
this source diff is sufficient rollback. Headless navigation checks exercise keyboard
activation, fixed controls under scroll, focus restoration, control identity, and
readings delivered while Settings is open. Compact Monitor and scrolled Settings
renders were inspected; the Windows native regression also passes.

This does not yet restore custom titlebar/window controls, 240 DIP minimum width,
all six original Settings categories, or the full Desktop. Existing three Settings
categories remain functional; their headers still scroll and require the subsequent
original-category migration. Full visual parity and stable release remain unaccepted.
Full scripts/Validate.ps1 passed for the navigation change.

### Shared custom titlebar and resize

Monitor now uses a shared custom titlebar with the original live/minimize/close
SVG geometry, independent foreground binding and localized accessible labels.
Titlebar dragging delegates to Avalonia BeginMoveDrag; eight edge/corner regions
delegate to BeginResizeDrag and disappear when resize is disabled or the window
is not Normal. Minimize and Close preserve the existing tray/lifetime ownership.
Original WPF source/assets, installed profiles, adapters and release state remain
unchanged. The change is source-reversible and uses the existing routine UI risk
classification; no native privilege or credential boundary changes.

Headless and Windows native regressions pass for minimize/restore, close with an
open floating view, stale tray actions, localization and resize-target availability.
The rendered compact header was inspected. A real isolated Windows demo was moved
through its titlebar (screen origin 494,494 to 614,554 at 150% scaling), resized
through its southeast corner from 800x560 to 720x510 logical pixels, then closed
through its titlebar; the process exited successfully. The cards reflowed and the
blur remained visible. Demo mode writes no user settings. This is Windows evidence
for one drag/corner, not all-edge, multi-monitor, macOS or Linux acceptance. Original
lock/snap behavior, original minimum size and complete Settings/Desktop parity are
still outstanding.
Full scripts/Validate.ps1 passed for this titlebar change.

### App window preferences and lock material transitions

Shared Appearance now exposes Always on Top and Lock Position and Size, using the
original labels. Preferences are isolated in PreviewSettingsStore and restored
before interaction; unknown profile fields remain preserved. Lock disables shared
titlebar dragging and all resize regions, but Settings/Back and Close remain usable.
Unlock restores resize and the drag cursor. Topmost applies to the App only, leaving
the floating view's independent preference intact.

The existing Core MaterialPolicy now receives lock and Settings visibility, matching
WPF Controls.ApplyMaterial: supported locked Monitor uses clear material at one quarter
of saved opacity; Settings restores saved opacity and an opaque content surface.
Solid/unsupported fallback remains opaque. Lock never rewrites the opacity preference.
The readable-default palette evidence applies to unlocked mode, not this deliberately
more transparent original locked mode.

Changes are limited to shared presentation/preferences/localization and their tests;
original WPF, assets, collectors, credentials, installed profiles and releases are
protected. Source reversion restores previous behavior and added profile keys are
ordinary optional booleans. Windows native regression passes for preference application,
close/reopen, resize availability, Settings access and lock/Settings opacity transitions.
Full OS-level move prevention, multi-monitor behavior, visual locked-mode acceptance,
tray preference commands and the rest of Settings/Desktop parity remain outstanding.
Headless regression and full scripts/Validate.ps1 also passed for this change.

### App Cards visibility and order

Shared Settings now has an App Cards page for the six hardware groups. Visibility
and order are independent saved preferences; invalid/duplicate order entries are
normalized, and missing known cards are appended in original order. Visibility is
applied after capability mapping on every presentation, so polling cannot re-enable
a hidden card. Re-enabling uses the current snapshot. Moving reuses existing card
instances and updates both visual/control order. Original lock blocks order changes.
All-hidden state retains Settings access and explains how to restore cards.

The allowed boundary is shared preferences/presentation/localization and tests;
original WPF/assets, collector sessions, quotas, installed profiles and releases are
unchanged. Added profile keys are optional and unknown fields remain preserved.
Headless tests cover malformed preferences, ordering, repeated polling, all-hidden
recovery, control reuse and persistence. The 360 DIP App Cards render was inspected;
Windows native regression passes. Original drag grips, hardware-name customization,
font/density controls, full six-category Settings and Desktop remain incomplete.
Full scripts/Validate.ps1 also passed for App Cards preferences.
