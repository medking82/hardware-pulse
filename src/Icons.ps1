function New-PulseIcon([string]$Name,[double]$Size=18,[string]$Color='#C2D8E5') {
    [xml]$svg=[IO.File]::ReadAllText((Join-Path $PSScriptRoot "assets/$Name.svg"))
    $path=[Windows.Shapes.Path]::new()
    $path.Data=[Windows.Media.Geometry]::Parse([string]$svg.svg.path.d)
    $path.Stroke=[Windows.Media.BrushConverter]::new().ConvertFromString($Color)
    $path.StrokeThickness=1.7;$path.StrokeStartLineCap='Round';$path.StrokeEndLineCap='Round';$path.StrokeLineJoin='Round'
    $canvas=[Windows.Controls.Canvas]::new();$canvas.Width=24;$canvas.Height=24
    $null=$canvas.Children.Add($path)
    $box=[Windows.Controls.Viewbox]::new();$box.Width=$Size;$box.Height=$Size;$box.Child=$canvas
    $box.IsHitTestVisible=$false
    return $box
}
