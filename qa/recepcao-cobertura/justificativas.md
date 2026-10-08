# Correspondência de controles antigos e novos

Comparação determinística dos registros efetivamente publicados por `IRegistroModuloWeb`, compostos com as páginas e diálogos compartilhados. A primeira versão que chamava fábricas estáticas isoladas omitia `DialogosConfiguracao`; foi corrigida. O Gerente não apresenta vínculos sem correspondência literal na composição completa.

O extrator lê os `Command` e os bindings de controles de entrada dos XAML. O relatório JSON preserva fonte, linha, VM inferida, caminho e destinos. Uma correspondência por nome não prova equivalência funcional; candidatos compostos/homônimos ficam separados. Controles implementados por eventos ou comportamento anexado precisam de verificação específica e não entram na contagem.

| Diferença nominal | Correspondência / situação |
|---|---|
| Assinatura `EhSim` / `EhNao` | Campo `Resposta` com `OpcoesResposta` Sim/Não, mesmo modelo de declaração; fluxo persistido pelo QA de Faturamento. |
| Busca `SugestaoNaTela` / `ListandoTodos` | Indicadores de modo somente leitura; operação é `LigarSugestao` / `DesligarSugestao`. Recepção recebeu também “Com horário hoje” condicionado a `Seletor.TemSugestao`, permitindo retornar depois de “Ver todos”. |
| Enfermagem `MostrandoHoje` / `MostrandoTodos` | Estado do modo de seleção; operações `Hoje` / `Todos` publicadas no registro clínico compartilhado. |
| Pacote `AVista` | Complemento de `APrazo`; caixa “Cobrar depois” informa explicitamente que desmarcada significa pagar agora. |
| Conta / recorrente `EhEntrada` | Complemento de `EhSaida`; caixa “Conta a pagar” informa que desmarcada significa conta a receber. |
| Conciliação `Recebido` | Complemento de `AReceber`, campo booleano publicado na linha de sessão particular. |
| Shell: pesquisa, avisos, treinamento, fila, fechar popovers | Contrato `SuiteFerramentasWeb` e comportamento do frontend; não são páginas/diálogos dos módulos. Adaptador testado com busca de aba, lista de avisos, leitura sem apagar histórico, restrição do catálogo e persistência do progresso. Integração visual é do host compartilhado. |
| Shell: senha e trocar usuário | Ações explícitas do `SuiteWebView`, fora dos registros de página. |
| `RetornoView` da Recepção | View legada: `ModuloRecepcao.CriarTela` já publica `AcompanhamentoView` / `AcompanhamentoViewModel` na chave de retorno. Os bindings antigos `DiasSemVir`, `FiltroNomeRetorno`, `EsconderChamados`, `SoComTelefone`, `RespondeuCommand` não são apresentados como implementados na nova página. |
| `ProntuarioView` da Recepção | View legada: a chave `prontuario` já publicava a lista de pacientes com seção 3. Nova rota conserva lista → `consultorio-prontuario`; os comandos antigos `NovaSessao`, `VerSessao`, `EditarSessao`, `ExcluirSessao` pertencem à View sem entrada atual. |
| Clínico `Administrativo.WhatsApp` / `Administrativo.Editar` | Composição administrativa publica `Administrativo.DadosWeb.WhatsApp` / `.Editar`. |
| Clínico `PontoSelecionado.*` | Editores `Nome`, `Tecnica`, `Observacao` por linha na tabela de pontos; seleção e remoção publicadas pelo agente clínico. |
| Clínico `Marcado` | Estado visual dos chips; agente clínico acrescentou coluna booleana para acompanhar comandos de filtro. |
| Faturamento `DarBaixaEmLote` | Fachada `DarBaixaSelecionadas` chama a operação original após selecionar IDs por `AlternarSelecao`; não se trata de pagamento. QA de baixa em lote executado pelo agente de Faturamento. |
| Faturamento atalhos Salvar / Imprimir / Atualizar | Integração de teclado no frontend compartilhado; ausência do nome no registro não substitui a verificação desses atalhos pelo host. |

As ausências do Clínico identificadas na comparação (atualizar documentos, emissão de tipo, prescrever infusão, assinaturas pendentes e paginação das sessões) foram corrigidas pelo dono do módulo e deixaram a lista final de ausências. Foram acrescentados os atalhos financeiros de período, contas fixas, validades, regras e catálogo de pacotes nos registros Financeiro original e Suite, além de Catálogo na página compartilhada. Mantêm comandos, diálogos e guardas reais.

Resultado final desta conferência: **76 páginas e 97 diálogos, zero divergência contratual**; 1.418 vínculos XAML comparados, sendo 1.140 correspondências por VM, 239 candidatos compostos/homônimos e 39 diferenças nominais. As 39 restantes pertencem aos aliases, estados derivados, ferramentas do host e Views legadas descritos nesta tabela. Não é uma declaração de que todas as combinações operacionais foram testadas; tampouco certifica automaticamente os 239 homônimos.

Executar na raiz da worktree:

1. `C:/Users/ROYA/.dotnet/dotnet.exe run --project qa/recepcao-cobertura/RecepcaoCobertura.csproj`
2. `python qa/recepcao-cobertura/comparar.py`

Saída: `artifacts/recepcao-cobertura/{registros,vinculos,comparacao}.json` e `comparacao.md`.
