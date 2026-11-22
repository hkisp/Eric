$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$out = Join-Path $root 'publish'
$resolvedRoot = [IO.Path]::GetFullPath($root)
$resolvedOut = [IO.Path]::GetFullPath($out)
if ($resolvedOut -ne [IO.Path]::GetFullPath((Join-Path $resolvedRoot 'publish'))) { throw 'Unexpected publish directory.' }
if (Test-Path -LiteralPath $out) {
    Get-ChildItem -LiteralPath $out -File -Filter 'IDCXray.*' | Remove-Item -Force
}
$dotnet = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) { $dotnet = (Get-Command dotnet -ErrorAction Stop).Source }
& $dotnet publish (Join-Path $root 'src\IDCXray.csproj') -c Release -r win-x64 --self-contained -o $out
if ($LASTEXITCODE -ne 0) { throw 'Self-contained publish failed.' }
New-Item -ItemType Directory -Force (Join-Path $out 'core') | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'src\core\xray.exe'),(Join-Path $root 'src\core\geoip.dat'),(Join-Path $root 'src\core\geosite.dat') -Destination (Join-Path $out 'core') -Force
if (-not (Test-Path -LiteralPath (Join-Path $out 'Eric agent.exe'))) { throw 'Published apphost not found.' }
$desktop = [Environment]::GetFolderPath('Desktop')
$zip = Join-Path $desktop 'Eric-agent-release.zip'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zipTemp = Join-Path $root 'Eric-agent-release.tmp.zip'
if (Test-Path -LiteralPath $zipTemp) { Remove-Item -LiteralPath $zipTemp -Force }
[IO.Compression.ZipFile]::CreateFromDirectory($out, $zipTemp, [IO.Compression.CompressionLevel]::Optimal, $false)
Copy-Item -LiteralPath $zipTemp -Destination $zip -Force
Remove-Item -LiteralPath $zipTemp -Force
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut((Join-Path $desktop 'Eric agent.lnk'))
$shortcut.TargetPath = Join-Path $out 'Eric agent.exe'
$shortcut.WorkingDirectory = $out
$shortcut.Save()
