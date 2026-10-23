# A Recepção em Blazor (out/2026)

> Pedido da direção: as telas da Recepção em WPF "parecem arcaicas" — refazê-las no padrão
> Blazor da casa (`docs/telas-blazor.md`), como o Financeiro e o Faturamento de guias.

## 1. O que mudou de tecnologia

| Chave (contrato de navegação — NÃO mudou) | Tela Blazor | Substitui (WPF removido) |
|---|---|---|
| `painel-recepcao` (Início) | `InicioRecepcao` | `PainelView` + `PainelViewModel` |
| `pedidos-enfermagem` | `PedidosDaEnfermagem` | o item de menu (a `PedidosEnfermagemView` **fica**: a faixa da Grade ainda a usa) |
| `fila` (aba **Dia** da Agenda) | `AgendaDoDia` | `FilaView` + `FilaViewModel` |
| `ocupacao-agenda` (aba **Visão geral**) | `VisaoGeralDaAgenda` | o item (o `OcupacaoAgendaViewModel` fica: o hospedeiro reaproveita as janelas dele) |
| `lancamentos` | `LancamentosDoBalcao` | `LancamentosView` + VM |
| `consultas` | `ConsultasDeConvenio` | `ConsultasView` + VM |
| `pagamentos-recepcao` | `PagamentosDoBalcao` | o item (`PagamentosView` fica para o validador do Windows) |
| `agenda-confirmacoes` | `ConfirmacoesDeAgenda` | `ConfirmacoesView` + VM |
| `retornos-a-marcar` | `RetornosAMarcar` | o item (a WPF fica para o validador do Windows) |
| `equipe` | `ProfissionaisESalas` | `EquipeView` + VM |
| ~~`canal-sms`~~ (saiu na fase 3 Recepção×CRM, out/2026 — `docs/canal-sms.md`) | ~~`CanalDeSms`~~ | — |
| `metas` (Gerente) | `MetasDaClinica` | `MetasView` + VM + `MetaWindow` |
| `tendencias` (**nova**) | `TendenciasDeAtendimento` | — (Financeiro, Faturamento e aba de Relatórios/BI) |

Continuam WPF, de propósito: a **Grade** (o vão clicável e a janela do horário), o **Lançar e
marcar**, a ficha do paciente e as janelas modais (editar horário, fechar sessão, convênio).
As telas Blazor chegam a elas por três pontes do hospedeiro, uma por responsabilidade:
`IJanelasDaAgenda` (`JanelasDaAgendaWpf`), `IAgendaDaVisaoGeral` (`AgendaDaVisaoGeral`) e
`IBalcaoDaRecepcao` (`BalcaoDaRecepcao`). Sem o registro, as telas caem num "indisponível"
que DIZ que a ação é do aplicativo da Recepção — é assim que a prévia e o bUnit as abrem.

## 2. Paridade

Cada frente devolveu a lista WPF → Blazor (comando, filtro, coluna, atalho, permissão). O
que a migração corrigiu ao reler os ViewModels inteiros:
- **Metas**: duas metas de sessões com recortes diferentes recebiam o realizado uma da outra
  (casava por indicador + mês + profissional; agora por `MetaId`); "Corrigir" com chave
  editável criava uma meta nova e deixava a antiga; editar apagava as observações; alvo zero
  era recusado, contradizendo o diálogo de exclusão ("ausência ≠ zero").
- **Profissionais**: salvar sem mexer nas habilitações regravava a lista inteira; a
  especialidade inativa no catálogo sumia do select e era apagada no Salvar.
- **Retornos**: o Marcar abria sempre em hoje — a ponte `IMarcacaoDeHorario` ganhou o dia.

Desvios declarados: o detalhe do pedido da enfermagem virou formulário sobre a lista; o
filtro de profissional da Agenda do dia é combo, não chips. (A campanha de SMS, que exigia
`GerenciarCampanhas`, saiu na fase 3 Recepção×CRM.)

## 3. O que ainda falta conferir

- Fotos em 1024 e 1366 da Agenda do dia e de Profissionais e salas (a prévia esgotou o
  tempo nas frentes) — `tools/PreviaBlazor/foto.js` antes do merge.
- O `ValidarLayoutWindows` (CI do PR) passou a conferir a Visão geral pelo componente
  hospedado (`TelaBlazor.Componente`).

## 4. UX · DX · CX

- 🧑‍💻 **UX** — as telas do balcão passam a ter a mesma cara do Financeiro e do Faturamento;
  nenhuma rota mudou, então o que a recepcionista sabe onde fica continua lá.
- 🛠️ **DX** — o que cada tela AFIRMA mora na Application (`*NaTela`), testado; o Razor só
  desenha. As janelas WPF que ficaram entram por três interfaces pequenas, não por cópia.
- 🤝 **CX** — o mesmo navegador por janela (sem a tela preta); os relógios de releitura e o
  "voltar à aba relê" foram mantidos. O build oficial continua sendo o do PR no Windows.
