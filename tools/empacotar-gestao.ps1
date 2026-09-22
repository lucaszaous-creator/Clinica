param([string]$Destino = (Join-Path $PSScriptRoot '../artifacts/entrega'))

$ErrorActionPreference = 'Stop'
$raizProjeto = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$pastaEntrega = Join-Path ([IO.Path]::GetFullPath($Destino)) ('Clinica-Gestao-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
if (Test-Path -LiteralPath $pastaEntrega) { throw 'Já existe uma entrega com este nome. Execute novamente para gerar outra pasta.' }
New-Item -ItemType Directory -Path $pastaEntrega -Force | Out-Null
$modulos = [ordered]@{
    Gerente = 'Clinica.Gerente'
    Recepcao = 'Clinica.Recepcao'
    Clinico = 'Clinica.Clinico'
    Faturamento = 'Clinica.Desktop'
    Financeiro = 'Clinica.Financeiro'
}
foreach ($modulo in $modulos.GetEnumerator()) {
    $projeto = Join-Path $raizProjeto ('src/' + $modulo.Value + '/' + $modulo.Value + '.csproj')
    & dotnet publish $projeto -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:DebugType=None -p:DebugSymbols=false -o (Join-Path $pastaEntrega $modulo.Key)
    if ($LASTEXITCODE -ne 0) { throw ('Falha ao publicar ' + $modulo.Key + '. O pacote não foi concluído.') }
}
Copy-Item -LiteralPath (Join-Path $raizProjeto 'docs/gestao-consolidada.md') -Destination $pastaEntrega
$revisao = (& git -C $raizProjeto rev-parse HEAD).Trim()
@"
Clínica — gestão em cinco módulos
Código: $revisao
Gerado em: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz')
Plataforma: Windows x64; runtime incluído.

Comece pelo Gerente. Cada pasta contém um aplicativo com suas funções e permissões.
Leia gestao-consolidada.md antes de atualizar as estações e aplicar a migration.
Este pacote não configura o banco nem é prova de instalação em produção.
"@ | Set-Content -LiteralPath (Join-Path $pastaEntrega 'LEIA-ME.txt') -Encoding utf8
$manifesto = Get-ChildItem -LiteralPath $pastaEntrega -Recurse -File | ForEach-Object {
    [pscustomobject]@{ arquivo = [IO.Path]::GetRelativePath($pastaEntrega, $_.FullName); sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
}
$manifesto | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $pastaEntrega 'sha256.json') -Encoding utf8
$arquivoZip = $pastaEntrega + '.zip'
Compress-Archive -LiteralPath $pastaEntrega -DestinationPath $arquivoZip -CompressionLevel Optimal
Write-Output ('ENTREGA: ' + $arquivoZip)
