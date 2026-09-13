$script:cardsVisible=@{}
$script:cardChecks=@{}
foreach($card in $cards.Children){
    $key=[string]$card.Tag
    $script:cardsVisible[$key]=if($null -ne $saved.cardsVisible.$key){[bool]$saved.cardsVisible.$key}else{$true}
    $check=[Windows.Controls.CheckBox]::new();$check.Content=$key;$check.Tag=$key;$check.Margin='0,0,0,8';$check.IsChecked=$script:cardsVisible[$key]
    $check.Add_Click({param($sender,$eventArgs)
        $script:cardsVisible[[string]$sender.Tag]=[bool]$sender.IsChecked
        Update-CardVisibility;Save-WidgetSettings
    })
    $script:cardChecks[$key]=$check
    $null=$window.FindName('CardOptions').Children.Add($check)
}
function Update-CardVisibility {
    $visible=0
    foreach($card in $cards.Children){
        $show=[bool]$script:cardsVisible[[string]$card.Tag]
        $card.Visibility=if($show){'Visible'}else{'Collapsed'}
        if($show){$visible++}
    }
    $window.FindName('CardsEmpty').Visibility=if($visible -eq 0 -and $window.FindName('SettingsPage').Visibility -eq 'Collapsed'){'Visible'}else{'Collapsed'}
    Update-CardDensity
}
Update-CardVisibility
