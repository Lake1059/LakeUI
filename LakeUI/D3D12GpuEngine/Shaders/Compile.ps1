param([string]$Compiler = '')
$ErrorActionPreference = 'Stop'
if (!$Compiler) {
    $sdk = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10/bin'
    $Compiler = (Get-ChildItem $sdk -Directory | Where-Object { $_.Name -match '^10\.' } | Sort-Object { [version]$_.Name } -Descending | ForEach-Object { Join-Path $_.FullName 'x64/fxc.exe' } | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1)
}
if (!$Compiler) { throw 'Install the Windows SDK shader compiler or specify -Compiler.' }
foreach ($name in 'Geometry','Image') {
    foreach ($stage in 'vs','ps') {
        $entry = $stage.ToUpperInvariant()
        if ($name -eq 'Geometry') { $entry += 'Main' }
        & $Compiler /nologo /Ges /O3 /T "${stage}_5_0" /E $entry /Fo (Join-Path $PSScriptRoot "$name.$stage.cso") (Join-Path $PSScriptRoot "$name.hlsl")
        if ($LASTEXITCODE) { throw "Shader compile failed for $name $stage" }
    }
}
& $Compiler /nologo /Ges /O3 /T ps_5_0 /E PSSubpixel /Fo (Join-Path $PSScriptRoot 'ImageSubpixel.ps.cso') (Join-Path $PSScriptRoot 'Image.hlsl')
if ($LASTEXITCODE) { throw 'Shader compile failed for ImageSubpixel ps' }
