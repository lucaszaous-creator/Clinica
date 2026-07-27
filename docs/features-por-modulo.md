# Features por módulo

> O catálogo do que a **proposta comercial** (`ApresentacaoSemDor.pdf`) vendeu, distribuído
> pelos **quatro módulos** da suíte, com o estado real de cada feature conferido no código.
> É a lista que a gente vai quitando em parcelas.

**Módulos são os quatro apps:** Faturamento · Recepção · Financeiro · Gerente Geral.
Os 14 itens numerados da proposta são **features**, e cada uma precisa de um módulo dono.

## Como ler

| Estado | Significado |
|---|---|
| ✅ | Pronto e em produção |
| 🟡 | Existe, mas não como a proposta promete |
| ⬜ | Vendido e **ainda não existe** |
| 🔵 | Existe e **não** está na proposta (nasceu depois) |

Cada estado ✅ ou 🟡 cita o arquivo que o sustenta. Estado sem evidência não entra aqui.

## A regra que manda em tudo: o Faturamento está congelado

Ele fatura a clínica hoje. **Não se encosta nele.** Como os quatro apps compartilham
`Clinica.Domain`, `Clinica.Application` e `Clinica.Infrastructure`, a fronteira precisa ser
exata — senão nada poderia ser construído:

| Pode | Não pode |
|---|---|
| Criar entidades e serviços novos nas camadas compartilhadas | Alterar ou remover o que o faturamento já usa |
| Migration **aditiva** (tabela nova, coluna nova anulável) | Renomear ou remover coluna/tabela existente |
| Ler dados do faturamento a partir de outro módulo | Editar telas, ViewModels ou fluxos de `Clinica.Desktop` |
| Publicar release dos outros três apps | Republicar o faturamento por mudança que não é dele |

**Por isso a Fase 4 da arquitetura foi cancelada** — migrar o faturamento para módulo é,
por definição, encostar nele. Ver `arquitetura-multi-exe.md`.

## Onde cada feature da proposta foi parar

| # | Feature | Módulo dono | Estado | Parcela |
|---|---|---|---|---|
| 01 | Início — painel com semáforo | Faturamento / Recepção | ✅ / ✅ | 1 |
| 02 | Agenda multiprofissional | Recepção | 🟡 | 1 |
| 03 | Fila em kanban | Recepção | ✅ | 1 |
| 04 | Pacientes — cadastro 360º | Recepção | ✅ | 2 |
| 05 | Prontuário — evolução + EVA | Recepção | ✅ | 2 |
| 06 | Mapa corporal | Recepção | ⬜ | 3 |
| 07 | Prescrição | Recepção | ⬜ | 3 |
| 08 | Pacotes, planos e vouchers | Financeiro | ⬜ | 4 |
| 09 | Caixa, repasses e conciliação | Financeiro | 🟡 | 4 |
| 10 | Estoque | Financeiro | ⬜ | 4 |
| 11 | Marketing — NPS e recall | Gerente | ⬜ | 5 |
| 12 | BI — indicadores | Gerente | 🟡 | 5 |
| 13 | Permissões e LGPD | Gerente / Recepção | 🟡 | 5 |
| 14 | Faturamento TISS 4.01 | Faturamento | ✅ | — |

**Placar: 5 completas, 4 parciais, 5 inexistentes.**

| Estado | Features |
|---|---|
| ✅ Completas | 01 · 03 · 04 · 05 · 14 |
| 🟡 Parciais | 02 (falta a confirmação automática) · 09 (falta o repasse) · 12 (falta a tela no Gerente) · 13 (LGPD feito, permissões não) |
| ⬜ Inexistentes | 06 · 07 · 08 · 10 · 11 |

---

## Módulo FATURAMENTO — `Clinica.Desktop`

**Congelado.** Nenhuma feature nova entra aqui. Está listado para que ninguém tente
reconstruir o que já existe — e para que o Gerente saiba o que pode ler.

### Feature 14 · Faturamento TISS 4.01 — ✅

