# GitHub hero

`pulse-hero.png` is generated from the built native App and Desktop windows by
`scripts/Render-Hero.ps1`. Cards, controls, typography and SVG icons come from
the shipped runtime. Hardware and quota data are fictional fixtures, with no
live credentials, collector, account queries or user settings involved.

The soft background and headings are a presentation composition. This is not
a desktop screenshot or a capture of Windows DWM blur. App transparency comes
from its existing appearance settings. The renderer uses WPF at 2x resolution
for the UI surfaces and exports a 1920 x 1280 PNG.

After building the current native App, run in Windows PowerShell 5.1 STA:

```powershell
powershell.exe -NoProfile -STA -File scripts/Render-Hero.ps1
```

Inspect the output for clipping, missing readings, legibility and correct icons
before updating GitHub. The script infers the displayed version from the binary.
Temporary fixture state stays in ignored `vendor/hero-*` directories.

The older `pulse-demo.png` remains for historical release-note references.
