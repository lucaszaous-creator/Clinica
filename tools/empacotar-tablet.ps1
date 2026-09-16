param([Parameter(Mandatory=$true)][string]$Site)
$ErrorActionPreference = 'Stop'
$raizTablet = Split-Path -Parent $PSScriptRoot
$siteTablet = (Resolve-Path -LiteralPath $Site).Path
foreach ($repoTablet in @($raizTablet,$siteTablet)) {
    $estadoTablet = & git -C $repoTablet status --porcelain
    if ($LASTEXITCODE -ne 0 -or $estadoTablet) { throw "O checkout precisa estar limpo antes de empacotar: $repoTablet" }
}
$shaBackendTablet = (& git -C $raizTablet rev-parse HEAD).Trim()
$shaSiteTablet = (& git -C $siteTablet rev-parse HEAD).Trim()
$saidaTablet = Join-Path $raizTablet ('artifacts/tablet-release-' + $shaBackendTablet.Substring(0,12) + '-' + $shaSiteTablet.Substring(0,12))
if (Test-Path -LiteralPath $saidaTablet) { throw "Pacote já existe; confira-o antes de criar outro: $saidaTablet" }
$efTablet = Join-Path $raizTablet '.tools/dotnet-ef.exe'
if (!(Test-Path -LiteralPath $efTablet)) { throw 'Instale dotnet-ef 8.0.11 em .tools conforme o roteiro.' }
New-Item -ItemType Directory -Path $saidaTablet | Out-Null
Push-Location $raizTablet
try {
    & dotnet publish src/Clinica.Assinaturas.Api -c Release -r linux-x64 --self-contained true -o (Join-Path $saidaTablet 'app')
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao publicar API.' }
    & $efTablet migrations script 20260910120000_AssinaturaRemotaGuardadaAntesDaConferencia 20260916193849_PostoClinicoTablet --idempotent --project src/Clinica.Infrastructure --startup-project src/Clinica.Infrastructure -o (Join-Path $saidaTablet 'migracao-tablet.sql')
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao gerar SQL; pacote incompleto.' }
    & python (Join-Path $siteTablet 'ferramentas/empacotar-portal.py')
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao empacotar interface.' }
    Copy-Item -LiteralPath (Join-Path $siteTablet 'artifacts/portal-release') -Destination (Join-Path $saidaTablet 'portal') -Recurse
    $configRaizTablet = Join-Path $raizTablet 'deploy/tablet'
    foreach ($configTablet in (Get-ChildItem -LiteralPath $configRaizTablet -File -Recurse | Where-Object { $_.FullName -notmatch '[\\/](bin|obj|__pycache__)[\\/]' })) {
        $destinoConfigTablet = Join-Path (Join-Path $saidaTablet 'configuracao') $configTablet.FullName.Substring($configRaizTablet.Length+1)
        New-Item -ItemType Directory -Path (Split-Path -Parent $destinoConfigTablet) -Force | Out-Null
        Copy-Item -LiteralPath $configTablet.FullName -Destination $destinoConfigTablet
    }
    & dotnet publish deploy/tablet/homologacao/PrepararHomologacao.csproj -c Release -r linux-x64 --self-contained true -o (Join-Path $saidaTablet 'preparar')
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao preparar ferramenta de homologação.' }
    Copy-Item -LiteralPath (Join-Path $raizTablet 'docs/operacao-termos-tablet.md') -Destination $saidaTablet
    Copy-Item -LiteralPath (Join-Path $raizTablet 'docs/atendimento-tablet.md') -Destination $saidaTablet
    $hashesTablet = [ordered]@{}
    foreach ($arquivoTablet in (Get-ChildItem -LiteralPath $saidaTablet -File -Recurse | Sort-Object FullName)) {
        $relativoTablet = $arquivoTablet.FullName.Substring($saidaTablet.Length+1).Replace('\','/')
        $hashesTablet[$relativoTablet] = (Get-FileHash -LiteralPath $arquivoTablet.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
    [ordered]@{contrato=1;backend=$shaBackendTablet;interface=$shaSiteTablet;arquivos=$hashesTablet} |
        ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $saidaTablet 'manifesto.json') -Encoding utf8
    Write-Host "Pacote preparado (não instalado): $saidaTablet"
} finally { Pop-Location }
