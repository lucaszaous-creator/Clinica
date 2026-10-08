param([Parameter(Mandatory=$true)][string]$Pasta)
$ErrorActionPreference = 'Stop'
$raizPublicada = (Resolve-Path -LiteralPath $Pasta).Path
$indices = @(Get-ChildItem -LiteralPath $raizPublicada -Recurse -File -Filter index.html)
if ($indices.Count -eq 0) { throw "Nenhuma interface web encontrada em $raizPublicada" }
foreach ($indice in $indices) {
    $html = Get-Content -LiteralPath $indice.FullName -Raw
    $referencias = [regex]::Matches($html, '(?:src|href)="([^"?#]+)(?:[?#][^"]*)?"')
    foreach ($referencia in $referencias) {
        $relativo = $referencia.Groups[1].Value
        if ($relativo -match '^(?:[a-z]+:|//|#)') { continue }
        $caminho = [IO.Path]::GetFullPath((Join-Path $indice.DirectoryName $relativo))
        if (!$caminho.StartsWith($raizPublicada + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Recurso fora da pasta publicada: $relativo"
        }
        if (!(Test-Path -LiteralPath $caminho -PathType Leaf)) { throw "Interface publicada sem recurso: $caminho" }
    }
    Write-Output "Recursos da interface conferidos: $($indice.FullName)"
}
