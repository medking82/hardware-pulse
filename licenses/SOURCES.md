# Dependency provenance

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
