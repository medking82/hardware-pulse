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

### Original font scale and height-driven card density

App font size is now saved with the original 10–16 DIP range and 12 DIP default.
Device headers, compact/full labels, values, pairs and usage typography use the
original fontSize/12 scale. Responsive columns use 270*scale minimum width. The
card area measures levels 0–3 against the available viewport: padding/gaps shrink,
and only the most compact non-Details mode hides device subtitles. Details keeps
identity labels even when scrolling is necessary. Existing cards are reused.

During verification, measuring the hidden card grid immediately after replacing
row definitions produced an Avalonia Grid.MeasureCellsGroup index exception. Rows
now remain allocated for all six cards; visibility changes only relocate controls.
Spacing belongs to visible cards so unused rows do not create empty gaps. The same
all-hidden/re-enable regression now passes. The interface-label fixture now uses
enough height to check its visible subtitle; separate checks explicitly cover
subtitle hiding and Details retention in a short window.

Headless coverage exercises 10/12/16 DIP at 360/800/1200 widths, card text bounds,
short-window density, preference persistence and the prior visibility/order checks.
10 and 16 DIP renders were inspected. Windows native regression passes. Scope is
shared presentation/preferences/localization and tests, with original WPF/assets,
installed profiles, data owners and releases unchanged. Original minimum window
size, Settings category layout, card drag handles and complete Desktop remain open.
Full scripts/Validate.ps1 passed for font scaling and card density.

### Fixed Settings category navigation

Settings category headers now remain outside each category's ScrollViewer, below
Back; the save-status message stays fixed at the bottom. Existing pages follow the
original naming/order: General, App Appearance, App Cards, AI Quota. Language is in
General with network selection. AI Quota's enable guidance points to the actual
category. Desktop/FPS pages are still absent until their controls are implemented;
this is not a claim that all six categories have been restored.

Navigation regression verifies a nonzero content scroll offset while both Back and
the selected category retain their window positions. Existing persistence, language,
material and card preference tests use the visible category ordering. The short
Appearance and narrow App Cards renders were inspected. Headless and Windows native
regressions pass. Scope remains shared UI/localized copy and tests, with original
WPF/assets, profiles, provider logic and releases unchanged. Original section styling,
responsive multi-column Settings, full category contents and Desktop remain open.
Full scripts/Validate.ps1 passed for fixed Settings navigation.

### Shared Desktop hardware metric mapping

The floating view now consumes the original DesktopMode-style metric grouping from
its owner's MonitorSnapshot: combined CPU/GPU temperature and load, VRAM, current
memory, drives, individual fans, network links/signals and selected-interface rates.
Linux sensor rows retain their stable IDs. DesktopReadings is presentation-only;
there are no new readers, timers, driver calls or credential access. Snapshot peaks
are used only for peak readings; memory/VRAM and link metadata remain current.

Rows reuse controls by metric key while capabilities remain present. Disappeared
keys are removed so changing Linux sensor identities cannot grow an unbounded cache.
Original SVG icons and hardware colors are used, with light-theme contrast and
in-place language/theme updates. Labels and values wrap within the original-style
icon/name/value layout, reserving space for names beside long values.

Headless and Windows native checks cover grouped CPU/GPU, individual fans/drive,
selected-interface provenance, stale clearing with retained capability rows,
peak/current memory semantics, singleton lifecycle, topmost, lock and narrow layout.
The 360 DIP hardware render was inspected. This does not turn the existing floating
preview into full Desktop mode: wallpaper layer, transparent material, geometry,
row/grid preference, metric visibility/order, quotas/FPS, local contrast, edit/return
flow and dedicated Settings still require migration. WPF, installed profiles,
collectors and releases remain unchanged; rollback is source-only.
Full scripts/Validate.ps1 passed for the Desktop metric mapping.

### Desktop appearance and layout preferences

Desktop Settings now exposes the original font range 10–32 DIP (default 16),
row spacing 4–40 DIP (default 14), Auto/1/2/3 columns and independent Always on Top.
PreviewSettingsStore owns persistence; MonitorWindow applies it to the existing
floating view. Its toolbar topmost control synchronizes the Settings control and
saved preference without changing App topmost. Core ColumnLayout selects the
columns that fit; row controls and the single owner snapshot stream remain shared.

The change is confined to shared presentation, preview preferences, localization
and regression fixtures. Original WPF/assets, installed profiles, hardware and
credential boundaries are unchanged. Rollback is source-only. Verification covers
live controls, close/reopen persistence, independent App preferences, toolbar sync,
three-column layout and narrow resizing in headless and native Windows sessions.
The initial resize regression exposed stale width during child SizeChanged; layout
now follows the window SizeChanged event. Settings and three-column renders were
inspected. This does not complete Desktop parity: explicit-column auto-expansion,
geometry, wallpaper layer/material, metric visibility/order, quota/FPS, local
contrast, and edit/return flow remain open.
Full scripts/Validate.ps1 passed for Desktop appearance/layout preferences.

### Desktop backing and foreground opacity

DesktopView.SetTextOpacity is the reference for this slice: Desktop backing and
Always on Top backing use separate percentages, and metric text/icons have their
own opacity. The original defaults (86/55/100) and dark RGB 20/29/38 backing with
light #F5F7FA foreground are retained. This is transparent Desktop backing, not the
App blur backdrop or Liquid Glass. User-selected App theme does not make these
light-on-dark Desktop readings dark-on-dark.

