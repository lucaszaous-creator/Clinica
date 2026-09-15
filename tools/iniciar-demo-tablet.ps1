param([Parameter(Mandatory=$true)][string]$Site)
$ErrorActionPreference = 'Stop'
$raizTablet = Split-Path -Parent $PSScriptRoot
$interfaceTablet = Join-Path (Resolve-Path -LiteralPath $Site).Path 'portal'
if (!(Test-Path -LiteralPath (Join-Path $interfaceTablet 'index.html'))) { throw 'Checkout clinica-site sem portal/index.html.' }
& python (Join-Path (Split-Path $interfaceTablet -Parent) 'ferramentas/preparar-marca-portal.py')
if ($LASTEXITCODE -ne 0) { throw 'Falha ao preparar a marca original do site.' }
$pastaTablet = Join-Path $raizTablet 'artifacts'
New-Item -ItemType Directory -Path $pastaTablet -Force | Out-Null
$variaveisTablet = @('ConnectionStrings__Clinica','ASPNETCORE_ENVIRONMENT','Portal__Demo','Portal__Interface','Portal__BancoDemo')
$ambienteAnteriorTablet = @{}
foreach ($nomeTablet in $variaveisTablet) { $ambienteAnteriorTablet[$nomeTablet] = [Environment]::GetEnvironmentVariable($nomeTablet, 'Process') }
try {
    # Nunca herdar a conexão da clínica no modo fictício.
    Remove-Item Env:ConnectionStrings__Clinica -ErrorAction SilentlyContinue
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:Portal__Demo = 'true'
    $env:Portal__Interface = $interfaceTablet
    $env:Portal__BancoDemo = Join-Path $pastaTablet ('tablet-demo-' + [Guid]::NewGuid().ToString('N') + '.db')
    Write-Host 'Demonstração fictícia: http://127.0.0.1:18120 — demo / TabletDemo#2026'
    & dotnet run --project (Join-Path $raizTablet 'src/Clinica.Assinaturas.Api') --no-launch-profile
    if ($LASTEXITCODE -ne 0) { throw 'A demonstração não iniciou.' }
} finally {
    foreach ($nomeTablet in $variaveisTablet) { [Environment]::SetEnvironmentVariable($nomeTablet, $ambienteAnteriorTablet[$nomeTablet], 'Process') }
}
