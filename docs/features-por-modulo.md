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
| 02 | Agenda multiprofissional | Recepção | ✅ | 1 e 5 |
| 03 | Fila em kanban | Recepção | ✅ | 1 |
| 04 | Pacientes — cadastro 360º | Recepção | ✅ | 2 |
| 05 | Prontuário — evolução + EVA | Recepção | ✅ | 2 |
| 06 | Mapa corporal | Recepção | ✅ | 3 |
| 07 | Prescrição | Recepção | ✅ | 3 |
| 08 | Pacotes, planos e vouchers | Financeiro | ✅ | 4 |
| 09 | Caixa, repasses e conciliação | Financeiro | ✅ | 4 |
| 10 | Estoque | Financeiro | ✅ | 4 |
| 11 | Marketing — NPS e recall | Gerente | ✅ | 5 |
| 12 | BI — indicadores | Gerente | ✅ | 5 |
| 13 | Permissões e LGPD | Gerente / Recepção | ✅ | 5 |
| 14 | Faturamento TISS 4.01 | Faturamento | ✅ | — |

**Placar: as 14 completas.**

| Estado | Features |
|---|---|
| ✅ Completas | 01 · 02 · 03 · 04 · 05 · 06 · 07 · 08 · 09 · 10 · 11 · 12 · 13 · 14 |

> Completa quer dizer **entregue como o produto se propôs a fazer**, não que não haja o
> que melhorar. Duas ressalvas seguem valendo e estão no fim deste documento: a feature
> 02 automatiza a RODADA de confirmação, não o disparo; e a "assinatura digital" da
> feature 07 é carimbo com código de conferência, não certificado ICP-Brasil.

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
| Confirmação **automática** por WhatsApp | ✅ | `CampanhaService.GerarConfirmacoesAsync` — rodada diária na tela Campanhas do Gerente (parcela 5) |

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

### Feature 06 · Mapa corporal — ✅ · parcela 3

| Item | Estado | Onde |
|---|---|---|
| Mapa corporal interativo | ✅ | `MapaCorporalViewModel` + as duas figuras (frente e costas) na `EvolucaoWindow` |
| Protocolo reutilizável entre sessões | ✅ | `ProtocoloCorporal`, `MapaCorporalService.AplicarProtocoloAsync` |
| Vinculado à evolução clínica | ✅ | `MapaCorporal.EvolucaoId` — 1:1 com a sessão, e some com ela |
| Repetir o mapa da sessão anterior | 🔵 | `MapaCorporalService.PontosDaSessaoAnteriorAsync` |

> **Aplicar um protocolo é COPIAR pontos, nunca apontar para ele.** Se fosse referência,
> corrigir um ponto hoje reescreveria o protocolo da clínica — e, pior, a sessão da
> semana passada. Prontuário é registro do que aconteceu; referência viva reescreveria o
> passado a cada edição.

> As coordenadas são **normalizadas (0 a 1)**, nunca pixels: a figura pode ser
> redesenhada, a tela pode mudar de resolução, e a marcação continua no mesmo lugar do
> corpo. Quem converte o clique em fração é a tela, que é quem conhece o tamanho do
> desenho.

> **Repetir a sessão anterior não grava nada** — traz os pontos para a tela, e só o
> Salvar da sessão os efetiva. Gravar no clique deixaria no prontuário um mapa que
> ninguém confirmou.

### Feature 07 · Prescrição — ✅ · parcela 3

| Item | Estado | Onde |
|---|---|---|
| Modelos de receita e orientação | ✅ | `ModeloDocumento`, aplicados e criados na `DocumentoWindow` |
| Impressão com a marca SemDor | ✅ | `DocumentosClinicosPdfService` (usa `MarcaSemDor`) |
| Carimbo do profissional e código de conferência | ✅ | nome + registro no conselho, e `DocumentoClinico.CodigoVerificacao` |
| Assinatura digital com certificado (ICP-Brasil) | ⬜ | **não entregue** — ver abaixo |

> **O que "assinatura digital" entrega hoje, e o que não entrega.** Sai impresso o
> carimbo do profissional (nome e registro no conselho), a linha de assinatura e um
> código de conferência que acha o documento no sistema para comparar com o papel.
> **Não** há certificado ICP-Brasil: chamar o que existe de assinatura digital seria
> mentir sobre o que a via garante. Se o cliente precisar de validade jurídica de
> assinatura eletrônica, isso é escopo novo.

