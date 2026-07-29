# Features por módulo

> O catálogo do que a **proposta comercial** (`ApresentacaoSemDor.pdf`) vendeu, distribuído
> pelos **quatro módulos** da suíte, com o estado real de cada feature conferido no código.
> É a lista que a gente vai quitando em parcelas.

**Módulos são os quatro apps:** Faturamento · Recepção · Financeiro · Gerente Geral.
Os 14 itens numerados da proposta são **features**, e cada uma precisa de um módulo dono.

⚠️ **A proposta tem DUAS listas, e por muito tempo este documento só conhecia uma.** Além
das 14 features numeradas, os mockups mostram uma **sidebar de 15 itens** — que é o que o
cliente vê e cobra. As duas listas não coincidem: a sidebar tem Telemedicina e Portal do
paciente, que não são features numeradas; e não tem "Taxas e impostos", que o cliente
cobrou assim mesmo. A conferência da sidebar está em
[A SIDEBAR da proposta × o que existe](#a-sidebar-da-proposta--o-que-existe).

## Como ler

| Estado | Significado |
|---|---|
| ✅ | Pronto e em produção |
| ❌ | **Fora de escopo** por decisão do cliente — sai do material comercial |
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

### Contas a pagar e a receber — ✅ · parcela 12

| Item | Estado | Onde |
|---|---|---|
| O que vence esta semana (7/15/30/60/90 dias) | ✅ | `ContasViewModel`, `ContasService.EmAbertoAsync` |
| Vencidas em destaque, com o prazo em palavras | ✅ | `LinhaConta.De` — "venceu faz 5 dias" |
| Baixa e reagendamento | ✅ | `FinanceiroService.RealizarAsync`, `ContasService.ReagendarAsync` |
| Conta fixa (aluguel, luz, contador, software) | ✅ | `LancamentoRecorrente` — 7 periodicidades, com vigência |
| Geração idempotente das previstas | ✅ | `ContasService.GerarAsync` + índice único em `OrigemRecorrencia` |

> **A recorrência é um MOLDE, não um lançamento.** Ela não entra em total nenhum: quem
> entra é o lançamento previsto que ela gera, e que dali em diante tem vida própria — a
> conta de luz nunca vem igual. Se a recorrência fosse "o lançamento que se repete",
> corrigir março reescreveria janeiro, que já foi pago e conciliado.

> **A série sai sempre do primeiro vencimento mais N períodos**, nunca da ocorrência
> anterior mais um: encadear faria o aluguel do dia 31 virar aluguel do dia 28 para
> sempre, por causa de fevereiro.

> **Nada nasce pago.** Tudo vem `Previsto` — o sistema sabe que a conta vence, não que
> ela foi quitada. E gerar é um CLIQUE, não automático na abertura do app: conta
> nascendo sozinha é escrita que o balcão não vê acontecer e depois não sabe explicar.

### Fluxo de caixa e resultado por categoria — ✅ · parcela 13

| Item | Estado | Onde |
|---|---|---|
| Série mês a mês (3/6/12/24 meses) | ✅ | `FluxoCaixaService.ProjecaoAsync`, `FluxoCaixaViewModel` |
| Dois gráficos de linha (entradas e resultado) | ✅ | `GraficoLinha` do design system |
| Para onde foi o dinheiro (quebra por categoria) | ✅ | `FluxoCaixaService.PorCategoriaAsync` |
| Margem e maior despesa do período | ✅ | `ResumoFluxo` |
| Exportação CSV | ✅ | `ExportacaoCsv` — ponto e vírgula + BOM |

> **Realizado e previsto nunca viram um número só.** Somados, o mês que fechou e o mês
> que ainda vai vencer teriam a mesma cara, e a direção decidiria sobre expectativa
> achando que era medida.

> **A coluna acumulada é VARIAÇÃO no período, não saldo em conta**, e a tela diz isso: a
> clínica nunca cadastrou saldo inicial, e chamar aquilo de saldo daria um número que ela
> conferiria com o extrato do banco e que não bateria nunca.

> **A fração da categoria é do total DO MESMO TIPO.** Comparar uma despesa com o total
> geral daria barras que não somam 100% e não querem dizer nada. E lançamento sem
> categoria aparece como "Sem categoria" em vez de sumir — dinheiro não classificado é o
> que a direção precisa ver para mandar classificar.

### Fechamento de caixa (conferência da gaveta) — ✅ · parcela 14

| Item | Estado | Onde |
|---|---|---|
| Proposta do dia (o que passou pela gaveta) | ✅ | `FechamentoCaixaService.PrepararAsync` |
| Conferência com diferença calculada ao digitar | ✅ | `FechamentoCaixaViewModel.RecalcularDiferenca` |
| Justificativa obrigatória na divergência | ✅ | `FechamentoCaixaService.ConferirAsync` |
| Dias com dinheiro que ninguém conferiu | ✅ | `FechamentoCaixaService.NaoConferidosAsync` |
| Reabertura para recontagem, com motivo | ✅ | `ReabrirAsync` — o anterior fica no histórico |

> **Só espécie.** Cartão e PIX não passam pela gaveta — o dinheiro deles cai na conta
> dias depois. Incluí-los faria a conferência nunca bater, e conferência que nunca bate
> não é controle: é ruído que treina a clínica a clicar "OK" sem olhar.

> **O valor do sistema é COPIADO no fechamento.** Um lançamento digitado amanhã com a
> data de ontem não pode reescrever a conferência de ontem, levando junto a justificativa
> que alguém escreveu.

> **Divergência exige justificativa** — a única regra do serviço que impede em vez de
> avisar. Sobra também: dinheiro a mais na gaveta costuma ser venda não lançada, que é
> problema, não sorte.

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
| Baixa ao atender | ✅ | `PacoteService.ConsumirPorAtendimentoAsync`, **chamado** pelo `FechamentoSessaoService` (parcela 6) |
| Catálogo do que está à venda | 🔵 | `PacoteCatalogo`, com preço e validade padrão |
| Orçamento do pacote em PDF | 🔵 | `DocumentoFinanceiroService.EmitirOrcamentoDoPacoteAsync` |

> **A venda COPIA o catálogo.** Mudar o preço de tabela em novembro não pode reescrever
> o que o paciente comprou em março — o vínculo com o catálogo fica só como procedência.

> **A situação é calculada, não guardada.** Um pacote gravado como "Ativo" viraria
> mentira à meia-noite do vencimento, e ninguém roda tarefa noturna nesta clínica.

> **Consumo é fato datado**: devolver a sessão é cancelar com motivo, nunca apagar. Sem
> isso, "o paciente diz que sobrou uma sessão" viraria a palavra de um contra a do outro
> — que é a conversa que este módulo existe para encerrar.

> A baixa debita o pacote que **vence primeiro**, e um atendimento debita **uma vez só**:
> concluir a mesma sessão de novo não come outra sessão do paciente. Ela é chamada pelo
> fluxo da Recepção, e **não** pelo `AtendimentoService` — aquele é compartilhado com o
> faturamento congelado, e dar-lhe efeito colateral novo mudaria o comportamento de um app
> em produção que nada tem com pacotes.

> ⚠️ **Correção de rota (parcela 6).** Até a parcela 5 esta linha dizia "baixa automática
> ✅" e estava **errada**: o serviço existia e era testado, mas **nenhum código de produção
> o chamava**. Na clínica, concluir a sessão gerava a guia e o saldo do pacote ficava
> parado — só baixava se alguém lembrasse de abrir o Financeiro e debitar à mão. Serviço
> testado sem chamador passa no CI e não faz nada no balcão; a parcela 6 ligou o fio
> (`FechamentoSessaoService`) e o ✅ passou a ser verdade.

> Cuidado para não confundir com `AutorizacaoSessoes`, que é **cota do convênio** — outra
> coisa. Pacote é venda da clínica.

### Feature 10 · Estoque — ✅ · parcela 4

| Item | Estado | Onde |
|---|---|---|
| Entrada e baixa por sessão | ✅ | `EstoqueService.EntrarAsync`/`BaixarAsync`; a baixa POR SESSÃO entrou no fechamento da Recepção (parcela 6) |
| Alerta de mínimo e validade | ✅ | `AbaixoDoMinimoAsync`, `ValidadesAsync` (janela de 60 dias) |
| Custo por atendimento | ✅ | `CustoDoAtendimentoAsync`, com custo médio das entradas |

> ⚠️ **Mesma correção de rota do pacote.** `BaixarAsync` aceitava `atendimentoId` desde a
> parcela 4, mas nenhuma tela o passava: toda saída era digitada à mão na tela de
> movimento, sem vínculo com a sessão — e por isso o "custo por atendimento" respondia
> zero para todo mundo. A baixa da sessão agora sai do fechamento da Recepção, que sugere
> o que a **última sessão** gastou (a clínica não cadastra "kit da sessão", e criar um
> cadastro novo seria pedir manutenção de uma lista que ninguém mantém).

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

## Como os módulos se comunicam

Os quatro apps compartilham `Domain`, `Application` e `Infrastructure` — o mesmo banco e os
mesmos serviços. Isso os deixa **capazes** de se comunicar, mas capacidade não é fluxo: até a
parcela 5 nenhum ato de um módulo disparava efeito em outro, e o resultado é o que está
corrigido acima (o pacote não baixava, o insumo não saía, o dinheiro não entrava).

As pontes que existem hoje, e o sentido de cada uma:

| Ponte | Sentido | Onde |
|---|---|---|
| Concluir a sessão → guia + pacote + insumo + caixa | Recepção → Faturamento, Financeiro | `FechamentoSessaoService` (parcela 6) |
| Guia efetivada que ainda não virou dinheiro | Faturamento → Financeiro | `FinanceiroService.GuiasSemLancamentoAsync` (Conciliação) |
| Quem atendeu, para repasse e produtividade | Recepção → Financeiro, Gerente | `Agendamento.ProfissionalId` + `AtendimentoId` |
| Pendência de guia no cartão de quem chega hoje | Faturamento → Recepção | `PainelRecepcaoService.PendenciasDoDiaAsync` |
| Leitura consolidada do faturamento | Faturamento → Gerente | `FaturamentoGerencialView` (só leitura) |
| Pendências, glosas, NC e lotes na direção | Faturamento → Gerente | `FaturamentoTissView` — 5 abas sobre os serviços compartilhados (parcelas 10b–10d) |
| Configuração da clínica editável fora do app congelado | Gerente → todos | `ConfiguracoesView` sobre `ParametrosService` (parcela 10a) |

Três regras que valem para qualquer ponte nova:

- **O sentido é sempre PARA FORA do faturamento.** Ele está congelado: os outros leem o que
  ele produz e escrevem em entidades próprias. Nenhuma ponte pode exigir mudança nele.
- **Ponte é serviço, não efeito colateral.** O elo mora num serviço próprio da camada
  Application, chamado explicitamente por quem opera. Pendurá-lo dentro do
  `AtendimentoService` (compartilhado) mudaria o comportamento de um app em produção.
- **A ponte que falha avisa.** Falha parcial não pode ser exibida como sucesso, e também não
  pode desfazer o fato principal: o atendimento aconteceu.

> **Não há mensageria entre os apps.** Dois postos abertos ao mesmo tempo só se enxergam
> quando a tela recarrega — não existe notificação de um `.exe` para outro. Para esta clínica
> (poucos postos, banco remoto) isso é decisão, não esquecimento: a alternativa é infraestrutura
> de eventos que ninguém aqui vai operar. A concorrência de escrita continua protegida pelo
> `xmin`.

## A SIDEBAR da proposta × o que existe

⚠️ **A falha de levantamento que originou as parcelas 7 a 11.** Este documento foi montado a
partir das **14 features numeradas** da proposta. Os mockups têm uma **sidebar de 15 itens**
que nunca foi catalogada aqui — e o cliente, com razão, cobrou pelo que via na arte.

| Item da sidebar | Módulo dono | Estado | Onde |
|---|---|---|---|
| **GESTÃO** · Início | Recepção | ✅ | `PainelView` |
| **GESTÃO** · Agenda | Recepção | ✅ | `AgendaView` |
| **GESTÃO** · Recepção / Check-in | Recepção | ✅ | `FilaView` (kanban) |
| **PACIENTE** · Pacientes / CRM | Recepção | ✅ | `PacientesView` + origem/indicação/contatos (parcela 8) |
| **PACIENTE** · Prontuário | Recepção | ✅ | `ProntuarioView` — item de menu próprio desde a parcela 8 |
| **PACIENTE** · Prescrições | Recepção | ✅ | `PrescricoesView` — idem |
| **PACIENTE** · Telemedicina | — | ❌ | **FORA DE ESCOPO** por decisão do cliente (jul/2026) |
| **PACIENTE** · Portal do paciente | — | ❌ | **FORA DE ESCOPO** por decisão do cliente (jul/2026) |
| **FINANCEIRO** · Pacotes / Sessões | Financeiro | ✅ | `PacotesView` |
| **FINANCEIRO** · Financeiro | Financeiro | ✅ | `CaixaView` + Conciliação, Produção, Repasses |
| **FINANCEIRO** · Faturamento (TISS) | Gerente | ✅ | `FaturamentoTissView` — 5 abas (parcelas 10b–10d) |
| **FINANCEIRO** · Estoque | Financeiro | ✅ | `EstoqueView` |
| **FINANCEIRO** · Taxas e impostos | Financeiro | ✅ | `TaxasView` (parcela 9) — 🔵 não estava na sidebar, mas o cliente cobrou |
| **FINANCEIRO** · Contas a pagar/receber | Financeiro | ✅ | `ContasView` (parcela 12) — 🔵 fora da sidebar da proposta |
| **FINANCEIRO** · Fluxo de caixa | Financeiro | ✅ | `FluxoCaixaView` (parcela 13) — 🔵 idem |
| **FINANCEIRO** · Fechamento de caixa | Financeiro | ✅ | `FechamentoCaixaView` (parcela 14) — 🔵 idem |
| **INTELIGÊNCIA** · Marketing / Recall | Gerente | ✅ | `CampanhasView` |
| **INTELIGÊNCIA** · Relatórios / BI | Gerente | ✅ | `IndicadoresView` com gráficos e exportação CSV (parcelas 10d e 11) |
| **INTELIGÊNCIA** · Configurações | Gerente | ✅ | `ConfiguracoesView` (parcela 10a) |

**Os grupos são temáticos, não por módulo.** `ItemMenuModulo.Grupo` (`GrupoSidebar`) decide
onde o item aparece; `ModuloNome` diz quem sabe construir a tela. São duas coisas que só
pareciam uma: antes da parcela 7 o cabeçalho era o nome do módulo carregado, e o Gerente —
que carrega os três — via "Recepção / Financeiro / Direção". Uma sidebar que explica a
arquitetura para quem só quer saber onde mexe no paciente.

## O que ainda NÃO existe

Levantado no código, não na memória:

| Falta | Módulo dono | Situação |
|---|---|---|
| ~~Telemedicina~~ | — | **FORA DE ESCOPO** (decisão do cliente, jul/2026). Precisa sair da arte da sidebar no material comercial |
| ~~Portal do paciente~~ | — | **FORA DE ESCOPO** (decisão do cliente, jul/2026). Idem |
| Assinatura ICP-Brasil na prescrição | Recepção | Depende de certificado digital — decisão comercial (ver feature 07) |
| NFS-e no fechamento | Financeiro | Depende de integração fiscal municipal — decisão comercial |
| Gerar lote TISS pelo Gerente | Gerente | **Decisão de projeto, não pendência**: o número do lote é sequência do faturamento, e dois apps gerando em paralelo produziriam dois com o mesmo número |

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
5. ~~**Telemedicina e Portal do paciente**~~ — **RESOLVIDA (jul/2026): estão FORA DE
   ESCOPO** por decisão do cliente. Apareciam na ARTE da sidebar dos mockups e não entre
   as 14 features numeradas; nunca existiram em linha nenhuma do repositório e nunca foram
   catalogados aqui como pendência — falha deste documento. São dois PRODUTOS, não duas
   telas (vídeo/WebRTC e aplicação web com login de paciente), e nenhum cabe num app WPF de
   balcão. **Ação pendente do lado comercial**: os dois itens precisam sair da arte da
   sidebar antes de o deck ir a outro cliente, senão a promessa se repete.
6. **O cronograma não fecha** — mas encolheu. A Fase 1 (1–2 meses) foi fechada com as
   parcelas 1 e 2, e a 3 fecha o ato clínico. O que resta do calendário original é
   decisão sua; as parcelas deste arquivo continuam sendo a ordem técnica correta.

## Parcelas

As **onze parcelas estão entregues** — 0 (instalável), 1 (fundação), 2 (cadastro e
prontuário), 3 (ato clínico), 4 (dinheiro e insumo), 5 (inteligência), 6 (integração),
7 (moldura e navegação), 8 (prontuário/prescrições/CRM), 9 (taxas e impostos),
10 (Configurações, faturamento no Gerente, exportação) e 11 (gráficos).

As parcelas 7 a 11 nasceram de uma comparação do cliente entre os mockups e o sistema
rodando. Três achados sustentaram todas elas: este documento catalogava 14 features e
ignorava os 15 itens da sidebar; a moldura boa (sidebar recolhível, busca global,
breadcrumb) estava presa no app congelado e nunca foi para a suíte; e **não havia um único
gráfico** em 16 telas — o que o deck mostra como rosca, barras e linhas estava como tabela
de texto.

A **parcela 6 não trouxe feature nova**: ligou o que já existia. As parcelas 1 a 5
construíram serviços e telas módulo a módulo, e o que ficou faltando foi o fio entre eles —
concluir a sessão na Recepção não debitava o pacote, não baixava o insumo e não lançava o
caixa, embora os três serviços estivessem prontos e testados. É a lição que vale registrar:
**serviço testado sem chamador passa no CI e não faz nada na clínica**, e o quadro de
features marcava ✅ para os dois casos.

Os quatro apps instalam sozinhos. A Recepção tem painel próprio, agenda multiprofissional
com encaixe e lista de espera, fila em kanban, cadastro de profissionais e salas,
Pacientes 360º com foto e consentimento LGPD, prontuário com evolução, escala EVA e
anexos, mapa corporal com protocolo reutilizável e os sete documentos clínicos. O
Financeiro tem caixa, conciliação, produção, pacotes com saldo e baixa automática, estoque
com alerta de mínimo e validade, repasse por profissional, plano de contas, recibo e
orçamento. O Gerente tem BI, campanhas (confirmação, NPS e recall), acessos com perfis e
a visão de leitura do faturamento. E, desde a parcela 6, concluir uma sessão na Recepção
atravessa os três módulos de uma vez.

As parcelas **12 a 14** fecharam o que o Financeiro respondia pela metade. Ele sabia o que
já tinha acontecido e o que estava "previsto", mas não *quando* — não havia data de
vencimento, e por isso nenhuma tela respondia à pergunta que se faz toda segunda-feira
("o que vence esta semana?"), nem havia como cadastrar o aluguel uma vez em vez de
redigitá-lo todo mês (12). Não havia SÉRIE: o Caixa mostrava o extrato do período e a
Produção o volume, e um mês ruim era indistinguível de três meses caindo (13). E o módulo
sabia tudo sobre o dinheiro *registrado* e nada sobre o dinheiro *físico* — que é
exatamente onde ele some numa clínica (14).

A parcela 5 saiu **antes** das 3 e 4 porque não dependia de nenhuma das duas: apoiava-se
na fundação da parcela 1 (`Profissional`) e no consentimento da parcela 2, que já
existiam.

> Como o cliente recebe os quatro apps e o cronograma completo:
> [`entrega-ao-cliente.md`](entrega-ao-cliente.md).
