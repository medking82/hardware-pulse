Pulse Desktop preview

English. Simplified Chinese: README.zh-CN.txt.

Launch
Choose the archive for your operating system and CPU architecture (x64 or ARM64).
Extract the entire archive into a separate folder; do not mix versions or copy only the executable.
Windows: run Pulse.Desktop.exe. This does not replace the stable WPF installer.
Linux: run ./Pulse.Desktop in a graphical desktop. X11 is tested; Wayland-native interaction is not verified.
macOS: open Pulse Preview.app. Keep its Contents directory intact when moving the App.
The .NET runtime is included; native OS graphics and font libraries are still required.
These Linux archives use glibc; they are not Alpine/musl packages. Chinese UI fonts are embedded.
The shared .NET 10 host does not target Windows 7 or Windows 8.1.
Native CI covers Windows Server 2022 x64, Windows 11 ARM64, Ubuntu 24.04 x64/ARM64 and macOS 15 x64/ARM64.
Framework support for another OS version does not establish Pulse device or desktop compatibility.
Current runtime OS matrix: https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md
UI backend requirements: https://docs.avaloniaui.net/docs/supported-platforms

Features
Live CPU/RAM and download/upload rates for the selected interface refresh once per second.
Settings > Network > Refresh interfaces discovers newly connected adapters without restarting.
A missing saved interface stays unselected; reconnect and refresh, or choose another interface.
Linux displays kernel-exposed temperature and fan channels when available. Missing readings display an em dash, not zero.
Session Max shows CPU, selected-network and exposed Linux sensor peaks. RAM and quota remain current.
Codex quota is optional and off by default. It reads an existing file login without changing credentials.
Claude and Antigravity have independent opt-ins. Claude reads existing login; Antigravity requires the current user's running Windows language server. Antigravity is explicitly unavailable on other platforms. Pulse does not sign in or renew tokens.
Settings remember window size, theme, network choice and each quota opt-in in a separate preview profile.
Settings > Appearance > Language offers Auto (System), English, Simplified Chinese and Traditional Chinese. UI and tray labels change immediately; device names and values are preserved.
Tray/menu bar offers Open Pulse and Quit Pulse where supported. Closing the window quits.
Open floating monitor is available from Monitor and the tray/menu bar. Both reuse the same window and readings.
The floating monitor supports Always on top and native move/resize. Closing it leaves Monitor running; quitting Monitor closes both.
Settings > Desktop controls font size, Always on top, background opacity and system blur. Preferences and floating size/position are remembered. Opacity changes only the background, not text. Blur status reports the achieved backend effect; its strength is controlled by the OS.
Enabled quota providers appear in the floating monitor using their existing sessions. Disabling one preserves the others. Locking hides editing controls; reopening restores them.
On Windows/macOS and Linux X11 with XFixes, Lock floating monitor requests native mouse input pass-through. Reopen it from Monitor or the tray/menu bar to unlock. X11 temporarily removes the interactive window frame and restores it on unlock. Windows ARM64 interactive validation remains incomplete; it is not included in this preview update. Native Wayland locking is not implemented.

Limits
This is an experimental preview, not feature parity with the stable Windows App.
GPU telemetry, FPS, complete Desktop overlay, adjustable blur radius/local contrast, global shortcuts,
startup registration and automatic updates are not connected.
Temperature/fan channels are currently supported only through Linux hwmon in this shared App.
Windows CPU requires a single processor group; macOS RAM is an estimate of used memory.
No installer or automatic update is performed. Replace the extracted folder to update while the App is closed.
Windows preview binaries are not publisher-signed. The macOS bundle is not Developer ID signed or notarized.
CI launch checks do not verify download quarantine/Gatekeeper behavior. Do not disable OS security to launch.

Verification and notices
Compare the archive SHA-256 with its sidecar before extraction. The internal manifest lists source commit and file hashes.
Notices are in licenses/ on Windows/Linux and Contents/Resources/licenses/ inside the macOS App.
These guides also travel inside Contents/Resources/ on macOS.
