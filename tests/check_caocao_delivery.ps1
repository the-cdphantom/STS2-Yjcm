param([string]$Target)
. (Join-Path (Split-Path $PSScriptRoot -Parent) 'tools/config.ps1')
$repo=$script:RepositoryRoot
$dist=Join-Path $repo 'artifacts/caocao/dist/yjcm'
if(!$Target){$Target=$dist}
$profile=Get-Content -LiteralPath (Join-Path $Target 'yjcm_build_profile.json.txt') -Raw | ConvertFrom-Json
if($profile.flavor -ne 'caocao' -or $profile.publishable){throw 'Expected explicitly non-publishable Caocao development profile'}
foreach($v in @('stable','beta')){
    $version=if($v -eq 'stable'){'0.111.0'}else{'0.114.0'}
    $stamp=Get-Content (Join-Path $repo "artifacts/caocao/staged/$v.build.json") -Raw | ConvertFrom-Json
    if(!$stamp.testsPassed -or $stamp.sourceFingerprint -ne (Get-YjcmSourceFingerprint -Flavor caocao) -or $stamp.dllSha256 -ne (Get-FileHash (Join-Path $Target "Yjcm.$version.dll")).Hash){throw "Unverified Caocao DLL: $v"}
}
foreach($file in Get-ChildItem -LiteralPath $dist -Recurse -File){
    $destination=Join-Path $Target ([IO.Path]::GetRelativePath($dist,$file.FullName))
    if((Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath $destination).Hash){throw "Development delivery mismatch: $destination"}
}
foreach($file in $profile.developmentOnlyFiles){if(!(Test-Path -LiteralPath (Join-Path $Target $file))){throw "Experimental file missing: $file"}}
foreach($folder in @('Voice','Spine','MeshAnimation')){
    $common=Join-Path $repo "assets/loose/$folder"
    foreach($file in Get-ChildItem -LiteralPath $common -Recurse -File){
        $destination=Join-Path $Target ($folder+'/'+[IO.Path]::GetRelativePath($common,$file.FullName))
        if((Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath $destination).Hash){throw "Common asset mismatch: $destination"}
    }
}
$experimental=Join-Path $repo 'assets/experimental/WeiCaoCao'
foreach($file in @('weicaozhang_jineng.skel','weicaozhang_jineng.atlas','weicaozhang_jineng.png','weicaozhang_jineng_2.png')){
    if("Spine/WeiCaoCao/KuiPan/$file" -notin $profile.developmentOnlyFiles){throw "KuiPan combo asset missing: $file"}
}
foreach($file in Get-ChildItem -LiteralPath $experimental -Recurse -File){
    $destination=Join-Path $Target (Get-YjcmRuntimeRelativePath ([IO.Path]::GetRelativePath($experimental,$file.FullName)))
    if((Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath $destination).Hash){throw "Caocao asset mismatch: $destination"}
}
foreach($voice in @('jun_he_1','jun_he_2','hu_jia','duo_yue_attack','duo_yue_draw','character_select','fu_le_1','fu_le_2','fu_le_3','fu_le_4','wen_lan_1','wen_lan_2','wen_lan_3','wen_lan_4','bi_zuo','dian_lun','ying_shi','shi_zha','shi_mou_yang','shi_mou_yin','xian_mou_yang','xian_mou_yin','qiao_dui','san_shi','chen_lue','chong_yi','su_jun','di_feng','zhi_dui','liang_yan','nu_chen','dun_xi','che_xie','gong_tu','bo_xuan','chou_tao','kang_li','wei_ti','zhan_yi','wu_mei','she_fu','ming_fa','tuo_yu','zhou_xi','shi_wu','bing_ji','po_rui','guang_ao','ning_pao','kui_pan','gui_xin_xing_tai','xie_tian_zi','jie_jun_he','jie_duo_yue_1','jie_duo_yue_2')){
    $path=Join-Path $Target "Voice/WeiCaoCao/$voice.wav"
    if(!(Test-Path -LiteralPath $path)){throw "Caocao voice missing: $voice"}
    $bytes=[IO.File]::ReadAllBytes($path)
    if([Text.Encoding]::ASCII.GetString($bytes,0,4) -ne 'RIFF' -or [BitConverter]::ToInt32($bytes,24) -ne 48000 -or [BitConverter]::ToInt16($bytes,22) -ne 1){throw "Caocao voice must be mono48k WAV: $voice"}
}
foreach($legacy in @('Yjcm.0.107.1.dll','Yjcm.0.113.0.dll')){
    if(Test-Path -LiteralPath (Join-Path $Target $legacy)){throw "Legacy runtime remains: $legacy"}
}
& node (Join-Path $repo 'tests/check_caocao_pack.mjs') (Join-Path $Target 'Yjcm.pck')
Assert-YjcmNativeExit 'Caocao PCK check failed'
Write-Output "CAOCAO_DELIVERY_OK target=$Target"
