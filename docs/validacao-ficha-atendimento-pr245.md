# Ficha e atendimento — validação da PR 245

Data: 08/10/2026. Escopo: pedidos explícitos do proprietário, com dados sintéticos e validações determinísticas. Não é auditoria adversarial nem aprovação visual do Jev.

## Caminhos e evidência

A ficha, atendimento e abas do paciente compartilham o contrato `ClinicoWebRegistro.Paginas.cs`. O host conserva autenticação, autorização, paciente em foco, dados e persistência. O React conserva as ações e parâmetros registrados.

| Região | Ações preservadas | Evidência automatizada |
|---|---|---|
| Cabeçalho da ficha | voltar, ficha, evolução, enfermagem, histórico, exames, dor, medidas, avaliações | Contratos de todas as rotas; WebView2 em 1440, 1044 e 900 px; cliques ficha → histórico → exames → ficha |
| Sessões e guias | paginação; **Ver sessão** diretamente pela ficha | Dois agendamentos sintéticos, um com evolução e outro sem; leitura real do primeiro; botão desabilitado e texto explícito no segundo |
| Sessão aberta | conteúdo, imprimir, correções, anexos, copiar | Clique real para abrir, verificar paciente e texto; imprimir PDF e conferir documento/paciente/data; abrir correções e anexos. Cópia tem cobertura de contrato, não teste de clipboard nesta execução |
| Dados e alertas | adicionar, editar, resolver, reabrir, descartar; incluir encerrados | Adicionar persistido; resolver cancelado/confirmado; reabrir restaurando ativo. `ListaProblemasTests` cobre regras de edição/descarte |
| Anamnese | abrir seis seções, editar, gravar, cancelar e versões | Escrever fora da edição recusado; seis seções persistidas; cancelamento da revisão não grava; cancelar edição restaura; revisão confirmada mantém versão anterior |
| Evolução | data, EVA antes/depois, retorno, texto rico, salvar; mapa | Gravação real SQLite, rascunho preservado entre abas, mapa cancelar/confirmar, gesto mouse/teclado no WebView2 |
| Evolução complementar | modelos, copiar última, campos complementares, vincular registro | Contratos explícitos; `ModeloEvolucaoTests`, `TesteCopiaEvolucaoEntrePacientesTests`, `VinculoDaEvolucaoTests`, `SessaoAnteriorNoAtendimentoTests`. Não foi simulada cada combinação pelo navegador |
| Iniciar, reabrir e concluir | estado da sessão, fechamento e materiais | Contratos/guardas; `FechamentoSessaoTests`, `EvolucaoNasceComAtendimentoTests`, `AtendimentoNaRecepcaoTests`, `ConvenioObrigatorioNoAtendimentoTests`; a matriz não equivale a clique em todas as variantes de fechamento |
| Histórico | buscar, abrir sessão, anexos, correções, copiar, linha do tempo | Leitura integral e tentativa de escrita recusada; cliques abrir/correções/anexos; `SessaoDoProntuarioTests` exige paridade de campos do domínio |
| Exames e anexos | registrar resultado, laudo, arquivo na ficha, cancelar/baixar | Resultado persistido e lido após clique na aba; `ResultadoExameTests`, `ProntuariosEExamesTests`, `MidiaDoProntuarioTests`. Seletores nativos de upload/download não automatizados |
| Medidas | registrar, escolher série, cancelar, exportar | Registro persistido, cancelar diálogo sem gravar; `MedidasClinicasTests`; CSV/seleção nativa não acionados nesta rodada |
| Avaliações | escolher escala, responder, aplicar, cancelar | Escala incompleta recusada e completa persistida; `InstrumentosAvaliacaoTests` |
| Dor | curva, atualizar, exportar | Navegação/contratos; regras de EVA da suíte de domínio. Exportação CSV não acionada nesta rodada |
| Enfermagem | passagem, consulta, cuidados, corrigir/cancelar/vincular, termos, folha | Contratos e rota por usuário de enfermagem; `EvolucaoEnfermagemTests`, `ConclusaoEnfermagemTabletTests`; fluxo infusão detalhado em `validacao-infusao-pr245.md` |
| Documentos | receita, atestado, comparecimento, pedido de exame, documentos, prescrições, termos e indicação | Contratos e serviços `DocumentosClinicosTests`, `AssinaturaDocumentoClinicoTests`, `ConsentimentoElegibilidadeTests`. Assinaturas externas/pagas não executadas |
| Infusão pela ficha | abrir prescrição com paciente correto; cancelar | Clique no menu Documentos → prescrição; paciente conferido; fechar não cria prescrição. Liberação/execução/PDF cobertos no harness dedicado |

## Correções desta rodada

1. A ponte web da anamnese aceitava editar o texto antes de acionar Editar. Agora `Editando & PodeEditar` controla o campo e a gravação; o botão Editar alterna com Gravar/Cancelar. O comportamento acompanha o modo de leitura já existente no WPF.
2. Sessões e guias da ficha não tinham entrada direta para a evolução. Foi acrescentado Ver sessão usando o leitor existente. Sem evolução vinculada, permanece o texto explícito e a ação não habilita. O host revalida paciente, agendamento, evolução vigente e permissão antes de abrir.

3. O workspace preservado mantinha também as listas de leitura carregadas antes da gravação: salvar evolução e abrir Histórico mostrava zero sessões. A abertura das abas agora recarrega somente suas listas (histórico, exames, ficha, dor, medidas e avaliações), mantendo os editores/rascunhos de evolução e anamnese. O teste não usa Atualizar histórico como contorno.

## Impressão e limites

O teste aciona **Imprimir esta sessão na interface WebView2**. Os serviços normais emitem o documento e geram o PDF. Somente a etapa de entrega ao seletor/leitor do Windows é substituída por um destino de arquivo sintético, por adaptador interno em escopo assíncrono, restaurado no Dispose. Não há variável de ambiente global nem mudança do comportamento normal. A validação confere PDF legível pelo parser com páginas e o documento de origem com paciente, data e evolução corretos. O complemento `verificar-pdf-ficha.py` extrai texto do PDF com PyMuPDF e exige o nome sintético do paciente e o texto exato da evolução. Não constitui teste da impressora física, do diálogo nativo ou inspeção visual de cada glifo.

Nenhuma base de produção é utilizada. SQLite vive em memória. Não foram efetuados envio, publicação de documento, assinatura paga, merge ou release.

## Execução

Comando centralizado (não executar builds simultâneos com publish):

```powershell
dotnet run --project tests/Clinica.Clinico.Web.Qa/Clinica.Clinico.Web.Qa.csproj -c Release -- --web
```

`Ficha.cs` contém os cenários novos de persistência/guardas. `FichaVisual.cs` contém os cliques reais. `Fluxos.cs` mantém os cenários clínicos existentes e captura 20 páginas. Execução desta rodada concluída com código de saída 0 no log `artifacts/fluxos-clinico-web.log`: contratos, persistência, seis seções da anamnese, alerta resolver/reabrir, abertura direta de sessão pela ficha, bloqueio da linha sem evolução, histórico atualizado após salvar, impressão, correções, anexos, exames e infusão aberta/cancelada pela ficha passaram. O complemento `python tests/Clinica.Clinico.Web.Qa/verificar-pdf-ficha.py` também passou: PDF de duas páginas com nome do paciente e evolução extraídos corretamente. Esses resultados não substituem a execução das classes de testes de domínio listadas nem demonstram os gestos explicitamente marcados como não exercitados.
