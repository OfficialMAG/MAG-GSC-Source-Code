# Compile « MAG GSC INJECTOR GHOSTS.exe » depuis ces sources (version publique : sans MAG Menu, sans obfuscation).
# Prérequis : Windows avec .NET Framework 4.8 (csc.exe est fourni avec Windows). Lancer : powershell -ExecutionPolicy Bypass -File build.ps1
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot; $src = "$root\src"
$fw = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'; $wpf = "$fw\WPF"
$refs = @("$wpf\PresentationFramework.dll", "$wpf\PresentationCore.dll", "$wpf\WindowsBase.dll", "$fw\System.Xaml.dll", "$fw\System.Xml.dll",
          "$fw\System.Windows.Forms.dll", "$fw\System.Core.dll", "$fw\System.Drawing.dll", "$fw\System.IO.Compression.dll") | ForEach-Object { "/r:$_" }
$files = Get-ChildItem "$src\*.cs" | ForEach-Object { $_.FullName }
New-Item -ItemType Directory -Force "$root\bin" | Out-Null
$out = "$root\bin\MAG GSC INJECTOR GHOSTS.exe"
& "$fw\csc.exe" /nologo /codepage:65001 /target:winexe /optimize+ "/out:$out" "/win32icon:$src\mag.ico" "/win32manifest:$src\app.manifest" "/resource:$src\mag.ico,mag.ico" "/resource:$src\natives_ghosts.txt,natives_ghosts.txt" "/resource:$src\ops_ghosts.txt,ops_ghosts.txt" "/resource:$src\ps4debug.bin,ps4debug.bin" $refs $files
if ($LASTEXITCODE -ne 0) { throw "compilation echouee" }
"OK : $out"
