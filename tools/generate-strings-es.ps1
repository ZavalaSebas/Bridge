$ErrorActionPreference = 'Stop'
$enPath = Join-Path $PSScriptRoot '..\Bridge\Resources\Strings.resx'
$esPath = Join-Path $PSScriptRoot '..\Bridge\Resources\Strings.es.resx'

[xml]$xml = Get-Content -Raw -Encoding UTF8 $enPath
Copy-Item $enPath $esPath -Force
[xml]$xml = Get-Content -Raw -Encoding UTF8 $esPath

. (Join-Path $PSScriptRoot 'strings-es-translations.ps1')

foreach ($data in $xml.root.data) {
    $name = $data.name
    if ($Translations.ContainsKey($name)) {
        $data.value = $Translations[$name]
    }
}

$xml.Save($esPath)
$missing = @($xml.root.data | ForEach-Object { $_.name } | Where-Object { -not $Translations.ContainsKey($_) })
if ($missing.Count -gt 0) {
    Write-Warning "Missing $($missing.Count) translations: $($missing -join ', ')"
    exit 1
}

Write-Host "Generated $esPath ($($Translations.Count) keys)"
