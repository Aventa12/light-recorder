$ErrorActionPreference = 'Stop'
$projectPath = $PSScriptRoot
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$libraryPath = Join-Path $projectPath 'vendor\screenrecorderlib\build\x64\ScreenRecorderLib.dll'
$distributionPath = Join-Path $projectPath 'dist'
if (!(Test-Path -LiteralPath $compilerPath)) { throw '需要 Windows .NET Framework 4.x 编译器。' }
if (!(Test-Path -LiteralPath $libraryPath)) { throw '缺少 ScreenRecorderLib 7.0.1，请先运行 fetch-dependency.py。' }
New-Item -ItemType Directory -Force -Path $distributionPath | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $projectPath 'tools') | Out-Null
& $compilerPath /nologo /target:exe /reference:System.Drawing.dll "/out:$projectPath\tools\BuildIcon.exe" (Join-Path $projectPath 'tools\BuildIcon.cs')
if ($LASTEXITCODE -ne 0) { throw '图标转换工具编译失败。' }
try {
    & (Join-Path $projectPath 'tools\BuildIcon.exe') (Join-Path $projectPath 'assets\icon.png') (Join-Path $projectPath 'assets\app.ico')
    if ($LASTEXITCODE -ne 0) { throw '图标转换失败。' }
} finally {
    Remove-Item -LiteralPath (Join-Path $projectPath 'tools\BuildIcon.exe') -ErrorAction SilentlyContinue
}
Copy-Item -LiteralPath $libraryPath -Destination $distributionPath
$sourcePaths = @('src\App.cs','src\Native.cs','src\Overlays.cs','src\RecorderEngine.cs','tests\SelfTest.cs','tests\OverlayTest.cs','src\AssemblyInfo.cs','src\HealthCheck.cs') | ForEach-Object { Join-Path $projectPath $_ }
& $compilerPath /nologo /target:winexe /platform:x64 /optimize+ /utf8output "/win32icon:$projectPath\assets\app.ico" "/win32manifest:$projectPath\src\app.manifest" "/out:$distributionPath\轻录.exe" "/reference:$libraryPath" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Runtime.Serialization.dll /reference:System.Xml.dll $sourcePaths
if ($LASTEXITCODE -ne 0) { throw '编译失败。' }
Copy-Item -LiteralPath (Join-Path $projectPath 'vendor\screenrecorderlib\LICENSE') -Destination (Join-Path $distributionPath 'ScreenRecorderLib-LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $projectPath '使用说明.txt') -Destination $distributionPath
Copy-Item -LiteralPath (Join-Path $projectPath 'src\轻录.exe.config') -Destination $distributionPath
Add-Type -AssemblyName System.IO.Compression.FileSystem
$releasePath = Join-Path $projectPath 'release'
New-Item -ItemType Directory -Force -Path $releasePath | Out-Null
$archiveStream = [IO.File]::Create((Join-Path $releasePath '轻录-Windows-x64.zip'))
$portableArchive = [IO.Compression.ZipArchive]::new($archiveStream, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($name in @('轻录.exe','轻录.exe.config','ScreenRecorderLib.dll','ScreenRecorderLib-LICENSE.txt','使用说明.txt')) {
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($portableArchive, (Join-Path $distributionPath $name), ('轻录/' + $name), [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally {
    $portableArchive.Dispose()
    $archiveStream.Dispose()
}
Write-Output '编译完成：dist\轻录.exe'
Write-Output '便携包已生成到 release。'
