$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1')
$installerCompiler = Join-Path $PSScriptRoot 'tools\inno-compiler\ISCC.exe'
if (!(Test-Path -LiteralPath $installerCompiler)) { throw '缺少 Inno Setup 6 编译器，请放置到 tools\inno-compiler。' }
$runtimePackage = Join-Path $PSScriptRoot 'vendor\vc_redist.x64.exe'
if (!(Test-Path -LiteralPath $runtimePackage)) { throw '缺少微软官方 x64 C++ 运行库安装程序。' }
$runtimeSignature = Get-AuthenticodeSignature -LiteralPath $runtimePackage
if ($runtimeSignature.Status -ne 'Valid' -or $runtimeSignature.SignerCertificate.Subject -notlike '*Microsoft Corporation*') { throw '微软运行库签名无效，拒绝打包。' }
& $installerCompiler /Q (Join-Path $PSScriptRoot 'packaging\installer.iss')
if ($LASTEXITCODE -ne 0) { throw '安装包生成失败。' }
$releasePath = Join-Path $PSScriptRoot 'release'
$packageHashes = foreach ($name in @('轻录-Win10-x64-安装包.exe','轻录-Windows-x64.zip')) {
    $hash = Get-FileHash -LiteralPath (Join-Path $releasePath $name) -Algorithm SHA256
    $hash.Hash.ToLowerInvariant() + '  ' + $name
}
Set-Content -LiteralPath (Join-Path $releasePath 'SHA256.txt') -Value $packageHashes -Encoding UTF8
Write-Output '安装包已生成到 release。'
