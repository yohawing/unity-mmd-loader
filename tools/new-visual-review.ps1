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
:root{color-scheme:dark}*{box-sizing:border-box}body{margin:0;font:14px system-ui,"Segoe UI",sans-serif;background:#15171c;color:#e6e6e6}
header{position:sticky;top:0;z-index:2;padding:12px 20px;display:flex;gap:16px;align-items:center;flex-wrap:wrap;background:#1c1f26;border-bottom:1px solid #2c3038}header h1{margin:0;font-size:16px}.status{color:#8b93a1;font-size:12px}
main{max-width:1500px;margin:auto;padding:8px 20px 60px}section h2{margin:26px 0 5px;font-size:14px}.count{color:#5d6470}.summary{margin:0 0 12px;color:#aeb5c0;line-height:1.5}
.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(260px,1fr));gap:14px}.card{margin:0;overflow:hidden;background:#1c1f26;border:1px solid #2c3038;border-radius:10px}
.imgwrap{aspect-ratio:1/1;display:flex;align-items:center;justify-content:center;background-color:#3a3f48;background-image:linear-gradient(45deg,#444a54 25%,transparent 25%),linear-gradient(-45deg,#444a54 25%,transparent 25%),linear-gradient(45deg,transparent 75%,#444a54 75%),linear-gradient(-45deg,transparent 75%,#444a54 75%);background-size:20px 20px;background-position:0 0,0 10px,10px -10px,-10px 0}.imgwrap img{display:block;max-width:100%;max-height:100%}
figcaption{padding:9px 11px;display:flex;gap:8px;align-items:center}.cname{font-size:12.5px}.badge{margin-left:auto;padding:2px 8px;border-radius:20px;font-size:11px;font-weight:700}.pass{background:#245d3b;color:#a8edc1}.fail{background:#6c2930;color:#ffc0c5}
details{margin-top:14px;padding:11px 13px;background:#1c1f26;border:1px solid #2c3038;border-radius:8px}summary{cursor:pointer;color:#aeb5c0}.meta{margin-top:10px;display:grid;grid-template-columns:repeat(auto-fit,minmax(230px,1fr));gap:8px}.meta div{white-space:pre-wrap;color:#aeb5c0;font-size:12px}.meta b{color:#e6e6e6}
.toolbar{margin:10px 0;display:flex;gap:9px;align-items:center;flex-wrap:wrap}.toolbar button,.toolbar label{font-size:12px}.toolbar button.active{background:#315f92}.compare{display:grid;grid-template-columns:1fr 1fr;gap:12px}.visual{position:relative;min-height:360px;overflow:hidden;background:#08090b;border:1px solid #29313d;border-radius:7px}.visual h3{position:absolute;z-index:3;margin:8px;padding:4px 7px;background:#000a;font-size:12px}.visual img{width:100%;height:100%;object-fit:contain;transform-origin:0 0;user-select:none;pointer-events:none}.overlay{display:none}.overlay img{position:absolute;inset:0}.compare[data-mode=blink] .side,.compare[data-mode=opacity] .side{display:none}.compare[data-mode=blink] .overlay,.compare[data-mode=opacity] .overlay{display:block;grid-column:1/-1}
.decision{margin-top:14px;display:grid;grid-template-columns:190px 1fr auto;gap:10px}button,select,textarea{font:inherit;color:inherit;background:#202630;border:1px solid #3a4658;border-radius:5px;padding:8px}textarea{min-height:42px;resize:vertical}@media(max-width:720px){.decision{grid-template-columns:1fr}.grid{grid-template-columns:1fr}}
</style></head><body>
<header><h1>MMD ビジュアルレビュー</h1><div id="run" class="status"></div></header>
<main id="cases"></main><script>
const manifest=__MANIFEST__,c=manifest.cases[0],key='mmd-visual-review:'+manifest.runId+':'+c.id,saved=JSON.parse(localStorage.getItem(key)||'{}'),deltaFloor=Number(c.expectedDeltaFloor||0),flipRule=deltaFloor>0?c.flipMean.toFixed(6)+'（差分下限 '+deltaFloor.toFixed(6)+' 以上）':c.flipMean.toFixed(6)+'（許容上限 '+Number(c.flipCeiling||0).toFixed(6)+' 以下）';
const escapeHtml=v=>String(v??'').replace(/[&<>"']/g,ch=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[ch])),value=(v,fallback)=>escapeHtml(v==null||v===''?fallback:v);
document.querySelector('#run').textContent=manifest.runId+' · Unity '+manifest.unityVersion+' · URP '+manifest.urpVersion+' · '+manifest.gpu;
document.querySelector('#cases').innerHTML=
'<section><h2>'+value(c.title,c.id)+' <span class="count">(3画像)</span></h2><p class="summary">'+value(c.description,'比較画像を確認します。')+'</p><div class="grid">'+
'<figure class="card"><div class="imgwrap"><img loading="lazy" src="'+escapeHtml(c.reference)+'"></div><figcaption><span class="cname">'+value(c.referenceLabel,'基準画像')+'</span></figcaption></figure>'+
'<figure class="card"><div class="imgwrap"><img loading="lazy" src="'+escapeHtml(c.candidate)+'"></div><figcaption><span class="cname">'+value(c.candidateLabel,'比較画像')+'</span></figcaption></figure>'+
'<figure class="card"><div class="imgwrap"><img loading="lazy" src="'+escapeHtml(c.heatmap)+'"></div><figcaption><span class="cname">差がある場所（FLIP '+escapeHtml(c.flipMean.toFixed(6))+')</span><span class="badge '+(c.passed?'pass':'fail')+'">'+(c.passed?'合格':'不合格')+'</span></figcaption></figure></div></section>'+
'<details><summary>詳しく比較する</summary><div class="toolbar"><button data-mode="side" class="active">左右</button><button data-mode="blink">交互表示</button><button data-mode="opacity">重ね合わせ</button><label>拡大 <input id="zoom" type="range" min="1" max="5" step=".1" value="1"></label><label>不透明度 <input id="opacity" type="range" min="0" max="1" step=".01" value=".5"></label><button id="reset">位置を戻す</button></div>'+
'<div id="compare" class="compare" data-mode="side"><div class="visual side"><h3>'+value(c.referenceLabel,'基準画像')+'</h3><img src="'+escapeHtml(c.reference)+'"></div><div class="visual side"><h3>'+value(c.candidateLabel,'比較画像')+'</h3><img src="'+escapeHtml(c.candidate)+'"></div><div class="visual overlay"><h3>基準 / 比較</h3><img src="'+escapeHtml(c.reference)+'"><img class="candidate" src="'+escapeHtml(c.candidate)+'"></div></div></details>'+
'<details><summary>比較条件を見る</summary><div class="meta"><div><b>ケースID</b>\n'+escapeHtml(c.id)+'</div><div><b>画像の差（FLIP）</b>\n'+flipRule+'</div>'+
'<div><b>シェーダー構成</b>\n'+escapeHtml(c.shaderProfile)+'</div><div><b>意図した変更</b>\n'+escapeHtml(c.intendedChange)+'</div><div><b>カメラ</b>\n位置 '+escapeHtml(c.cameraPosition.join(', '))+'\n注視点 '+escapeHtml(c.cameraTarget.join(', '))+'\n画角 '+escapeHtml(c.cameraFieldOfView)+'</div>'+
'<div><b>環境光</b>\n色 '+escapeHtml(c.ambientLightColor.join(', '))+'\n強度 '+escapeHtml(c.ambientLightIntensity)+'</div><div><b>メインライト</b>\n色 '+escapeHtml(c.directionalLightColor.join(', '))+'\n強度 '+escapeHtml(c.directionalLightIntensity)+'\n位置 '+escapeHtml(c.directionalLightPosition.join(', '))+'\n注視点 '+escapeHtml(c.directionalLightTarget.join(', '))+'\n'+escapeHtml(c.directionalLightMode)+'</div><div><b>Volume</b>\n'+escapeHtml(c.volume)+'</div><div><b>対象外</b>\n'+value(c.limitations,'特記事項はありません。')+'</div></div></details>'+
'<section class="decision"><select id="decision"><option value="">判定を選択</option><option value="Accept">承認</option><option value="Reject">却下</option><option value="Needs follow-up">要フォローアップ</option></select><textarea id="note" placeholder="判定理由や気になった点を短く記入"></textarea><button id="export">review.jsonを書き出す</button></section>';
const compare=document.querySelector('#compare'),candidate=document.querySelector('.overlay .candidate'),zoom=document.querySelector('#zoom'),opacity=document.querySelector('#opacity');let pan={x:0,y:0},drag=null,blinkTimer=null;
function render(){document.querySelectorAll('.visual img').forEach(img=>{img.style.transform='translate('+pan.x+'px,'+pan.y+'px) scale('+zoom.value+')'});candidate.style.opacity=opacity.value}
function setMode(mode){compare.dataset.mode=mode;document.querySelectorAll('[data-mode]').forEach(b=>b.classList.toggle('active',b.dataset.mode===mode));clearInterval(blinkTimer);candidate.style.visibility='visible';if(mode==='blink')blinkTimer=setInterval(()=>{candidate.style.visibility=candidate.style.visibility==='hidden'?'visible':'hidden'},500)}
document.querySelectorAll('[data-mode]').forEach(b=>b.onclick=()=>setMode(b.dataset.mode));zoom.oninput=opacity.oninput=render;document.querySelector('#reset').onclick=()=>{pan={x:0,y:0};zoom.value=1;render()};compare.onpointerdown=e=>{drag={x:e.clientX-pan.x,y:e.clientY-pan.y};compare.setPointerCapture(e.pointerId)};compare.onpointermove=e=>{if(drag){pan={x:e.clientX-drag.x,y:e.clientY-drag.y};render()}};compare.onpointerup=()=>{drag=null};
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