Settings expose all three saved values and enable the backing slider applicable
to the topmost mode. The metric layer fades independently of the editor and window.
Unsupported composition falls back to opaque backing without overwriting saved
preferences; high contrast forces an opaque backing and full metric opacity.
Platform color changes are observed and unsubscribed on close. This is an isolated
shared presentation/preferences change with the same routine risk facts above;
no platform capture, driver, privilege or installed profile changes. Rollback is
source-only. Acceptance checks cover opacity endpoints, independent foreground,
mode switching, fallback and persistence in headless/native Windows sessions.
Full Desktop editor/layer, native glass visual acceptance, custom color/contrast,
geometry, quota/FPS and exact original styling remain separate open requirements.
Headless and Windows native regressions passed; the narrow Desktop render was inspected. Full scripts/Validate.ps1 passed. Native composition over real wallpaper and high-contrast visual acceptance remain unverified.

### Desktop editor completion and return

The original DesktopView editor visibility and Return to App interaction are now
represented in the shared floating view. Done locks via the existing native input
adapter, hides editor controls and taskbar entry, disables resize and removes the
OS frame. Monitor/tray reopening unlocks the same view and restores its editor.
Return to App closes only the floating view and restores the existing Monitor,
including recovery when it was hidden. Unsupported lock platforms retain an
operable editor; no new input hooks or privilege boundary are introduced.

A new Windows native assertion caught Avalonia replacing extended input styles
when chrome changed after pass-through. Chrome now changes before enabling native
pass-through; unlocking removes pass-through before restoring editor chrome.
Regression covers preserved layered/transparent/no-activate flags, editor state,
tray restoration, Return from a hidden Monitor and existing shutdown/singleton
behavior. Scope is shared view/lifecycle/localization and tests; original WPF,
assets, collectors and installed profiles remain protected. The same isolated,
ordinary, reversible routine classification applies. Full borderless editing,
wallpaper layer placement, saved geometry and Mac/Linux native acceptance remain
open; this is not full Desktop mode acceptance.
Headless, Windows native and full scripts/Validate.ps1 passed for this editor/lifecycle change. The narrow editing render was inspected; no Mac/Linux native or real-wallpaper visual acceptance is claimed.

### Explicit Desktop columns resize

Selecting 1/2/3 Desktop columns now uses the original DesktopMode cell-width
formula to resize the existing view; reopening applies the saved explicit count.
Auto keeps the current width. Ordinary opacity/font updates do not force a resize.
The current screen working area and scaling bound the width, and the horizontal
position is clamped after expansion. Core ColumnLayout still falls back to fewer
columns when the screen cannot fit the requested count. This is shared window
presentation only; installed profiles and original WPF/assets remain untouched.

Settings regression covers the requested expansion and working-area cap in
headless and Windows native sessions. An additional native regression confirms
changing topmost while locked preserves pass-through/no-activate flags and does
not unlock the Desktop; no production change was needed for that behavior.
Saved Desktop geometry, actual multi-monitor moves and platform acceptance remain
open. Same routine isolated/reversible presentation boundary; rollback source-only.
Full scripts/Validate.ps1 passed for explicit-column resizing.

### Desktop geometry persistence and recovery

The shared Desktop now uses the original 466x400 default and 280x140 minimum.
PreviewSettingsStore persists Desktop width/height in DIP and host x/y coordinates
in physical pixels, separately from App size. Monitor owns move/resize/close saving;
restoration suppresses intermediate geometry writes. Saved Desktop width takes
precedence on reopen, while explicit columns selection still resizes and saves it.

Restore clamps the view to a current monitor working area, falling back to the
primary monitor when the saved monitor is unavailable. Reset position opens the
existing Desktop and places it at the original 40/100 DIP offset, clamped to fit.
Settings remain preview-only, never overwriting the installed WPF geometry/profile.
This slice has the same routine reversible presentation/persistence boundary;
no new platform input, privileged calls, samplers or migration behavior.

Headless and Windows native regressions verify offscreen saved coordinates,
original minimum size, reset, move/resize/close save, and owner restart restoration.
Physical multi-monitor removal/reconnection, mixed-DPI movement and macOS/Linux
native sessions remain unverified. Full Desktop layer, borderless editing and
original Settings styling remain open. Rollback is source-only.
Full scripts/Validate.ps1 passed for Desktop geometry persistence.

### Shared borderless Desktop editor

Monitor and Desktop now consume the same WindowChrome resize regions. The helper
owns only eight edge/corner hit targets and their enabled/normal-window visibility;
window state, native input policy and persistence stay with each existing host.
Desktop is borderless both while editing and locked. Center dragging uses the
host BeginMoveDrag and excludes buttons, scrollbars and thumbs. The original move
hint is localized. Editor controls stay outside the readings ScrollViewer so long
lists cannot scroll Return to App out of reach. Locked resize grips are hidden.

Headless and Windows native regressions pass for shared grips, locking, reopening,
return, geometry, App titlebar and native pass-through flags. An isolated --demo
Windows computer-use session verified center drag from screen origin (60,150) to
(240,240), SE-corner resize from 466x400 to 416x350 DIP, Done hiding the editor,
Monitor reopening the same editor and Return to App. The session was closed with
exit 0 and used no personal profile. The narrow editor render was inspected.
Other edges, real wallpaper-layer placement, mixed-DPI monitors and native Mac/Linux
remain outside this acceptance. Original WPF/assets and installed profiles remain
unchanged. This is routine reversible shared presentation work; no data or privilege
boundary changed. Rollback is source-only.
Full scripts/Validate.ps1 passed for the shared borderless editor.

