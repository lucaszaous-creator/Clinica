param([Parameter(Mandatory=$true)][string]$Site)
$ErrorActionPreference = 'Stop'
$raizPortal = Split-Path -Parent $PSScriptRoot
$sitePortal = (Resolve-Path -LiteralPath $Site).Path
foreach ($repoPortal in @($raizPortal,$sitePortal)) {
    $estadoPortal = & git -C $repoPortal status --porcelain
    if ($LASTEXITCODE -ne 0 -or $estadoPortal) { throw "Checkout deve estar limpo: $repoPortal" }
}
$shaApiPortal = (& git -C $raizPortal rev-parse HEAD).Trim()
$shaSitePortal = (& git -C $sitePortal rev-parse HEAD).Trim()
$nomePortal = 'tablet-continuidade-' + $shaApiPortal.Substring(0,12) + '-' + $shaSitePortal.Substring(0,12)
$saidaPortal = Join-Path $raizPortal ('artifacts/' + $nomePortal)
if (Test-Path -LiteralPath $saidaPortal) { throw 'Este pacote já existe.' }
New-Item -ItemType Directory -Path $saidaPortal | Out-Null
& dotnet publish (Join-Path $raizPortal 'src/Clinica.Assinaturas.Api') -c Release -r linux-x64 --self-contained true -o (Join-Path $saidaPortal 'app')
if ($LASTEXITCODE -ne 0) { throw 'Falha ao publicar API.' }
& python (Join-Path $sitePortal 'ferramentas/empacotar-portal.py')
if ($LASTEXITCODE -ne 0) { throw 'Falha ao preparar interface.' }
Copy-Item -LiteralPath (Join-Path $sitePortal 'artifacts/portal-release') -Destination (Join-Path $saidaPortal 'portal') -Recurse
Copy-Item -LiteralPath (Join-Path $raizPortal 'deploy/tablet/atualizar-posto.py') -Destination $saidaPortal
Copy-Item -LiteralPath (Join-Path $raizPortal 'deploy/tablet/migracao-infusao.sql') -Destination $saidaPortal
Copy-Item -LiteralPath (Join-Path $raizPortal 'docs/continuidade-portal.md') -Destination $saidaPortal
$hashesPortal = [ordered]@{}
foreach ($arquivoPortal in (Get-ChildItem -LiteralPath $saidaPortal -File -Recurse | Sort-Object FullName)) {
    $relativoPortal = $arquivoPortal.FullName.Substring($saidaPortal.Length+1).Replace('\','/')
    $hashesPortal[$relativoPortal] = (Get-FileHash -LiteralPath $arquivoPortal.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
}
[ordered]@{contrato=3;backend=$shaApiPortal;interface=$shaSitePortal;migracao_minima='20260916193849_PostoClinicoTablet';migracao_nova='20260926120000_DevolucaoInfusaoExterna';arquivos=$hashesPortal} |
    ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $saidaPortal 'manifesto.json') -Encoding utf8
$pacotePortal = Join-Path (Split-Path -Parent $saidaPortal) ($nomePortal + '.tar.gz')
& tar -czf $pacotePortal -C (Split-Path -Parent $saidaPortal) $nomePortal
if ($LASTEXITCODE -ne 0) { throw 'Falha ao arquivar pacote.' }
Get-FileHash -LiteralPath $pacotePortal -Algorithm SHA256