| Item | Estado | Onde |
|---|---|---|
| Motor de regras por convênio (Unimed Padrão/Intercâmbio, Amil, Petrobras) | ✅ | `Domain/Regras/`, `RegistroRegras` |
| BSV com inversão de datas | ✅ | modalidade `BsvComAcupuntura` |
| 2º código automático com data prevista +24h | ✅ | `AtendimentoService` |
| Lote → envio → retorno → glosa → recurso | ✅ | `LoteTissService` |
| XML TISS 4.01 com epílogo validado por hash | ✅ | `TissExportService`, `TissValidador` |
| Guia em PDF no leiaute ANS | ✅ | `GuiaTissPdfService` |
| Importar demonstrativo e pré-preencher o retorno | ✅ | `TissRetornoImport` |
| Recurso de glosa em XML | ✅ | `TissExportService.GerarRecursoGlosaXml` |
| Glosa com prazo de recurso e motivo ANS | ✅ | `GlosaService`, `MotivosGlosa` |
| Radar de prevenção de glosa | 🔵 | `PrevencaoGlosaService` |
| Convênios personalizados criados em runtime | 🔵 | `RegraGenerica`, `CatalogoConvenios` |
| Cota de sessões autorizadas (evita glosa 2006) | 🔵 | `AutorizacaoService` |

### Feature 01 · Início — painel com semáforo — ✅ (versão do faturamento)

| Item | Estado | Onde |
|---|---|---|
| Semáforo de urgência por guia | ✅ | `PendenciaService`, `DashboardViewModel` |
| KPIs, filtros por convênio e urgência | ✅ | `DashboardViewModel` |
| Baixa, WhatsApp e NC na própria linha | ✅ | `DashboardView.xaml` |
| Baixa em lote | ✅ | `Alertas/BaixaLoteWindow` |
| Rodada de não conformidade com bloqueio | ✅ | `RodadaPendenciasService` |
| **Agenda do dia e ocupação no painel** | ⬜ | Vai na Recepção, não aqui |

> O faturamento também tem hoje telas de Agenda, Pacientes e Consultas. Elas **permanecem
> como estão** e não evoluem — a versão que cresce é a da Recepção.

---

## Módulo RECEPÇÃO — `Clinica.Modulo.Recepcao`

O balcão e o ato clínico. É o módulo com mais dívida: **quatro das sete features são do
zero.**

### Feature 01 · Painel próprio da recepção — ✅ · parcela 1

| Item | Estado | Onde |
|---|---|---|
| Pendências de guias em destaque | ✅ | `PainelRecepcaoService.PendenciasDoDiaAsync` — recortadas para quem vem hoje |
| Agenda do dia e ocupação | ✅ | `PainelRecepcaoService.ResumoAsync`, `AgendaService.OcupacaoDoDiaAsync` |
| Atalho de 1 clique para o WhatsApp | ✅ | `Desktop.Shell/Componentes/Whatsapp` |

> O painel da Recepção **não é** o do faturamento. Lá a pergunta é "que guia vence
> primeiro"; aqui é "como está o dia": quem chegou, quem espera, quanto cada
> profissional tem na agenda. As guias pendentes aparecem só para os pacientes de HOJE
> — é o único momento barato de cobrar o documento.

### Feature 02 · Agenda multiprofissional — 🟡 · parcela 1

| Item | Estado | Onde / observação |
|---|---|---|
| Grade de horários com remarcação | ✅ | `AgendaView` (Recepção) + `AgendaService.RemarcarAsync` |
| Visão por profissional ou por sala | ✅ | `Agendamento.ProfissionalId`/`SalaId`, uma coluna por profissional |
| Encaixe rápido e lista de espera | ✅ | `Agendamento.Encaixe`, `ListaEsperaService` |
| Confirmação **automática** por WhatsApp | ⬜ | O envio de 1 clique existe; automatizar é campanha — vai com a feature 11 (parcela 5) |

> O choque de horário passou a ser por **intervalo e por recurso**: uma sessão de 30 min
> marcada às 14h colide com outra às 14h30, e o que colide é o profissional ou a sala
> (respeitando a capacidade dela). A agenda **recusa** o choque; a recepção pode assumir
> o **encaixe**, e aí ele fica registrado em vez de virar conflito silencioso. Quem não
> informa profissional nem sala — o faturamento — enxerga o comportamento de sempre.

