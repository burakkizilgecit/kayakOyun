param([string]$method = "SledSurfers.EditorTools.SimTests.RunAll", [string]$extra = "")
$sp = "C:\Users\burak\UnityProjects\SledRunner\Tools\out"
$src = "C:\Users\burak\UnityProjects\SledRunner"
$dst = "C:\Users\burak\UnityProjects\SledRunner_ci"
robocopy "$src\Assets" "$dst\Assets" /E /NFL /NDL /NJH /NJS /NP | Out-Null
$u = "C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Unity.exe"
$log = "$sp\sim.log"
& $u -batchmode -quit -projectPath $dst -executeMethod $method -logFile $log @($extra -split " " | Where-Object { $_ }) | Out-Null
$errs = Select-String -Path $log -Pattern "error CS" | Select-Object -First 10 | ForEach-Object { $_.Line }
if ($errs) { $errs; exit 1 }
$lines = Get-Content $log -Encoding UTF8
$start = ($lines | Select-String -Pattern "^\[SIM\]" | Select-Object -First 1).LineNumber
if ($start) { $lines[($start - 1)..($start + 3000)] | Where-Object { $_ -match "^\s|^\[SIM\]|^Parkur" } | Select-Object -First 3000 }

