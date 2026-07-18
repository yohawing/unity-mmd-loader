param([string] $ManifestPath = "", [string] $OutputRoot = "", [string] $CaseId = "")

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrEmpty($ManifestPath)) {
    $ManifestPath = Join-Path $repoRoot "artifacts\visual-parity\manifest.json"
}
$ManifestPath = [IO.Path]::GetFullPath($ManifestPath)
if (-not (Test-Path -LiteralPath $ManifestPath)) { throw "Visual review manifest not found: $ManifestPath" }
$manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
if ($manifest.schemaVersion -ne 1) {
    throw "Unsupported visual review schema: $($manifest.schemaVersion)"
}
if (-not [string]::IsNullOrEmpty($CaseId)) {
    $selectedCases = @($manifest.cases | Where-Object { $_.id -eq $CaseId })
    if ($selectedCases.Count -ne 1) {
        throw "Visual review case '$CaseId' was not found exactly once: $ManifestPath"
    }
    $manifest.cases = $selectedCases
} elseif ($manifest.cases.Count -ne 1) {
    throw "Manifest has $($manifest.cases.Count) cases; select one with -CaseId: $ManifestPath"
}
if ([string]::IsNullOrEmpty($OutputRoot)) {
    $OutputRoot = Join-Path $repoRoot ("artifacts\visual-review\" + $manifest.runId)
}
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

$manifestDirectory = Split-Path -Parent $ManifestPath
foreach ($case in $manifest.cases) {
    foreach ($property in @("reference", "candidate", "heatmap")) {
        $fileName = [string]$case.$property
        $source = Join-Path $manifestDirectory $fileName
        if ([string]::IsNullOrEmpty($fileName) -or -not (Test-Path -LiteralPath $source)) {
            throw "Case '$($case.id)' $property image is missing: $source"
        }
        Copy-Item -LiteralPath $source -Destination (Join-Path $OutputRoot $fileName) -Force
    }
}

$json = ($manifest | ConvertTo-Json -Depth 20 -Compress).Replace("</", "<\/")
$html = @'
<!doctype html>
<html lang="ja"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>MMD ビジュアルレビュー</title><style>
:root{color-scheme:dark;background:#101216;color:#e9edf3;font:14px system-ui,sans-serif}*{box-sizing:border-box}
body{margin:0;padding:24px}header,.summary,.toolbar,.meta,.decision{max-width:1500px;margin:auto}h1{margin:0 0 6px}.status{color:#9ba7b7}
.summary{margin-top:18px;padding:18px 20px;background:#171b22;border:1px solid #29313d;border-radius:7px}.summary h2{margin:0 0 8px}.summary h3{margin:16px 0 6px;font-size:14px}.summary p{margin:6px 0;line-height:1.65}.image-guide{display:grid;grid-template-columns:1fr 1fr;gap:12px;margin-top:14px}.image-guide article{padding:12px;background:#101319;border-radius:5px}.review-points{margin:6px 0 0;padding-left:1.4em;line-height:1.65}.limitations{color:#c4ad82}
.toolbar{display:flex;gap:10px;align-items:center;flex-wrap:wrap;margin-top:18px}button,select,input,textarea{font:inherit;color:inherit;background:#202630;border:1px solid #3a4658;border-radius:5px;padding:7px}button.active{background:#315f92}
.compare{max-width:1500px;margin:18px auto;display:grid;grid-template-columns:1fr 1fr;gap:12px}.visual{position:relative;min-height:360px;overflow:hidden;background:#08090b;border:1px solid #29313d;border-radius:7px}
.visual h2{position:absolute;z-index:3;margin:8px;padding:4px 7px;background:#000a;font-size:12px}.visual img{width:100%;height:100%;object-fit:contain;transform-origin:0 0;user-select:none;pointer-events:none}
.overlay{display:none}.overlay img{position:absolute;inset:0}.heatmap{grid-column:1/-1;max-height:420px}.compare[data-mode=blink] .side,.compare[data-mode=opacity] .side{display:none}.compare[data-mode=blink] .overlay,.compare[data-mode=opacity] .overlay{display:block;grid-column:1/-1}
.meta{display:grid;grid-template-columns:repeat(auto-fit,minmax(240px,1fr));gap:8px;background:#171b22;padding:14px;border-radius:7px}.meta div{white-space:pre-wrap}.decision{margin-top:16px;display:grid;grid-template-columns:220px 1fr auto;gap:10px}textarea{min-height:70px}.pass{color:#7be0a3}.fail{color:#ff8181}
</style></head><body>
<header><h1>MMD ビジュアルレビュー</h1><div id="run" class="status"></div></header>
<div class="toolbar"><button data-mode="side" class="active">左右比較</button><button data-mode="blink">A/B 点滅</button><button data-mode="opacity">重ね合わせ</button>
<label>拡大率 <input id="zoom" type="range" min="1" max="5" step=".1" value="1"></label><label>比較画像の不透明度 <input id="opacity" type="range" min="0" max="1" step=".01" value=".5"></label><button id="reset">位置をリセット</button></div>
<main id="cases"></main><script>
const manifest=__MANIFEST__,c=manifest.cases[0],key='mmd-visual-review:'+manifest.runId+':'+c.id,saved=JSON.parse(localStorage.getItem(key)||'{}'),deltaFloor=Number(c.expectedDeltaFloor||0),flipRule=deltaFloor>0?c.flipMean.toFixed(6)+'（差分下限 '+deltaFloor.toFixed(6)+' 以上）':c.flipMean.toFixed(6)+'（許容上限 '+Number(c.flipCeiling||0).toFixed(6)+' 以下）';
const escapeHtml=v=>String(v??'').replace(/[&<>"']/g,ch=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[ch])),value=(v,fallback)=>escapeHtml(v==null||v===''?fallback:v),points=Array.isArray(c.reviewPoints)?c.reviewPoints:[],pointList=points.length?'<ul class="review-points">'+points.map(p=>'<li>'+escapeHtml(p)+'</li>').join('')+'</ul>':'<p>ケース固有の確認ポイントは未記載です。</p>';
document.querySelector('#run').textContent=manifest.runId+' · Unity '+manifest.unityVersion+' · URP '+manifest.urpVersion+' · '+manifest.gpu;
document.querySelector('#cases').innerHTML=
'<section class="summary"><h2>'+value(c.title,c.id)+'</h2><p>'+value(c.description,'この比較ケースの説明はまだ記載されていません。')+'</p><div class="image-guide"><article><b>'+value(c.referenceLabel,'基準画像')+'</b><p>'+value(c.referenceDescription,'変更前または比較基準の画像です。')+'</p></article><article><b>'+value(c.candidateLabel,'比較画像')+'</b><p>'+value(c.candidateDescription,'確認対象となる変更後の画像です。')+'</p></article></div><h3>確認ポイント</h3>'+pointList+'<p class="limitations"><b>このケースで証明しないこと:</b> '+value(c.limitations,'特記事項なし')+'</p></section>'+
'<section id="compare" class="compare" data-mode="side"><div class="visual side"><h2>'+value(c.referenceLabel,'基準画像')+'</h2><img src="'+escapeHtml(c.reference)+'"></div><div class="visual side"><h2>'+value(c.candidateLabel,'比較画像')+'</h2><img src="'+escapeHtml(c.candidate)+'"></div>'+
'<div class="visual overlay"><h2>基準画像 / 比較画像</h2><img src="'+escapeHtml(c.reference)+'"><img class="candidate" src="'+escapeHtml(c.candidate)+'"></div><div class="visual heatmap"><h2>FLIP 差分ヒートマップ</h2><img src="'+escapeHtml(c.heatmap)+'"></div></section>'+
'<section class="meta"><div><b>ケースID</b>\n'+escapeHtml(c.id)+'</div><div><b>FLIP知覚差分</b>\n'+flipRule+' <span class="'+(c.passed?'pass':'fail')+'">'+(c.passed?'合格':'不合格')+'</span></div>'+
'<div><b>シェーダー構成</b>\n'+escapeHtml(c.shaderProfile)+'</div><div><b>意図した変更</b>\n'+escapeHtml(c.intendedChange)+'</div><div><b>カメラ</b>\n位置 '+escapeHtml(c.cameraPosition.join(', '))+'\n注視点 '+escapeHtml(c.cameraTarget.join(', '))+'\n画角 '+escapeHtml(c.cameraFieldOfView)+'</div>'+
'<div><b>環境光</b>\n色 '+escapeHtml(c.ambientLightColor.join(', '))+'\n強度 '+escapeHtml(c.ambientLightIntensity)+'</div><div><b>メインライト</b>\n色 '+escapeHtml(c.directionalLightColor.join(', '))+'\n強度 '+escapeHtml(c.directionalLightIntensity)+'\n位置 '+escapeHtml(c.directionalLightPosition.join(', '))+'\n注視点 '+escapeHtml(c.directionalLightTarget.join(', '))+'\n'+escapeHtml(c.directionalLightMode)+'</div><div><b>Volume</b>\n'+escapeHtml(c.volume)+'</div></section>'+
'<section class="decision"><select id="decision"><option value="">判定を選択</option><option value="Accept">承認</option><option value="Reject">却下</option><option value="Needs follow-up">要フォローアップ</option></select><textarea id="note" placeholder="判定理由や気になった点を短く記入"></textarea><button id="export">review.jsonを書き出す</button></section>';
const compare=document.querySelector('#compare'),candidate=document.querySelector('.overlay .candidate'),zoom=document.querySelector('#zoom'),opacity=document.querySelector('#opacity');
let pan={x:0,y:0},drag=null,blinkTimer=null;
function render(){document.querySelectorAll('.visual img').forEach(function(img){img.style.transform='translate('+pan.x+'px,'+pan.y+'px) scale('+zoom.value+')'});candidate.style.opacity=opacity.value}
function setMode(mode){compare.dataset.mode=mode;document.querySelectorAll('[data-mode]').forEach(function(b){b.classList.toggle('active',b.dataset.mode===mode)});clearInterval(blinkTimer);candidate.style.visibility='visible';if(mode==='blink'){blinkTimer=setInterval(function(){candidate.style.visibility=candidate.style.visibility==='hidden'?'visible':'hidden'},500)}}
document.querySelectorAll('[data-mode]').forEach(function(b){b.onclick=function(){setMode(b.dataset.mode)}});zoom.oninput=opacity.oninput=render;document.querySelector('#reset').onclick=function(){pan={x:0,y:0};zoom.value=1;render()};
compare.onpointerdown=function(e){drag={x:e.clientX-pan.x,y:e.clientY-pan.y};compare.setPointerCapture(e.pointerId)};compare.onpointermove=function(e){if(drag){pan={x:e.clientX-drag.x,y:e.clientY-drag.y};render()}};compare.onpointerup=function(){drag=null};
const decision=document.querySelector('#decision'),note=document.querySelector('#note');decision.value=saved.decision||'';note.value=saved.note||'';
function persist(){localStorage.setItem(key,JSON.stringify({decision:decision.value,note:note.value}))}decision.onchange=note.oninput=persist;
document.querySelector('#export').onclick=function(){persist();const review={schemaVersion:1,runId:manifest.runId,reviewedAt:new Date().toISOString(),cases:[{id:c.id,decision:decision.value||'Needs follow-up',note:note.value}]},a=document.createElement('a');a.href=URL.createObjectURL(new Blob([JSON.stringify(review,null,2)],{type:'application/json'}));a.download='review.json';a.click();URL.revokeObjectURL(a.href)};
render();
</script></body></html>
'@
$html = $html.Replace("__MANIFEST__", $json)
$outputPath = Join-Path $OutputRoot "index.html"
Set-Content -LiteralPath $outputPath -Value $html -Encoding utf8
Write-Host "Visual review generated: $outputPath"
