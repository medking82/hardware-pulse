param([Parameter(Mandatory=$true)][string]$OutputPath)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase
$visual=[Windows.Media.DrawingVisual]::new();$dc=$visual.RenderOpen()
$dc.DrawRoundedRectangle([Windows.Media.SolidColorBrush]::new([Windows.Media.ColorConverter]::ConvertFromString('#182B3B')),$null,[Windows.Rect]::new(8,8,240,240),56,56)
$pen=[Windows.Media.Pen]::new([Windows.Media.SolidColorBrush]::new([Windows.Media.ColorConverter]::ConvertFromString('#A5E7D5')),16)
$pen.StartLineCap='Round';$pen.EndLineCap='Round';$pen.LineJoin='Round'
$dc.DrawGeometry($null,$pen,[Windows.Media.Geometry]::Parse('M48 132H88L112 68L148 188L172 132H208'));$dc.Close()
$bitmap=[Windows.Media.Imaging.RenderTargetBitmap]::new(256,256,96,96,[Windows.Media.PixelFormats]::Pbgra32);$bitmap.Render($visual)
$encoder=[Windows.Media.Imaging.PngBitmapEncoder]::new();$encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
$memory=[IO.MemoryStream]::new();$encoder.Save($memory);$png=$memory.ToArray();$memory.Dispose()
$stream=[IO.File]::Create($OutputPath);$writer=[IO.BinaryWriter]::new($stream)
try {
    $writer.Write([UInt16]0);$writer.Write([UInt16]1);$writer.Write([UInt16]1)
    $writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([byte]0)
    $writer.Write([UInt16]1);$writer.Write([UInt16]32);$writer.Write([UInt32]$png.Length);$writer.Write([UInt32]22);$writer.Write($png)
} finally {$writer.Dispose()}
