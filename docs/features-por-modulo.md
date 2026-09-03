# Features por módulo

> O catálogo do que a **proposta comercial** (`ApresentacaoSemDor.pdf`) vendeu, distribuído
> pelos **quatro módulos** da suíte, com o estado real de cada feature conferido no código.
> É a lista que a gente vai quitando em parcelas.

**Módulos são os cinco apps:** Faturamento · Recepção · Consultório · Financeiro · Gerente Geral.
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

## A regra que manda em tudo (atualizada na parcela 45)

O faturamento **saiu do congelamento na parcela 45**, quando a cliente pediu três coisas
dentro dele (formato do número da guia por convênio, login com permissões granulares e
filtros na consulta de guias) — feature pedida no app que fatura a clínica não se entrega
noutro app. O que mudou foi a proibição de **editar arquivo**; o que NÃO mudou é a razão
dela: **ele fatura a clínica hoje, roda em produção e não tem quem o teste antes do
usuário.**

Continua valendo, e está por extenso no `CLAUDE.md`:

| Pode | Não pode |
|---|---|
| Criar entidades e serviços novos nas camadas compartilhadas | Migration destrutiva: renomear ou remover coluna/tabela que ele usa (checagem 18) |
| Migration **aditiva** (tabela nova, coluna nova anulável) | Acrescentar valor a enum embutido sem perceber que ele vira opção nova no seletor dele |
| Editar as telas dele quando a feature É dele, com teste | Tirar capacidade de quem já a usava (foi o que desenhou o perfil `Faturista`) |
| Publicar release dos outros quatro apps | Republicar o faturamento por mudança que não é dele |

**A Fase 4 segue cancelada**: o faturamento não vira módulo da suíte — os dois declaram
tipos no namespace `Clinica.Desktop.Controls` e as referências ficariam ambíguas. Ver
`arquitetura-multi-exe.md`.

## Onde cada feature da proposta foi parar

| # | Feature | Módulo dono | Estado | Parcela |
|---|---|---|---|---|
| 01 | Início — painel com semáforo | Faturamento / Recepção | ✅ / ✅ | 1 |
| 02 | Agenda multiprofissional | Recepção | ✅ | 1, 5 e 63 |
| 03 | Fila em kanban | Recepção | ✅ | 1 |
| 04 | Pacientes — cadastro 360º | Recepção | ✅ | 2 |
| 05 | Prontuário — evolução + EVA | Recepção | ✅ | 2 |
| 06 | Mapa corporal | Recepção | ✅ | 3 |
| 07 | Prescrição | Recepção | ✅ | 3 |
| 08 | Pacotes, planos e vouchers | Financeiro **e Recepção** | ✅ | 4 e 60 |
| 09 | Caixa, repasses e conciliação | Financeiro | ✅ | 4 |
| 10 | Estoque | Financeiro | ✅ | 4 |
| 11 | Marketing — NPS e recall | Gerente | ✅ | 5 |
| 12 | BI — indicadores | Gerente | ✅ | 5 |
| 13 | Permissões e LGPD | Gerente / Recepção | ✅ | 5 |
| 14 | Faturamento TISS 4.01 | Faturamento | ✅ | — |

**Placar: as 14 completas.**

> A 02 voltou a ✅ na parcela **63**, e o caminho até aqui é a lição: ela estava marcada
> ✅ desde a parcela 1 porque o CAMPO `SalaId` existe; a auditoria da parcela 62 a
> devolveu a 🟡 ao descobrir que **campo não é tela**; e a 63 construiu a visão que
> faltava. O ✅ de agora é o primeiro que corresponde a uma coluna que a recepcionista
> consegue ver.

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

### Feature 02 · Agenda multiprofissional — ✅ · parcelas 1, 5 e 63

| Item | Estado | Onde / observação |
|---|---|---|
| Grade de horários com remarcação | ✅ | `AgendaView` (Recepção) + `AgendaService.RemarcarAsync` |
| Visão por profissional | ✅ | uma coluna por profissional na grade (`AgendaViewModel.Colunas`) |
| **Visão por SALA** | ✅ | **parcela 63** — `AgendaViewModel.AgruparPorSala`: as colunas viram as salas, o vão livre leva a sala para o formulário (`SalaPreferidaId`) e "Sem sala" continua aparecendo, porque é o caso NORMAL desta clínica. Vale no modo dia; na semana as colunas já são os dias |
| **Vão FECHADO na grade** | ✅ | **parcela 63** — férias, feriado e folga aparecem pintados com o motivo. A marcação já era recusada pelo `AgendaService`; o vão bloqueado era visualmente idêntico ao livre, e o livre é clicável desde a parcela 58 |
| Encaixe rápido e lista de espera | ✅ | `Agendamento.Encaixe`, `ListaEsperaService` |
| **Quem chamar para o horário que vagou** | ✅ | `CandidatosParaAsync` — cancelar/faltar já aponta a lista para o horário (parcela 25) |
| Confirmação **automática** por WhatsApp | ✅ | `CampanhaService.GerarConfirmacoesAsync` — rodada diária, agora também com porta na própria Recepção (`ConfirmacoesWindow`, parcela 26) |
| **Bloqueio de agenda** (férias, feriado, folga) | ✅ | `BloqueioAgendaService`, `BloqueioWindow` (parcela 26) |
| **Agendamento em série** (o pacote de dez) | ✅ | `AgendaService.AgendarSerieAsync`/`CancelarSerieAsync` (parcela 26) |
| **Visão de semana** | ✅ | `AgendaViewModel.ModoSemana` — o dia continua sendo o padrão (parcela 26) |
| Elegibilidade antes de marcar | ✅ | `ElegibilidadeService` no agendamento e no check-in da Fila (parcela 26) |

> **A lista de espera passou a responder ao horário, e não só a existir.** Até a parcela 25
> ela mostrava todo mundo que espera, e a recepção cruzava turno, janela de datas e
> profissional de cabeça — no minuto em que o telefone toca. `CandidatosParaAsync` fazia
> esse cruzamento desde a parcela 1 e nenhuma tela o chamava. Agora cancelar um horário (ou
> marcar falta) já aponta a lista para ele, e o botão "Quem chamar?" faz o mesmo a pedido.
> A lista filtrada **diz que está filtrada** — título e texto do vazio mudam, porque
> "ninguém espera" e "ninguém serve para este horário" são respostas diferentes.

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
| **Estornar atendimento** — desfaz a sessão lançada por engano, item a item, sem apagar nada | ✅ | `EstornoAtendimentoService`, `EstornoAtendimentoWindow` (Recepção → Novo atendimento) — parcela 94 · `docs/estorno-de-atendimento.md` |
| **Convênio é condição para lançar** — sem ele o atendimento é recusado, e a escolha acontece no lançamento | ✅ | `AtendimentoService.MontarAsync` (a recusa), `EscolhaDeConvenioWindow` + `VinculoDeConvenio` (a janela) — parcela 92 |
| Foto pela webcam | ✅ | `Desktop.Shell/Componentes/CameraServico`, `Retrato` |
| Histórico de sessões e guias | ✅ | `FichaPacienteViewModel` (Recepção): sessões, guias em aberto, última sessão |
| Validação de elegibilidade | ✅ | `ElegibilidadeService` |
| LGPD: consentimento registrado | ✅ | `ConsentimentoService`, `ConsentimentoLgpd` |
| **LGPD: acesso e eliminação a pedido do titular** | ✅ | `TitularDadosService` — cartão "Direitos do titular" na ficha (parcela 26) |

> A tela é **master-detail**: lista à esquerda, ficha à direita. O balcão trabalha com o
> paciente na frente — navegar para outra seção custaria um clique e o contexto a cada
> atendimento. A busca **não** foi reescrita: usa o `SeletorPacienteViewModel` da suíte
> com `limite: null`, como manda a convenção.

> **A elegibilidade é o que esta tela tem de mais valioso.** Carteirinha vencida e cota
> estourada hoje só aparecem na hora de faturar, quando a sessão já aconteceu e o
> prejuízo é certo. O `ElegibilidadeService` junta o que já existia espalhado
> (`Paciente.CarteirinhaVencida`, `AutorizacaoService`, consentimento) e responde no
> balcão. Ele **informa, nunca impede** — quem decide é a clínica. Desde a parcela 26 ele
> responde também **onde a decisão é tomada**: ao marcar o horário e no check-in da fila.

> **Bloquear a agenda não desmarca ninguém.** Fechar o Natal depois de alguém ter marcado
> no dia 25 devolve a lista de quem está marcado, para a recepção remarcar. Sessão que
> some do sistema sem ninguém avisar o paciente é pior do que o choque de horário.

