param([string[]]$modes = @("screens","home","themes","menus","equipment","review","login","check","reset","splash","hazards","collapse","newtracks","ramp","pads","gorge","boost","bridges","gems","tracks","rocket","wall","surfaces","wings","glide","updraft","snow","woods","drawn","parts","avatars","icons"))
# Uçtan uca: bir kez derler, her otomatik test modunu çalıştırır, kayıtlarda hata/istisna/başarısız kontrol arar.
$sp = "C:\Users\burak\UnityProjects\SledRunner\Tools\out"
$report = "$sp\e2e-report.txt"
"E2E $(Get-Date)" | Out-File $report -Encoding utf8
$build = & "$PSScriptRoot\cycle.ps1" "e2e_build" "screens" 2>&1
if (-not ($build -match "Succeeded")) { "BUILD FAILED" | Out-File $report -Append; $build | Out-File $report -Append; exit 1 }
foreach ($m in $modes) {
  $t0 = Get-Date
  $null = & "$PSScriptRoot\cycle.ps1" "e2e_$m" $m -nobuild 2>&1
  $log = "$sp\e2e_$m.log"
  $n = (Get-ChildItem "$sp\e2e_$m" -ErrorAction SilentlyContinue | Measure-Object).Count
  $bad = Select-String -Path $log -Pattern "Exception|\[HOME\].*FAIL|FAIL|Missing|NullReference|IndexOutOfRange|error|abnormal|Assertion" -ErrorAction SilentlyContinue |
         Where-Object { $_.Line -notmatch "Licensing|Fallback handler|Shader warning|d3d11|WindowsPlayer|CrashReport|Microsoft Media Foundation|ArgumentException: Getting control|d3d12: failed to query info queue|Capture completed. Consult snow-report" } |
         Select-Object -First 6 | ForEach-Object { "    " + $_.Line }
  $secs = [int]((Get-Date) - $t0).TotalSeconds
  $status = if ($bad) { "ISSUES" } else { "ok" }
  "$m : $status  shots=$n  ${secs}s" | Out-File $report -Append
  if ($bad) { $bad | Out-File $report -Append }
}
"DONE" | Out-File $report -Append
