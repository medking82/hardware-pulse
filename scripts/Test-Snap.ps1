$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase
Add-Type -Path @("$PSScriptRoot\..\src\WindowSnap.cs","$PSScriptRoot/WindowSnapDpiTests.cs") -ReferencedAssemblies @('PresentationFramework','PresentationCore','WindowsBase','System.Xaml')
[WindowSnapDpiTests]::Run()
'PASS: native per-monitor DPI and legacy WPF DPI fallback'
function Rect($l,$t,$r,$b){$v=[WindowSnap+Rect]::new();$v.Left=$l;$v.Top=$t;$v.Right=$r;$v.Bottom=$b;return $v}
$work=Rect -1920 0 0 1080
$none=[WindowSnap+Rect[]]@()
$r=[WindowSnap]::Snap((Rect -1914 5 -1634 655),$work,$none,12)
if($r.Left -ne -1920 -or $r.Top -ne 0 -or ($r.Right-$r.Left) -ne 280){throw 'Screen edge / negative monitor coordinates failed'}
$target=[WindowSnap+Rect[]]@((Rect -1600 0 -800 800))
$r=[WindowSnap]::Snap((Rect -1885 100 -1605 750),$work,$target,12)
if($r.Right -ne -1600){throw 'Window adjacency failed'}
$r=[WindowSnap]::Snap((Rect -1885 850 -1605 1050),$work,$target,12)
if($r.Right -ne -1605){throw 'Non-overlapping windows must not attract'}
$r=[WindowSnap]::Snap((Rect -1800 100 -1520 750),$work,$none,12)
if($r.Left -ne -1800 -or $r.Top -ne 100){throw 'Outside snap threshold should remain free'}
$origin=Rect -1920 100 -1640 750
foreach($distance in @(1,4,8,12,16,24,40)){
    $desired=Rect ($origin.Left+$distance) $origin.Top ($origin.Right+$distance) $origin.Bottom
    $actual=[WindowSnap]::Snap($desired,$work,$none,12)
    $expected=if($distance -le 12){-1920}else{-1920+$distance}
    if($actual.Left -ne $expected){throw "Dragging away stuck at distance $distance"}
}
$neighbor=[WindowSnap+Rect[]]@((Rect 800 100 1100 750))
$largeWork=Rect 0 0 1920 1080
$r=[WindowSnap]::Snap((Rect 500 95 780 743),$largeWork,$neighbor,12)
if($r.Top -ne 100){throw 'Side-by-side top alignment failed'}
$r=[WindowSnap]::Snap((Rect 500 200 780 743),$largeWork,$neighbor,12)
if($r.Bottom -ne 750 -or ($r.Bottom-$r.Top) -ne 543){throw 'Side-by-side bottom alignment failed'}
$r=[WindowSnap]::Snap((Rect 400 200 680 743),$largeWork,$neighbor,12)
if($r.Bottom -ne 743){throw 'Distant windows must not align'}
'PASS: screen edges, adjacent windows, top/bottom alignment, negative coordinates and free movement'
foreach($edge in @('left','right')){
    foreach($distance in @(18,24,25,48)){
        $left=if($edge -eq 'left'){$distance}else{1920-280-$distance}
        $r=[WindowSnap]::Snap((Rect $left 100 ($left+280) 700),$largeWork,$none,24)
        $expected=if($distance -gt 24){$left}elseif($edge -eq 'left'){0}else{1640}
        if($r.Left -ne $expected){throw 'Strong side snap or release failed'}
    }
}
'PASS: stronger left/right attraction and release outside the magnet range'

foreach($side in @('left','right')){
    $origin=if($side -eq 'left'){Rect 0 100 280 700}else{Rect 1640 100 1920 700}
    for($step=1;$step -le 80;$step++){
        $dx=if($side -eq 'left'){$step}else{-$step}
        $raw=[WindowSnap]::DragRect($origin,100,150,(100+$dx),150)
        $actual=[WindowSnap]::Snap($raw,$largeWork,$none,24)
        if($step -gt 24 -and $actual.Left -ne $origin.Left+$dx){throw 'Continuous small-step drag remains trapped'}
        if($actual.Right-$actual.Left -ne 280){throw 'Dragging changed width'}
    }
}
'PASS: cursor-anchored continuous one-pixel steps escape both screen edges without Alt'
$raw=Rect 24 24 224 124;$target=Rect 0 0 200 100
$soft=[WindowSnap]::Attract($raw,$target,24)
if($soft.Left -ne 24 -or $soft.Top -ne 24){throw 'Magnet entry jumps at threshold'}
$raw=Rect 2 2 202 102;$soft=[WindowSnap]::Attract($raw,$target,24)
if($soft.Left -ne 0 -or $soft.Top -ne 0 -or $soft.Right-$soft.Left -ne 200){throw 'Near-edge magnet precision or size changed'}
