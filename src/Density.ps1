$script:showDetails=[bool]$saved.details
$script:densityCards=@($cards.Children)
$script:compactGroups=@()
foreach($group in @(@(0,@(@('Load','cpuLoad'),@('Fan','cpuFan'),@('Vcore','vcore'))),@(1,@(@('Load','gpuLoad'),@('Fan','gpuFan'),@('VRAM Temp','vram'),@('Voltage','gpuVolt'))))){
    $card=$script:densityCards[$group[0]]
    $grid=[Windows.Controls.Grid]::new();$grid.Margin='0,2,0,0';$grid.Visibility='Collapsed'
    $grid.ColumnDefinitions.Add([Windows.Controls.ColumnDefinition]::new());$grid.ColumnDefinitions.Add([Windows.Controls.ColumnDefinition]::new())
    $rows=[Collections.Generic.List[object]]::new()
    for($i=0;$i -lt $group[1].Count;$i++){
        if($i%2 -eq 0){$grid.RowDefinitions.Add([Windows.Controls.RowDefinition]::new())}
        $pair=$group[1][$i];$original=$script:cells[$pair[1]][0];$rows.Add($original.Parent)
        $line=[Windows.Controls.StackPanel]::new();$line.Orientation='Horizontal';$line.Margin='0,1,5,1'
        [Windows.Controls.Grid]::SetRow($line,[int][Math]::Floor($i/2));[Windows.Controls.Grid]::SetColumn($line,$i%2)
        $label=[Windows.Controls.TextBlock]::new();$label.Text=$pair[0];$label.FontSize=10;$label.Margin='0,0,4,0';$label.VerticalAlignment='Center'
        $value=[Windows.Controls.TextBlock]::new();$value.FontSize=11;$value.FontWeight='SemiBold'
        $binding=[Windows.Data.Binding]::new('Text');$binding.Source=$original
        $null=$value.SetBinding([Windows.Controls.TextBlock]::TextProperty,$binding)
        $line.Tag=$pair[1]
        $null=$line.Children.Add($label);$null=$line.Children.Add($value);$null=$grid.Children.Add($line)
    }
    $card.Child.Children.Insert(2,$grid)
    $script:compactGroups+=@{grid=$grid;rows=$rows}
}
function Set-CardDensity([int]$level) {
    if(-not $script:densityCards){return}
    if(Get-Command Update-CardType -ErrorAction SilentlyContinue){Update-CardType}
    $typeScale=$window.FontSize/12.0
    $compact=$level -ge 2; $tight=$level -ge 1
    foreach($card in $script:densityCards){
        $card.Padding=if($level -ge 3){[Windows.Thickness]::new(7,3,7,3)}elseif($tight){[Windows.Thickness]::new(7,5,7,5)}else{[Windows.Thickness]::new(10,7,10,7)}
        $card.Margin=if($tight){[Windows.Thickness]::new(0,0,0,3)}else{[Windows.Thickness]::new(0,0,0,6)}
    }
    foreach($key in @('CPU','GPU','Memory','NVMe','Airflow')){
        $script:labels[$key].Visibility=if($level -ge 3){'Collapsed'}else{'Visible'}
    }
    foreach($card in $script:densityCards){$card.ToolTip=$card.Child.Children[1].Text}
    foreach($group in $script:compactGroups){
        $group.grid.Visibility=if($compact){'Visible'}else{'Collapsed'}
        foreach($row in $group.rows){$row.Visibility=if($compact){'Collapsed'}else{'Visible'}}
    }
    foreach($key in @('ramA','ramB','diskC','diskD')){
        $script:labels[$key].TextWrapping=if($compact){'NoWrap'}else{'Wrap'}
        $script:labels[$key].TextTrimming=if($compact){'CharacterEllipsis'}else{'None'}
        $script:cells[$key][0].FontSize=$(if($compact){18}else{21})*$typeScale
        $script:cells[$key][0].Margin=if($compact){[Windows.Thickness]::new(0)}else{[Windows.Thickness]::new(0,6,0,0)}
    }
    foreach($key in @('cpu','gpu','system')){$script:cells[$key][0].FontSize=$(if($compact){21}else{23})*$typeScale}
    foreach($key in @('cpuLoad','vcore','cpuFan','gpuLoad','vram','gpuVolt','gpuFan','bottom','top')){
        $label=$script:cells[$key][0].Parent.Children[0]
        $label.TextWrapping='NoWrap';$label.TextTrimming='CharacterEllipsis';$label.ToolTip=$label.Text
    }
    if(Get-Command Update-CardHeaders -ErrorAction SilentlyContinue){Update-CardHeaders}
    foreach($group in $script:compactGroups){
        $widest=0
        foreach($line in $group.grid.Children){$line.Measure([Windows.Size]::new([double]::PositiveInfinity,[double]::PositiveInfinity));$widest=[Math]::Max($widest,$line.DesiredSize.Width)}
        $single=$widest*2 -gt $window.FindName('CardScroll').ActualWidth-54
        while($group.grid.RowDefinitions.Count -lt $group.grid.Children.Count){$group.grid.RowDefinitions.Add([Windows.Controls.RowDefinition]::new())}
        for($i=0;$i -lt $group.grid.Children.Count;$i++){
            $line=$group.grid.Children[$i]
            [Windows.Controls.Grid]::SetRow($line,$(if($single){$i}else{[int][Math]::Floor($i/2)}))
            [Windows.Controls.Grid]::SetColumn($line,$(if($single){0}else{$i%2}))
            [Windows.Controls.Grid]::SetColumnSpan($line,$(if($single){2}else{1}))
        }
    }
    foreach($entry in $script:usageCells.Values){$entry[0].Parent.Margin=if($compact){[Windows.Thickness]::new(0,3,0,0)}else{[Windows.Thickness]::new(0,8,0,0)}}
    if($null -ne $script:availableSensors){
        foreach($key in @('cpu','gpu','system')){$script:cells[$key][0].Visibility=if($script:availableSensors[$key]){'Visible'}else{'Collapsed'}}
        foreach($key in @('cpuLoad','vcore','cpuFan','gpuLoad','vram','gpuVolt','gpuFan','bottom','top')){
            if(-not $script:availableSensors[$key]){$script:cells[$key][0].Parent.Visibility='Collapsed'}
            elseif($key -in @('bottom','top')){$script:cells[$key][0].Parent.Visibility='Visible'}
        }
        foreach($group in $script:compactGroups){foreach($line in $group.grid.Children){$line.Visibility=if($script:availableSensors[[string]$line.Tag]){'Visible'}else{'Collapsed'}}}
        foreach($key in @('ramA','ramB','diskC','diskD')){
            $column=$script:cells[$key][0].Parent
            $column.Visibility=if($script:availableSensors[$key]){'Visible'}else{'Collapsed'}
            $index=[Windows.Controls.Grid]::GetColumn($column)
            $column.Parent.ColumnDefinitions[$index].Width=if($script:availableSensors[$key]){[Windows.GridLength]::new(1,[Windows.GridUnitType]::Star)}else{[Windows.GridLength]::new(0)}
        }
        foreach($key in $script:usageCells.Keys){$script:usageCells[$key][0].Parent.Visibility=if($script:availableUsage[$key]){'Visible'}else{'Collapsed'}}
    }
}

