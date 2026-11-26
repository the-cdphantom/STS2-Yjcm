param([ValidateSet('stable','beta')][string]$Variant)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$version=if($Variant -eq 'stable'){'0.111.0'}else{'0.114.0'}
$deps=Join-Path $repo "dependencies/$Variant-$version"
foreach($file in @('GodotSharp.dll','0Harmony.dll','sts2.dll','STS2-RitsuLib.dll','STS2-RitsuLib.Shared.dll','STS2-RitsuLib.Ui.dll','STS2-RitsuLib.Settings.dll','STS2-RitsuLib.Runtime.dll')) {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $deps $file))
}
$assembly=[Reflection.Assembly]::LoadFrom((Join-Path $repo "artifacts/staged/Yjcm.$version.dll"))
$flags=[Reflection.BindingFlags]'Static,Instance,Public,NonPublic'
$script:checks=0
function Require($condition,[string]$message) {
    if(!$condition){throw $message}
    $script:checks++
}

# Exercise the actual prefix: a mutable clone fails the collection's HashSet lookup.
$modelDb=[MegaCrit.Sts2.Core.Models.ModelDb]
$upgraded=[Activator]::CreateInstance($assembly.GetType('Yjcm.YjcmJieQingShi',$true))
$starter=[Activator]::CreateInstance($assembly.GetType('Yjcm.YjcmQingShi',$true))
$models=$modelDb.GetField('_contentById',$flags).GetValue($null)
$id=$modelDb::GetId($upgraded.GetType())
$models[$id]=$upgraded
$prefix=$assembly.GetType('Yjcm.OrobasRefinementPatch',$true).GetMethod('Prefix',$flags)
$callArgs=[object[]]@($starter,$null)
Require (!$prefix.Invoke($null,$callArgs)) 'QingShi upgrade should be handled'
Require ([object]::ReferenceEquals($upgraded,$callArgs[1])) 'Upgrade must return the canonical singleton'
Require ($callArgs[1].IsCanonical) 'Collection upgrade must not be mutable'
$unlocked=[Collections.Generic.HashSet[MegaCrit.Sts2.Core.Models.RelicModel]]::new()
[void]$unlocked.Add($upgraded)
Require ($unlocked.Contains($callArgs[1])) 'Upgraded relic is incorrectly locked in collection'
Require ($unlocked.Contains($callArgs[1].CanonicalInstance)) 'Seen state must match canonical relic'
$otherArgs=[object[]]@($null,$null)
Require ($prefix.Invoke($null,$otherArgs)) 'Unrelated relic upgrades must not be intercepted'

# Resolve all four partner combinations without loading, enumerating or copying a partner DLL.
$partnerType=$assembly.GetType('Yjcm.Interop.ShangFangGuPartners',$true)
$resolve=@($partnerType.GetMethods($flags)|Where-Object {$_.Name -eq 'Resolve' -and $_.GetParameters().Length -eq 4})[0]
$reward=[Activator]::CreateInstance($assembly.GetType('Yjcm.YiZhenQingMeng',$true))
$script:activeMods=[Collections.Generic.HashSet[string]]::new()
$script:resolutionCalls=0
$isLoaded=[Func[string,bool]]{param($modId) return $script:activeMods.Contains($modId)}
$findCard=[Func[string,string,MegaCrit.Sts2.Core.Models.CardModel]]{
    param($modId,$name)
    $script:resolutionCalls++
    if(($modId -eq 'sunqian-universe' -and $name -eq 'Squ.Cards.ShangfangguSigh') -or
       ($modId -eq 'newsanguo' -and $name -eq 'newsanguo.Scripts.Cards.BetterThanYilingFlames')) { return $reward }
    return $null
}
foreach($sun in @($false,$true)) { foreach($news in @($false,$true)) {
    $script:activeMods.Clear()
    if($sun){[void]$script:activeMods.Add('sunqian-universe')}
    if($news){[void]$script:activeMods.Add('newsanguo')}
    $script:resolutionCalls=0
    $sigh=$resolve.Invoke($null,[object[]]@('sunqian-universe','Squ.Cards.ShangfangguSigh',$isLoaded,$findCard))
    $fate=$resolve.Invoke($null,[object[]]@('newsanguo','newsanguo.Scripts.Cards.BetterThanYilingFlames',$isLoaded,$findCard))
    Require (($null -ne $sigh) -eq $sun) 'Sunqian option presence mismatch'
    Require (($null -ne $fate) -eq $news) 'Newsanguo option presence mismatch'
    Require ($script:resolutionCalls -eq ([int]$sun+[int]$news)) 'Disabled partner must not be resolved'
}}
[void]$script:activeMods.Add('sunqian-universe')
Require ($null -eq $resolve.Invoke($null,[object[]]@('sunqian-universe','MissingOldVersionCard',$isLoaded,$findCard))) 'Old partner with missing card must be unavailable'

