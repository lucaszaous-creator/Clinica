# Tela do paciente em Blazor, e o que ainda é WPF (out/2026, PR 285)

Continuação da [PR 284](https://github.com/lucaszaous-creator/Clinica/pull/284), cujo documento
(`docs/migracao-wpf-blazor-out2026.md`, §2) listava o que continuava WPF. Esta PR resolve a maior
parte da lista: a **tela do paciente**, a **Enfermagem**, as **seções de Prescrições e de
Prescrição de infusão** e as **listas de pacientes** que ainda abriam a tela WPF. Este documento
é o mapa do que entrou, do que sobrou e de quanto custa terminar.

---

## 1. O que a PR 285 entregou

**A tela do paciente** (`Clinica.Ui.Blazor/Telas/Consultorio/TelaDoPaciente.razor`).
- **No alto:** o crachá (foto ou iniciais, convênio, alergias, problemas) e a faixa da sessão (iniciar, reabrir, finalizar, cronômetro).
- **Ao lado:** um rail com sete seções.

| Seção | Conteúdo |
|---|---|
| Atendimento (médico) | Folha S-O-A-P com negrito/itálico, mapa corporal em SVG, modelos, busca de CID, alertas, emissão de papéis, "Salvar e finalizar" |
| Atendimento de enfermagem | Passagem, consulta COFEN em 5 etapas, catálogo de cuidados, plano do dia, linha do tempo |
| Ficha do paciente | Dados e alertas, problemas, anamnese, sessões e guias, e a aba administrativa: agenda e pendências, convênio e autorizações, relacionamento, privacidade/LGPD e termos do dia |
| Histórico | Sessões anteriores com leitura completa, versões, anexos e a ficha do atendimento |
| Exames e anexos | Pedidos, resultados e arquivos |
| Prescrições e documentos | Documentos e prescrição de infusão do paciente, sem sair da tela |
| Acompanhamento | Evolução da dor, medidas, avaliações |

O rail mostra a cada perfil o que é dele (`TelaDoPacienteNaTela.RailPara`):
- a enfermagem não vê o S-O-A-P;
- o médico não vê a seção de enfermagem;
- sem `VerProntuario`, sobra só a Ficha.

O rascunho do médico e a passagem da enfermagem são guardados pela TELA (`ITelaDoPaciente.Guardado`), então sobrevivem à troca de seção. O "Finalizar" pergunta antes de encerrar sem eles (`IFolhaAberta`, `IPassagemAberta`).

**As outras telas:**
- A tela **Enfermagem** é Blazor nos dois módulos.
- **Pacientes** (Consultório) e **Prontuário › Por paciente** (Recepção) usam uma lista só, `ListaDePacientesDoPosto`.
- O botão "Prontuário" da ficha do balcão leva ao Histórico da tela Blazor (`ChavesSuite.ProntuarioDoPaciente`).

**O que saiu:**
- `PacienteWorkspaceView` e todas as seções e janelas WPF que só ele abria;
- a lista de pacientes WPF da Recepção e a `EnfermagemView` do shell;
- `IFabricaListaPacientes`, `IFabricaFichaPaciente` e `IFichaAdministrativaPaciente`;
- as ferramentas e os passos do CI que só exercitavam essas telas.

**Decisão deliberada:** a privacidade não tem o "Registrar autorização" da ficha WPF. O consentimento LGPD entra só pelo termo assinado (parcela 89; o `ConsentimentoService` avisa que tela pendurada no registro direto reabre o caminho sem assinatura).

---

## 2. O que ainda é WPF no Consultório

Toda tela aberta pelo menu ou pela navegação do Consultório é Blazor: nenhuma rota do `ModuloClinico.CriarTela` monta mais tela WPF. O que resta são **janelas** abertas por cima das telas Blazor, por ponte.

### 2.1 Congeladas: ficam WPF por decisão da direção

| Janela | Para quê |
|---|---|
| `PrescricaoInternaWindow` | Prescrever a infusão, com a assinatura do médico |
| `FolhaExecucaoWindow` | Folha de execução da enfermagem, com checagem e 2ª assinatura |
| `EscolherCertificadoWindow` | Escolher o e-CPF |
| `ColetaDeTermo` / `AssinaturaPacienteWindow` | Assinatura do paciente (termo do procedimento e LGPD) |

Elas são a área do SafeID e da assinatura digital ([`safeid-congelado.md`](safeid-congelado.md)):
- cada tentativa de assinatura é cobrada pelo PSC;
- foram sete rodadas em produção até funcionar;
- os testes não enxergam e-CPF real.

As telas Blazor só as chamam.

### 2.2 Podem virar Blazor

| Janela | Tamanho (XAML + VM) | Onde é usada | Esforço |
|---|---|---|---|
| `ResultadosDoPedidoWindow` + `RegistrarResultadoExameWindow` | ~300 + ~350 linhas | Exames (Consultório) | meio dia, as duas |
| `ResumoProntuarioWindow` | ~500 linhas | Prontuários (Consultório) | meio dia |
| `InfusaoExternaWindow` | ~180 linhas | Sala de infusão, prescrição | meio dia, com as duas abaixo |
| `EscolhaDeConvenioWindow` (`VinculoDeConvenio`) | ~310 linhas | Finalizar, fila, Novo atendimento | |
| `CobrancaDoPacienteWindow` | ~350 linhas | Ficha, Novo atendimento | |
| `CadastroPacienteWindow` | ~530 linhas | Ficha, Pacientes, Agenda | meio dia |
| `DocumentoWindow` (emitir receita, atestado, laudo) | ~1.200 linhas, 9 chamadores | os cinco apps | **1 dia**: a mais delicada (conferência de alergia na receita, prévia, CID sob autorização) |
| `EvolucaoEnfermagemWindow` | já há o equivalente Blazor (`FolhaDaPassagem`) | a folha de execução | **depende de decisão** (§4) |

As cinco últimas são do **shell** e servem também à Recepção e ao Gerente: convertê-las resolve os três módulos de uma vez.

### 2.3 Por arquitetura

A **moldura dos apps** (janela principal, menu, login) é WPF em todos os módulos: o Blazor Hybrid roda dentro dela (um navegador por janela, `NavegadorDaJanela` + `Palco`).

---

## 3. O que ainda é WPF nos outros módulos

| Módulo | O que resta |
|---|---|
| Gerente, Financeiro | Nada |
| Faturamento | As duas janelas da abertura (aviso de pendências e rodada bloqueante), WPF de propósito: travam o app antes de a área de trabalho existir |
| Recepção | **Nada** (ver §5): a ficha do balcão virou a tela Blazor `FichaDoBalcao`, as janelas da agenda e o orçamento saíram, e o código morto foi apagado. Sobram só as pontes de navegação e as janelas do shell que outras frentes convertem (cadastro, cobrança, escolha de convênio) |
| Shell | 39 componentes WPF; parte é da área congelada, parte serve às janelas da §2.2 e sai com elas |

---

## 4. Plano e estimativa

A estimativa se baseia no ritmo desta PR: as sete seções, as listas e a aba administrativa
saíram em cerca de um dia de trabalho, com três frentes em paralelo.

| Etapa | Escopo | Desenvolvimento | Até a release |
|---|---|---|---|
| **PR A: Consultório completo** | as janelas da §2.2 | 2 a 3 dias | ~1 semana, com o CI e a conferência no build de teste |
| **PR B: Recepção** | ficha do balcão + limpeza dos XAML mortos | ~3 a 5 dias | ~1,5 a 2 semanas para a suíte inteira, somando as duas PRs |
| 100% literal | moldura + área congelada | não é possível sem mudar as duas decisões da §2.1 e §2.3 | — |

Mesmo ritmo da PR 284: **uma PR por parte, mesclada só com o CI inteiro verde**.

### Decisões que a direção precisa tomar

1. **A passagem de enfermagem da Sala de infusão.** Ela é aberta de dentro da folha de execução, que está congelada. Para virar Blazor, a folha congelada teria de chamar a tela Blazor, e isso é mexer na área congelada. Sem autorização expressa, ela fica WPF.
2. **A área congelada como um todo.** Reescrevê-la exige autorização expressa e testes de assinatura em homologação, porque cada tentativa é cobrada.

---

## 5. Recepção: a ficha do balcão e o que sobrava de WPF (out/2026, branch `claude/wpf-balcao`)

A "PR B" da §4 para a Recepção. O módulo `Clinica.Modulo.Recepcao` ficou sem nenhum XAML:
só o módulo (itens, rotas e pontes de navegação) e as duas pontes de pré-preenchimento.

### 5.1 A ficha do balcão é Blazor

- **`Telas/Recepcao/FichaDoBalcao.razor`**, chave `ficha-balcao` (item OCULTO da Recepção, `VerFichaPaciente`).
  Abre no MESMO navegador da janela. Antes era a `FichaBalcaoView` numa `ConsultaContextualWindow` por cima, ou seja um segundo navegador e uma segunda moldura.
- **O crachá é o da tela do paciente** (as classes `tp-*`). Mostra CPF, idade, telefone, convênio e carteirinha, "paciente desde" e os alertas de elegibilidade como selos.
- **As ações do crachá:**
  - Prontuário: pelo posto, só para quem tem `VerProntuario`.
  - Editar cadastro: pela ponte `IJanelasDaFicha`, sem mudança.
  - Emitir documento: leva à aba Documentos.
  - Marcar: pela ponte `IMarcacaoDeHorario` → aba Marcar.
- **Cinco abas:**
  - **Resumo**: próximos horários, convênio e pacote (autorização vigente, guias em aberto, pacote em uso), cadastro e linha do tempo.
  - **Agenda e sessões**: próximos horários com "Abrir na agenda" e as sessões realizadas.
  - **Convênio, contatos e LGPD**: a `FichaAdministrativa` da tela do paciente, cascateada com o contexto daqui. Inclui convênio, autorizações (`AutorizacaoForm`), relacionamento, privacidade e termos.
  - **Pacotes e pagamentos**: os pacotes e o "Receber…" pela ponte `IJanelasDaFicha`.
  - **Documentos**: a central de Documentos embutida com o paciente fixo.
- **A privacidade continua SEM "Registrar autorização"** (a decisão da §1).
- **As portas.** Agenda do dia, Pacientes e Início abrem a ficha por `PedidoDaFichaDoBalcao.AbrirAsync`. Onde a ficha do balcão não existe, cai na ficha do host.
  O host (`TelaBlazor.AbrirFichaDoPacienteAsync`) passou a cair na ficha do balcão quando a tela do paciente não é alcançável (sem `VerProntuario`). Assim o clique no nome do paciente no Início, no Acompanhamento e nas Glosas deixa de responder "não disponível" para a recepcionista.
- **A trilha de leitura** (`AcessoProntuarioService`, origem `FichaPaciente`) é registrada na troca de paciente, como no WPF.
- **O que a tela afirma mora na Application**: `FichaDoBalcao.Cabecalho`, `PacoteDe`, `Autorizacao`, `Comunicacao`, `GuiasEmAberto` e `DocumentoNaLinha`, com teste.
  ⚠️ **Guias em aberto mudou de propósito.** A ficha WPF contava todo código sem baixa, inclusive o `NaoAplicavel` do particular e o do atendimento estornado, que nunca serão baixados.

### 5.2 As janelas da agenda saíram, e o `AgendaViewModel` também

A Visão geral e o "horário do paciente no local" do Início ainda pediam ao WPF por `IAgendaDaVisaoGeral.AbrirHorarioAsync`/`ExecutarAsync` e `IBalcaoDaRecepcao.AbrirHorarioAsync`. Isso montava o `AgendaViewModel` (~2.050 linhas) sem desenho, só para abrir janelas WPF.

Agora as duas telas abrem os MESMOS formulários Blazor da Grade:
- `HorarioDaAgendaForm`;
- `BloqueioForm`;
- `ListaEsperaPainelForm`;
- `ConciliacaoAgendaForm`;
- a folha do dia pelo `AgendaPdfService`.

As pontes encolheram para o que é navegação. Saíram:
- `DetalheHorarioWindow`, `AgendamentoWindow`, `BloqueioWindow`, `ListaEsperaPainelWindow`, `ListaEsperaWindow` e `ConciliacaoAgendaWindow`;
- os ViewModels delas e `AgendaPlanejamento`/`FiltroListaAgenda`;
- `AcaoDaAgenda`, `RespostaDaAgenda` e `ResultadoDoBalcao`.

### 5.3 O orçamento livre é Blazor

`OrcamentoWindow`/`OrcamentoViewModel` viraram **`Formularios/Recepcao/OrcamentoLivreForm.razor`**. A regra (totais, recusas, montagem dos itens) está em `OrcamentoLivreNaTela`, com teste.
- `IDocumentosDaRecepcao.EmitirOrcamentoAsync` saiu.
- `DocumentosDaRecepcaoWpf` também saiu: a Recepção registra a mesma `DocumentosNoAplicativoWpf` dos outros apps.
- A barreira é a união das portas (`EditarFinanceiro` OU `VenderPacote`).
- Consequência: o orçamento passa a existir na central de Documentos dos três apps, para quem tem o bit. Antes o Consultório dizia "é emitido pela Recepção".

### 5.4 Código morto apagado (conferido sem chamador em produção)

**Recepção:**
- `EstornoAtendimentoWindow`, `ProximasVagasWindow`, `ProntuarioView`/`ProntuarioViewModel` (e o registro de DI);
- `AutorizacaoWindow`/`AutorizacaoEdicaoViewModel`;
- `ConvenioPacienteView`, `TermosPacienteView`, `RelacionamentoPacienteView` e `PrivacidadePacienteView`.

**Shell:**
- `EscreverSessaoWindow`/ViewModel e `FolhaDaSessaoView`/ViewModel;
- `MapaCorporalWindow`/`MapaCorporalControl`/`MapaCorporalViewModel`;
- `ModelosEvolucaoWindow`/ViewModel e `DetalheSessaoWindow`;
- `MateriaisProcedimentoWindow`/ViewModel (com `ConferenciaMateriaisProcedimento`);
- `DadosClinicaView`/ViewModel, `RegrasFaturamentoView`/ViewModel e `BuscaDePacienteView`;
- `ConsultaContextualWindow`.

**Shell, órfãos depois dessas remoções:** `DialogosDaSessao`, `EscolhaDeDocumentoWindow`, `IconeClinico`, `LinhaDoTempoClinicaViewModel` e `RenovacaoConsultaFluxo`.

**Ferramentas acertadas:**
- `tools/ValidarLayoutWindows`: sem a janela de materiais; a regra da quantidade fracionada continua.
- `tools/Clinica.Verificacao.Desktop`: a conferência "salvar dados da clínica preserva os TUSS" passou a usar o método da tela Blazor.
- `tools/verificar-editor-clinico`: sem a folha e o mapa WPF.
- `tools/validar-rolagem-suite`: sem a lista de autorizações.

⚠️ **O que NÃO foi apagado:** `BuscaCidWindow`/`BuscaCidViewModel`. Continuam com chamador, a `DocumentoEdicaoViewModel` (a emissão de documento, de outra frente). Saem quando a `DocumentoWindow` virar Blazor.

### 5.5 UX · DX · CX desta etapa

- 🧑‍💻 **UX:**
  - A ficha do balcão abre no mesmo navegador, sem janela por cima e sem flash preto, e com o mesmo crachá da tela do paciente.
  - A recepcionista passa a abrir a ficha também pelo Início e pelo Acompanhamento.
  - A Visão geral e o Início abrem o horário, o bloqueio, a lista de espera e a conciliação nos mesmos formulários da Grade.
  - O que falta é ver rodando: a ficha não foi fotografada na prévia nem aberta no app.
- 🛠️ **DX:**
  - Cerca de 15 mil linhas a menos (a maior parte WPF) (o `FichaPacienteViewModel` e o `AgendaViewModel` sozinhos somavam ~4.300).
  - As regras da ficha e do orçamento passaram para a Application, com teste.
  - As pontes `IAgendaDaVisaoGeral`, `IBalcaoDaRecepcao`, `IJanelasDaAgenda` e `IDocumentosDaRecepcao` encolheram.
  - `FichaDoBalcaoBlazorTests` abre a ficha num paciente e clica as abas e as ações. A tela entra sozinha no `TelasBlazorAbremTests` (sem pedido, o estado vazio).
- 🤝 **CX:**
  - Sem migration.
  - O risco está no fluxo da ficha nova (abas, voltar, receber, editar cadastro) e no orçamento, que agora existe nos três apps para quem tem o bit.
  - Antes da release, abrir a ficha pela agenda do dia, por Pacientes e pelo Início com o perfil Recepção, e emitir um orçamento.

---

## UX · DX · CX

- 🧑‍💻 **UX:**
  - A tela do paciente abre no mesmo navegador da janela, sem uma janela WPF por cima.
  - Todas as seções seguem o visual das demais telas Blazor.
  - Médico e enfermagem trabalham na mesma tela, cada um na sua seção, e o rascunho não se perde ao trocar de seção.
  - O que falta conferir é o visual rodando: nenhuma seção foi aberta no app nem fotografada na prévia.
- 🛠️ **DX:**
  - Cerca de 18,6 mil linhas removidas e 14 mil acrescentadas.
  - As regras das telas foram para a Application, com teste.
  - Uma lista e uma tela atendem as duas portas, onde antes havia cópias.
  - A checagem 38 passa a exigir um `case` na tela Blazor para cada seção declarada.
  - O teste instável do formulário de agendamento foi consertado: o bUnit descartava calado o clique num id de evento que já tinha morrido.
- 🤝 **CX:**
  - Sem migration: nada muda no banco.
  - O risco está no fluxo e no visual das seções novas. Antes da release, abrir a tela do paciente no build de teste com os três perfis (médico, enfermagem, balcão) e passar por cada seção.
  - Terminar a migração custa cerca de uma semana para o Consultório e mais uma para a Recepção.