### Desktop metric visibility and order

Desktop Settings now owns independent desktopOrder/desktopVisible preferences for
the 18 currently connected hardware/network metric keys from DesktopOrder.cs.
Known order keys are normalized/deduplicated with missing defaults appended;
unknown boolean visibility entries survive save. The floating view orders existing
controls and changes their visibility without recreating them on polling. Only
visible rows consume layout cells. The editor provides recovery guidance when all
readings are hidden; collector status remains visible rather than hiding failures.

Settings changes apply to the current Desktop and persist independently of App
card choices. Accessible up/down controls provide ordering; original drag handles,
dynamic Linux sensor settings, custom hardware names, quota and FPS rows remain
open. No nonexistent provider/FPS readings have been invented. This is routine,
isolated reversible shared presentation/persistence work with unchanged sampler,
privilege, installed-profile and original WPF/assets boundaries.

Headless and Windows native regressions cover normalization, persistence, control
reuse across visibility/polling, compact layout without hidden gaps, all-hidden
recovery and App/Desktop independence. The narrow Settings render was inspected.
Rollback is source-only. Full original Settings layout and Desktop mode acceptance
remain open.
Full scripts/Validate.ps1 passed for Desktop metric visibility/order.

### Shared reorder handles

App card settings and Desktop metric settings now consume one ReorderHandle.
It previews row positions with transforms, preserves pointer capture, commits
canonical order only on drop, and cancels on Esc/capture loss/detachment. Up/down
keyboard operation and the existing accessible buttons remain available. App lock
is checked at gesture admission and during movement/commit. Edge movement scrolls
the enclosing Settings list. Labels/tooltips localize without exposing metric IDs.

Headless pointer events exercise App preview/drop, Esc cancellation, keyboard
reorder, persistence and independent Desktop drop. Existing Windows native
regressions pass, and the narrow Desktop Settings render was inspected. Native
physical drag gestures, original easing/scale motion, reduced-motion preferences
and multi-platform interaction remain unverified; this is not complete drag/UI
parity. Original WPF/CardDrag/assets, installed profile, quota and samplers are
unchanged. Routine reversible presentation boundary; rollback source-only.
Full scripts/Validate.ps1 passed for shared reorder handles.

### Windows native reorder acceptance

Verified against local HEAD 2bc7b5198704edc83ca5c130dbee5f9238320761 in the
isolated --demo session. Actual Windows pointer input moved Desktop CPU from
first position below Memory, producing GPU, VRAM, Memory, CPU, Drive 1, Drive 2.
Navigating to App Cards showed its independent GPU, Memory, CPU, NVMe, Airflow,
Network order; returning to Desktop retained the new metric order and scroll
position. No personal profile was used: demo explicitly reported session-only
changes. The demo window was closed through its titlebar.

This closes native pointer-drop/navigation acceptance for these two Settings
lists only. Native Esc cancellation, edge auto-scroll, restart persistence,
original motion, full 0.6.27 visual fidelity and other platforms are not established
by this session. Existing automated persistence/cancellation checks remain
separate evidence. No source/runtime change or release was made in this check.

### Original App geometry

Panel.xaml establishes the 0.6.27 App default of 280x650 and minimum 240x340 DIP.
Shared new/missing-size profiles now use those values; existing valid saved
geometry remains unchanged. PreviewSettingsStore and MonitorWindow own this
bounded change. Original WPF, Desktop geometry, installed profiles, samplers and
release state remain protected; rollback is source-only. The existing routine
classification applies to this isolated reversible presentation change.

Headless tests now exercise device text bounds at 240/280/360/800/1200 DIP and
font sizes 10/12/16. Keyboard Settings/Back, fixed navigation, retained cards and
live readings are exercised at the original minimum 240x340. Settings tests check
new-profile defaults and normalization. Headless and Windows native-session
regressions pass. The 240px device render and minimum-size Settings render were
inspected; this is geometry acceptance, not complete original styling parity.
Full scripts/Validate.ps1 passed for original App geometry. Protected WPF/native/assets diff remains empty against the 0.6.27 baseline.

### Device usage track fidelity

The 240px render exposed GPU/Memory track overflow. A red-capable rendered-bounds
regression reproduced x=-4 and width=200 inside a 192px card with Fluent ProgressBar.
Native/Cards.cs AddUsage instead owns a three-pixel Grid with proportional columns.
DeviceCard now follows that original structure, preserving accent/track colors,
rounded fill, current usage and unavailable visibility. Percent values are bounded
before constructing Grid lengths. This affects shared device presentation only;
quota, sampling, original WPF/assets and installed profiles are unchanged.

The regression requires both usage tracks to exist and checks their rendered
parts inside card bounds at 240/280/360/800/1200px. It failed before the repair and
passes after it; the full headless suite passes and the new 240px render was
inspected. Existing font-scale checks at 10/12/16 also pass. This closes the track
overflow defect, not complete card/Settings/native-material parity. Routine
isolated reversible implementation; existing admission facts remain applicable.
Full scripts/Validate.ps1 passed for device usage track fidelity; protected baseline source/assets diff remains empty.

### Monitor mode control appearance