function Update-CardDensity([switch]$Animate) {
    if($script:measuringDensity -or -not $script:densityCards){return}
    $scroll=$window.FindName('CardScroll')
    if($scroll.ActualHeight -le 0 -or $scroll.ActualWidth -le 0){return}
    $script:measuringDensity=$true
    try {
        $previous=$script:densityLevel
        $level=0
        # Measure logical WPF space, including hidden cards, wrapping and font size.
        # Reserve the scrollbar width so a disappearing scrollbar cannot flip density.
        $width=[Math]::Max(1,$scroll.ActualWidth-12)
        while($true){
            Set-CardDensity $level
            $cards.UpdateLayout()
            $cards.Measure([Windows.Size]::new($width,[double]::PositiveInfinity))
            if($script:showDetails -or $cards.DesiredSize.Height -le $scroll.ActualHeight-2 -or $level -eq 3){break}
            $level++
        }
        $script:densityLevel=$level
        if($Animate -and $null -ne $previous -and $level -ne $previous){
            $cards.BeginAnimation([Windows.UIElement]::OpacityProperty,$null)
            if([Windows.SystemParameters]::ClientAreaAnimation){
                $fade=[Windows.Media.Animation.DoubleAnimation]::new(0.82,1,[Windows.Duration]::new([TimeSpan]::FromMilliseconds(180)))
                $fade.FillBehavior='Stop'
                $cards.BeginAnimation([Windows.UIElement]::OpacityProperty,$fade)
            }
        }
    } finally {$script:measuringDensity=$false}
}