### Feature 03 · Fila em kanban — ✅ · parcela 1

| Item | Estado | Onde |
|---|---|---|
| Fila do dia com confirmar/cancelar/faltou | ✅ | `FilaViewModel` |
| Colunas Aguardando · Chegou · Em atendimento · Finalizado | ✅ | `Agendamento.Etapa`, `FilaView` |
| Tempo de espera visível | ✅ | `Agendamento.EsperaMinutos`, atualizado a cada minuto na tela |
| Aviso de pendência já no check-in | ✅ | `AgendaService.ConfirmarPresencaAsync` + etiqueta no cartão |

> As colunas saem dos **carimbos de hora** (`ChegadaEm`, `InicioAtendimentoEm`), não de
> um status novo: o faturamento continua vendo o mesmo `StatusAgendamento` de sempre.
> "Concluir" é o antigo check-in — gera o atendimento e os códigos — e fica no fim do
> fluxo de propósito: a guia nasce quando a sessão de fato aconteceu.

### Feature 04 · Pacientes — cadastro 360º — ✅ · parcela 2

| Item | Estado | Onde |
|---|---|---|
| Dados, convênio e carteirinha | ✅ | `PacienteEdicaoViewModel` (Recepção) |
| Foto pela webcam | ✅ | `Desktop.Shell/Componentes/CameraServico`, `Retrato` |
| Histórico de sessões e guias | ✅ | `FichaPacienteViewModel` (Recepção): sessões, guias em aberto, última sessão |
| Validação de elegibilidade | ✅ | `ElegibilidadeService` |
| LGPD: consentimento registrado | ✅ | `ConsentimentoService`, `ConsentimentoLgpd` |

> A tela é **master-detail**: lista à esquerda, ficha à direita. O balcão trabalha com o
> paciente na frente — navegar para outra seção custaria um clique e o contexto a cada
> atendimento. A busca **não** foi reescrita: usa o `SeletorPacienteViewModel` da suíte
> com `limite: null`, como manda a convenção.

> **A elegibilidade é o que esta tela tem de mais valioso.** Carteirinha vencida e cota
> estourada hoje só aparecem na hora de faturar, quando a sessão já aconteceu e o
> prejuízo é certo. O `ElegibilidadeService` junta o que já existia espalhado
> (`Paciente.CarteirinhaVencida`, `AutorizacaoService`, consentimento) e responde no
> balcão. Ele **informa, nunca impede** — quem decide é a clínica.

### Feature 05 · Prontuário — evolução + EVA — ✅ · parcela 2

| Item | Estado | Onde |
|---|---|---|
| Escala de dor EVA por sessão | ✅ | `Evolucao.EvaAntes`/`EvaDepois`, régua de 0 a 10 na tela |
| Evolução em texto e estruturada | ✅ | queixa, conduta, evolução e orientações em campos próprios |
| Anexos e imagens no histórico | ✅ | `AnexoProntuario`, `ProntuarioService.AnexarAsync` |
| Evolução da dor ao longo do tratamento | 🔵 | `ProntuarioService.EvolucaoDaDorAsync` |

> **A EVA vale em PAR.** Medir só antes (ou só depois) não diz se o tratamento
> funcionou, então a `EvolucaoDaDorAsync` só considera as sessões com as duas medidas —
> deixar meia medida entrar faria a linha oscilar por falta de dado, não por dor. A tela
> mostra quantas sessões têm o par, para o número nunca parecer mais firme do que é.

> A régua da EVA é uma fileira de casas clicáveis, não campo de texto: no balcão a
> pergunta é feita em voz alta ("de 0 a 10, quanto dói?") e a resposta tem de caber num
> clique. Campo de texto aqui é o caminho mais curto para a medida não ser registrada.

> Os anexos ficam em tabela própria e a **lista vem por projeção, sem os bytes**: abrir o
> prontuário não pode arrastar megabytes pela rede (o banco é remoto). Só quem pede um
> arquivo específico materializa o conteúdo.

### Feature 06 · Mapa corporal — ⬜ · parcela 3