$lampType=$assembly.GetType('Yjcm.QiXingDeng',$true)
$lamp=[Activator]::CreateInstance($lampType)
$model=[MegaCrit.Sts2.Core.Models.AbstractModel]
$model.GetProperty('IsMutable',$flags).SetValue($lamp,$true)
$canPrevent=$lampType.GetMethod('CanPreventDeath',$flags)
Require ($canPrevent.Invoke($null,[object[]]@($false,$true))) 'Unused lamp must prevent owner death'
Require (!$canPrevent.Invoke($null,[object[]]@($false,$false))) 'Lamp must not revive teammates/enemies'
Require (!$canPrevent.Invoke($null,[object[]]@($true,$true))) 'Spent lamp cannot prevent death'
Require (!$canPrevent.Invoke($null,[object[]]@($true,$false))) 'Spent lamp cannot affect another creature'
Require (!$lamp.IsUsedUp) 'New lamp must be unused'
$saved=$lampType.GetProperty('WasUsed').GetCustomAttributesData()|Where-Object {$_.AttributeType.Name -eq 'SavedPropertyAttribute'}
Require ($null -ne $saved) 'Spent state must be saved across battles/reload'
$lamp.WasUsed=$true
Require ($lamp.IsUsedUp -and $lamp.Status -eq [MegaCrit.Sts2.Core.Entities.Relics.RelicStatus]::Disabled) 'Spent lamp must stay disabled'
Require (!$canPrevent.Invoke($null,[object[]]@($lamp.WasUsed,$true))) 'Lamp must only prevent death once per run'
Require ($lamp.Rarity -eq [MegaCrit.Sts2.Core.Entities.Relics.RelicRarity]::Event) 'Lamp must not enter random/shop pools'
Require ($null -ne $lampType.GetMethod('AfterPreventingDeath',$flags)) 'Use native death prevention callback'

$source=Get-Content -LiteralPath (Join-Path $repo 'mod/src/Relics/QiXingDeng.cs') -Raw
Require ($source.IndexOf('WasUsed = true;') -lt $source.IndexOf('await CreatureCmd.Heal')) 'Consume before nested HP hooks'
Require ($source.Contains('PlayerCmd.GainEnergy(7, Owner)')) 'Energy reward must be seven'
Require ($source.Contains('PowerCmd.Apply<EnergyNextTurnPower>')) 'Off-turn energy must use the native after-reset reward'
$deferEnergy=$lampType.GetMethod('ShouldDeferEnergy',$flags)
foreach($phase in [Enum]::GetValues([MegaCrit.Sts2.Core.Combat.PlayerTurnPhase])) {
    Require ($deferEnergy.Invoke($null,[object[]]@([MegaCrit.Sts2.Core.Combat.CombatSide]::Enemy,$phase))) 'Enemy-turn revive energy must survive the next reset'
    $expected=$phase.ToString() -in @('None','AutoPostPlay','End')
    Require ($deferEnergy.Invoke($null,[object[]]@([MegaCrit.Sts2.Core.Combat.CombatSide]::Player,$phase)) -eq $expected) 'Owner-turn energy timing mismatch'
}
Require ($source.Contains('CardPileCmd.Draw(context, 7, Owner, false)')) 'Draw reward must be seven'
Require (!$source.Contains('Task.Run') -and !$source.Contains('new ThrowingPlayerChoiceContext')) 'Death draw must use synchronized choice context'
$eventSource=Get-Content -LiteralPath (Join-Path $repo 'mod/src/Events/ShangFangGuZhiYu.cs') -Raw
Require ($eventSource.Contains('CreateCard(canonicalCard, Owner)') -and $eventSource.Contains('PileType.Deck')) 'Reward must enter permanent deck with player ownership'
Require ($eventSource.Contains('SunqianCard != null || ShangFangGuPartners.NewsanguoCard != null')) 'Event needs at least one partner'
Require ($eventSource.Contains('CardOption("SIGH", sigh)') -and $eventSource.Contains('CardOption("FATE", fate)')) 'Options must use actual partner cards'
$events=Get-Content -LiteralPath (Join-Path $repo 'mod/Yjcm/localization/zhs/events.json') -Raw|ConvertFrom-Json
$relics=Get-Content -LiteralPath (Join-Path $repo 'mod/Yjcm/localization/zhs/relics.json') -Raw|ConvertFrom-Json
Require ($events.'YJCM_EVENT_SHANG_FANG_GU_ZHI_YU.title' -eq '上方谷之雨') 'Event localization missing'
Require ($relics.'YJCM_RELIC_QI_XING_DENG.title' -eq '七星灯') 'Confirmed relic name missing'
Require ($relics.'YJCM_RELIC_QI_XING_DENG.flavor' -eq '魏延将军，走慢点！') 'Relic flavor mismatch'
$layoutSource=Get-Content -LiteralPath (Join-Path $repo 'mod/src/Patches/ShangFangGuEventLayoutPatch.cs') -Raw
Require ($layoutSource.Contains('eventModel is not ShangFangGuZhiYu')) 'Visual override must only affect this event'
Require ($layoutSource.Contains('MoveChild(backdrop, anchor.GetIndex())')) 'Backdrop must replace portrait layer, above old background and below UI'
Require ($layoutSource.Contains('portrait.Texture = null;')) 'Oversized native portrait must not remain behind UI'
Require (!$layoutSource.Contains('SetDescription') -and !$layoutSource.Contains('GenerateInitialOptions')) 'Visual fix must not change event interactions'
Write-Output "SHANGFANGGU_RELICS_OK variant=$Variant assertions=$script:checks"
