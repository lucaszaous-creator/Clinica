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
| 01 | Início — painel com semáforo | Faturamento / Recepção | ✅ / ⬜ | 1 |
| 02 | Agenda multiprofissional | Recepção | 🟡 | 1 |
| 03 | Fila em kanban | Recepção | 🟡 | 1 |
| 04 | Pacientes — cadastro 360º | Recepção | 🟡 | 2 |
| 05 | Prontuário — evolução + EVA | Recepção | ⬜ | 2 |
| 06 | Mapa corporal | Recepção | ⬜ | 3 |
| 07 | Prescrição | Recepção | ⬜ | 3 |
| 08 | Pacotes, planos e vouchers | Financeiro | ⬜ | 4 |
| 09 | Caixa, repasses e conciliação | Financeiro | 🟡 | 4 |
| 10 | Estoque | Financeiro | ⬜ | 4 |
| 11 | Marketing — NPS e recall | Gerente | ⬜ | 5 |
| 12 | BI — indicadores | Gerente | 🟡 | 5 |
| 13 | Permissões e LGPD | Gerente | ⬜ | 5 |
| 14 | Faturamento TISS 4.01 | Faturamento | ✅ | — |

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

### Feature 01 · Painel próprio da recepção — ⬜ · parcela 1

| Item | Estado |
|---|---|
| Pendências de guias em destaque | ⬜ (lê `PendenciaService`, já existe) |
| Agenda do dia e ocupação | ⬜ |
| Atalho de 1 clique para o WhatsApp | 🔵 já existe no faturamento; replicar |

### Feature 02 · Agenda multiprofissional — 🟡 · parcela 1

| Item | Estado | Observação |
|---|---|---|
| Grade de horários com remarcação | ✅ | Existe **no faturamento** (`Secao.Agenda`) |
| Visão por profissional ou por sala | ⬜ | `Agendamento` não tem `ProfissionalId` nem `SalaId` |
| Encaixe rápido e lista de espera | ⬜ | |
| Confirmação automática por WhatsApp | ⬜ | Hoje o envio é manual, 1 clique |

### Feature 03 · Fila em kanban — 🟡 · parcela 1

| Item | Estado | Onde |
|---|---|---|
| Fila do dia com confirmar/cancelar/faltou | ✅ | `FilaViewModel` — mas em **lista**, não kanban |
| Colunas Chegou · Em atendimento · Finalizado | ⬜ | |
| Tempo de espera visível | ⬜ | |
| Aviso de pendência já no check-in | ✅ | `AgendaService.ConfirmarPresencaAsync` |

### Feature 04 · Pacientes — cadastro 360º — 🟡 · parcela 2

| Item | Estado | Onde |
|---|---|---|
| Dados, convênio e carteirinha | ✅ | Existe no faturamento; replicar na Recepção |
| Foto pela webcam | ✅ | `CameraServico`, `Retrato` — é a webcam da recepção |
| Histórico de sessões e guias | 🟡 | `FichaPacienteViewModel` (faturamento) |
| Validação de elegibilidade | ⬜ | |
| LGPD: consentimento registrado | ⬜ | |

### Feature 05 · Prontuário — evolução + EVA — ⬜ · parcela 2

| Item | Estado |
|---|---|
| Escala de dor EVA por sessão | ⬜ |
| Evolução em texto e estruturada | ⬜ |
| Anexos e imagens no histórico | ⬜ |

Nada existe. Entidades, serviço, telas e testes do zero.

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

### Feature 13 · Permissões e LGPD — ⬜ · parcela 5

| Item | Estado | Onde |
|---|---|---|
| Trilha de auditoria imutável | ✅ | `EventoAuditoria`, `RegistrarAuditoriaAsync` |
| Perfis e permissões finas | ⬜ | Depende de `Profissional` |
| Conformidade LGPD (consentimento) | ⬜ | |

---

## Documentos impressos — página 21 da proposta

**12 prometidos, 3 existem.**

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

## O bloqueio de fundação

Não existe entidade `Profissional`, e `Agendamento` não tem `ProfissionalId` nem `SalaId`
(`src/Clinica.Domain/Entities/Agendamento.cs`). Isso trava, de uma vez:

- agenda multiprofissional (02)
- quem atendeu, no prontuário (05)
- repasse por profissional (09)
- produtividade e ocupação no BI (12)
- perfis de acesso (13)

É por isso que a fundação é a **parcela 1**. E ela é puramente aditiva — tabela e colunas
novas —, então não encosta no faturamento.

## Divergências da proposta

O documento já foi ao cliente. Estas três precisam de decisão comercial:

1. **Página 24 diz "Dois apps, um banco".** São **quatro** apps, um por perfil.
2. **Página 23 marca ✓ em "Prontuário com mapa corporal e EVA"** para a SemDor contra os
   concorrentes. São as features 05 e 06 — **não existem**. É a afirmação mais exposta do
   documento.
3. **O cronograma não fecha.** A Fase 1 (1–2 meses) inclui prontuário com EVA: domínio,
   telas, PDF e testes do zero. As parcelas deste arquivo são a ordem técnica correta; o
   calendário contra o cliente é decisão sua.

> Como o cliente recebe os quatro apps, em que ordem e o que precisa estar pronto antes:
> [`entrega-ao-cliente.md`](entrega-ao-cliente.md).