> **A série sai da PRIMEIRA data mais N períodos**, nunca da anterior mais um: encadear
> faria uma sessão adiada empurrar todas as seguintes, e o paciente perderia o horário
> fixo — que é exatamente o motivo de marcar em série. Data que esbarra em choque ou em
> agenda fechada é **PULADA e dita**, e a janela fica aberta: a recepção resolve com o
> paciente ainda na frente dela, em vez de descobrir o buraco na semana seguinte.

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
| **Apagar um protocolo** | ✅ | `ExcluirProtocoloAsync` — as sessões salvas com ele não mudam (parcela 25) |
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
| Modelos de receita e orientação | ✅ | `ModeloDocumento`, aplicados, criados e **apagados** na `DocumentoWindow` (parcela 25) |
| Impressão com a marca SemDor | ✅ | `DocumentosClinicosPdfService` (usa `MarcaSemDor`) |
| Carimbo do profissional e código de conferência | ✅ | nome + registro no conselho, e `DocumentoClinico.CodigoVerificacao` |
| Assinatura digital com certificado (ICP-Brasil) | ✅ | **parcelas 42 e 43** — assinatura qualificada PAdES (PKCS#7 SHA-256) na folha de infusão e nos quatro documentos que saem da clínica, com CPF conferido de dentro do certificado |

> **O que "assinatura digital" entrega hoje, e o que não entrega.** Assinatura
> QUALIFICADA ICP-Brasil (parcelas 42 e 43): PKCS#7 destacado SHA-256 embutido no PDF,
> CPF do assinante conferido de dentro do certificado contra o cadastro do profissional,
> carimbo do tempo RFC 3161 opcional e QR que leva ao validador de saúde do ITI. A via
> sem assinatura eletrônica continua saindo com carimbo, linha de assinatura e código de
> conferência — vale à caneta, como sempre valeu. O que **não** há é LTV/PAdES-LT (a
> assinatura é PAdES-B, e o rodapé diz isso) nem receituário de controle especial
> eletrônico (depende do SNCR da ANVISA). ⚠️ Pendência OPERACIONAL, não de código: a
> assinatura nunca foi validada em campo com um e-CPF de verdade — sem certificado no
> posto, assinar e publicar não funcionam em produção.

> O modelo **nasce do documento que o profissional acabou de escrever** ("guardar como
> modelo"), e não de uma tela de cadastro: ninguém senta para cadastrar modelos antes de
> precisar deles. Guardar com um nome já usado corrige o modelo em vez de criar um gêmeo.

> **Modelo e protocolo se APAGAM mesmo** (parcela 25) — e essa é a diferença deles para o
> documento emitido, que só se cancela com motivo. Modelo e protocolo não registram nada
> que aconteceu: são atalhos. E apagá-los não toca no passado, porque aplicar um modelo (ou
> um protocolo) **copia** o conteúdo para o documento e para a sessão, nunca aponta para
> ele. Sem essa porta a lista só crescia: o modelo criado com o nome errado ficava no combo
> para sempre. Os dois botões perguntam antes — ficam ao lado do "Aplicar".

---

### Central de documentos — as nove folhas — ✅ · parcela 24

| Item | Estado | Onde |
|---|---|---|
| Catálogo das nove folhas, com o que cada uma exige | ✅ | `CentralDocumentosService.Catalogo` |
| Receituário · Atestado · Declaração · Solicitação de exames | ✅ | abre a `DocumentoWindow` com o tipo pré-selecionado |
| Relatório de evolução · Anamnese · Consentimento | ✅ | `DocumentoClinicoService.Emitir*Async` (montadas do prontuário) |
| Recibo de pagamento | ✅ | nasce no Caixa; o cartão navega até lá (`NavegacaoSuite`) |
| Orçamento **livre**, sem depender de pacote | ✅ | `OrcamentoWindow` → `DocumentoFinanceiroService.EmitirAsync` |
| Fechamento do período, **na suíte** | ✅ | `CentralDocumentosService.GerarFechamentoPeriodoAsync` |
| Lista unificada do que já saiu (clínico + financeiro) | ✅ | `EmitidasAsync` · `DocumentosClinicosNoPeriodoAsync` |
| Segunda via e cancelamento com motivo | ✅ | `DocumentosViewModel.Reimprimir` / `Cancelar` |
| **Conferir o papel pelo código impresso** | ✅ | `DocumentoClinicoService.PorCodigoAsync` (parcela 25) |

> **As nove existiam e nenhuma estava no mesmo lugar.** Quatro saíam de uma janela dentro
> da ficha do paciente, três só do botão certo na aba certa dessa ficha, o recibo do Caixa,
> o orçamento só de dentro de um pacote vendido — e o fechamento do período **só do app de
> faturamento**, que está congelado e que a suíte nem abre. Quem foi treinado no mockup
> procurava "Documentos" e não achava: não faltava capacidade, faltava porta.

> **Duas capacidades estavam sem porta nenhuma.** `DocumentoFinanceiroService.EmitirAsync`
> aceitava linhas quaisquer desde a parcela 4 e nenhuma tela o chamava; `FechamentoPdfService`
> só era chamado pelo app congelado. É a variante mais discreta do defeito recorrente do
> projeto — o teste passa, o CI fica verde, e a clínica pega o bloquinho de papel.

> **A tela não reimplementa emissão nenhuma.** Abre a janela que já existe ou chama o
> serviço dono da folha. Reescrever aqui daria dois caminhos para o mesmo papel, e só um
> receberia a próxima correção.

> **Cada cartão diz o que FALTA** em vez de deixar o botão aceso e só depois avisar.
> Descobrir o requisito errando é o que faz a pessoa desistir da tela.

> **O recibo continua nascendo no Caixa.** Ele comprova dinheiro que JÁ entrou e fica
> apontando para o lançamento; emiti-lo de outro lugar deixaria sair dois recibos do mesmo
> pagamento. Sem o módulo Financeiro carregado no executável, o botão fica desabilitado
> dizendo isso.

> **Cancelada aparece marcada, nunca sumindo**, e o **fechamento do período não tem segunda
> via**: ele não é gravado, é conferência montada na hora a partir das guias, então a lista
> devolve vazio para ele de propósito.

> **O rótulo segue o mockup, não o enum.** A cliente chama de "Receituário" e "Solicitação
> de exames"; o enum chama de "Receita" e "Pedido de exame".

> ⚠️ **O código impresso não levava a lugar nenhum (corrigido na parcela 25).** Todo
> documento sai com um código de conferência, e é ele que o sistema oferece **no lugar do
> certificado ICP-Brasil**: "confira no sistema da clínica". Só que `PorCodigoAsync` existia
> desde a parcela 3 e **nenhuma tela o recebia** — quem chegava com o atestado na mão (a
> empresa, a escola, o próprio paciente) não tinha onde digitar. O que substitui a
> assinatura não pode ser justamente o que ninguém consegue usar. O cartão "Conferir um
> papel" responde tipo, número, paciente, data e situação, marca em vermelho o **cancelado**
> e o **código que não existe**, e oferece a segunda via.

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

### Inadimplência — "quem me deve" — ✅ · parcela 23

| Item | Estado | Onde |
|---|---|---|
| Conta a receber com DONO | ✅ | `ContasService.LancarContaAsync(..., pacienteId)` |
| Seletor de paciente no formulário (só em "A receber") | ✅ | `ContasView.xaml` + `ContasViewModel.Seletor` |
| Devedores agrupados, somados e ordenados | ✅ | `InadimplenciaService.PorPacienteAsync` |
| Envelhecimento por faixa, com VALOR | ✅ | `ResumoAsync` → `FaixaInadimplencia` |
| Cobrança por WhatsApp, um clique por paciente | ✅ | `MensagemDeCobranca` + `Whatsapp.Abrir` |
| Baixa da conta a partir da lista | ✅ | `ReceberAsync` → `FinanceiroService.RealizarAsync` |
| Exportação em CSV | ✅ | `InadimplenciaViewModel.ExportarAsync` |

> **A conta vencida já existia — e não tinha dono.** Desde a parcela 12 a conta a receber
> vencida aparecia na lista de Contas, uma linha por lançamento, misturada com o que a
> clínica tem a PAGAR; e `LancarContaAsync` não tinha `pacienteId`. Ninguém consegue cobrar
> assim: para saber que o mesmo paciente tem três sessões em aberto era preciso ler a lista
> inteira e somar de cabeça.

> **Só conta de PACIENTE entra.** Entrada prevista vencida sem paciente — reembolso de
> convênio, venda de produto, aporte — é a receber, não inadimplência. Cobrar quem não deve
> custa mais do que a sessão em aberto.

> **A situação é CALCULADA, nunca gravada.** Não existe campo "inadimplente" no cadastro, e
> não deve existir: paciente marcado assim continua marcado depois de pagar, e quem lê a
> ficha dois meses depois trata como caloteiro alguém que quitou.

> **A ordem padrão é o mais ANTIGO, não o maior valor.** A chance de receber cai com o
> tempo, e o caso de seis meses precisa de decisão — acordo, ou parar de contar com o
> dinheiro —, não de mais um lembrete. Quem tem pouco tempo de cobrança troca a ordem.

> **A mensagem é lembrete, não ameaça, e não leva dado clínico.** Quem está em atraso quase
> sempre esqueceu; um texto duro custa o paciente inteiro para recuperar uma sessão; e o
> telefone pode não ser só do paciente. **Cobrança não exige consentimento de marketing** —
> é transacional, como a confirmação da própria sessão; NPS e recall continuam exigindo.

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

### Regime tributário — ✅ · parcela 15

| Item | Estado | Onde |
|---|---|---|
| Vários tributos (ISS, PIS, COFINS, IRPJ, CSLL, Simples) | ✅ | `Tributo`, `TributoService` |
| Base de cálculo (Lucro Presumido) | ✅ | `Tributo.AliquotaEfetiva` = alíquota × base |
| Vigência por tributo | ✅ | `VigenteEm(dia)` — reajuste não reescreve o mês declarado |
| Detalhe da retenção no lançamento | ✅ | `LancamentoFinanceiro.DetalheImposto`, copiado na emissão |
| Simulador de recebimento | ✅ | aba "Simulador" da `TaxasView` |

> **Uma alíquota só não separa nada.** A clínica no Lucro Presumido paga cinco tributos —
> cinco guias, cinco vencimentos, cinco linhas na apuração. O número somado da parcela 9
> respondia "quanto saiu" e não "de quê", que é a pergunta que o contador faz.

> **A base de cálculo existe porque nem todo tributo incide sobre a receita inteira.** No
> Presumido o IRPJ de 15% incide sobre 32% e a efetiva é 4,8%. Sem o campo, a clínica faria
> a conta de cabeça e digitaria 15% — o número que ela conhece —, triplicando o imposto.

> **Cada tributo é arredondado ao centavo separadamente**: arredondar só no fim daria um
> total que não bate com a soma das linhas do detalhe, e é o detalhe que vai ao contador.

> **A alíquota única da parcela 9 continua valendo como fallback** enquanto não houver
> tributo cadastrado. O valor já está na base do cliente, e trocar o mecanismo zerando a
> retenção faria a clínica emitir um mês inteiro sem imposto sem perceber.

### Retenção na fonte por convênio — ✅ · parcela 18

| Item | Estado | Onde |
|---|---|---|
| Retenção por operadora (IRRF, CSLL/PIS/COFINS, ISS) | ✅ | `Tributo.ConvenioCodigo`, `TributoService.ApurarAsync` |
| Prévia da retenção antes de lançar | ✅ | botão "Reter?" na `ConciliacaoView` |
| Retenção aplicada à receita da guia | ✅ | `FinanceiroService.LancarReceitaDaGuiaAsync` |

> **A operadora retém antes de depositar.** A guia vale R$ 1.000 e caem R$ 943,50. Até a
> parcela 18 o sistema gravava os R$ 1.000 e não sabia da diferença — o mesmo defeito que a
> parcela 9 corrigiu para a maquininha, intocado justamente no convênio, que é onde esta
> clínica fatura mais.

> **A retenção SUBSTITUI os tributos gerais naquele recebimento**, não se soma a eles. Os
> dois representam o mesmo imposto, e somá-los contaria duas vezes: é o erro que mais
> aparece em planilha de clínica de convênio. Se a operadora retém uns e a clínica recolhe
> outros, cadastre todos como linhas do convênio.

> **Sem retenção cadastrada, o convênio cai nos tributos gerais** — o comportamento de
> antes. Mudar isso faria a receita de convênio parar de sofrer imposto da noite para o
> dia, em toda base que já existe. E a retenção de um convênio nunca vaza para recebimento
> que não é dele: a da Unimed não incide sobre o particular pago em dinheiro.

> **O detalhe copiado diz "(retido na fonte)".** Não é decoração: retido já saiu, recolhido
> ainda vai sair, e sem a marca o contador recolheria de novo o que a operadora já reteve.

### Recebíveis de cartão — ✅ · parcela 16

| Item | Estado | Onde |
|---|---|---|
| O que a maquininha ainda deve depositar | ✅ | `RecebiveisService.EsperadosAsync` |
| Depósito atrasado (o alarme) | ✅ | `AtrasadosAsync` |
| Confirmação do crédito, em lote | ✅ | `ConfirmarAsync` + `RecebimentoConfirmadoEm` |
| **Desfazer a confirmação lançada errada** | ✅ | aba "Já caíram" → `ConfirmadosAsync` + `DesfazerConfirmacaoAsync` (parcela 25) |

> **`PrevisaoRecebimento` era gravada desde a parcela 9 e nenhuma tela a lia.** Terceira
> ocorrência do mesmo defeito no projeto (o pacote que debitava, o insumo que baixava):
> dado gravado sem leitor passa no CI e não faz nada na clínica.

> **A conciliação é por DEPÓSITO, não por venda.** A adquirente deposita o lote do dia;
> conferir venda a venda contra um extrato que traz um valor só nunca fecharia.

> **A data real fica separada da prevista.** Quando a adquirente atrasa as duas divergem, e
> sobrescrever a previsão apagaria a prova do atraso. E a data do crédito é **informada**,
> nunca assumida como hoje: conferir na segunda um depósito que caiu na sexta viraria três
> dias de atraso que não houve.

> ⚠️ **E o desfazer nasceu sem porta.** `DesfazerConfirmacaoAsync` era testado desde a
> parcela 16 e nenhuma tela o chamava: quem marcasse "caiu" na data errada ficava com um
> atraso que não houve gravado para sempre — e com o dinheiro sumido da lista do que ainda
> falta cair, que é a única coisa que esta tela existe para vigiar. A aba "Já caíram" mostra
> as **duas datas** (é para isso que elas são campos separados) e devolve o depósito à
> espera; o lançamento continua no caixa, só a data do crédito é apagada.

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
| **Devolver uma sessão ao saldo** | ✅ | `ConsumosPacoteWindow` → `PacoteService.CancelarConsumoAsync` (parcela 25) |
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

> ⚠️ **E devolver UMA sessão não tinha porta até a parcela 25.** `CancelarConsumoAsync`
> existia e era testado desde a parcela 4; a tela só sabia cancelar o pacote INTEIRO. Na
> clínica, a sessão debitada por engano (o paciente não veio, a baixa automática pegou o
> pacote errado) só se resolvia cancelando tudo e revendendo — ou não se resolvia. A janela
> "Sessões…" lista os consumos e devolve o escolhido: **cancelando com motivo**, nunca
> apagando, porque a linha é o que encerra o "o paciente diz que sobrou uma sessão".

> Cuidado para não confundir com `AutorizacaoSessoes`, que é **cota do convênio** — outra
> coisa. Pacote é venda da clínica.

### Feature 10 · Estoque — ✅ · parcela 4

| Item | Estado | Onde |
|---|---|---|
| Entrada e baixa por sessão | ✅ | `EstoqueService.EntrarAsync`/`BaixarAsync`; a baixa POR SESSÃO entrou no fechamento da Recepção (parcela 6) |
| Alerta de mínimo e validade | ✅ | `AbaixoDoMinimoAsync`, `ValidadesAsync` (janela de 60 dias) |
| Custo por atendimento | ✅ | `CustoDoAtendimentoAsync`, com custo médio das entradas |
| **Custo por sessão na tela** | ✅ | aba "Custo por sessão" da `EstoqueView` → `CustosDeSessaoAsync` (parcela 25) |

> ⚠️ **E o custo continuou sem leitor por mais dezenove parcelas.** A parcela 6 ligou a
> baixa por sessão justamente para o "custo por atendimento" parar de responder zero — e
> `CustoDoAtendimentoAsync` seguiu sem **nenhuma tela** que o chamasse até a parcela 25.
> É a variante mais discreta do defeito da casa: o número passou a ser calculável e
> continuou invisível. Agora a aba mostra sessão a sessão, com média e a mais cara; e
> **só entra saída ligada a um atendimento** — a baixa digitada à mão na tela de movimento
> não pertence a sessão nenhuma, e rateá-la daria a cada uma um custo que ela não teve.

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

### Painel da direção — ✅ · parcela 22

| Item | Estado | Onde |
|---|---|---|
| Abertura própria do Gerente Geral | ✅ | `ItemMenuModulo.Inicial` + `ShellViewModel` |
| Dinheiro do mês com variação | ✅ | `PainelDirecaoService.MontarAsync` |
| Contas a pagar vencidas · paciente devendo | ✅ | `AssuntoDirecao.ContasVencidas` / `PacientesDevendo` |
| Depósito de cartão atrasado | ✅ | `RecebiveisService.ResumoAsync` |
| Gaveta não conferida | ✅ | `FechamentoCaixaService.NaoConferidosAsync` |
| Guia baixada sem receita | ✅ | `FinanceiroService.GuiasSemLancamentoAsync` |
| Pendência de faturamento vencida | ✅ | `RodadaPendenciasService.ObterStatusAsync` |
| Glosa dentro e fora do prazo de recurso | ✅ | `PendenciaService.GlosasARecorrerAsync` |
| Cada alerta LEVA ao assunto | ✅ | `NavegacaoSuite` + `ChavesSuite` |

> **O Gerente abria no painel da RECEPÇÃO.** Ele carrega os três módulos, e o shell abria
> no primeiro item do primeiro deles: quem manda na clínica entrava no sistema e via a fila
> do balcão. Informação correta, dona errada. Reordenar os módulos resolveria desmontando a
> sidebar, que já está na ordem do dia de trabalho — daí `Inicial`.

> **O painel não calcula nada.** Cada número vem do serviço que já é dono dele. Painel que
> recalcula por conta própria vira a segunda verdade sobre o mesmo dinheiro, e quando as
> duas divergem ninguém sabe qual está certa.

> **A comparação é com o MESMO TRECHO do mês anterior.** No dia 5, cinco dias contra trinta
> apontariam queda de 80% todo começo de mês, e a direção aprenderia a ignorar a seta — o
> pior destino de um indicador. Sem base de comparação, nada de seta.

> **Cada bloco falha sozinho.** Erro na leitura das contas não derruba o painel nem, muito
> pior, aparece como "nada vencido": o bloco entra em `NaoVerificados` e a tela diz qual
> leitura não rodou.

> **Conta a pagar e paciente devendo são alertas SEPARADOS.** Somá-los daria um número sem
> significado: um se resolve pagando, o outro cobrando.

### Auditoria — a trilha que ninguém lia — ✅ · parcela 21

| Item | Estado | Onde |
|---|---|---|
| Consulta filtrada (ação, operador, detalhe, período) | ✅ | `AuditoriaService.ConsultarAsync` |
| Filtro e corte no SQL | ✅ | `ClinicaRepositorio.ConsultarAuditoriaAsync` |
| Contagem por ação e por operador | ✅ | `ResumoAsync` |
| Trilha de uma guia · de um paciente | ✅ | `DaGuiaAsync` / `DoPacienteAsync` |
| Exportação em CSV | ✅ | `AuditoriaViewModel.ExportarAsync` |

> **Estava gravado e ninguém lia.** `EventoAuditoria` é escrito por praticamente tudo o que
> mexe em dinheiro ou permissão — baixa, estorno, glosa, lote, lançamento, conta, tributo,
> preço, usuário, senha — e nenhuma tela o lia. Quarta ocorrência do mesmo defeito no
> projeto, e a mais grave: as outras três eram dinheiro que ninguém via; esta é a resposta
> para "quem fez isso?", que é o que se pergunta justamente quando algo deu errado.

> **Somente leitura, e isso é decisão.** Registro de auditoria que se pode editar ou apagar
> não é auditoria, é rascunho. Não há exclusão no serviço nem na tela, e não deve haver.

> **O dia final entra inteiro** — senão um evento das 14h de hoje ficaria fora de um filtro
> que pede "até hoje", e a pessoa concluiria que a ação não foi registrada.

> **Bater no limite é avisado.** Um "300 eventos" que na verdade são 300 de 4.000 faria a
> direção concluir que o período teve pouca movimentação.

> **Fica sob `VerAuditoria`, não sob `GerenciarUsuarios`.** Ler a trilha e mexer em
> permissão são coisas diferentes; amarrar as duas obrigaria a dar poder de criar usuário a
> quem só precisa conferir o que aconteceu.

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

### Custo de taxas e impostos — ✅ · parcela 17

| Item | Estado | Onde |
|---|---|---|
| Quanto a maquininha e o fisco custaram | ✅ | `CustoTransacaoService.ResumoAsync` |
| Taxa **efetiva** contra a de **tabela** | ✅ | `PorAdquirenteAsync` + `FaixaDeTabelaAsync` |
| Série mensal com gráfico | ✅ | `SerieAsync`, `GraficoLinha` |
| Exportação CSV | ✅ | `ExportacaoCsv` |

> **O Financeiro cadastra; a direção mede.** São perguntas diferentes: lá é "qual é a taxa
> da Cielo"; aqui é "quanto ela comeu". Por isso o item se chama **Custo de taxas e
> impostos** — o Gerente carrega os dois módulos, e dois itens de mesmo nome em seções
> diferentes da mesma sidebar confundiriam quem só quer saber onde mexer.

> **A taxa efetiva contra a de tabela é a leitura que só existe aqui.** A tabela diz 3,1%;
> se a clínica parcela mais do que imagina, o efetivo do ano é 3,9%. É a diferença entre as
> duas que dá argumento na renegociação. A tabela vem como **faixa** (menor e maior
> vigente), porque uma adquirente tem várias taxas — um número só teria de escolher uma
> modalidade, e a comparação com o efetivo, que mistura todas, seria enganosa.

> **A dedução aparece como percentual do faturamento.** "Foram R$ 14.200 em taxa" é um
> número grande sem referência; "9,4% de tudo o que entrou" é uma decisão.

### Tabela de preço por convênio — ✅ · parcela 20

| Item | Estado | Onde |
|---|---|---|
| Cadastro por convênio, tipo de guia e especialidade | ✅ | `PrecoConvenio`, tela `PrecosConvenioView` (Gerente) |
| Vigência por linha | ✅ | reajuste entra como linha nova |
| Valor proposto na conciliação | ✅ | `PrecoConvenioService.ProporAsync` → `ConciliacaoViewModel` (Financeiro) |
| Procedência do número na linha | ✅ | "tabela: Unimed · Acupuntura — R$ 145,00" |

> **Cadastrada na direção, usada no balcão.** Quem negocia tabela com a operadora é a
> direção; quem concilia guia é o balcão. Mesmo banco, sem sincronização nem cópia — o
> Financeiro lê a mesma tabela que o Gerente escreve.

> **O valor da guia era digitado à mão.** Um R$ 45 no lugar de R$ 145 não é recusado por
> ninguém, e a diferença só apareceria numa conferência que a clínica não faz. Com a tabela,
> a conciliação deixa de ser digitação e passa a ser CONFERÊNCIA contra o demonstrativo.

> **É proposta, não imposição.** A operadora pode ter pago menos (glosa parcial) ou um valor
> negociado fora da tabela. E a linha mostra **de onde veio o número**: campo que se preenche
> sozinho sem explicar é pior que campo vazio — a pessoa confirma sem conferir, e o erro
> entra no caixa com aparência de conferido.

> **Sem preço cadastrado não se inventa valor.** O campo fica vazio para ser digitado, como
> sempre foi. E o **valor é copiado** no lançamento: reajustar a tabela não reescreve o que a
> operadora já pagou.

### Rentabilidade por convênio — ✅ · parcela 19

| Item | Estado | Onde |
|---|---|---|
| Faturado, recebido, retido e líquido por operadora | ✅ | `RentabilidadeConvenioService.PorConvenioAsync` |
| **Líquido por guia** | ✅ | `RentabilidadeConvenio.LiquidoPorGuia` |
| Prazo médio de pagamento | ✅ | da baixa da guia ao dinheiro no caixa |
| Guias efetivadas sem receita lançada | ✅ | `SemReceita` — a fila da conciliação por convênio |
| Taxa de glosa e exportação CSV | ✅ | `ResumoRentabilidade.TaxaGlosa`, `ExportacaoCsv` |

> **O faturamento sabia quantas guias saíram e o financeiro sabia quanto entrou** — os dois
> números nunca se encontravam por convênio, e é o encontro deles que revela quem paga
> pouco, quem paga tarde e quem glosa muito.

> **Líquido por guia é o único número comparável** entre operadoras que pagam valores e
> volumes diferentes. "A Unimed faturou mais" não diz nada se ela precisou de três vezes
> mais atendimentos — por isso a "menos rentável" do resumo é apurada por guia, não pelo
> total, que apontaria sempre a de menor volume.

> **O prazo só conta o que JÁ foi pago.** Incluir o previsto mediria uma promessa, e a
> operadora que atrasa teria o melhor número — o oposto do que a métrica existe para
> mostrar.

> **O agrupamento é pelo CÓDIGO, não pelo nome exibido.** Duas operadoras da mesma família
> que ainda não estejam no catálogo resolvem para o mesmo nome padrão, e agrupar por ele as
> fundiria — apagando a distinção que a retenção da parcela 18 usa.

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

> ⚠️ **E a linha valia para a rodada, não para a tela ao lado** (corrigido na parcela 64).
> A tela "Quem parou de vir" abre o WhatsApp com o mesmo convite de retorno — é recall —,
> e mandava a mensagem **sem checar consentimento**, porque a lista vem de outro serviço
> (`RetencaoPacienteService`). Duas portas para o mesmo ato, uma com regra e outra sem: a
> sem regra é a que ninguém confere, porque a existência da outra dá a sensação de que o
> assunto está coberto. Agora as duas leem o MESMO
> `PacientesComConsentimentoVigenteAsync`, quem não consentiu segue na lista com o motivo
> escrito e o botão apagado, e `RetencaoConsentimentoTests` prova que os dois lados
> respondem igual — revogação incluída.

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
| Direitos do titular: acesso e eliminação | ✅ | `TitularDadosService` — exportar e anonimizar, na ficha do paciente (parcela 26) |
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

> **O app de FATURAMENTO passou a pedir login na parcela 45**, a pedido da cliente, com
> permissões granulares (dar baixa, estornar, glosar, gerar lote, lançar atendimento,
> decidir não faturar, configurar). O motivo não foi arquitetura: enquanto ele não teve
> sessão, toda ação dele ia para a auditoria assinada pelo usuário do WINDOWS, e a direção
> não conseguia responder "quem fez isso?" justamente na tela onde a pergunta importa.

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

Todos os apps compartilham `Domain`, `Application` e `Infrastructure` — o mesmo banco e os
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
| Preço negociado da guia usado na conciliação | Gerente → Financeiro | `PrecoConvenioService` (parcela 20) |
| Retenção na fonte por operadora | Gerente → Financeiro | `Tributo.ConvenioCodigo` (parcela 18) |
| Alerta da direção que LEVA à tela dona | Gerente → todos | `PainelDirecaoService` + `NavegacaoSuite`/`ChavesSuite` (parcela 22) |
| **Glosa que derruba receita já contada** | **Faturamento → Financeiro** | **`ReceitaGlosadaService` (parcela 27)** |
| **Guia glosada marcada na conciliação** | **Faturamento → Financeiro** | **`GuiaSemLancamento.GlosaEmAberto` (parcela 27)** |
| **Conta vencida do paciente no balcão** | **Financeiro → Recepção** | **`ElegibilidadeService` + `InadimplenciaService` (parcela 27)** |
| **Guia glosada no balcão, com o prazo de recurso** | **Faturamento → Recepção** | **`ElegibilidadeService` (parcela 27)** |

### O que a parcela 27 corrigiu

Até ela, **todas as pontes iam para a frente**. A sessão virava guia, a guia virava
receita, a receita virava indicador — e nada voltava. O buraco mais caro estava aí: o
convênio recusava a guia (`GlosaService`) e o Financeiro **nunca ficava sabendo**. A
palavra "Glosa" não aparecia em um único arquivo do módulo. O dinheiro recusado continuava
no fluxo de caixa, no previsto e na rentabilidade por convênio, como se fosse entrar.

> **Receita fantasma é a pior espécie de número errado, porque tem cara de número exato.**
> Não há linha vermelha, não há aviso: a direção olha a previsão do mês e decide errado
> sem saber que está decidindo errado.

Os outros dois eram do mesmo tipo, na direção contrária: **o balcão não sabia de nada
que os outros módulos sabiam sobre o paciente à frente dele.** A conta vencida existia
desde a parcela 12 e virou tela na 23 — no Financeiro. A glosa existia desde o começo —
no Faturamento. Quem podia resolver as duas em trinta segundos (com a pessoa presente,
uma assinatura, uma frase) era exatamente quem não via nenhuma delas.

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

### O circuito é testado inteiro (parcela 33)

`CircuitoCompletoTests` percorre o caminho de ponta a ponta, uma vez por circuito. Existe
porque **o resto da suíte testa trechos**: o fechamento sozinho, a conciliação sozinha, a
glosa sozinha — e os três podem passar com o circuito partido, já que o que liga um módulo
ao outro aqui não é chamada de método, é **chave estrangeira**.

| Circuito | O elo que o teste prende |
|---|---|
| Recepção → Faturamento → Financeiro | A guia sai da conciliação porque passou a **ter receita** (`CodigoFaturamentoId`), não porque alguém a marcou |
| Sessão de pacote | Debita o saldo e **não sugere cobrança** — sessão comprada já foi paga |
| Glosa → Financeiro | Cancelada a receita PREVISTA, a guia **reaparece sozinha** na conciliação |
| Glosa → conciliação | A guia volta **marcada**, nunca em branco |
| NC → Recepção | O paciente que volta **reabre** a não conformidade da guia antiga |
| Tudo → Gerente | O dia fechado na Recepção chega ao painel, com os onze serviços montados à mão |
| Glosa → Gerente | Receita glosada é alerta **próprio**, não somado ao resto |
| Base vazia | O painel abre na clínica recém-instalada sem nenhum bloco "não verificado" |

> **Elo partido não vira erro — vira número zerado.** É por isso que o painel da direção é
> o teste certo para o fim do circuito, e é a mesma razão pela qual ele nunca calcula nada
> por conta própria: zero por defeito é indistinguível de zero porque o dia foi fraco.

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
| **PACIENTE** · Retorno de pacientes | Recepção | 🔵 | `RetornoView` (parcela 48) — o recall onde quem telefona trabalha |
| **PACIENTE** · Convênio (senha/cota) | Recepção | 🔵 | aba na ficha + `AutorizacaoWindow` (parcela 48) |
| **GESTÃO** · Sala de infusão | Recepção · Consultório | 🔵 | `Desktop.Shell/Componentes/SalaInfusaoView` (parcelas 42 e 48) |
| **PACIENTE** · Telemedicina | — | ❌ | **FORA DE ESCOPO** por decisão do cliente (jul/2026) |
| **PACIENTE** · Portal do paciente | — | ❌ | **FORA DE ESCOPO** por decisão do cliente (jul/2026) |
| **FINANCEIRO** · Pacotes / Sessões | Financeiro **e Recepção** | ✅ | `Desktop.Shell/Componentes/PacotesView` — a tela SUBIU para o shell na parcela 60 e os dois módulos publicam a MESMA chave (`ChavesSuite.Pacotes`); quem vende dez sessões ao paciente é o balcão, com ele na frente |
| **FINANCEIRO** · Financeiro | Financeiro | ✅ | `CaixaView` + Conciliação, Produção, Repasses |
| **FINANCEIRO** · Faturamento (TISS) | Gerente | ✅ | `FaturamentoTissView` — 5 abas (parcelas 10b–10d) |
| **FINANCEIRO** · Estoque | Financeiro | ✅ | `EstoqueView` |
| **FINANCEIRO** · Taxas e impostos | Financeiro | ✅ | `TaxasView` (parcela 9) — 🔵 não estava na sidebar, mas o cliente cobrou |
| **FINANCEIRO** · Contas a pagar/receber | Financeiro | ✅ | `ContasView` (parcela 12) — 🔵 fora da sidebar da proposta |
| **FINANCEIRO** · Fluxo de caixa | Financeiro | ✅ | `FluxoCaixaView` (parcela 13) — 🔵 idem |
| **FINANCEIRO** · Fechamento de caixa | Financeiro | ✅ | `FechamentoCaixaView` (parcela 14) — 🔵 idem |
| **FINANCEIRO** · Recebíveis de cartão | Financeiro | ✅ | `RecebiveisView` (parcela 16) — 🔵 idem |
| **INTELIGÊNCIA** · Custo de taxas e impostos | Gerente | ✅ | `CustoTransacaoView` (parcela 17) — 🔵 idem |
| **INTELIGÊNCIA** · Rentabilidade por convênio | Gerente | ✅ | `RentabilidadeConvenioView` (parcela 19) — 🔵 idem |
| **FINANCEIRO** · Tabela de preço (convênios) | Gerente | ✅ | `PrecosConvenioView` (parcela 20) — 🔵 idem |
| **INTELIGÊNCIA** · Marketing / Recall | Gerente | ✅ | `CampanhasView` |
| **INTELIGÊNCIA** · Relatórios / BI | Gerente | ✅ | `IndicadoresView` com gráficos e exportação CSV (parcelas 10d e 11) |
| **INTELIGÊNCIA** · Configurações | Gerente | ✅ | `ConfiguracoesView` (parcela 10a) |

**Os grupos são temáticos, não por módulo.** `ItemMenuModulo.Grupo` (`GrupoSidebar`) decide
onde o item aparece; `ModuloNome` diz quem sabe construir a tela. São duas coisas que só
pareciam uma: antes da parcela 7 o cabeçalho era o nome do módulo carregado, e o Gerente —
que carrega os três — via "Recepção / Financeiro / Direção". Uma sidebar que explica a
arquitetura para quem só quer saber onde mexe no paciente.

## O que ainda NÃO existe

Levantado no código, não na memória. Conferido de novo na **parcela 48**.

| Falta | Módulo dono | Situação |
|---|---|---|
| ~~Telemedicina~~ | — | **FORA DE ESCOPO** (decisão do cliente, jul/2026). Precisa sair da arte da sidebar no material comercial |
| ~~Portal do paciente~~ | — | **FORA DE ESCOPO** (decisão do cliente, jul/2026). Idem |
| ~~Assinatura ICP-Brasil~~ | Consultório | **✅ parcelas 42 e 43** — assinatura qualificada PAdES (PKCS#7 SHA-256) na folha de infusão E nos quatro documentos que saem da clínica (receita, atestado, comparecimento, pedido de exame), com carimbo do tempo RFC 3161 opcional e QR para o validador de saúde do ITI |
| ~~Agendamento em série~~ | Recepção | **✅ parcela 26** — `AgendaService.AgendarSerieAsync`; a data sai sempre da primeira mais N períodos |
| ~~LGPD além do consentimento~~ | Recepção | **✅ parcela 26** — `TitularDadosService`: exportação e anonimização (o prontuário não se apaga, art. 16, II) |
| ~~Metas (faturamento, ocupação)~~ | Gerente | **✅ parcela 28** — `MetaService`; teto de despesa na 31 (`OrcamentoService`) |
| ~~Apuração mensal por tributo~~ | Gerente | **✅ parcelas 28 e 31** — ISS, PIS, COFINS, IRPJ e CSLL separados, com a retenção resolvida por recebimento |
| ~~Cota do convênio sem porta na Recepção~~ | Recepção | **✅ parcela 48** — o balcão era avisado de que a cota ia estourar e a única porta para registrar a senha estava no faturamento |
| ~~Saldo de pacote invisível ao marcar~~ | Recepção | **✅ parcela 48** — entrou no `ElegibilidadeService`, então chega ao agendamento, ao check-in e à ficha |
| ~~Recall só no Gerente~~ | Recepção | **✅ parcela 48** — quem telefona é o balcão |
| ~~Sala de infusão fora do alcance da enfermagem~~ | Recepção | **✅ parcela 48** — a tela subiu para o shell; os dois módulos publicam a mesma chave |
| NFS-e no fechamento | Financeiro | Depende de integração fiscal municipal — decisão comercial |
| Gerar lote TISS pelo Gerente | Gerente | **Decisão de projeto, não pendência**: o número do lote é sequência do faturamento, e dois apps gerando em paralelo produziriam dois com o mesmo número |
| ~~**Conciliação bancária (OFX)**~~ | Financeiro | **✅ parcela 63** — `LeitorOfx` + `ConciliacaoBancariaService` + aba "Extrato do banco". O sistema PROPÕE o par e a pessoa confirma; nada concilia sozinho |
| ~~**Faixa lateral de 300–400 px**~~ | Financeiro · Gerente | **✅ já estava paga** — a parcela 49 converteu as cinco do Financeiro (catálogo de pacotes, contas fixas, regras de repasse, validades do estoque, alíquota) em botão + janela. A varredura da parcela 63 conferiu as seis telas e **nenhuma tem faixa**: os 330 px do `AcessosView` e os 300 do `CampanhasView` são a coluna de **Ações** de uma tabela de largura inteira — o falso positivo que a parcela 54 já tinha documentado. Esta linha estava obsoleta |

> **Nenhuma continua aberta.** As três últimas foram fechadas na parcela 63 — e a de
> leiaute só precisava ser CONFERIDA: já estava paga desde a 49, e a planilha é que não
> tinha sido atualizada. É o defeito da feature 02 pelo avesso (lá o ✅ mentia a favor,
> aqui o pendente mentia contra), e a lição é a mesma: **linha de placar sem conferência
> no código é chute com aparência de registro.**

## Divergências da proposta

O documento já foi ao cliente. Estas precisam de decisão comercial:

1. **Página 24 diz "Dois apps, um banco".** São **quatro** apps, um por perfil.
2. ~~**Página 23 marca ✓ em "Prontuário com mapa corporal e EVA"**~~ — **resolvida na
   parcela 3.** A EVA saiu na parcela 2 e o mapa corporal saiu agora; a afirmação passou
   a ser inteiramente verdadeira.
3. ~~**"Assinatura digital" da feature 07.**~~ — **resolvida nas parcelas 42 e 43.** A
   assinatura qualificada ICP-Brasil existe (PAdES-B, com QR para o validador de saúde do
   ITI); a divergência que RESTA é operacional: falta o e-CPF da clínica para o teste de
   campo, e sem ele a feature não opera em produção. Cobrar o certificado é ação
   comercial, não técnica.
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

As **vinte e cinco parcelas estão entregues** — 0 (instalável), 1 (fundação), 2 (cadastro e
prontuário), 3 (ato clínico), 4 (dinheiro e insumo), 5 (inteligência), 6 (integração),
7 (moldura e navegação), 8 (prontuário/prescrições/CRM), 9 (taxas e impostos),
10 (Configurações, faturamento no Gerente, exportação), 11 (gráficos), 12 (contas a pagar e
receber), 13 (fluxo de caixa), 14 (fechamento de caixa), 15 (regime tributário),
16 (recebíveis de cartão), 17 (custo de transação), 18 (retenção por convênio),
19 (rentabilidade por convênio), 20 (tabela de preço), 21 (auditoria), 22 (painel da
direção), 23 (inadimplência), 24 (central de documentos) e 25 (as capacidades sem porta).

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

A **parcela 25 não trouxe feature nova** — como a 6, ligou o que já existia. Seis
capacidades estavam prontas, testadas e **sem um único chamador em produção**: o custo por
sessão (que a parcela 6 destravou e ninguém lia), devolver uma sessão ao pacote, desfazer a
confirmação de depósito, sugerir quem chamar para o horário que vagou, conferir o documento
pelo código impresso e apagar modelo/protocolo. É a quinta rodada do mesmo defeito no
projeto, e a mais discreta: as outras eram **dado gravado sem leitor**, esta é **serviço
testado sem porta**. Vale a mesma lição, agora escrita como regra: antes de dar uma feature
por pronta, **procure o chamador em produção** — CI verde não prova que a clínica alcança
a função.

A **parcela 26 é a primeira desde a 6 a olhar um módulo inteiro em vez de um assunto**: a
Recepção, que é onde o dia da clínica começa. Seis buracos, todos do mesmo tipo — a
capacidade existia em algum canto do sistema, mas não no lugar onde a decisão é tomada. A
elegibilidade respondia na ficha e não na hora de marcar; a rodada de confirmação morava só
no Gerente, embora quem liga para o paciente seja o balcão; férias e feriado não existiam
para a agenda, que continuava aceitando marcação no dia 25; o pacote de dez sessões era
vendido pelo Financeiro e marcado à mão, um horário por vez; a agenda só respondia "como
está hoje", nunca "quando cabe"; e a LGPD parava no consentimento, sem os dois pedidos que
o paciente pode fazer e a clínica é obrigada a atender. Nenhum deles aparecia como ⬜ no
quadro — todos eram ✅ **em outro lugar**. É a variante do defeito recorrente que o quadro
de features não pega: **feature entregue longe de onde ela é usada equivale a feature que
não existe.**

A **parcela 27** é a primeira a olhar as LIGAÇÕES em vez dos módulos. O achado: todas as
pontes iam para a frente. A sessão virava guia, a guia virava receita, a receita virava
indicador — e **nada voltava**. Quando o convênio dizia não, o não morria no faturamento:
a receita glosada continuava contada no caixa e na rentabilidade, e a direção decidia
sobre um número que não existia. Na direção contrária, o balcão — o único lugar onde o
paciente está de corpo presente — não sabia da conta vencida dele (Financeiro) nem da
guia glosada dele (Faturamento), embora as duas estivessem gravadas havia parcelas e as
duas se resolvessem ali, em trinta segundos. É a sexta rodada do defeito recorrente do
projeto, agora na forma mais estrutural de todas: **o dado existe, tem leitor, tem tela —
e a tela está do lado errado da ponte.**

As **parcelas 28 a 32** foram uma rodada de 30 melhorias em cinco lotes, e o que elas têm
em comum não é um módulo — é um TIPO de lacuna. Três padrões apareceram:

**O dado existia e nenhuma tela o lia.** A data de nascimento estava no cadastro desde
sempre (aniversariantes), `Faltou` era gravado desde a parcela 1 e só virava taxa da
CLÍNICA (padrão de falta do paciente), `AbaixoDoMinimoAsync` existia desde a parcela 4 sem
um chamador (lista de compras). É a mesma coisa que as parcelas 21 a 25 já tinham corrigido
em outros pontos — e continua aparecendo.

**A pergunta não tinha régua.** O painel comparava tudo com o mês anterior, e variação
responde "melhorou?" e nunca "chegamos onde a gente disse que ia chegar?". Meta (28) e teto
de gasto (31) são a mesma ideia aplicada às duas pontas do dinheiro.

**O número existia consolidado e ninguém conseguia abri-lo.** `ValorImposto` somava cinco
tributos num campo só (apuração), e o resultado do mês não existia em lugar nenhum — o
módulo respondia "o que entrou e saiu", "como se distribui no tempo" e "quanto de imposto",
e nunca "sobrou quanto".

> **O que NÃO entrou de propósito.** Ao levantar o lote do Financeiro, duas melhorias
> planejadas (alerta de mínimo e alerta de validade no estoque) já existiam na tela, e uma
> terceira (comparativo ano a ano) teria pouco valor com um ano de base. Foram trocadas por
> lacunas reais em vez de contadas como entrega — contar o que já está pronto é a forma
> mais fácil de um quadro de features mentir.

## Parcela 36 — o Consultório, o app de quem atende

Até aqui a suíte cobria o balcão (Recepção), o dinheiro (Financeiro) e a leitura do
negócio (Gerente). Faltava a máquina do **médico, do fisioterapeuta e do
acupunturista** — e a lacuna não era "eles não veem a agenda", porque a agenda existe
desde a parcela 1. Era outra, e é a lacuna clássica deste projeto: **o registro clínico
que fica para depois.**

| Feature | Estado | Onde |
|---|---|---|
| Meu dia — a agenda do profissional, com o que já foi atendido | ✅ | `ConsultorioService.DoDiaAsync` |
| **Sessões atendidas sem evolução escrita** (dias anteriores) | ✅ | `ConsultorioService.RegistrosPendentesAsync` |
| Atendimento com EVA antes/depois e as últimas sessões abertas ao lado | ✅ | `AtendimentoViewModel` sobre `ProntuarioService` |
| Repetir a conduta da última sessão (traz para a tela, não grava) | ✅ | `AtendimentoViewModel.RepetirUltima` |
| Painel de evolução da dor — duas curvas, redução % e tendência | ✅ | `EvolucaoDorViewModel` sobre `ProntuarioService.EvolucaoDaDorAsync` |
| Escalas por especialidade: PHQ-9, GAD-7, Oswestry, Katz, FINDRISC | ✅ | `Domain/Avaliacoes/`, `AvaliacaoClinicaService` |
| Curva do escore de um instrumento, com o sinal da melhora resolvido | ✅ | `AvaliacaoClinicaService.EvolucaoDoEscoreAsync` |
| Carteira da clínica, com a leitura da dor de cada paciente | ✅ | `ConsultorioService.PacientesAsync` — sem dono: *"não existe 'meu paciente', todos atendem todos"* (parcela 88) |
| Especialidade **Neurocirurgia** | ✅ | Código do catálogo (`InstrumentoOswestry.CodigoNeurocirurgia`) — **fora do enum**, para não aparecer no seletor do app congelado |
| Quinto executável, instalável e com auto-update no canal `clinico` | ✅ | `src/Clinica.Clinico` |

**As duas curvas do painel de dor não são enfeite.** A curva ANTES mede o TRATAMENTO; a
curva DEPOIS mede a SESSÃO. Um paciente pode sair de toda sessão com 2 e voltar na semana
seguinte com 8 — a dor alivia e retorna, e o tratamento não está sustentando. Uma linha só
esconderia exatamente esse caso, que é o que muda conduta.

**Por que escalas, e por que estas cinco.** A clínica é de acupuntura, geriatria,
psiquiatria, neurocirurgia e endocrinologia. A EVA responde à primeira e à dor; as outras
quatro não tinham número nenhum — escreviam "refere melhora do humor" na evolução, o que é
verdade e não compara consulta com consulta, não desenha curva e não vai para o relatório.
O que o sistema faz com elas é **pontuar e registrar**: a faixa é a leitura publicada da
escala, e a interpretação é de quem assina o prontuário — a tela diz isso o tempo todo.

### O Consultório dentro do circuito

A primeira versão do módulo tinha um defeito estrutural: gravava avaliação e evolução, e
**só ele as lia**. É a sexta ocorrência do padrão que este documento já registrou cinco
vezes — e a mais constrangedora, porque cometida logo depois de descrevê-lo. O que fecha
o circuito:

| Sentido | O que passa | Onde |
|---|---|---|
| Consultório → Gerente | Sessões atendidas sem evolução escrita, com a guia faturada contada à parte | `PainelDirecaoService` (`AssuntoDirecao.ProntuarioEmAberto`) |
| Consultório → Gerente | Completude do prontuário por profissional | `ProdutividadeProfissional.CompletudeProntuario`, na tela e no CSV |
| Consultório → Recepção | Escalas aplicadas no relatório de evolução e no prontuário | `DocumentoClinicoService.EmitirRelatorioEvolucaoAsync`, `ProntuarioViewModel` |
| Faturamento/Financeiro → Consultório | Carteirinha, cota, conta vencida e guia glosada na tela de atendimento | `ElegibilidadeService` |
| Recepção ↔ Consultório | Mapa corporal e emissão de documento, um componente só | `Clinica.Desktop.Shell/Componentes` |

O elo do primeiro é uma **chave estrangeira**, não uma chamada: `Evolucao.AgendamentoId`.
Se o atendimento parasse de gravá-lo, nenhum teste de unidade falharia — o Consultório
cobraria para sempre um registro já escrito e a direção veria prontuário em aberto numa
clínica que documenta tudo. Por isso o circuito é testado de ponta a ponta
(`CircuitoCompletoTests`, circuito 5), como os quatro da parcela 27.

**Nada disso toca o faturamento.** A parcela foi auditada arquivo a arquivo contra o app
congelado, e a auditoria achou uma coisa: `Especialidade.Neurocirurgia`, acrescentada ao
enum, apareceria sozinha no seletor de especialidade do lançamento de atendimento — porque
o `App.xaml.cs` dele carrega o catálogo na abertura e o catálogo garante as embutidas por
`Enum.GetValues`. Ninguém tinha tocado num arquivo de `Clinica.Desktop`; a tela mudaria
assim mesmo. A especialidade virou código de catálogo, e duas travas nasceram daí:
`FaturamentoCongeladoTests` (fixa os enums que ele consome) e a **checagem 18** do
`verificar-suite.py` (reprova migration destrutiva).

**O que ficou de fora, e por quê.** O `PrevencaoGlosaService` seria o lugar natural do
aviso "guia sem prontuário" — e ele é chamado **só pelo app congelado**. Acrescentar o
alerta ali mudaria o comportamento do faturamento em produção e dispararia para toda guia
numa clínica que ainda não documenta. A leitura foi para a direção, onde é nova.

> **O que NÃO entrou de propósito.** Antropometria e sinais vitais (peso, IMC,
> circunferência, pressão, glicemia) seriam uma entidade nova, com tela e histórico
> próprios — valem uma parcela, não uma nota de rodapé desta. O FINDRISC pergunta IMC e
> circunferência ao aplicar, então a endocrinologia já tem por onde começar; o registro
> seriado dessas medidas fica em aberto, declarado, em vez de meio-feito.
> *(Entregue na parcela 37, logo abaixo.)*

## Parcela 37 — o prontuário que o Consultório precisava

A parcela 36 entregou o app de quem atende e, ao ser auditada com a lente do próprio
projeto, revelou quatro lacunas — três delas da mesma família, e a família é a que dá
nome ao produto: **o que o profissional precisa ver e não alcança.**

| Feature | Estado | Onde |
|---|---|---|
| **Anexos da sessão no Consultório** — o laudo que volta do exame que ele pediu | ✅ | `AnexosSessaoViewModel` sobre `ProntuarioService.AnexarAsync` |
| **Prontuário inteiro com busca por texto**, na máquina de quem atende | ✅ | `ProntuarioClinicoViewModel` |
| **Lista de problemas** — diagnóstico, alergia, antecedente, uso contínuo | ✅ | `ProblemaPacienteService`, `ProblemaPaciente` |
| Alergia e uso contínuo como alerta na tela de atendimento | ✅ | `AtendimentoViewModel.AlertasClinicos` |
| **Antropometria e sinais vitais seriados** (peso, altura, cintura, PA, glicemia, HbA1c) | ✅ | `MedidaClinicaService`, `CatalogoMedidas` |
| IMC derivado do peso com a altura vigente, com a procedência da altura | ✅ | `MedidaClinicaService.ResumoAsync` / `SerieAsync` |
| Curva de qualquer medida, com a direção da variação resolvida | ✅ | `MedidasViewModel` sobre `SerieMedida` |
| Contagem de anexos do prontuário em UMA consulta (era uma por sessão) | ✅ | `ProntuarioService.ContagemDeAnexosAsync` |

**As três primeiras são a mesma tese.** Nenhuma exigiu capacidade nova do sistema: anexar
existe desde a parcela 2 e a busca no prontuário desde a 28. O que faltava era **porta no
módulo certo** — e essa é a variante mais discreta do defeito recorrente deste projeto,
porque `dotnet test` fica verde e a Recepção usa todo dia. A mais gritante das três é a
dos anexos: o Consultório **emitia pedido de exame** desde a parcela 36 e não tinha onde
ler o laudo de volta. Ele pedia e não recebia. A da busca é a mais irônica: o comentário
que a acompanha no módulo da Recepção diz, com todas as letras, "a pergunta que o
profissional faz antes de atender é sempre a mesma" — a feature foi justificada pelo
profissional e entregue na tela de quem não atende.

**Por que medidas seriadas, e por que agora.** A parcela 36 declarou a lacuna por escrito
e a deixou em aberto. O argumento que a fechou foi o FINDRISC: ele PERGUNTA o IMC e a
circunferência de cintura, o paciente responde, o escore é gravado — e os dados que o
produziram evaporavam. As regras que a parcela fixou:

- **O IMC não se digita.** É derivado do peso com a altura vigente, e a data dessa altura
  vai junto: um IMC calculado com altura de três anos atrás continua sendo a melhor
  leitura disponível, desde que quem lê saiba disso. Gravá-lo criaria um terceiro número
  livre para contradizer os dois que o originam, com a mesma cara de fato.
- **A única recusa é a implausibilidade.** 2500 kg é dedo escorregado no teclado; 210 kg é
  anormal e possível, e recusá-lo esconderia justamente o paciente que precisa de atenção.
- **Faixa ausente e faixa normal são coisas diferentes.** O peso isolado não tem leitura
  publicada — 80 kg é obesidade numa pessoa e magreza noutra —, e o selo SOME em vez de
  dizer "normal".
- **Meia pressão arterial não existe**: tipo com par exige os dois números, e a diastólica
  maior que a sistólica é campo trocado, não paciente raro.
- Os cortes da cintura são os do **FINDRISC**, e seguem o sexo. Usar aqui um corte
  diferente do da escala aplicada ao lado faria a mesma clínica dar duas leituras da mesma
  fita métrica.

**A lista de problemas é fundação de prontuário.** Até aqui o CID morava só dentro de
`DocumentoClinico`: um campo por papel emitido, nunca o diagnóstico da pessoa. O
profissional redigitava "M54.5" a cada atestado, ninguém respondia "o que este paciente
tem?" sem ler o prontuário inteiro, e a alergia ficava enterrada em texto livre no meio de
uma evolução de dois anos atrás. As regras seguem as da NC do faturamento e do documento
clínico cancelado: **não se apaga — muda-se a situação**, e **descartar exige motivo
escrito**. A que é só dela: **alergia alerta mesmo depois de dada por resolvida**, porque
"resolvida" numa alergia é quase sempre "não reagiu da última vez", e o dia em que reagir
é o dia em que o aviso teria valido. Só o descarte a cala.

**Nada disso toca o faturamento.** Nenhum valor novo em enum embutido, nenhuma migration
destrutiva (`ProntuarioMedidasEProblemas` só cria tabela), e nenhum serviço que o app
congelado chama mudou de comportamento. O catálogo de medidas mora em CÓDIGO pelo mesmo
desenho das escalas — o corte do IMC é definição publicada, não configuração da clínica.

> **O que NÃO entrou de propósito.** O saldo da autorização de sessões na tela de
> atendimento ("faltam 2 de 10") ficou de fora a pedido: `AutorizacaoService` segue com
> chamador só no app congelado, e o `ElegibilidadeService` continua avisando a cota
> DEPOIS de estourada. É lacuna declarada, não resolvida.

### Parcela 37 (2ª rodada) — a UI do Consultório

O módulo funcionava e **parecia um esboço**. Rodado contra um banco de verdade, três das
cinco telas abriam assim: coluna de busca vazia com 300 px de altura inteira, miolo de mil
e duzentos pixels em branco com uma frase cinza flutuando no meio, e botões acesos sobre
uma tela sem paciente nenhum.

| O que estava errado | O que passou a ser |
|---|---|
| Subtítulo de toda página flutuando no meio da tela | `TextoSuave` com `HorizontalAlignment="Left"` — o `MaxWidth` sem alinhamento fazia o WPF centralizar o texto encolhido |
| Busca vazia como único caminho para escolher paciente | `PainelPacienteClinico`: abre com a **fila do dia** e a **carteira**; a busca vira atalho |
| Frase cinza solta como "estado vazio" | `EmptyState` com ícone, título e o que fazer |
| Nome do paciente como `H2` solto sobre o fundo | `FaixaPaciente`: avatar, nome, contexto e as ações da tela |
| Exportar/Atualizar acesos sem paciente escolhido | Ações dentro da faixa — só existem depois de haver alguém |
| Contagem de anexos, uma consulta por sessão | `ContagemDeAnexosAsync`, uma para o prontuário inteiro |

**Nada disso pedia dado novo.** A fila do dia (`DoDiaAsync`) e a carteira
(hoje `PacientesAsync`) existiam desde a parcela 36, cada uma servindo a uma tela só — é a
mesma família do defeito que a parcela inteira corrigiu, agora na camada da tela: a
informação estava lá e o lugar onde ela decidia alguma coisa não a alcançava.

Três armadilhas de WPF que a implementação encontrou e que valem para qualquer lista da
suíte: **uma ListBox por bloco não funciona** (duas amarradas ao mesmo `SelectedItem` se
limpam mutuamente — use `CollectionViewSource` + `GroupStyle`); **remonte por busca
concluída**, não por `CollectionChanged`, que dispara uma vez por linha inserida; e
**vazio antes da resposta não é resposta** — enquanto a busca do termo digitado não voltou,
o painel não diz "nenhum paciente encontrado".

## Parcela 38 — o quadro do consultório e a chamada do próximo

O "Meu dia" do Consultório era uma **lista corrida** do que estava marcado. Ela responde
"quem vem hoje" e não responde a pergunta que quem atende faz vinte vezes por dia: **quem
já está aí e quem eu posso chamar agora**. A fila do balcão era kanban desde a parcela 26;
o consultório, que é quem consome essa fila, olhava a agenda.

E faltava o recado de volta. Quem chama pelo nome na sala de espera é a **recepção** — o
profissional está na sala, com a porta fechada. Sem uma porta no sistema, a alternativa
real da clínica é abrir a porta e gritar, ou ligar para o balcão a cada paciente.

| O que passou a existir | Onde |
|---|---|
| "Meu dia" em kanban de cinco colunas | `MeuDiaViewModel` / `MeuDiaView` |
| **Chamar próximo** — um clique, o primeiro da recepção | `ChamarProximoCommand` |
| Chamar um paciente específico / desfazer a chamada | `ChamarCommand`, `DesfazerChamadaCommand` |
| Coluna **CHAMADO** e a faixa "anuncie na sala de espera" | `FilaViewModel` / `FilaView` (Recepção) |
| "Chamado há N min", com destaque a partir de 3 | os dois quadros, mesmo corte |
| Releitura automática do quadro, a cada minuto | `ReconferirAsync` nos dois ViewModels |

**A sincronização é o BANCO**, como todo o resto da suíte — nem fila de mensagens, nem
evento, nem um módulo conhecendo o outro. O consultório carimba `Agendamento.ChamadoEm`, a
recepção lê a mesma linha, e é isso que faz os dois quadros nunca divergirem. O estágio
`Chamado` **não virou coluna no banco**: ele é derivado dos carimbos de hora, como os
outros quatro (`Agendamento.Etapa`). O que precisou de migration foi só o fato novo — a
hora da chamada —, e ela é puramente aditiva: o faturamento congelado não conhece a fila,
só lê `StatusAgendamento`, e segue subindo sobre este banco sem saber que a coluna existe.

As regras que a parcela fixou:

- **Só se chama quem já fez check-in.** Chamar quem não chegou faria a recepção anunciar
  um nome para uma sala de espera onde a pessoa não está — e a fila perderia a única
  informação que a torna confiável, que é a de quem está no prédio.
- **Chamar duas vezes não reinicia o cronômetro** (`??=`). Quem quer insistir precisa ver
  há QUANTO tempo chamou, e um segundo clique zerando esse número esconderia justamente o
  caso que exigia a insistência.
- **Chamar existe dos dois lados.** Em metade das clínicas o profissional avisa pela porta,
  e obrigar o balcão a esperar um clique da sala faria a coluna nascer sempre vazia num
  fluxo que funciona há anos. Carimba quem clicar primeiro.
- **Entrar carimba a chamada junto.** Uma linha do tempo com entrada e sem chamada não
  existe; sem isso o histórico diria que a pessoa entrou sem ser chamada.
- **A espera para na CHAMADA, não na entrada.** O que se mede é quanto tempo o paciente
  ficou sem notícia — contar até ele levantar da cadeira somaria o tempo de atravessar a
  sala e faria a clínica parecer pior do que é.
- **A releitura de fundo é silenciosa.** Não acende "Carregando" nem escreve erro: quem
  está no balcão com um paciente à frente não pode ver a fila piscar em branco a cada
  minuto, nem levar um aviso vermelho porque o banco demorou uma vez. Falha vai ao log e a
  tela segue com o quadro do minuto anterior. E só relê **hoje** — quem confere a agenda da
  semana que vem não tem fila correndo.
- **A faixa acima do quadro nomeia quem chamar e para qual sala.** A coluna sozinha não
  bastaria: o balcão passa o dia com esta tela aberta e os olhos no paciente à frente dele,
  e um cartão que muda de coluna em silêncio é um cartão que ninguém vê. "1 paciente
  chamado" mandaria a recepcionista procurar antes de abrir a boca.
- **Só HOJE se chama.** Olhar a agenda de terça que vem e poder chamar alguém de lá seria
  mandar a recepção anunciar um nome com dois dias de antecedência.

### Parcela 38 (2ª rodada) — a tela picada em caixas

A 5ª reprovação do cliente, e a mais direta: *"várias box dividindo uma tela"*. O "Meu dia"
saiu da 1ª rodada com **quatro faixas empilhadas antes do quadro** — e num dia já
terminado, as quatro raias vazias viravam um buraco de mil pixels ao lado da única com
conteúdo.

| O que estava errado | O que passou a ser |
|---|---|
| Slab azul de largura inteira gastando 70 px para escrever *"ninguém aguardando no balcão"* | **Botão** no cabeçalho, que diz o NOME de quem vai chamar e fica desabilitado quando não há ninguém |
| Faixa azul permanente do aviso de vínculo | **Linha de texto** sob o subtítulo — faixa permanente vira moldura |
| Linha de resumo repetindo as cinco contagens em sequência | Contagem no **cabeçalho de cada raia**, junto do que ela conta (`CabecalhoRaia`) |
| Caixa de `MaxHeight="180"` cortando um nome de paciente ao meio | **Tela própria** de largura inteira (`ChaveRegistrosPendentes`) + botão com a contagem |
| Cinco `Card` com borda lado a lado | **Raias**: faixa de fundo levíssima, sem moldura, separadas por espaço |
| Quatro colunas vazias como vão branco | Cada raia vazia mostra um **traço** e continua legível |
| Quatro botões acesos em todo cartão | **Uma ação principal**; as secundárias só na raia em que servem |

A regra foi escrita no **topo do `README.md`** ("A REGRA DE LEIAUTE"), com a tabela do que
é proibido e as três perguntas que decidem: *isto é o que a pessoa VÊ ou o que ela FAZ de
vez em quando?*, *esta seção existe sem um item escolhido?* e *quantas perguntas esta tela
responde?*. **Na dúvida entre outra caixa e outra tela, é outra tela** — e um botão que
leva até ela.

O mesmo tratamento foi aplicado à **fila da Recepção**, que tinha o defeito idêntico
(cinco `Card` emoldurados + resumo repetindo as contagens). O `CabecalhoRaia` nasceu no
shell justamente porque os dois quadros o usam — copiar faria as duas versões divergirem
na primeira correção.

## Parcela 39 — a sidebar do Consultório tinha três itens

E não porque o app do médico seja simples: **as portas estavam no módulo errado.** É a
sétima ocorrência do defeito recorrente do projeto, na variante mais discreta — não é dado
sem leitor nem serviço sem chamador, é capacidade inteira, testada e em produção cuja
única porta está no módulo de quem não a usa.

| Lacuna | Onde estava | Onde passou a estar |
|---|---|---|
| **Prescrições** (receita, atestado, comparecimento, pedido de exame) | Só na Recepção | `ChavePrescricoes` — abre no paciente em foco, quatro tipos como botões |
| **Minha semana** | Só na Recepção (desde a parcela 26) | `ChaveMinhaSemana` — sete colunas, uma consulta só |
| **Meus números** (produtividade + completude de prontuário) | Só no BI do Gerente | `ChaveMeusNumeros` — só quem está logado, sem comparar colegas |
| **Prescrição de infusão** (parcela 42) | Não existia | `ChavePrescricaoInfusao` — folha multi-item assinada com ICP-Brasil |
| **Sala de infusão** (parcela 42) | Não existia | `ChaveSalaInfusao` — a fila do dia e a checagem de enfermagem |

### Prescrições — quem prescreve é quem atende

O fluxo de emissão existia inteiro, e a parcela 36 já tinha subido os componentes para o
**shell** (`DocumentoWindow`) exatamente por isto. Faltava a porta. Enquanto ela não
existiu, ou a receita saía do bloquinho de papel, ou o profissional descia até o balcão
para pedir que alguém emitisse por ele.

A tela **abre no paciente em foco** — no consultório o paciente é contexto, não parâmetro
—, e oferece os quatro tipos como **botões** em vez de um "Novo documento" que pergunta o
tipo: ali a decisão vem antes do clique. A busca continua como atalho, para a segunda via
de quem não está na cadeira. Nada de emissão foi reimplementado.

### Prescrição de infusão e checagem de enfermagem (parcela 42)

A clínica faz **infusão de medicação**, e a folha que ela usa não é a receita: é uma
prescrição de vários itens *"destinada ao próprio consultório — o paciente não vai
apresentar lá fora"*. O que faltava não era o papel; era a **checagem da técnica de
enfermagem**: o ✓ com o horário quando a medicação foi administrada, e a **"rodela"** (o
horário circulado) com justificativa escrita quando não foi.

Checar não é preencher um campo. É afirmar *"foi prescrito assim e foi realizado assim"* e
responder por isso — o mesmo peso da baixa da guia no faturamento, do lado clínico.

**As regras que o serviço cobra**

| Regra | Por quê |
|---|---|
| Só se checa folha **assinada** | Administrar sobre rascunho é o buraco clássico do papel |
| Não realizado **exige justificativa** | Terceira recusa do projeto (com a divergência de caixa e o descarte de problema). Item que não entrou no paciente sem dizer por quê é a pior linha de um prontuário |
| A hora é **informada**, nunca o relógio | A técnica administra às 14h e registra às 14h20. `RegistradoEm` guarda o relógio ao lado, e a diferença entre os dois é o que uma auditoria procura |
| Hora **futura é recusada** | Pré-checagem é o hábito que faz um item aparecer como feito num paciente que saiu antes de recebê-lo |
| Checagem **não se apaga: retifica-se** | Apagar e regravar é exatamente o gesto que uma auditoria de enfermagem procura |
| Item **já checado** não se edita nem se suspende | Senão a folha passa a desdizer uma execução que já tem assinatura |
| Quem checa é **quem fez login** | É o vínculo com a pessoa que dá valor à checagem. Daí o perfil `Enfermagem` |

**O circuito que ela fecha.** O exemplo que a clínica deu ao pedir a feature foi *"o
paciente apresentou reação alérgica e não quis fazer a dipirona"*. Até aqui essa frase
morreria num campo de texto e a próxima receita sairia com dipirona de novo. Agora a não
realização por reação registra a **alergia na lista de problemas**, no mesmo SaveChanges —
e a conferência da parcela 40 passa a acender na próxima prescrição.

### A certificação digital — o que se fez, e o que não se fez

O pedido original era **escanear o carimbo** e colar no PDF. Isso é pior do que não
assinar: uma imagem não prova autoria (qualquer um a copia para outro documento) e, a
partir do dia em que está no banco, existe uma assinatura da médica reutilizável por quem
tiver acesso ao sistema. O pior é que ela **parece** uma garantia.

O que foi feito é **assinatura qualificada ICP-Brasil**: PKCS#7 destacado SHA-256 dentro do
PDF (PdfSharp + `SignedCms`), com **carimbo do tempo RFC 3161 opcional** (configurável em
Configurações → Operação). O que **não** foi feito é LTV/PAdES-LT — embutir CRL/OCSP para o
documento continuar verificável depois de o certificado expirar; anunciá-lo sem
implementá-lo seria a mesma mentira do carimbo escaneado.

**O CPF sai de dentro do certificado.** Um e-CPF carrega o CPF do titular numa extensão
ICP-Brasil (OID `2.16.76.1.3.1`, dentro do *subjectAltName* como `otherName`), e o .NET não
expõe `otherName` em API pronta — o ASN.1 é lido à mão em `CertificadoIcpBrasil`. Isso não
é preciosismo: **é a metade que faz a assinatura valer**. Sem comparar esse CPF com o do
profissional, o sistema provaria só que *alguém* com *algum* token assinou, e o e-CPF da
recepcionista assinaria a prescrição da médica sem um alerta.

**Uma assinatura eletrônica, e é a de quem prescreve** (decisão da clínica, ago/2026 — a
primeira versão exigia duas). Quem confere e assina a execução é a enfermeira, **na via
impressa**, depois que a folha sai da impressora: por isso a Prescrição sai com as colunas
de checagem **em branco** ("Feito às", "Visto") e três linhas para o motivo do não
realizado.

A prescritora assinar um PDF com esses campos vazios é correto, e vale entender por quê:
eles são pré-impressos do formulário, como no talão de papel. Ela atesta o que **mandou**
fazer; a caneta escreve por cima da impressão, não do PDF.

Isso dispensou um segundo e-CPF (um por técnica) para produzir, com muita cerimônia, a
garantia que a assinatura manuscrita já dava — e de quebra resolveu a restrição de o PDF
não se assinar incrementalmente. O **Registro de execução** segue existindo como espelho
eletrônico do que foi checado (prontuário e conferência do fim do dia), é montado **na
hora** porque muda a cada item, e o rodapé dele diz que a autoria está no papel.

**A folha nunca promete mais do que garante.** O rodapé escreve o nível da assinatura, e
quando não há ACT contratada diz que a data é *declarada pelo relógio de quem assinou*. E
a reimpressão devolve os **bytes guardados**, nunca um PDF novo — a assinatura cobre uma
faixa de bytes do arquivo, e um documento "igual" regerado agora abriria como inválido.

### Minha semana

"Meu dia" responde o que acontece hoje; ele não responde o que se pergunta **com o
paciente ainda na frente**: *"quando eu tenho espaço?"*, *"vale marcar o seu retorno para
sexta?"*. Sete dias numa consulta só (o banco é remoto, e um laço custaria catorze idas);
começa na **segunda**, e o domingo é o sétimo dia — tratá-lo como primeiro devolveria a
semana que só começa amanhã. **Dia sem horário aparece vazio**, porque semana com cinco
colunas faria o olho procurar a quarta que sumiu. É tela de **leitura**: quem marca é o
balcão, com o telefone na mão e a agenda de todo mundo à vista.

### Meus números

`ProdutividadeProfissional` existe desde a parcela 5 e `CompletudeProntuario` desde a 36 —
e o único leitor de ambos era o BI do Gerente. O sistema media quem atende, e a pessoa
medida não via o próprio número. **Indicador que só o chefe enxerga não corrige
comportamento nenhum**: ele só produz a conversa desagradável no fim do mês, quando o mês
já passou. A completude é o caso exemplar — ela mede exatamente o que este módulo existe
para resolver, e quem podia agir sobre ela era quem não a enxergava.

A tela mostra **só quem está logado e não compara colegas** (ranking entre profissionais é
decisão de gestão; no app de cada um viraria placar), e a **dívida de prontuário é de
HOJE, não do período** — ela é fila de trabalho, e recortá-la pelo filtro faria escolher
"mês passado" esconder o que está em aberto agora. Métrica sem base aparece como **—**,
nunca zero.

> **A lição para a próxima auditoria:** ao procurar chamador em produção, conte também
> quantos ITENS DE MENU o módulo tem. Sidebar curta demais para o que o app faz é sintoma,
> não simplicidade.

> Como o cliente recebe os cinco apps e o cronograma completo:
> [`entrega-ao-cliente.md`](entrega-ao-cliente.md).