Live, Session Max and Details now use the original Panel.xaml rounded plate
(radius 15, minimum height 28, border #496F829A), Shell's selected #607898A8
background and original hover/pressed/focus colors. Their existing ToggleButton
semantics remain; the custom template binds the plate/content to the owner.
Scope is these three Monitor controls only, not all Settings buttons. Existing
routine presentation risk facts and source-only rollback still apply; original
WPF/assets, sampling, providers and installed profiles are protected.

The 240px render was inspected. Headless keyboard regression verifies exclusive
Live/Session Max, repeated active Live activation, both Details transitions and
actual rendered plate selection/radius, alongside fixed Settings/Back navigation.
An initial assertion incorrectly compared transparent RGB channels; it now checks
zero alpha for the transparent state, while requiring the exact selected color.
Headless suite and full scripts/Validate.ps1 pass. Native pointer hover/pressed
and high-contrast visual acceptance remain open, as does full UI parity.

### Desktop quick action placement

Panel.xaml places DesktopQuick beside Live/Max/Details. The shared Monitor's
existing OpenFloatingMonitor action now occupies that fixed wrapping controls
row with the localized Desktop label, rather than the scrolling card body.
The rounded plate helper accepts Button as well as ToggleButton; checked styles
continue to apply only where the checked pseudo-state exists. No new Desktop
lifecycle, persistence or sampling owner was introduced.

At 240px the action wraps onto the next controls row. Headless keyboard tests
verify fixed position while cards scroll, opening Desktop and Return to App,
plus the previous exclusive mode and Details transitions. Full headless suite
passes and the narrow render was inspected. This is navigation placement parity;
wallpaper-layer mode, complete Settings and native pointer acceptance remain open.
Routine isolated presentation scope and source-only rollback are unchanged.
Full scripts/Validate.ps1 passed for Desktop quick action placement.

### Network rate unit parity

The original App Cards Network Speed Unit setting (auto, KB/s, MB/s, Mbit/s)
is now persisted by the isolated shared settings owner. MonitorSnapshot carries
nullable current/peak byte rates from the existing selected-interface session;
its presentation projection uses Core NetworkRate.Format. Monitor and Desktop
consume the same projected snapshot, including an already-open Desktop and
Session Max. No additional sampler, credential access or timer is introduced.
Failure clears current raw rates as well as text; historical peaks stay separate.
Demo raw values match its existing sample strings. String-only external fixtures
retain their supplied text instead of guessing/parsing numeric rates.

Headless and Windows native-session settings tests verify immediate changes in
both views, peak conversion and persistence. Core-session fixture verifies raw
conversion and invalid-rate handling; transient sampling regression still passes.
The App Cards render was inspected. English/SC/TC catalogs and font coverage pass.
The first localization run caught a blank catalog line introduced by the edit;
that line was removed and the full suite rerun successfully. Existing reorder,
visibility, network selection and settings error-preservation checks still pass.
Scope is shared presentation/preferences with ordinary data and unchanged platform
sampling/privilege boundaries; routine admission and source-only rollback apply.
Installed WPF profiles, original source/assets and release state remain protected.
Full scripts/Validate.ps1 passed for network rate unit parity. Protected baseline diff remains empty.

### Settings category headers

The dynamic LayoutSettings.BuildSettingsLayout reference uses wrapping rounded
category buttons, not Fluent underlines. Shared TabItem templates now retain
TabControl selection/keyboard semantics while restoring rounded plates, original
base/hover/focus colors, and bold/two-pixel selected emphasis. Content owners and
existing category indices remain unchanged. Missing FPS content is still an open
parity requirement, not represented as implemented by this header change.

Headless regressions verify arrow-key next/previous selection, actual plate radius
and selected state, header bounds at 240px, fixed Back/category navigation while
content scrolls, and retained view state. The 240px minimum Settings and 360px
App Cards renders were inspected. Full headless suite and scripts/Validate.ps1
pass. Native hover/high-contrast and full Settings content/layout parity remain
open. Routine isolated shared presentation change; source-only rollback, original
WPF/assets and installed-profile protection remain unchanged.

### Codex quota shared with Desktop

CodexQuotaPanel remains the sole QuotaSession/timer/adapter owner. It exposes its
current reading and change notification; Monitor projects that result into the
same snapshot delivered to Desktop, including reopen. Desktop renders every
AllWindows pool, unknown remaining as unavailable, and non-Live state instead of
stale percentages. Session Max never peaks quota. Desktop Settings has a Codex
quota visibility/order group; disabling provider clears its Desktop rows at once.
No authentication, adapter or scheduling semantics changed. Claude/Antigravity
shared integration and real-account acceptance remain open.

Synthetic quota tests cover additional pools, unknown values, a single reader,
login failure, disabling/cancellation and generation rejection. Owner integration
uses demo readings to verify already-open Desktop receives results and clearing.
Hiding quota reproduced an Avalonia Grid index exception: invisible retained rows
kept indices into removed row definitions. Resetting hidden row/column indices
before shrinking definitions closes that regression while preserving reuse.
The render also exposed Light-theme editor text on the dark Desktop backing;
Desktop now explicitly requests Dark for its editor, matching its fixed material.

Headless and Windows native-session tests passed; desktop-codex-quota.png was
inspected after the editor correction. Full scripts/Validate.ps1 passed; its WPF
checks are separate from shared headless/native evidence. Routine isolated shared
presentation with unchanged privilege/data access boundaries; source-only rollback.
Original WPF/assets and installed profiles remain protected. This is not full
Desktop mode or stable release acceptance.

### Provider recovery source audit

Read-only inspection of the preserved owner checkout (main HEAD
 a2867abfa8bb98c725c0201992dba8882f5b174d) found reusable adapter work rather than a
need to recreate providers:

- 7e96ee1 contains ClaudeFileLogin, ClaudeQuotaClient, QuotaJson, MacClaudeLogin,
  MacClaudeKeychain and synthetic Claude/Keychain tests. Its QUOTA-INTEGRATION
  document records a Native Review and a bounded restore-cleanup correction;
  that historical statement is not acceptance of a new transplanted diff.
- b579638 contains the modern Antigravity bridge: System.Management 10.0.0,
  linked Windows provider sources, bounded QuotaData bridge and host composition.
  Its Windows-only language-server discovery still requires the App to run.
- WindowsClaudeLogin.cs is staged-added, while DesktopQuotaReaders.cs and
  WindowsQuotaTests.cs are staged-modified in the owner checkout. These are
  preserved user work, not committed implementation covered by those SHA refs.
  Do not extract their working-tree contents as if they were committed baseline.

Next integration must use immutable Git source blobs for the committed adapters,
port only host wiring needed by the recovered 0.6.27 UI, and test independent
provider enable/disable, cancellation, complete windows and Desktop projection.
Do not cherry-pick the old UI or use historical review as new-diff approval.
Windows credential fallback needs separate provenance and validation. Read-only
credential acquisition/transport changes retain the quota risk/review boundary.
The native-review owner is available at the configured local skill location.
No credential store, live provider endpoint, owner checkout or installation was
changed during this audit. No provider stability or release completion is claimed.

### In-flight provider adapter restoration

Bounded implementation scope: restore committed provider adapters and focused
fixtures, then wire independent provider opt-ins into the existing shared panels
and Desktop snapshot projection. No old MonitorWindow/UI source is transplanted.
Protect original Native/Panel/assets, installed profiles, credential stores and
all staged work in the owner checkout. No login/refresh/write or live credential
probe is authorized by these fixtures. Rollback is removing this uncommitted port.
The full quota diff must pass deterministic tests and independent review before
commit/release; historical review is not a substitute.

<!-- sop-risk-classification: {"facts":{"blast_radius":"shared","change_kind":"implementation","data_boundary":"sensitive","destructive":"no","failure_cost":"material","irreversibility":"reversible","operational_controls":"not_applicable","privilege_boundary":"changed","project_policy":"default","rollback":"easy","scope_knowledge":"known","uncertainty":"material","verification":"deterministic"},"formal_review":"required","kind":"risk-classification-assessment","reasons":{"formal_review":["high_risk_requires_review"],"risk":["privilege_boundary_change"]},"risk":"high","schema_version":2} -->

Restored from immutable 7e96ee1 Git archive: Common/Modern ClaudeFileLogin,
ClaudeQuotaClient, QuotaJson; MacClaudeLogin/MacClaudeKeychain; ClaudeQuotaTests,
KeychainTests and the SDK ABI fixture. MacAdapterTests Program runs both restored
synthetic suites. `dotnet run --project scripts/MacAdapterTests/Pulse.Mac.Tests.csproj`
passes on Windows x64, including existing Codex transport regressions. This proves
portable fixture behavior, not native Mac Keychain compatibility or SDK ABI.
The new adapters are not wired into the host yet and remain uncommitted pending
integration/review. The archive and extracted reference are ignored vendor evidence.
A first source-copy attempt exposed that invoke-hidden writes Console output,
not pipeline objects; no source files were created by that failed capture. Git
archive provided the exact immutable files instead. Missing test assets were
resolved with normal project restore before successful fixture execution.

### Desktop editor theme ownership regression

Opening Desktop from a Light App overwrote FloatingMonitorWindow's fixed Dark
editor theme; changing App theme repeated that overwrite. The Settings regression
failed before the repair (`Desktop editor keeps dark theme when opened from Light
App`). MonitorWindow now leaves Desktop theme ownership with FloatingMonitorWindow.
The test covers opening under Light and switching to System while Desktop is open.
Shared headless and Windows native-session suites both pass after the two call-site
corrections. This is a bounded presentation repair, not full glass/UI acceptance.
Original WPF, installed profiles and in-flight provider restoration are unchanged.

### Desktop quick action enters monitoring rather than editing

Original Native/LayoutSettings.EnterDesktop locks Desktop and hides the App. Shared
Monitor previously sent the quick action to OpenFloatingMonitor, an editor entry.
The native regression failed on the missing lock/hide transition before repair.
The quick action now calls EnterDesktop, opening the existing singleton and hiding
App only after SetLocked(true) succeeds. Tray editing remains an unlocked entry.
Unsupported pass-through retains the visible owner and interactive editor.
Headless and Windows native-session suites pass: quick entry, same-window tray
restore, unlocked editing, Return, owner shutdown and native input flags. No
installed profile, original WPF source, quota adapter or release state changes.
This isolated reversible presentation transition classified routine with the
risk-classification script; no independent review required for this transition.
Persisted Desktop startup and wallpaper-layer parity remain open.

### Persistent Desktop mode lifecycle

Shared PreviewSettings now owns desktopEnabled (default false) and desktopLocked
(default true), mirroring the original Shell mode keys without touching installed
WPF profiles. MonitorWindow restores the saved mode once on first Opened, retains
one sampling lifetime, and saves editor/lock transitions. Successful Done hides
the owner; Return or editor close disables Desktop and restores the owner. Owner
shutdown cancels first so child closure preserves the selected mode for restart.
Unsupported pass-through restores an interactive editor and visible App.

DesktopModeTests uses an isolated temporary profile and synthetic source. Its
startup assertion failed before implementation. Headless and Windows native-session
suites now pass startup restore, locked/edit state, Done, Return persistence,
editor-close recovery, owner shutdown and subsequent reopen. This is ordinary
bounded UI/settings state, no credential or privilege-boundary changes; deterministic
risk-classification reported routine / formal_review not_required. Source-only
rollback; preserved WPF/assets, installed profile and pending quota port untouched.
Wallpaper attachment, full tray/settings parity and visual acceptance remain open.
Full scripts/Validate.ps1 also passed after the Desktop lifecycle changes; existing CS0649 DTO warnings remain. This validates the WPF baseline separately from the shared headless/native suites.

### Desktop Settings mode controls

Restored the original visible Desktop Mode checkbox and Edit Desktop Position
entry, with the original editing instruction and both Chinese translations.
The original hidden DesktopLocked control was deliberately not made visible;
Done remains on the Desktop editor. One settings-owned enabled state synchronizes
quick/tray entry, Settings disable, Return and editor closure without creating a
second window. Regression first failed on the missing DesktopEnabled control;
headless and Windows native-session suites now pass mode enable/disable and
external-entry/Return synchronization. desktop-settings.png was visually inspected.

During validation the Settings fixture exposed a timer race: its manually supplied
network sample could be replaced by background demo polling before the unit
assertion. It now supplies readings and initial interfaces explicitly, retaining
all assertions; worker lifecycle remains covered by the main Desktop suite.
A failed intermediate build from a duplicated edit anchor and an initial fixture
setup timeout were corrected before the passing runs. Full Settings section
layout, wallpaper layer, shortcuts and visual parity remain open. No release.

### Desktop Settings sections and responsive layout

Replaced the flat Desktop list with original Layout / Appearance / Readings
sections, expanded by default. SettingsSections uses the existing Core ColumnLayout
(350 DIP minimum, 10 DIP gap, hysteresis, up to three columns) and original
independent-column placement. Expander plates reproduce Panel.xaml rounded hover,
focus border, chevron and lower separator; Space toggles the existing section
without reconstructing controls. Slider values moved beside labels as in WPF.
Headless checks verify 240/840/1200 widths, section order, retained values after
collapse/reopen and immediate localization. Existing native mode/settings suite
also passes. The 240 and 1200 renders were inspected; full Settings parity remains
open. Initial two-column fixture at 800 exposed the retained Core hysteresis
(724 available < 726 threshold), so the test now uses an unambiguous width.
Reorder fixture now translates list coordinates into ScrollViewer content rather
than assuming its former direct-child Y coordinate; pointer reorder still passes.
The bounded shared presentation change retains ordinary reversible UI state and
no authentication, installed-profile or WPF modifications. No release performed.

### Shared Settings section renderer across categories

General, App Appearance / Window, App Cards and AI Quota now use the same original
section template and responsive layout as Desktop. App font/opacity values align
beside their labels; Window owns pin/position lock controls. Category indices,
control instances, language, quota opt-in and persistence contracts stay intact.
Headless tests verify the exact available section titles and bounds at 360/840,
keyboard collapse, saved values, drag reorder and sticky category/Back navigation.
The navigation fixture now identifies SettingsSections rather than its removed
StackPanel wrapper. App Appearance 840 and App Cards 360 renders were inspected.
Windows native-session and full scripts/Validate.ps1 pass. Existing CS0649 DTO
warnings remain. Original Native/Panel/assets diff is empty. This completes only
the available section structure, not missing FPS, hardware-name/color controls,
wallpaper layer or release acceptance. UI source/tests can be committed separately
from the pending high-risk provider restoration and its required review.

### Claude shared presentation integration (uncommitted, review pending)

Generalized the existing CodexQuotaPanel to QuotaPanel with an injected reader and
validated Codex/Claude provider identity. DesktopQuotaReaders now owns platform
composition, preserving the Codex adapter route. Claude uses the restored immutable
ClaudeQuotaClient with MacClaudeLogin on macOS and ClaudeFileLogin elsewhere.
No token refresh, login, credential writes or real-account probes were performed.
Windows Credential Manager fallback remains outside this port; no stability claim.

Monitor/Settings expose independent remembered Claude opt-in. Desktop receives the
same published result, with all windows, provider visibility/order, unavailable
values, current quota during Session Max and immediate disable/failure clearing.
Claude SVG is the existing asset, newly included as a shared resource. Existing
Settings section layout remains the owner; no historical UI was transplanted.

The quota lifecycle fixture now runs for both providers: no reads while disabled,
additional windows, unknown availability, one reader, safe failure replacement,
cancellation, late-result rejection and dispose. Owner integration tests prove
Claude enable/disable leaves Codex results intact and persists separately. Shared
headless and Windows native-session pass; desktop-claude-quota.png was inspected.
MacAdapterTests pass synthetic Claude HTTP and Keychain fixtures on Windows, not
native Mac acceptance. The full restored credential boundary remains high risk
under the earlier classification and must receive independent review before
commit/release. Antigravity bridge integration and real-account acceptance remain.

### Antigravity shared presentation integration (uncommitted, review pending)

Recovered AntigravityQuota, QuotaProviders, modern QuotaData bridge and
WindowsQuotaTests from immutable b579638 Git archive. Modern Windows now links
those established sources with pinned System.Management 10.0.0; normal restore
updated the affected lockfiles. QuotaJson uses the historical nullable directive.
Original Native/Panel/assets remain untouched; existing adapter source differences
are the historical cancellation, safe-status and modern-compiler compatibility fixes.

Antigravity joins QuotaPanel, host-only composition, independent remembered opt-in
and the shared Desktop snapshot projection. Unsupported platforms report an explicit
unavailable source. The Windows adapter still requires the running Antigravity App;
no independent cloud-login or background service is claimed or introduced.
All three provider lifecycle fixtures pass (synthetic only), including additional
windows, unknown values, failure clearing, disable cancellation and late results.
Windows bridge fixtures verify bounded parsing, cancellation and current-user WMI
ownership without reading login stores. Owner native-session checks verify the
Antigravity opt-in is separate, disabling clears its Desktop rows and retains other
providers, and its preference persists. Full scripts/Validate.ps1 passes; existing
CS0649 warnings remain. Desktop Antigravity render was inspected.

Review preparation instructions have been read: admission, mode selection, packet
preparation, quota selection and potential Antigravity/Pi controls. No reviewer has
been launched. Balanced preparation requires this host's established Token Monitor
snapshot/bindings paths; none were found in the bounded repo handoff/docs search.
Do not invent bindings, discover credentials, or silently switch to legacy auto.
The high-risk quota diff remains uncommitted pending this preparation and independent
review, plus real account/platform acceptance. Other UI/release gaps remain open.

### Focused quota presentation polish

Following the user's apple-design request, quota cards now use the recovered device
card palette, 12 DIP padding, a compact provider header, aligned label/value rows and
3 DIP tracks. Text inherits the App font preference; the provider heading scales with
it. Refresh remains a normal keyboard-focusable button. This is presentation-only:
it adds no blur/capture loop and does not change provider scheduling or login behavior.
The full Desktop headless suite passes, including new checks for all three providers
at 240 DIP width, 16 DIP text and Dark theme. Narrow Dark renders were generated and
the Codex render inspected. This evidence covers layout, not native glass acceptance.
The installed WPF build and personal profile were not modified.

### Original quota display selection

Native/QuotaView.cs and Panel.xaml establish the original contract: App defaults to
essential windows, can opt into all available windows, and Desktop always consumes
essential windows. Shared Settings now restores that choice with the `quotaFull`
preference, including both Chinese catalogs. QuotaPanel switches its presentation
from the existing result without new provider IO; DesktopReadings uses Windows only.
This supersedes the earlier all-pools Desktop fixture expectation. The three-provider
fixtures exercise both directions of the switch, essential Desktop under Session Max,
and unchanged cancellation/disable behavior. Headless checks pass. Settings persistence
and shared control wiring are also exercised by the native-session fixture.

### Quota freshness parity

Shared QuotaPanel previously skipped every tick when the provider returned the same
object. Its Live percentage could therefore remain displayed past the original WPF
ten-minute freshness boundary while a refresh was pending. Both shared views now
use QuotaPresentation's time-based status. An expired result loses its percentage and
progress bar; the owner publishes a sanitized stale status to Desktop without another
provider request. A fresh completion restores the readings. Non-Live results cannot
display live progress even if a provider supplies retained windows.

Synthetic clock/blocked-reader fixtures pass for all three providers at exactly ten
minutes, one tick beyond it, and recovery, with exactly two reads. The first full
headless run exposed a localization fixture missing its observation timestamp; the
fixture now supplies a current timestamp rather than weakening freshness behavior.
The subsequent complete headless suite and diff whitespace check pass. No credential,
network, installed profile or provider acquisition behavior changed in this correction.
Its deterministic classification is routine; this does not waive the independent
review still required for the surrounding high-risk provider restoration.

### Provider review preparation

Repository Validate.ps1 passes on this quota integration, including preserved WPF,
hardware/FPS, startup and updater regression. Existing updater CS0649 warnings remain.
MacAdapterTests passes on Windows x64 with synthetic Claude transport and Keychain
implementations; it is not macOS native ABI or real-account acceptance. The protected
src/Native, src/Panel.xaml and assets paths remain byte-identical to the 0.6.27 baseline.
The default balanced review cannot yet be prepared because the machine-local bound
snapshot/bindings sources have not been located. A route-selection question is pending;
no reviewer has been launched and no review has been claimed complete.

The review preparation gap was subsequently resolved using existing same-host
Token Monitor bindings and the explicit native Gemini source. The old Token Monitor
snapshot was rejected as stale. Balanced selection chose gemini-3.8-flash-high;
packet 5244387a169c3a6cc9fcc538ad2aeba7217fcbac24bc57ab443c4d7fe0612a77 completed
with zero findings against tree 1ccf455d2c17cc3baa30ed327cabf3754d564dd2. Commit
bf17d75 contains that exact tree. This closes the quota integration review step,
not real-account, native macOS, performance or release acceptance.

### App and Desktop reading colors

The shared ReadingPalette now owns the original hardware/unified palette used by
DeviceCard, QuotaPanel and Desktop icons. App Appearance restores Hardware Colors,
Unified Color and its color picker. Temperature values follow the selected palette;
labels and other small values remain neutral. Desktop restores its independent
text color picker and Icons follow App colors switch; turning it off makes icons
match Desktop text. Text opacity, background opacity, geometry and sampling are
unchanged. The editor controls keep their readable foreground regardless of the
selected metric color. This does not implement Auto Contrast or Local Contrast.

The change is limited to shared presentation, optional profile fields and tests.
Invalid hex colors fall back independently, and unknown profile fields survive.
No WPF source, installed profile, credential reader or release is changed. Rollback
is the corresponding palette commit. Risk classification is routine (known shared
presentation, deterministic checks, easy rollback, ordinary data, unchanged
privilege boundary); no independent model review is required.

ReadingPaletteTests and all three provider fixtures verify live recoloring without
another poll, independent Desktop text, topmost retention and persistence. The
complete shared headless suite and repository Validate.ps1 passed; Monitor and
Desktop palette renders were inspected. The Settings keyboard fixture now selects
the named section header because the restored checkbox also inherits ToggleButton.
This preserves its collapse/focus assertions. These checks do not close the full
UI parity or installed performance gates.

### Desktop Local Contrast

The shared Desktop restores the opt-in Windows Local Contrast control and its
15-second Screenshot mode. WindowsBackgroundCapture owns bounded, reusable GDI
pixels behind the owned Desktop HWND; Core ContrastAnalysis owns analysis, and
WindowsLocalContrast owns presentation/cadence. Pixels remain in memory and are
cleared on disposal; no capture is saved or sent. Demo, smoke, measurement and
unsupported sessions cannot enable capture. Existing personal profiles and WPF
source are not changed.

Analysis includes the actual Desktop surface color/opacity, so a dark surface over
light wallpaper retains light text. Text opacity is applied once by the metric
container. App-colored icons retain their palette with contrast edges; disabling
that preference permits adaptive monochrome icons. Screenshot mode restores
capture visibility and freezes current colors before automatically resuming.
Hiding, disabling and closing stop capture and restore the owned affinity.
Late sampling results cannot recolor a disabled or moved panel. Manual text color
selection disables Local Contrast, preserving the original explicit-color behavior.

The bounded change covers Desktop presentation/settings, Windows capture and
corresponding native/headless tests. Risk classification is routine: known scope,
deterministic native verification, easy source rollback, unchanged privileges;
in-memory background pixels are sensitive and remain local. No model review is
required. Rollback is the source change; no installed settings are migrated.

`DesktopTests --native-contrast` passed native self-exclusion, foreign-window
rejection, buffer reuse/clearing, GDI lifetime, dark/light backgrounds, surface
opacity, text opacity, icon colors, hide/show, Screenshot freeze/recovery and
disable cleanup. It is also included in `--native-session`. Repository
`Validate.ps1` passed. These checks do not establish overall UI parity or the
CPU/RAM cost of opt-in contrast; comparable resource measurements remain required.

The shared headless suite and Settings section renders also passed. An initial
full native-session run failed after Screenshot recovery (active status but no
light text on the changed dark fixture). A diagnostic build added ink, surface,
geometry and a single pixel value on failure without saving screenshots/pixels.
The subsequent focused and full native-session runs passed; the intermittent
failure has not been attributed or proven fixed and remains an acceptance item.
The 840-DIP Desktop Settings render was inspected for section ownership and
readable status/control layout. No stable release is authorized by these results.
Three bounded diagnostic runs of the same native capture/Screenshot fixture also
passed on 2026-09-20. No failing diagnostic sample was obtained; this narrows no
root cause and does not erase the earlier intermittent failure. Keep that item in
final native acceptance while continuing other parity work.

### App custom background color

App Appearance restores the original background color picker using the existing
shared ColorPicker and the original `background` profile key. Custom tint keeps
MaterialPolicy opacity and native backdrop behavior. The original luminance rule
selects Light/Dark foreground, card and control appearance; the Theme selector
reflects that choice. Choosing a different theme or Use theme background restores
the theme tint. High contrast ignores custom tint. Invalid hex falls back without
discarding other preferences; foreground opacity remains independent.

Scope is shared App presentation, an optional profile field, catalogs and material
regression coverage. Defaults, installed profiles, WPF, collectors, quota and
release identity remain unchanged. Rollback is this source change. Classifier
returned routine / not_required (known shared presentation, deterministic checks,
easy rollback, low failure cost, ordinary data, unchanged privilege boundary).

Material tests passed custom white/dark readable foreground, theme synchronization,
reset, saved tint, invalid input, unknown-field preservation and all existing
opacity/lock/solid checks. Full shared regression and repository Validate passed
before the final Theme-selector synchronization correction; the affected material
fixture passed again afterwards. Final shared regression is being rerun.
Final shared regression passed after Theme-selector synchronization; the light Settings render was inspected with a matching Light selection and readable dark text.

### Desktop tray recovery actions

The shared tray now exposes Done (lock), Return to App and Screenshot mode in
addition to the existing Open/Edit action. Commands query the existing Desktop
instance, never create another sampling owner, and recheck capability/lifetime
at execution. NativeMenu.NeedsUpdate refreshes enabled states before presentation.
Done requires working pass-through support; Screenshot requires opt-in capture
in a supported live session. Closing/disposal invalidates stale callbacks.

This routine presentation change preserves existing Open/Quit and maximized
restore behavior. Focused headless and Windows-native tray/Desktop fixtures passed
lock/edit, Return, unsupported/demo rejection, singleton and lifetime checks.
Screenshot capture/recovery itself retains the separate native evidence and the
unattributed intermittent failure recorded above. Installed state is unchanged.
Repository Validate.ps1 passed after the tray changes; the focused native fixture exercised the real window lock/edit/restore transitions.
