param([string]$name = "ui")
$sp = "C:\Users\burak\UnityProjects\SledRunner\Tools\out"
$src = "C:\Users\burak\UnityProjects\SledRunner"
$dst = "C:\Users\burak\UnityProjects\SledRunner_ci"
robocopy "$src\Assets" "$dst\Assets" /E /NFL /NDL /NJH /NJS /NP | Out-Null
$u = "C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Unity.exe"
$log = "$sp\build.log"
& $u -batchmode -quit -projectPath $dst -executeMethod SledSurfers.EditorTools.BuildTools.BuildWindowsTest -logFile $log | Out-Null
$errs = Select-String -Path $log -Pattern "error CS|\[BUILD\]" | Select-Object -First 10 | ForEach-Object { $_.Line }
$errs
if (-not ($errs -match "Succeeded")) { exit 1 }
$shots = "$sp\$name"
Remove-Item $shots -Recurse -ErrorAction SilentlyContinue
$p = Start-Process "$dst\Builds\WindowsTest\SledSurfers.exe" -ArgumentList "-screen-width 540 -screen-height 960 -screen-fullscreen 0 -autoshot `"$shots`" -logFile `"$shots.log`"" -PassThru
$p.WaitForExit(900000) | Out-Null
(Get-ChildItem $shots).Name -join " "
Select-String -Path "$shots.log" -Pattern "Exception|NullReference|\[UI\]" | Select-Object -First 10 | ForEach-Object { $_.Line }

