$text = [Console]::In.ReadToEnd()
$lines = $text -split "`r?`n"
$filtered = $lines | Where-Object {
    $_ -notmatch '^Co-authored-by:\s*Cursor\b' -and
    $_ -notmatch '^Made-with:\s*Cursor\b'
}
while ($filtered.Count -gt 0 -and [string]::IsNullOrWhiteSpace($filtered[-1])) {
    $filtered = $filtered[0..($filtered.Count - 2)]
}
[Console]::Out.Write(($filtered -join "`n"))
