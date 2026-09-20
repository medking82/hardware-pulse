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
preview profiles start dark with 30% background opacity; existing explicit
Light/System/Dark preferences remain supported. Solid mode and an unsupported
platform produce an opaque background without overwriting the preference or
fading foreground text. Settings content remains opaque. The native request
prefers AcrylicBlur, then Blur, then transparency. High contrast requests an
opaque surface; native high-contrast appearance still requires acceptance.

Build, headless Desktop regression, and Windows native-session regression pass.
The repository's complete `scripts/Validate.ps1` also passes on this change;
the WPF presentation, Panel.xaml and original assets still match v0.6.27.
AppMaterialTests cover saved preferences, zero/full opacity, solid fallback,
foreground independence, opaque Settings and unknown-field preservation. An
isolated Windows demo was visually inspected: the backdrop is active, but the
old preview card hierarchy and navigation visibly remain. Do not label this
as WPF visual parity. The demo was closed without changing installed profiles.

The Windows input fixture passed once and later failed its immediate red-pixel
assertion after pass-through hit testing succeeded. The fixture now waits up
to its existing five-second deadline for that exact red pixel and rechecks
pass-through afterwards. The subsequent native suite passed. This addresses
the compositor observation timing, without relaxing the pixel or ownership
assertion; it is not evidence for other machines or platforms.

<!-- sop-risk-classification: {"facts":{"blast_radius":"isolated","change_kind":"implementation","data_boundary":"ordinary","destructive":"no","failure_cost":"low","irreversibility":"reversible","operational_controls":"not_applicable","privilege_boundary":"unchanged","project_policy":"default","rollback":"easy","scope_knowledge":"known","uncertainty":"low","verification":"deterministic"},"formal_review":"not_required","kind":"risk-classification-assessment","reasons":{"formal_review":["routine_no_review"],"risk":["no_high_risk_signal"]},"risk":"routine","schema_version":2} -->
