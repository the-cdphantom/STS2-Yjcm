param([ValidateSet('stable','beta')][string]$Variant)
$ErrorActionPreference='Stop'
$dev=Split-Path $PSScriptRoot -Parent
$root=$dev
$version=if($Variant -eq 'stable') {'0.111.0'} else {'0.114.0'}
$deps=Join-Path $root "dependencies\$Variant-$version"
foreach($file in @('GodotSharp.dll','0Harmony.dll','sts2.dll','STS2-RitsuLib.dll','STS2-RitsuLib.Shared.dll','STS2-RitsuLib.Ui.dll','STS2-RitsuLib.Settings.dll','STS2-RitsuLib.Runtime.dll')) {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $deps $file))
}
$assembly=[Reflection.Assembly]::LoadFrom((Join-Path $dev "artifacts\staged\Yjcm.$version.dll"))
$flags=[Reflection.BindingFlags]'Static,NonPublic,Public'
$rules=$assembly.GetType('Yjcm.WeiDongZhuoGuangYongRules',$true)
$calculate=$rules.GetMethod('Calculate',$flags)
$pay=$rules.GetMethod('PayMaxHp',$flags)
$script:checks=0
function Assert-True($condition,[string]$message) {
    if(!$condition) { throw $message }
    $script:checks++
}
function Transfer([int]$enemy,[int]$player,[bool]$upgrade,[bool]$cap,[bool]$seen) {
    $calculate.Invoke($null,[object[]]@($enemy,$player,$upgrade,$cap,$seen))
}
foreach($enemy in @(-3,0,1,2,3,7,20,999)) {
    foreach($player in @(-5,0,1,2,7,99)) {
        foreach($upgrade in @($false,$true)) {
            $v=Transfer $enemy $player $upgrade $false $false
            if($enemy -le 0) {
                Assert-True (!$v.Success -and $v.Enemy -eq $enemy -and $v.Player -eq $player) 'Nonpositive buff stolen'
            } else {
                $take=if($upgrade){$enemy}else{1}
                Assert-True ($v.Success -and $v.Enemy -eq $enemy-$take -and $v.Player -eq $player+$take) 'Wrong normal transfer'
                Assert-True ($v.Enemy -ge 0) 'Enemy underflow'
            }
            foreach($seen in @($false,$true)) {
                $v=Transfer $enemy $player $upgrade $true $seen
                if($enemy -le 0) { Assert-True (!$v.Success) 'Empty cap stolen'; continue }
                Assert-True ($v.Enemy -ge 0 -and $v.Player -ge 1) 'Cap below floor'
                if($upgrade) { Assert-True ($v.Enemy -eq 0 -and $v.Player -eq 1) 'Upgrade cap not cleared/set to 1' }
                elseif(!$seen -and $player -le 0) { Assert-True ($v.Enemy -eq $enemy -and $v.Player -eq $enemy) 'First cap copy' }
                elseif($player -le 1) { Assert-True ($v.Enemy -eq 0 -and $v.Player -eq 1) 'Own cap 1 must clear enemy' }
                else { Assert-True ($v.Enemy -eq $enemy-1 -and $v.Player -eq $player-1) 'Cap decrement' }
            }
        }
    }
}
$e=3; $p=0; $seen=$false
foreach($expected in @(@(3,3),@(2,2),@(1,1),@(0,1))) {
    $v=Transfer $e $p $false $true $seen
    Assert-True ($v.Enemy -eq $expected[0] -and $v.Player -eq $expected[1]) 'Cap sequence 3/3 -> 2/2 -> 1/1 -> 0/1'
    $e=$v.Enemy; $p=$v.Player; $seen=$true
}
$v=Transfer 20 $p $false $true $seen
Assert-True ($v.Enemy -eq 0 -and $v.Player -eq 1) 'New enemy reset cap memory'
$v=Transfer 20 0 $false $true $true
Assert-True ($v.Enemy -eq 0 -and $v.Player -eq 1) 'Lost cap should not be first acquisition again this combat'
foreach($success in @($false,$true)) {
    foreach($hp in @(1,75,79,80)) {
        Assert-True ($pay.Invoke($null,[object[]]@($hp,80,$success)) -eq ($success -and $hp -lt 80)) 'Wounded/payment gate'
    }
}
# Per-target payment stops if maxHP catches current HP; no healing and no forced HP loss.
$max=80
foreach($success in @($true,$true,$false,$true)) {
    if($pay.Invoke($null,[object[]]@(78,$max,$success))) {$max--}
}
Assert-True ($max -eq 78) 'AOE must recheck wounded status for each successful target'
$expectedPriority='StrengthPower,DexterityPower,FocusPower,VigorPower,DoubleDamagePower,ArtifactPower,BufferPower,IntangiblePower,PlatingPower,ThornsPower,RegenPower,BarricadePower,RitualPower,HardToKillPower,SlipperyPower,HardenedShellPower'
$base=$assembly.GetType('Yjcm.WeiDongZhuoGuangYongBase',$true)
$maxHpCap=$assembly.GetType('Yjcm.WeiDongZhuoMaxHpCapPatch',$true).GetField('Cap',$flags).GetRawConstantValue()
Assert-True ($maxHpCap -eq 350234) 'WeiDongZhuo max HP hard cap changed'
$priority=$base.GetField('Priority',$flags).GetValue($null)
Assert-True (($priority.Name -join ',') -eq $expectedPriority) 'Whitelist or priority drift'
$stealPriority=$base.GetMethod('StealPriority',$flags)
$normalPriority=@($stealPriority.Invoke($null,[object[]]@($false)))
$upgradedPriority=@($stealPriority.Invoke($null,[object[]]@($true)))
Assert-True (($normalPriority.Name -join ',') -eq (($expectedPriority -split ',' | Select-Object -First 12) -join ',')) 'Normal relic must exclude four upgrade-only powers'
Assert-True (($upgradedPriority.Name -join ',') -eq $expectedPriority) 'Upgraded relic must retain all sixteen powers in order'
foreach($name in @('WeiDongZhuoGuangYong','WeiDongZhuoJieGuangYong')) {
    $type=$assembly.GetType("Yjcm.$name",$true)
    Assert-True ($type.GetCustomAttributesData().AttributeType.Name -contains 'RegisterRelicAttribute') 'Relic not registered'
    $instance=[Activator]::CreateInstance($type)
    $tips=$base.GetProperty('AdditionalHoverTips',[Reflection.BindingFlags]'Instance,NonPublic').GetValue($instance)
    Assert-True (@($tips).Count -eq 0) 'Relic must not attach buff hover tips'
}
$source=Get-Content -LiteralPath (Join-Path $dev 'mod\src\Relics\WeiDongZhuoGuangYong.cs') -Raw
Assert-True ($source.Contains('override async Task BeforeCardPlayed')) 'Effect must run before card effects'
Assert-True (!$source.Contains('await CreatureCmd.GainMaxHp')) 'Max HP gain must not heal'
Assert-True ($source.Contains('if (Upgraded && gained > 0)') -and $source.Contains('await CreatureCmd.Heal(self, gained)')) 'Only JieGuangYong should heal for actual max-HP gain'
Assert-True (!$source.Contains('UsedThisTurn') -and !$source.Contains('IsFirstInSeries')) 'Do not cap per turn or reject replays'
Write-Output "GUANGYONG_RULES_OK variant=$Variant assertions=$script:checks"
