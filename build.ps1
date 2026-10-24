$ErrorActionPreference = 'Stop'
$bridgeCompiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (-not (Test-Path -LiteralPath $bridgeCompiler)) { throw '.NET Framework compiler is not installed.' }
$bridgeOutput = Join-Path $PSScriptRoot 'unsigned-build'
if (Test-Path -LiteralPath $bridgeOutput) { throw 'Output already exists. Preserve it and choose a new build directory.' }
New-Item -ItemType Directory -Path $bridgeOutput | Out-Null
foreach ($bridgeBits in @('x64', 'x86')) {
    $bridgeExe = Join-Path $bridgeOutput ('JvLinkProbe-' + $bridgeBits + '.exe')
    & $bridgeCompiler '/nologo' '/target:exe' ('/platform:' + $bridgeBits) '/reference:System.Web.Extensions.dll' ('/out:' + $bridgeExe) (Join-Path $PSScriptRoot 'JvLinkProbe.cs') (Join-Path $PSScriptRoot 'AssemblyInfo.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
    Get-FileHash -Algorithm SHA256 -LiteralPath $bridgeExe
}
Write-Output 'Unsigned build only. This script does not execute or sign the binaries.'
