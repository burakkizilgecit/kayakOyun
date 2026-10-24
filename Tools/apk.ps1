# Android test APK'sı: kaynakları derleme projesine kopyalar, orada derler, APK'yı ana projenin Builds/Android klasörüne koyar.
$sp = "C:\Users\burak\UnityProjects\SledRunner\Tools\out"
$src = "C:\Users\burak\UnityProjects\SledRunner"
$dst = "C:\Users\burak\UnityProjects\SledRunner_ci"
robocopy "$src\Assets" "$dst\Assets" /E /NFL /NDL /NJH /NJS /NP | Out-Null
$u = "C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Unity.exe"
$log = "$sp\apk.log"
& $u -batchmode -quit -projectPath $dst -buildTarget Android -executeMethod SlingRiders.EditorTools.BuildTools.BuildAndroidTest -logFile $log | Out-Null
Select-String -Path $log -Pattern "error CS|\[ANDROID BUILD\]|Android APK build failed|BuildFailedException|Android SDK|JDK|NDK.*not" | Select-Object -First 12 | ForEach-Object { $_.Line }
$apk = "$dst\Builds\Android\SlingRiders-test.apk"
if (Test-Path $apk) {
  New-Item -ItemType Directory -Force "$src\Builds\Android" | Out-Null
  Copy-Item $apk "$src\Builds\Android\SlingRiders-test.apk" -Force
  $f = Get-Item "$src\Builds\Android\SlingRiders-test.apk"
  "APK: $($f.FullName)  $([math]::Round($f.Length / 1MB, 1)) MB  $($f.LastWriteTime)"
} else { "APK üretilmedi"; exit 1 }
