# QA de Faturamento, formulários administrativos e apresentação compartilhada

Executar em Windows com WebView2:

```powershell
dotnet run --project tools/validar-faturamento-web/Qa.csproj -- --web
dotnet run --project tools/validar-faturamento-web/Qa.csproj -- --entrada-only
dotnet run --project tools/validar-faturamento-web/Qa.csproj -- --ferramentas-only
dotnet run --project tools/validar-faturamento-web/Qa.csproj -c Release -- --buscas-ui-only
```

Sem argumentos, confere contratos e gravações reais dos serviços contra SQLite em memória. `--web` acrescenta as capturas das oito rotas em três dimensões, seis modos de entrada em duas dimensões, contingência, canvas principal e segunda tela da assinatura. `--entrada-only` concentra os fluxos de autenticação e configuração. `--ferramentas-only` abre o host com registros Gerente/Recepção/Faturamento para os recursos de cabeçalho e treinamento.

Os dados são sintéticos. O teste de conexão usa porta indisponível local e nunca grava configurações. O teste de treinamento usa diretório novo em artifacts/ferramentas-web e não altera o progresso original. Se houver Documentos no cache, copia o vídeo para essa pasta e usa a validação de hash do serviço antes de exercitar reprodução e retomada. Não publica vídeos, não envia mensagens, não usa o banco configurado da clínica.

Evite builds simultâneos da mesma árvore WPF. Com dependências já compiladas, `/p:BuildProjectReferences=false` evita competir pelo obj dos projetos compartilhados.

`--buscas-ui-only` reproduz a digitação no DOM do WebView2 real, sem atribuir campos às ViewModels ou injetar mensagens de operação no host. Cobre busca de guias por nome/CPF, ausência de resultados, anotação persistida, cancelamento de baixa, lançamento financeiro com paciente encontrado e selecionado, filtros do caixa, criação/edição/cancelamento de usuário, escolha de paciente na Auditoria e na Guarda, seletor compartilhado e metas decimais com ponto/vírgula. Todas as gravações são conferidas no SQLite sintético em memória. As capturas ficam em `artifacts/buscas-digitadas-web`.