> O modelo **nasce do documento que o profissional acabou de escrever** ("guardar como
> modelo"), e não de uma tela de cadastro: ninguém senta para cadastrar modelos antes de
> precisar deles. Guardar com um nome já usado corrige o modelo em vez de criar um gêmeo.

---

## Módulo FINANCEIRO — `Clinica.Modulo.Financeiro`

### Feature 09 · Caixa, repasses e conciliação — ✅ · parcela 4

| Item | Estado | Onde |
|---|---|---|
| Fluxo de caixa diário | ✅ | `CaixaViewModel` |
| Lançamento manual, realizar e cancelar | ✅ | `LancamentoEdicaoViewModel` |
| Conciliação com o atendimento | ✅ | `FinanceiroService.GuiasSemLancamentoAsync` |
| Produção do período | 🔵 | `ProducaoViewModel` |
| Plano de contas (categorias) | ✅ | `PlanoContasViewModel` — criar, renomear e ativar/desativar |
| **Repasse por profissional** | ✅ | `RepasseService`, `RepassesViewModel` |
| Recibo do lançamento | ✅ | botão na linha do Caixa → `DocumentoFinanceiroService` |

> A fronteira é regra de projeto: **o dinheiro vive só em `LancamentoFinanceiro`**.
> `CodigoFaturamento` e `Atendimento` nunca ganham campo de valor.

> **Quem atendeu vem do AGENDAMENTO.** O atendimento é entidade do faturamento congelado
> e não guarda profissional; o agendamento guarda os dois — o profissional e o
> atendimento que ele gerou —, e é por essa ponte que a produção de cada um é apurada.

> **O repasse incide sobre a receita que ENTROU**, não sobre o que foi faturado. Pagar
> percentual de dinheiro que ainda não chegou descapitaliza a clínica exatamente no mês
> em que o convênio atrasa.

> **Apurar trava o período.** `RepasseApurado` existe para o repasse não ser pago duas
> vezes: sem esse registro, nada impediria duas máquinas de fechar o mesmo mês, e o erro
> só apareceria no extrato — depois do pagamento. Cancelar a apuração cancela junto a
> saída prevista no caixa.

> **O código da categoria não muda depois de criado**: ele é a referência estável que os
> lançamentos já gravados apontam. Nome, ordem e "ativa" mudam à vontade.

### Feature 08 · Pacotes, planos e vouchers — ✅ · parcela 4

| Item | Estado | Onde |
|---|---|---|
| Saldo de sessões por paciente | ✅ | `PacotePaciente.SaldoSessoes`, `PacoteService.DoPacienteAsync` |
| Vouchers e planos recorrentes | ✅ | `TipoPacote` — plano sem número de sessões é livre dentro da validade |
| Baixa automática ao atender | ✅ | `PacoteService.ConsumirPorAtendimentoAsync` |
| Catálogo do que está à venda | 🔵 | `PacoteCatalogo`, com preço e validade padrão |
| Orçamento do pacote em PDF | 🔵 | `DocumentoFinanceiroService.EmitirOrcamentoDoPacoteAsync` |

> **A venda COPIA o catálogo.** Mudar o preço de tabela em novembro não pode reescrever
> o que o paciente comprou em março — o vínculo com o catálogo fica só como procedência.

> **A situação é calculada, não guardada.** Um pacote gravado como "Ativo" viraria
> mentira à meia-noite do vencimento, e ninguém roda tarefa noturna nesta clínica.

> **Consumo é fato datado**: devolver a sessão é cancelar com motivo, nunca apagar. Sem
> isso, "o paciente diz que sobrou uma sessão" viraria a palavra de um contra a do outro
> — que é a conversa que este módulo existe para encerrar.

> A baixa automática debita o pacote que **vence primeiro**, e um atendimento debita **uma
> vez só**: concluir a mesma sessão de novo não come outra sessão do paciente. Ela é
> chamada pelo fluxo da Recepção, e **não** pelo `AtendimentoService` — aquele é
> compartilhado com o faturamento congelado, e dar-lhe efeito colateral novo mudaria o
> comportamento de um app em produção que nada tem com pacotes.

> Cuidado para não confundir com `AutorizacaoSessoes`, que é **cota do convênio** — outra
> coisa. Pacote é venda da clínica.

### Feature 10 · Estoque — ✅ · parcela 4

| Item | Estado | Onde |
|---|---|---|
| Entrada e baixa por sessão | ✅ | `EstoqueService.EntrarAsync`/`BaixarAsync`, `MovimentoEstoque.AtendimentoId` |
| Alerta de mínimo e validade | ✅ | `AbaixoDoMinimoAsync`, `ValidadesAsync` (janela de 60 dias) |
| Custo por atendimento | ✅ | `CustoDoAtendimentoAsync`, com custo médio das entradas |

> **O saldo NÃO é campo**: é a soma dos movimentos. Guardar um total e
> mantê-lo em dia é como o estoque para de bater — uma gravação que falha no meio e o
> número fica errado para sempre, sem ninguém saber desde quando.

> **A validade fica no LOTE, não no item.** O mesmo insumo entra em lotes com vencimentos
> diferentes, e uma validade só por item apagaria justamente o que vence primeiro. O
> alerta ignora lote de item zerado: não há o que descartar.

> Saída maior que o saldo é **recusada** — estoque negativo não existe no mundo, e aceitar
> o número esconderia o erro de contagem em vez de mostrá-lo. Perda exige motivo escrito.

---

## Módulo GERENTE GERAL — `Clinica.Gerente`

Ele **carrega os módulos dos outros** — então tem, por construção, tudo o que estiver
marcado acima. Só entram aqui as features que **só** fazem sentido nele.

Com a Fase 4 cancelada, o Gerente não herda as telas do faturamento. Ele ganha **telas
próprias de leitura** sobre os mesmos serviços compartilhados (`PendenciaService`,
`RelatorioService`, `LoteTissService`) — enxerga tudo sem tocar no app em produção, e
sendo só leitura não há risco de escrita concorrente.

Desde a parcela 5 existe o **`Clinica.Modulo.Gerente`** (biblioteca carregada só pelo
`Clinica.Gerente.exe`), com quatro telas: **Indicadores**, **Faturamento** (leitura),
**Campanhas** e **Acessos**. Elas ficam fora dos outros apps em vez de aparecerem
escondidas por permissão — quem instala a Recepção não precisa baixar a tela de BI.

### Feature 12 · BI — indicadores — ✅ · parcela 5

| Item | Estado | Onde |
|---|---|---|
| Faturamento e taxa de glosa | ✅ | `RelatorioService` + tela `FaturamentoGerencialView` (leitura) |
| Envelhecimento e evolução mensal | 🔵 | `FaixaEnvelhecimento`, `ResumoMensal` |
| Ocupação e no-show | ✅ | `IndicadoresService`, `IndicadoresAgenda` |
| Produtividade por profissional | ✅ | `ProdutividadeProfissional` — sessões, horas, evoluções e queda média da EVA |

> **A ocupação é medida contra os dias em que o profissional TEVE agenda**, multiplicados
> pela jornada configurável (`ParametrosService.ChaveJornadaDiariaMinutos`, padrão 8 h).
> A clínica não cadastra jornada por pessoa, então o indicador responde "nos dias em que
> abriu a agenda, quanto dela ficou ocupado" — e não "quanto da capacidade instalada foi
> usada". Inventar dias úteis daria um número mais bonito e menos verdadeiro.

> **Falta de base de cálculo devolve `null`, nunca 0%.** A tela mostra "—". Zero por cento
> e "não deu para medir" são coisas diferentes, e confundi-las faria a direção decidir em
> cima de um número que não existe. Vale para ocupação, NPS e queda média da dor.

> **Cancelamento avisado não conta como falta.** O no-show é sobre os horários que
> chegaram ao fim (atendidos + faltas): quem desmarcou deu à clínica a chance de reocupar
> o horário, e somá-lo esconderia o problema que o indicador existe para mostrar.

### Feature 11 · Marketing — NPS e recall — ✅ · parcela 5

| Item | Estado | Onde |
|---|---|---|
| NPS automático pós-consulta | ✅ | `CampanhaService.GerarNpsAsync`, `ResumoNps` |
| Recall de pacientes inativos | ✅ | `CampanhaService.GerarRecallAsync`, `CandidatoRecall` |
| Campanhas por WhatsApp | ✅ | Tela `CampanhasView` + `Whatsapp` do shell |

> **As três campanhas são UMA entidade** (`ContatoCampanha`): o fato registrado é o
> mesmo — falamos (ou vamos falar) com este paciente, por este motivo, e ele respondeu
> isto. Três tabelas quase idênticas dariam três telas, três consultas e três lugares
> para esquecer de checar o consentimento.

> **A linha da LGPD**: confirmar a PRÓPRIA sessão marcada é transacional (o paciente
> pediu o horário; avisar sobre ele não é marketing) e não exige consentimento. NPS e
> recall são comunicação ativa e só saem com `ComunicacaoEMarketing` vigente. Quem não
> consentiu **não some da lista**: aparece contado no resultado da rodada, para a clínica
> ir colher o consentimento no balcão.

> **Gerar e enviar são passos separados.** Gerar é a parte automática; enviar é um clique
> por paciente, porque o número é o WhatsApp da clínica e disparo em massa automatizado
> por ali termina com o número bloqueado — perder o canal inteiro para economizar cliques
> é um mau negócio. O que a parcela automatiza é o TRABALHO, não o clique.

> **Rodar a campanha duas vezes não duplica ninguém**: `ContatoCampanha.Origem` é a chave
> do fato (`AGD:123`, `ATD:987`, `REC:55:2026-07`), com índice único junto do tipo — a
> regra é do banco, não só do código, porque duas máquinas gerando a rodada ao mesmo
> tempo passariam pela checagem em memória.

### Feature 13 · Permissões e LGPD — ✅ · parcela 5

| Item | Estado | Onde |
|---|---|---|
| Trilha de auditoria imutável | ✅ | `EventoAuditoria`, `RegistrarAuditoriaAsync` |
| Conformidade LGPD (consentimento) | ✅ | `ConsentimentoService` — entregue na parcela 2, na Recepção |
| Perfis e permissões finas | ✅ | `UsuarioSistema`, `PerfisAcesso`, `AcessoService`, tela `AcessosView` |
| Login nos apps da suíte | ✅ | `LoginWindow` + `SessaoUsuario` no shell |

> A metade LGPD saiu antes da hora, junto do cadastro do paciente (parcela 2) — é lá que
> o consentimento é colhido, no balcão. A parcela 5 fecha o controle de acesso: usuário,
> login e perfil **apontando** para o `Profissional` que a parcela 1 criou (e não
> duplicando a pessoa).

> **O perfil dá o conjunto base; a permissão fina é o delta.** `Efetivas = padrão do
> perfil + extras − negadas`, resolvido na LEITURA — assim corrigir o padrão de um perfil
> alcança quem já estava cadastrado. Negada vence extra de propósito: tirar acesso é a
> decisão que não pode ser anulada por engano de configuração.

> **Base sem usuário abre o "primeiro acesso"** (que nasce Gerente) em vez de pedir
> credencial que ninguém tem: a versão que introduz login não pode trancar a clínica do
> lado de fora da própria clínica. E o sistema recusa deixar a base sem ninguém capaz de
> gerenciar acessos.

> **O app de FATURAMENTO continua sem login.** Está congelado, roda num posto só, e
> encostar nele é exatamente o que a parcela inteira evita. Isso está documentado, não
> esquecido — a tela de Acessos diz isso ao usuário.

---

## Documentos impressos — página 21 da proposta

**12 prometidos, 12 existem.** A página 21 está fechada.

| Documento | Módulo | Estado | Onde |
|---|---|---|---|
| Capa de lote | Faturamento | ✅ | `CapaFaturamentoService` |
| Guia TISS SP/SADT | Faturamento | ✅ | `GuiaTissPdfService` |
| Fechamento | Faturamento | ✅ | `FechamentoPdfService` |
| Receita | Recepção | ✅ | `DocumentosClinicosPdfService` |
| Atestado | Recepção | ✅ | idem — CID só sai com autorização do paciente |
| Comparecimento | Recepção | ✅ | idem |
| Pedido de exame | Recepção | ✅ | idem |
| Relatório de evolução (EVA) | Recepção | ✅ | montado do prontuário na emissão |
| Consentimento | Recepção | ✅ | montado do `ConsentimentoService` |
| Anamnese | Recepção | ✅ | preenchida com o prontuário, em linhas com o resto |
| Recibo | Financeiro | ✅ | `DocumentosFinanceirosPdfService` — emitido do lançamento do caixa |
| Orçamento | Financeiro | ✅ | idem — com validade padrão de 30 dias |

Os sete da Recepção saem do mesmo `DocumentoClinico`, numerado por ano (`2026/0001`) e
com código de conferência. Quatro são **escritos** pelo profissional (receita, atestado,
comparecimento, pedido de exame) e três são **montados** pelo sistema a partir do que ele
já tem (relatório, termo de consentimento, anamnese) — pedir para alguém digitar o que o
banco já sabe é o caminho mais curto para o documento sair errado.

Três decisões que valem registrar:

- **Documento emitido é fato.** Uma vez impresso e entregue, existe no mundo: não se
  apaga nem se reescreve. Corrige-se **cancelando com motivo** e emitindo outro — a
  mesma lógica do consentimento revogado da parcela 2. A linha cancelada continua na
  ficha, porque a via em papel não some por ser apagada do sistema.
- **O conteúdo fica gravado na emissão, não é remontado na reimpressão.** A segunda via
  de um relatório tem de sair idêntica à primeira, mesmo que o prontuário tenha andado
  desde então. Por isso até os documentos montados gravam suas linhas.
- **O CID só entra no atestado com autorização expressa do paciente.** O campo fica
  gravado (é dado clínico), mas `CidImpresso` devolve nulo sem a autorização e o PDF não
  o imprime. A tela avisa antes, para ninguém entregar o papel achando que o diagnóstico
  foi junto.

> **Receita, atestado e pedido de exame exigem o profissional que assina.** São
> documentos que só existem porque alguém habilitado assina; sem assinante o papel não
> vale nada — e vale menos ainda descobrir isso na frente do paciente, com o documento
> já impresso. É a única exceção à regra da agenda ("avisa, mas não impede").

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

O documento já foi ao cliente. Estas precisam de decisão comercial:

1. **Página 24 diz "Dois apps, um banco".** São **quatro** apps, um por perfil.
2. ~~**Página 23 marca ✓ em "Prontuário com mapa corporal e EVA"**~~ — **resolvida na
   parcela 3.** A EVA saiu na parcela 2 e o mapa corporal saiu agora; a afirmação passou
   a ser inteiramente verdadeira.
3. **"Assinatura digital" da feature 07.** O que existe é carimbo do profissional, linha
   de assinatura e código de conferência — **não** certificado ICP-Brasil. Se a clínica
   precisar de validade jurídica de assinatura eletrônica, é escopo novo.
4. **"Confirmação automática por WhatsApp" (feature 02).** O que a parcela 5 automatiza é
   a RODADA — descobrir quem confirmar, escrever a mensagem, aplicar a LGPD e não repetir
   ninguém. O **disparo continua sendo um clique por paciente**, de propósito: o número é
   o WhatsApp da clínica, e envio em massa automatizado por ali leva ao bloqueio do
   número. Se o cliente entendeu "automática" como "sozinho, sem ninguém clicar", isso
   precisa ser dito antes de a expectativa virar cobrança — e a alternativa real é
   contratar a API oficial do WhatsApp Business, que é decisão comercial, não técnica.
5. **O cronograma não fecha** — mas encolheu. A Fase 1 (1–2 meses) foi fechada com as
   parcelas 1 e 2, e a 3 fecha o ato clínico. O que resta do calendário original é
   decisão sua; as parcelas deste arquivo continuam sendo a ordem técnica correta.

## Parcelas

As **seis parcelas estão entregues** — 0 (instalável), 1 (fundação), 2 (cadastro e
prontuário), 3 (ato clínico), 4 (dinheiro e insumo) e 5 (inteligência).

Os quatro apps instalam sozinhos. A Recepção tem painel próprio, agenda multiprofissional
com encaixe e lista de espera, fila em kanban, cadastro de profissionais e salas,
Pacientes 360º com foto e consentimento LGPD, prontuário com evolução, escala EVA e
anexos, mapa corporal com protocolo reutilizável e os sete documentos clínicos. O
Financeiro tem caixa, conciliação, produção, pacotes com saldo e baixa automática, estoque
com alerta de mínimo e validade, repasse por profissional, plano de contas, recibo e
orçamento. O Gerente tem BI, campanhas (confirmação, NPS e recall), acessos com perfis e
a visão de leitura do faturamento.

A parcela 5 saiu **antes** das 3 e 4 porque não dependia de nenhuma das duas: apoiava-se
na fundação da parcela 1 (`Profissional`) e no consentimento da parcela 2, que já
existiam.

> Como o cliente recebe os quatro apps e o cronograma completo:
> [`entrega-ao-cliente.md`](entrega-ao-cliente.md).
