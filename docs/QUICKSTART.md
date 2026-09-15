# Pulse visual quick guides / 操作示意图

These images use the real WPF interface and shipped SVG icons with fictional demo data. They do not include personal readings or account information. [Render source](../scripts/Render-Hero.ps1).

## Desktop: move, resize, lock

Choose **Edit Desktop** from the system tray. Drag a reading to move the panel, or an edge/corner to resize. Click **Lock Desktop** to finish; unlock again through the tray. Locked panels let clicks pass through.

在 system tray 选择 **Edit Desktop**，拖动文字移动面板，拖动边缘或角落 resize。完成后点击 **Lock Desktop**；再次编辑时从 tray 解锁。

![Desktop move, resize and lock guide](showcase/desktop-edit-guide.png)

## Screenshot mode

Local Contrast can exclude Pulse from captures. Select **Screenshot mode · 15 seconds** in the tray or **Settings → Desktop**. Press **Win + Shift + S** within 15 seconds. Current colors are frozen temporarily, then Local Contrast resumes automatically.

Local Contrast 开启后如截图看不到 Pulse，可先开启 **Screenshot mode**，15 秒内按 **Win + Shift + S**；随后自动恢复。

![Screenshot mode guide](showcase/screenshot-guide.png)

## App controls and FPS

**Details** reveals extra readings. **Desktop** opens the wallpaper panel. The App **FPS** switch controls the separate FPS overlay. To show FPS inside Desktop without that overlay, enable its reading in **Settings → Desktop**; choose the capture target in **Settings → FPS**. FPS badges mean current, rolling average and rolling minimum—not 1% Low.

**Details** 显示更多读数。Desktop 内的 FPS reading 可独立开启，无需开启单独的 FPS overlay。`NOW / AVG / MIN` 分别为当前、滚动平均和滚动最低 FPS。

![App controls and FPS guide](showcase/app-controls-guide.png)
