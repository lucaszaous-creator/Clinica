# Pedidos da gerência — novembro/2026

A gerente da clínica mandou treze pedidos, em três blocos. Este documento diz, pedido a pedido,
**onde a resposta mora** — porque metade já existia e a regra 1 de tela ("uma porta por pergunta")
manda reaproveitar a tela que já responde antes de criar outra.

## O mapa: pedido → tela

| # | Pedido | Onde | O que mudou nesta entrega |
|---|---|---|---|
| 1 | Filtrar a agenda por período específico | Recepção › Agenda › **Visão geral** → botão **Período** | **Novo**: modo Período com "De/até" (até 93 dias), setas que andam o mesmo tamanho, KPIs, lista de todos os horários e ocupação por profissional |
| 2 | Filtrar por procedimento: BSV, consulta e acupuntura | Visão geral (modos **Dia** e **Período**) | **Novo**: pílulas Todos · Acupuntura · BSV · Consulta, com a contagem antes do clique. BSV + acupuntura no mesmo dia conta como BSV |
| 3 | Buscas por convênio | Recepção › **Lançamentos** (convênio + período); Faturamento › Pendências / Consulta de guias / Faturados | Já existia; os filtros de convênio agora estão nas três telas do faturamento, no mesmo lugar |
| 4 | Pacientes efetivamente atendidos no mês/período | Recepção › **Lançamentos** › Atendimentos | **Novo**: o resumo diz "N atendimento(s) · M paciente(s)" (paciente distinto pelo cadastro, não pelo nome) e **Exportar CSV** da lista filtrada |
| 5 | Agenda dos profissionais por semana, mês ou período | Visão geral: Semana · Mês · **Período** + seletor de profissional | **Novo** o modo Período (Semana e Mês já existiam) |
| 6 | Filtrar pendências de documentos | Faturamento › **Pendências** | **Novo**: paciente (nome ou telefone), período do atendimento e **como obter** (app do paciente, sistema do convênio, ligação) — o recorte "o que depende de documento do paciente" |
| 7 | Emitir e imprimir relatório de guias pendentes | Pendências → **Imprimir relatório** (PDF) e **Exportar CSV** | **Novo**: PDF em paisagem com o recorte escrito no cabeçalho, a mais atrasada primeiro |
| 8 | Filtrar pendências e baixas por período | Pendências: De/até (atendimento). Faturados: De/até (baixa) + atalhos **Este mês · Mês passado · Últimos 3 / 12 meses** | **Novo** nas pendências; os atalhos são novos nos faturados |
| 9 | Padronizar o layout de pendências e baixas como a Consulta de guias | Pendências e Faturados | **Novo**: mesmos campos, na mesma ordem (paciente, período, código, modalidade, especialidade, convênio), mesma faixa de resumo do recorte e mesmas colunas (Atendimento · Paciente · Convênio · Modalidade e código) |
| 10 | Cadastrar valores dos procedimentos por convênio | Gerente › **Preços por convênio** | Já existia (por convênio × consulta/acupuntura/eletro/BSV/preventivo, com especialidade e vigência). É a tabela que o relatório do item 12 usa |
| 11 | Baixas de pagamento por paciente | Faturamento › **Faturados**, campo Paciente | Já filtrava; agora com os atalhos de período e um aviso quando se procura um paciente num período curto ("use Últimos 12 meses") |
| 12 | Relatório de atendimentos por médico, com valores (folha e repasse) | Financeiro › **Repasses** → **Atendimentos por profissional** | **Novo**: cada atendimento realizado, por profissional, com **valor de tabela** (preço por convênio na vigência do dia) e **recebido no caixa**, lado a lado; resumo por profissional, detalhe, PDF e CSV |
| 13 | Atendimentos por convênio com período | Recepção › **Lançamentos** (convênio + período + CSV) | Já existia o filtro; o CSV é novo |

## Decisões que não são óbvias

