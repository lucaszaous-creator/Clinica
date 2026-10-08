param(
    [string]$Destino = "artifacts/teste-pr245",
    [string]$Dotnet = "dotnet"
)
$ErrorActionPreference = 'Stop'
$raizProjeto = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$pastaSaida = [IO.Path]::GetFullPath((Join-Path $raizProjeto $Destino))
if (!$pastaSaida.StartsWith($raizProjeto + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'A pasta de saída deve estar dentro desta cópia do projeto.'
}
New-Item -ItemType Directory -Force -Path $pastaSaida | Out-Null
$aplicativos = [ordered]@{
    Recepcao = 'Clinica.Recepcao'
    Clinico = 'Clinica.Clinico'
    Financeiro = 'Clinica.Financeiro'
    Faturamento = 'Clinica.Desktop'
    Gerente = 'Clinica.Gerente'
}
foreach ($nome in $aplicativos.Keys) {
    $projeto = $aplicativos[$nome]
    $saidaAplicativo = Join-Path $pastaSaida $nome
    $log = Join-Path $pastaSaida ("compilacao-" + $nome + '.log')
    & $Dotnet publish (Join-Path $raizProjeto "src/$projeto/$projeto.csproj") -c Release -r win-x64 --self-contained true -m:1 -p:BuildInParallel=false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -p:ClinicaTesteLocal=true -p:Version=1.0.245-test.20261008.2 -o $saidaAplicativo *> $log
    if ($LASTEXITCODE -ne 0) { throw "Falha ao compilar $nome. Consulte $log" }
    foreach ($recurso in @("$projeto.exe", 'WebSuite/wwwroot/index.html', 'WebSuite/wwwroot/entrada.html', 'WebSuite/wwwroot/entrada.js', 'WebSuite/wwwroot/assinatura-paciente.html')) {
        if (!(Test-Path -LiteralPath (Join-Path $saidaAplicativo $recurso))) { throw "$nome sem recurso obrigatório: $recurso" }
    }
    Write-Output "${nome}: executável de teste e interface local conferidos."
}
Copy-Item -LiteralPath (Join-Path $raizProjeto 'docs/teste-pr245.md') -Destination (Join-Path $pastaSaida 'LEIA-PRIMEIRO.md')
Get-ChildItem -LiteralPath $pastaSaida -Recurse -File | Where-Object { $_.Extension -notin @('.log', '.sha256') } | ForEach-Object {
    $hash = Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256
    $relativo = [IO.Path]::GetRelativePath($pastaSaida, $_.FullName)
    "$($hash.Hash.ToLowerInvariant())  $relativo"
} | Set-Content -LiteralPath (Join-Path $pastaSaida 'arquivos.sha256') -Encoding utf8
Write-Output "Pacote local pronto: $pastaSaida"
