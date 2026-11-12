$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$out = Join-Path $root 'bin'
New-Item -ItemType Directory -Force $out | Out-Null
$src = Get-ChildItem (Join-Path $root 'src') -Filter *.cs | ForEach-Object FullName
$refs = '/r:System.Web.Extensions.dll', '/r:System.Windows.Forms.dll', '/r:System.Drawing.dll', '/r:System.Management.dll'
& $csc /nologo /target:winexe /optimize+ /utf8output /codepage:65001 "/out:$out\应用分流助手.exe" "/win32icon:$root\src\app.ico" "/win32manifest:$root\src\app.manifest" @refs @src
if ($LASTEXITCODE) { throw 'build failed' }
if ($args -contains '-Test') {
  & $csc /nologo /target:exe /utf8output /codepage:65001 "/out:$out\RoutingTests.exe" /main:RoutingTests @refs @src (Join-Path $root 'tests\RoutingTests.cs')
  if ($LASTEXITCODE) { throw 'test build failed' }
}
Write-Host "OK -> $out\应用分流助手.exe"