- **Período das pendências é o do ATENDIMENTO**, como na Consulta de guias. A data prevista muda
  com a regra do convênio; o dia em que o paciente veio, não. Guia sem data de atendimento
  conhecida fica fora de um período declarado.
- **O relatório por profissional não soma o que não sabe.** Atendimento de convênio com uma
  guia sem preço cadastrado mostra "sem preço (1 guia)" e fica fora do total — e o papel diz
  quantos ficaram de fora. Somar só as guias com preço daria uma folha menor com cara de conferida.
- **Valor de tabela e recebido nunca se somam**: o primeiro é o que a operadora paga pelas
  guias; o segundo é o que entrou no caixa (particular, coparticipação, pacote). Somar contaria
  a mesma sessão duas vezes.
- **Quem atendeu vem do horário realizado** (o mesmo alimentador do Repasses): o atendimento não
  guarda profissional, e o horário cancelado que manteve o atendimento não é sessão dada.
  Atendimento estornado sai.
- **Procedimento = família da modalidade** (a mesma cor do traço na agenda). "BSV" inclui o BSV
  com acupuntura no mesmo dia; "Acupuntura" é a simples e a com eletro.

## 🧑‍💻 UX

- Pendências: os filtros e o resumo do recorte agora estão onde a faturista já os encontra na
  Consulta de guias; o relatório impresso leva o recorte escrito, para o papel não ser lido como
  "tudo". Em 1024 px a tabela das pendências passou a caber inteira (antes o último botão ficava
  cortado): a data do atendimento desce para baixo da prevista.
- Faturados: um clique em "Últimos 12 meses" responde "o que já foi baixado desse paciente".
  Em 1024 px a glosa desce para a célula da baixa, para o nome do paciente não sumir.
- Agenda: Período é um botão ao lado de Dia/Semana/Mês; as datas substituem o "ir para".
  Período inválido mostra a frase onde estaria a lista, sem consultar.
- Relatório por profissional: abre já no mês que a tela de Repasses mostra; "Detalhar" mostra
  cada atendimento sem sair do formulário.

## 🛠️ DX

- Uma peça de desenho nova: `RelatorioListaPdf` (PDF de lista em paisagem), usada pelos dois
  relatórios; quem decide o que entra é o serviço de cada um.
- Regras novas na Application, com teste (`PedidosGerenciaNov2026Tests`,
  `AtendimentosPorProfissionalTests`): `FiltroPendencias` (paciente, período, forma),
  `PainelPendenciasFaturamento.RelatorioPdf/LinhaRelatorio`, `FiltroFaturados` (modalidade,
  especialidade), `GuiasFaturadasNaTela.Periodo`, `VisaoGeralDaAgendaNaTela.PeriodoPersonalizado
  /ContagemPorProcedimento`, `LancamentosNaTela.PacientesDistintos/LinhaCsv` e o
  `AtendimentosPorProfissionalService`.
- **Nenhuma consulta nova ao banco e nenhuma migration**: o relatório por profissional usa as
  leituras que o Repasses e os Lançamentos já usam, em sequência no mesmo escopo.
- `PendenciaCodigo.DataAtendimento` e `LinhaLancamentoBalcao.PacienteId` são propriedades
  `init` aditivas — os records são compartilhados com o faturamento em produção.

## 🤝 CX

- **Quais apps publicar**: Pendências e Faturados são telas do **faturamento** (e do Gerente);
  a Visão geral e os Lançamentos, da **Recepção** (e do Gerente); o relatório por profissional,
  do **Financeiro** (e do Gerente). Sem migration: nenhum risco ao banco em produção.
- O que aparece amanhã para quem fatura: os filtros novos e dois botões (Imprimir relatório,
  Exportar CSV) na tela de pendências. Nada some, nenhuma permissão nova — o relatório e o CSV
  pedem **Ver faturamento**; o relatório por profissional pede **Ver financeiro**.
- O valor do relatório por profissional depende da tabela de **Preços por convênio** estar
  cadastrada; o próprio relatório avisa quando faltar preço e diz onde cadastrar.
