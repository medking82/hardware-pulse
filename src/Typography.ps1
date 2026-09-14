# Keep glyphs at their requested size; reflow layout instead of scaling a bitmap.
$script:cardType=@()
function Register-CardType($node){
    if($node -is [Windows.Controls.TextBlock]){
        $local=$node.ReadLocalValue([Windows.Controls.TextBlock]::FontSizeProperty)
        $base=if($local -eq [Windows.DependencyProperty]::UnsetValue){12.0}else{[double]$local}
        $script:cardType+=,@($node,$base)
    }
    if($node -is [Windows.DependencyObject]){
        foreach($child in [Windows.LogicalTreeHelper]::GetChildren($node)){Register-CardType $child}
    }
}
Register-CardType $cards
function Update-CardType {
    $scale=$window.FontSize/12.0
    foreach($entry in $script:cardType){$entry[0].FontSize=[Math]::Round($entry[1]*$scale,2)}
}
function Update-CardHeaders {
    foreach($key in @('cpu','gpu','system')){
        $value=$script:cells[$key][0];$header=$value.Parent;$name=$header.Children[0]
        if($header.RowDefinitions.Count -eq 0){
            1..2 | ForEach-Object {$row=[Windows.Controls.RowDefinition]::new();$row.Height='Auto';$header.RowDefinitions.Add($row)}
        }
        $name.Measure([Windows.Size]::new([double]::PositiveInfinity,[double]::PositiveInfinity))
        $value.Measure([Windows.Size]::new([double]::PositiveInfinity,[double]::PositiveInfinity))
        $card=$header.Parent.Parent
        $width=[Math]::Max(1,$window.FindName('CardScroll').ActualWidth-36-$card.Padding.Left-$card.Padding.Right-2)
        $stacked=$name.DesiredSize.Width+$value.DesiredSize.Width+10 -gt $width
        [Windows.Controls.Grid]::SetRow($value,$(if($stacked){1}else{0}))
        [Windows.Controls.Grid]::SetColumn($value,$(if($stacked){0}else{1}))
        [Windows.Controls.Grid]::SetColumnSpan($value,$(if($stacked){2}else{1}))
        $value.HorizontalAlignment=if($stacked){'Left'}else{'Right'}
        $value.Margin=if($stacked){[Windows.Thickness]::new(0,3,0,0)}else{[Windows.Thickness]::new(10,0,0,0)}
    }
}
