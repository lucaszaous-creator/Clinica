# Features por módulo

> O mapa entre o que a **apresentação comercial** promete
> (`docs/apresentacao/apresentacao-semdor.html`) e o que cada módulo da suíte entrega.
> Serve para evoluir um módulo de cada vez sem perder de vista o que foi vendido.

A apresentação foi feita quando existia **um** produto ("Clínica SemDor — Sistema de
Faturamento") e **um** executável. A suíte multi-exe partiu isso em quatro
(`docs/arquitetura-multi-exe.md`). Nenhuma feature sumiu no caminho — mas várias
mudaram de dono, e é isso que este arquivo registra.

## Como ler

| Estado | Significado |
|---|---|
| ✅ | Pronto e em produção |
| 🟡 | Existe, mas não como a apresentação promete |
| ⬜ | Prometido na apresentação e **ainda não existe** |
| 🔵 | Existe, mas **não** está na apresentação (nasceu depois) |

**A regra de dono:** uma feature pertence ao módulo de **quem faz o trabalho**, não de
quem olha o resultado. A recepção marca e faz o check-in; o faturamento cobra a guia; o
financeiro registra o dinheiro. Quem só consulta ganha uma visão, não a feature.

**O Gerente Geral não aparece nas tabelas abaixo de propósito**: ele carrega todos os
módulos, então tem, por construção, tudo o que estiver marcado aqui. Ele só tem seção
própria no fim deste arquivo, para o que **só** faz sentido nele.

---

## Faturamento — `Clinica.Desktop`

O módulo que a apresentação descreve. É também o que está em produção na clínica, e por
isso o último a ser mexido (Fase 4 da arquitetura).

### Painel de pendências — *Tela 01 da apresentação*

| Feature | Estado | Onde |
|---|---|---|
| Semáforo de urgência por guia (verde/amarelo/vermelho) | ✅ | `PendenciaService`, `DashboardViewModel` |
| 2º código visível até a baixa | ✅ | `PendenciaService.CodigosPendentesAsync` |
| KPIs no topo (em aberto, urgentes, consultas a renovar, total) | ✅ | `DashboardViewModel` |
| Filtros por convênio e urgência | ✅ | `DashboardViewModel.FiltroConvenio/FiltroUrgencia` |
| Ações na linha: baixa, WhatsApp, NC | ✅ | `DashboardView.xaml` |
| Baixa em lote | ✅ | `Alertas/BaixaLoteWindow` |
| Consultas a renovar e carteirinhas a vencer | ✅ | `PendenciaService` |

### Rodada de não conformidade — *Tela 03*

| Feature | Estado | Onde |
|---|---|---|
| Prazo por guia, com bloqueio na abertura | ✅ | `RodadaPendenciasService`, `RodadaPendenciasFluxo` |
| Decisão obrigatória: baixa ou NC justificada | ✅ | `RodadaPendenciasWindow` |
| NC reabre sozinha quando o paciente volta | ✅ | `AtendimentoService.LancarAsync` |
| Carência de 1ª execução (backlog não trava tudo) | ✅ | `ParametrosService.ChaveInicioRodadaPrazo` |
| Aba própria de NC + NC proativa pelo painel | ✅ | `NaoConformidadesViewModel` |
| **Texto da apresentação diz "10 dias após o atendimento"** | 🟡 | Ver *Dívidas da apresentação* |

### Novo atendimento e motor de regras — *Tela 02*

| Feature | Estado | Onde |
|---|---|---|
| 2º código gerado automaticamente com data prevista +24h | ✅ | `AtendimentoService`, `Domain/Regras/` |
| Uma regra por convênio (Unimed Padrão/Intercâmbio, Amil, Petrobras) | ✅ | `RegistroRegras` |
| BSV com inversão de datas | ✅ | modalidade `BsvComAcupuntura` dentro das regras |
| Convênio personalizado criado em runtime | 🔵 | `RegraGenerica`, `CatalogoConvenios` |
| Escolha de qual código sai primeiro (modalidade dupla) | ✅ | `NovoAtendimentoViewModel` |
| Capa inicial em PDF | ✅ | `CapaFaturamentoService` |
| Baixa da 1ª guia na própria tela de lançamento | 🔵 | `NovoAtendimentoViewModel` |
| Aviso de guia pendente / NC do paciente ao lançar | 🔵 | `PendenciaService.PendenciasDoPacienteAsync` |
| Aviso de cota de sessões autorizadas (evita glosa 2006) | 🔵 | `AutorizacaoService` |

### Ciclo TISS — *Tela 04*

| Feature | Estado | Onde |
|---|---|---|
| Lote → envio → retorno → glosa → recurso | ✅ | `LoteTissService` |
| XML TISS 4.01 (consulta e SP/SADT) | ✅ | `TissExportService` |
| Epílogo validado por hash antes de sair | ✅ | `TissValidador` |
| XSD oficial opcional | ✅ | `%APPDATA%\ClinicaFaturamento\tiss\schemas` |
| Guia exportada nunca entra em dois lotes | ✅ | `LoteTissService` |
| Importar demonstrativo e pré-preencher o retorno | ✅ | `TissRetornoImport` |
| Guia em PDF no leiaute ANS | ✅ | `GuiaTissPdfService` |

### Glosas — *Tela 05*

| Feature | Estado | Onde |
|---|---|---|
| Prazo de recurso com data-limite e semáforo | ✅ | `GlosaService`, `PendenciaService` |
| Motivo padronizado pela tabela ANS | ✅ | `Domain/Regras/MotivosGlosa` |
| Recurso de glosa em XML | ✅ | `TissExportService.GerarRecursoGlosaXml` |
| Radar de prevenção de glosa na exportação | 🔵 | `PrevencaoGlosaService` |

### Relatórios — *"Relatórios que provam"*

| Feature | Estado | Onde |
|---|---|---|
| Taxa de baixa | ✅ | `ResumoFaturamento.TaxaBaixa` |
| Taxa de glosa por convênio | ✅ | `FaturamentoPorConvenio.TaxaGlosa` |
| Tempo médio do atendimento até a baixa | ✅ | `ResumoFaturamento.TempoMedioBaixaDias` |
| Envelhecimento das pendências e evolução mensal | 🔵 | `FaixaEnvelhecimento`, `ResumoMensal` |

---

## Recepção — `Clinica.Modulo.Recepcao`

A apresentação chama a tela do faturamento de "Recepção" porque, no produto único, era a
secretária que fazia tudo. Com a suíte, **a recepção vira um módulo próprio** — e as
features de atendimento ao paciente são dela, não do faturamento.

| Feature | Estado | Observação |
|---|---|---|
| Fila do dia (confirmar presença, cancelar, marcar falta) | ✅ | Entregue na Fase 1 |
| Check-in que gera o atendimento e os códigos | ✅ | `AgendaService.ConfirmarPresencaAsync` — alimenta o faturamento sem redigitar |
| Agenda em grade de horários, com remarcação | 🟡 | Existe, mas **no faturamento** (`Secao.Agenda`) |
| Cadastro de paciente | 🟡 | Existe no faturamento (`Secao.Pacientes`) |
| Foto do paciente pela webcam | 🟡 | Existe no faturamento — e é literalmente a webcam da recepção |
| Carteirinha vencida / cota de sessões: avisar na chegada | 🟡 | Existe no lançamento do faturamento |
| Tela de setup da conexão própria | ⬜ | Hoje depende do Faturamento instalado na máquina |

**Decisão pendente:** as linhas 🟡 acima estão hoje no faturamento e são candidatas
naturais a migrar para o módulo de Recepção na Fase 4 — ou a virar um módulo
compartilhado ("Cadastro"), já que o faturamento também precisa delas. Não é mecânico;
depende de quem você quer que instale o quê.

---

## Financeiro — `Clinica.Modulo.Financeiro`

**Nada aqui foi prometido na apresentação** — ela é sobre faturamento, e faturamento
neste produto não tem campo de dinheiro. O módulo nasceu depois, e por isso é todo 🔵.

| Feature | Estado | Onde |
|---|---|---|
| Caixa do mês (entradas, saídas, saldo realizado e previsto) | ✅ | `CaixaViewModel` |
| Lançamento manual, realizar e cancelar (com motivo) | ✅ | `LancamentoEdicaoViewModel` |
| Conciliação: guia efetivada que não virou receita | ✅ | `FinanceiroService.GuiasSemLancamentoAsync` |
| Produção do período (volume de códigos) | ✅ | `ProducaoViewModel` |
| Plano de contas (categorias) | 🟡 | Serviço pronto; sem tela de gestão |
| Repasse por profissional | ⬜ | Depende de uma entidade `Profissional` que não existe |
| Contas recorrentes e centro de custo | ⬜ | — |

> A fronteira é regra de projeto, não acaso: **o dinheiro vive só em
> `LancamentoFinanceiro`**. `CodigoFaturamento` e `Atendimento` nunca ganham campo de
> valor, e a dependência aponta só do financeiro para o faturamento.

---

## Transversal — `Clinica.Desktop.Shell` e camadas compartilhadas

A apresentação vende isto como "Por dentro / pronto para produção". Não é de nenhum
módulo: é de todos.

| Feature | Estado | Onde |
|---|---|---|
| Trilha de auditoria (baixa, estorno, glosa, lote) | ✅ | `IClinicaRepositorio.RegistrarAuditoriaAsync` |
| Multiusuário sem sobrescrever (concorrência otimista `xmin`) | ✅ | `ClinicaRepositorio.SalvarAsync` |
| Migrations na abertura, com advisory lock entre os apps | ✅ | `ShellBootstrap.PrepararBancoAsync` |
| Backup local antes de migrar + backup diário | ✅ | `BackupLocal` |
| Atualização automática | ✅ | `UpdateService` (faturamento), `AtualizadorSuite` (suíte) |
| Log de erros em arquivo | ✅ | `LogErros` + `LogSuite` |
| Design system único | 🟡 | Duplicado entre faturamento e shell até a Fase 4 |
| **Contingência offline** | ⬜ | **Não existe** — ver abaixo |

---

## Gerente Geral — `Clinica.Gerente`

Ele **reflete tudo**: carrega os módulos dos outros e ganha cada feature acima sem uma
linha de código. Não tem, nem deve ter, tela própria.

O que só faz sentido nele:

| Feature | Estado | Observação |
|---|---|---|
| Todos os módulos numa janela só | ✅ | Entregue na Fase 3 |
| Módulo de faturamento na lista | ⬜ | Entra na Fase 4 — hoje o Gerente **não** tem as telas de faturamento |
| Visão consolidada (faturamento + caixa + produção) | ⬜ | Uma tela que cruza os módulos; nenhum módulo isolado pode oferecê-la |
| Perfis de acesso (quem pode ver o quê) | ⬜ | Fora do escopo das Fases 1–4 |

---

## O que a apresentação promete e ainda não existe

Curto de propósito — é a lista que importa.

1. **Contingência offline.** A apresentação afirma: *"Internet caiu? O sistema mostra as
   últimas pendências sincronizadas — a recepção sabe o que faturar hoje."* **Não há nada
   disso no código**: sem banco, o app não abre. Existe `BackupLocal`, mas é recuperação
   de desastre, não leitura offline. É a única promessa da apresentação sem nenhuma
   implementação — e é uma promessa fácil de checar numa demonstração.

2. **Tela de setup própria da suíte.** Recepção, Financeiro e Gerente só sobem se o
   Faturamento já tiver sido configurado na mesma máquina. Não está na apresentação, mas
   impede vender qualquer módulo separadamente — que é a premissa comercial da suíte.

### Dívidas da apresentação (texto desatualizado)

A apresentação vai para cliente, então divergência aqui não é detalhe:

- Ela diz **"cada guia vence 10 dias após o atendimento"** e *"3 guias passaram de 10 dias
  desde o atendimento"*. Desde o PR #44 o prazo conta **da data prevista de faturamento**,
  não do atendimento. O texto precisa ser corrigido.
- Ela não menciona nada da suíte (Recepção, Financeiro, Gerente Geral), que hoje é o
  principal argumento de venda além do faturamento.