| Item | Estado |
|---|---|
| Mapa corporal interativo | ⬜ |
| Protocolo reutilizável entre sessões | ⬜ |
| Vinculado à evolução clínica | ⬜ |

### Feature 07 · Prescrição — ⬜ · parcela 3

| Item | Estado |
|---|---|
| Modelos de receita e orientação | ⬜ |
| Impressão com a marca SemDor | 🔵 infraestrutura existe (`MarcaSemDor`) |
| Assinatura e carimbo digitais | ⬜ |

---

## Módulo FINANCEIRO — `Clinica.Modulo.Financeiro`

### Feature 09 · Caixa, repasses e conciliação — 🟡 · parcela 4

| Item | Estado | Onde |
|---|---|---|
| Fluxo de caixa diário | ✅ | `CaixaViewModel` |
| Lançamento manual, realizar e cancelar | ✅ | `LancamentoEdicaoViewModel` |
| Conciliação com o atendimento | ✅ | `FinanceiroService.GuiasSemLancamentoAsync` |
| Produção do período | 🔵 | `ProducaoViewModel` |
| Plano de contas (categorias) | 🟡 | Serviço pronto, sem tela de gestão |
| **Repasse por profissional** | ⬜ | Depende de `Profissional` (parcela 1) |

> A fronteira é regra de projeto: **o dinheiro vive só em `LancamentoFinanceiro`**.
> `CodigoFaturamento` e `Atendimento` nunca ganham campo de valor.

### Feature 08 · Pacotes, planos e vouchers — ⬜ · parcela 4

| Item | Estado |
|---|---|
| Saldo de sessões por paciente | ⬜ |
| Vouchers e planos recorrentes | ⬜ |
| Baixa automática ao atender | ⬜ |

> Cuidado para não confundir com `AutorizacaoSessoes`, que é **cota do convênio** — outra
> coisa. Pacote é venda da clínica.

### Feature 10 · Estoque — ⬜ · parcela 4

| Item | Estado |
|---|---|
| Entrada e baixa por sessão | ⬜ |
| Alerta de mínimo e validade | ⬜ |
| Custo por atendimento | ⬜ |

---

## Módulo GERENTE GERAL — `Clinica.Gerente`

Ele **carrega os módulos dos outros** — então tem, por construção, tudo o que estiver
marcado acima. Só entram aqui as features que **só** fazem sentido nele.

Com a Fase 4 cancelada, o Gerente não herda as telas do faturamento. Ele ganha **telas
próprias de leitura** sobre os mesmos serviços compartilhados (`PendenciaService`,
`RelatorioService`, `LoteTissService`) — enxerga tudo sem tocar no app em produção, e
sendo só leitura não há risco de escrita concorrente.

### Feature 12 · BI — indicadores — 🟡 · parcela 5

| Item | Estado | Onde |
|---|---|---|
| Faturamento e taxa de glosa | ✅ | `RelatorioService` — falta a tela no Gerente |
| Envelhecimento e evolução mensal | 🔵 | `FaixaEnvelhecimento`, `ResumoMensal` |
| Ocupação e no-show | ⬜ | Depende de `Profissional`/`Sala` |
| Produtividade por profissional | ⬜ | Depende de `Profissional` |

### Feature 11 · Marketing — NPS e recall — ⬜ · parcela 5

| Item | Estado |
|---|---|
| NPS automático pós-consulta | ⬜ |
| Recall de pacientes inativos | ⬜ |
| Campanhas por WhatsApp | ⬜ |

### Feature 13 · Permissões e LGPD — 🟡 · parcela 5

| Item | Estado | Onde |
|---|---|---|
| Trilha de auditoria imutável | ✅ | `EventoAuditoria`, `RegistrarAuditoriaAsync` |
| Conformidade LGPD (consentimento) | ✅ | `ConsentimentoService` — entregue na parcela 2, na Recepção |
| Perfis e permissões finas | ⬜ | `Profissional` já existe; falta usuário, login e perfil |

> A metade LGPD saiu antes da hora, junto do cadastro do paciente (parcela 2) — é lá que
> o consentimento é colhido, no balcão. O que resta para a parcela 5 é o controle de
> acesso: usuário, login e perfil apontando para o `Profissional` que a parcela 1 criou.

