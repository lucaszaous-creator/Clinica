# Validação da agenda e da prescrição

Compilar primeiro `src/Clinica.Desktop.Shell/WebClinica/frontend` com `npm ci` e `npm run build`.

No Windows, executar `dotnet run --project tools/validar-agenda-documentos/Qa.csproj` com .NET 8 e WebView2. As janelas de teste ficam fora da área visível. Não se lê configuração de produção: o banco de infusão/recepção é SQLite em memória e todos os registros são fictícios.

`Program.cs` valida os componentes e a fronteira WebView2. `FilaQa.cs` verifica a busca React filtrando a tabela real da recepção. `InfusaoQa.cs` exercita editor, adapter, ViewModel e gravação/reabertura no banco sintético. Capturas em `artifacts/agenda-documentos`.

Para a revisão Jev explicitamente solicitada, `python tools/validar-agenda-documentos/revisar_jev.py --chave CAMINHO_FORA_DO_REPOSITORIO` prepara os pedidos; `--executar` os envia. A chave nunca é incluída nos pedidos, respostas ou Git. Não se atribui ao Jev execução de testes nem avaliação de produção.
