# Dependency provenance

- PresentMon 2.5.1 console: https://github.com/GameTechDev/PresentMon/releases/tag/v2.5.1
  MIT; original license and third-party notices included. Official x64 binary is hash-pinned.

- LibreHardwareMonitor v0.9.6: https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/tree/v0.9.6
  MPL-2.0. The exact unmodified source archive is included in the installer.
- PawnIO 2.2.0: https://github.com/namazso/PawnIO/tree/2.2.0
  GPL-2.0. Official installer: https://github.com/namazso/PawnIO.Setup/releases/tag/2.2.0
  Source tag commit: 5cdf470831fdfff3f7f1d06363ca6b230f3bf35a. The source archive is included;
  to build it, clone the tag with `--recurse-submodules` as required by upstream.
- LibreHardwareMonitor's transitive library versions are provided by its pinned official release
  and source project manifests. See the included upstream notices and project files.
- Inno Setup 6.7.3 compiler: https://github.com/jrsoftware/issrc/releases/tag/is-6_7_3
  Build tool only. This personal, non-commercial build does not bundle the compiler.
- Segoe UI is supplied by Windows; no font files are redistributed.
- Hardware Pulse SVG line icons and Pulse app artwork are original project assets.
# Token Monitor quota parsing

Quota response mappings are adapted from Javis603/token-monitor under the MIT
license. Copyright (c) 2026 Javis. See TokenMonitor.txt. Only response mappings and
documented provider request shapes are reused; Pulse has its own native C# runtime.
Source: https://github.com/Javis603/token-monitor (src/shared/limitCollector.js,
src/shared/antigravityProbe.js).
Reference checkout commit: 1e2c03d2a55b5eef97c7732341281415c2a3d7ea.

Provider SVG silhouettes (Codex/OpenAI, Claude, Antigravity) are adapted from
LobeHub Icons as vendored by the same Token Monitor checkout. Antigravity uses
the mask silhouette for themeable monochrome rendering. See LobeIcons-MIT.txt
(copyright 2023 LobeHub), https://github.com/lobehub/lobe-icons.
Provider names and marks belong to their respective owners; no affiliation implied.
# Shared Desktop preview dependencies

The self-contained preview uses Avalonia 12.1.2 and .NET 10.0.12. Vendored
upstream notices were read from these versioned sources:

- Avalonia-MIT.txt and Avalonia-NOTICE.md: https://github.com/AvaloniaUI/Avalonia/tree/12.1.2
- DotNet-MIT.txt and DotNet-NOTICES.txt: https://github.com/dotnet/runtime/tree/v10.0.12
- MicroCom-MIT.txt: https://github.com/kekekeks/MicroCom/blob/76785efcafd91b5902fd19dd11145f6dd655b7b4/LICENSE
- SkiaSharp-MIT.txt: LICENSE.txt in the locked SkiaSharp 3.119.4 NuGet package.
- HarfBuzzSharp-MIT.txt: LICENSE.txt in the locked HarfBuzzSharp 8.3.1.3 NuGet package.

- SkiaSharp-HarfBuzzSharp-NOTICES.txt: unmodified THIRD-PARTY-NOTICES.txt from
  SkiaSharp.NativeAssets.Linux 3.119.4. The corresponding macOS package and
  HarfBuzzSharp.NativeAssets.Linux/macOS 8.3.1.3 contain identical bytes.
  Notice SHA-256: `21504c46c4c58aa64c1055bd2dcbc5f9a136b4b8c412ed3cc6740e22c5b127f5`.
  NuGet cache contentHash metadata for all four packages matches packages.lock.json.
  This is the upstream combined notice bundle, including native Skia/HarfBuzz
  and their third-party material; it is not replaced by the wrapper MIT licenses.

These supplement the existing Pulse and icon/decoder notices. The package builder
copies these original notices, and verification rejects missing or empty required
license files even when an archive manifest otherwise matches. Dependency updates
must revisit the versioned upstream notice bundle. CI archives remain development
validation artifacts; inclusion of notices does not imply feature or release readiness.