---

## Documentos impressos — página 21 da proposta

**12 prometidos, 3 existem.**

> A base do **Relatório de evolução (EVA)** e da **Anamnese** já existe no domínio
> (`Evolucao`, `EvolucaoDaDor`); o que falta para os dois é o PDF, que vai na parcela 3
> junto com os outros documentos.

| Documento | Módulo | Estado | Onde |
|---|---|---|---|
| Capa de lote | Faturamento | ✅ | `CapaFaturamentoService` |
| Guia TISS SP/SADT | Faturamento | ✅ | `GuiaTissPdfService` |
| Fechamento | Faturamento | ✅ | `FechamentoPdfService` |
| Receita | Recepção | ⬜ | parcela 3 |
| Atestado | Recepção | ⬜ | parcela 3 |
| Comparecimento | Recepção | ⬜ | parcela 3 |
| Pedido de exame | Recepção | ⬜ | parcela 3 |
| Relatório de evolução (EVA) | Recepção | ⬜ | parcela 3 |
| Consentimento | Recepção | ⬜ | parcela 3 |
| Anamnese | Recepção | ⬜ | parcela 3 |
| Recibo | Financeiro | ⬜ | parcela 4 |
| Orçamento | Financeiro | ⬜ | parcela 4 |

---

## O bloqueio de fundação — resolvido na parcela 1

Até a parcela 1 não existia entidade `Profissional`, e `Agendamento` não tinha
`ProfissionalId` nem `SalaId`. Isso travava, de uma vez, agenda multiprofissional (02),
"quem atendeu" no prontuário (05), repasse por profissional (09), produtividade e
ocupação no BI (12) e perfis de acesso (13).

Agora existem `Profissional`, `Sala` e `ListaEspera`
(`src/Clinica.Domain/Entities/Equipe.cs`), e o agendamento ganhou `ProfissionalId`,
`SalaId`, `DuracaoMinutos`, `Encaixe`, `ChegadaEm` e `InicioAtendimentoEm`. A migration
(`20260727180000_FundacaoRecepcao`) é **puramente aditiva** — tabelas novas e colunas
novas anuláveis —, então o faturamento em produção segue lendo e gravando
`Agendamentos` sem saber que estes campos existem.

O que ainda depende disto, e vem nas parcelas seguintes: carimbar o **atendimento** com
quem atendeu (05), repasse (09), produtividade (12) e perfis (13). Nenhum deles perdeu
dado no caminho: o agendamento guarda o profissional e aponta para o atendimento que
gerou.

## Divergências da proposta

O documento já foi ao cliente. Estas três precisam de decisão comercial:

1. **Página 24 diz "Dois apps, um banco".** São **quatro** apps, um por perfil.
2. **Página 23 marca ✓ em "Prontuário com mapa corporal e EVA"** para a SemDor contra os
   concorrentes. Depois da parcela 2, **metade da afirmação passou a ser verdadeira**: o
   prontuário com EVA existe (feature 05). O **mapa corporal** (feature 06) continua não
   existindo — vai na parcela 3. Até lá, a página 23 segue afirmando mais do que o
   produto entrega.
3. **O cronograma não fecha** — mas encolheu. A Fase 1 (1–2 meses) foi fechada com as
   parcelas 1 e 2. O que resta do calendário original é decisão sua; as parcelas deste
   arquivo continuam sendo a ordem técnica correta.

## Parcelas

As parcelas **0 (instalável)**, **1 (fundação)** e **2 (cadastro e prontuário)** estão
entregues: os quatro apps instalam sozinhos, e a Recepção já tem painel próprio, agenda
multiprofissional com encaixe e lista de espera, fila em kanban, cadastro de
profissionais e salas, Pacientes 360º com foto e consentimento LGPD, e prontuário com
evolução, escala EVA e anexos. Com isso **a Fase 1 da proposta está fechada**. A próxima
é a **parcela 3 (ato clínico)**: mapa corporal, prescrição e os documentos impressos.

> Como o cliente recebe os quatro apps e o cronograma completo:
> [`entrega-ao-cliente.md`](entrega-ao-cliente.md).
