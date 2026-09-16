Pulse Desktop preview

English. Simplified Chinese: README.zh-CN.txt.

Launch
Choose the archive for your operating system and CPU architecture (x64 or ARM64).
Extract the entire archive into a separate folder; do not mix versions or copy only the executable.
Windows: run Pulse.Desktop.exe. This does not replace the stable WPF installer.
Linux: run ./Pulse.Desktop in a graphical desktop. X11 is tested; Wayland-native interaction is not verified.
macOS: open Pulse Preview.app. Keep its Contents directory intact when moving the App.
The .NET runtime is included; native OS graphics and font libraries are still required.

Features
Live CPU/RAM and download/upload rates for the selected interface refresh once per second.
Settings > Network > Refresh interfaces discovers newly connected adapters without restarting.
A missing saved interface stays unselected; reconnect and refresh, or choose another interface.
Linux displays kernel-exposed temperature and fan channels when available. Missing readings display an em dash, not zero.
Session Max shows CPU, selected-network and exposed Linux sensor peaks. RAM and quota remain current.
Codex quota is optional and off by default. It reads an existing file login without changing credentials.
Settings remember window size, theme, network choice and Codex opt-in in a separate preview profile.
Settings > Appearance > Language offers Auto (System), English, Simplified Chinese and Traditional Chinese. UI and tray labels change immediately; device names and values are preserved.
Tray/menu bar offers Open Pulse and Quit Pulse where supported. Closing the window quits.

Limits
This is an experimental preview, not feature parity with the stable Windows App.
GPU telemetry, FPS, Desktop overlay, click-through, blur/local contrast, global shortcuts,
startup registration, automatic updates and other quota providers are not connected.
Temperature/fan channels are currently supported only through Linux hwmon in this shared App.
Windows CPU requires a single processor group; macOS RAM is an estimate of used memory.
No installer or automatic update is performed. Replace the extracted folder to update while the App is closed.
Windows preview binaries are not publisher-signed. The macOS bundle is not Developer ID signed or notarized.
CI launch checks do not verify download quarantine/Gatekeeper behavior. Do not disable OS security to launch.

Verification and notices
Compare the archive SHA-256 with its sidecar before extraction. The internal manifest lists source commit and file hashes.
Notices are in licenses/ on Windows/Linux and Contents/Resources/licenses/ inside the macOS App.
These guides also travel inside Contents/Resources/ on macOS.
