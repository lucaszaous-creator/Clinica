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

## 2. Inventário: o que já é Blazor e o que ainda é WPF (a suíte inteira)

Esta PR juntou várias frentes (a tela do paciente, os exames e o prontuário, o cadastro, a
moldura e a Recepção). Esta seção é o inventário ÚNICO do resultado; o detalhe de cada frente
fica na sua própria seção (§1, §2.3, §4 e §5).

Toda tela aberta pelo menu ou pela navegação dos cinco apps é Blazor: nenhuma rota de
`IModuloApp.CriarTela` monta tela WPF, e os módulos da Recepção, do Financeiro e do Gerente não
têm mais XAML nenhum. O que resta de WPF são **janelas** abertas por cima das telas Blazor, por
ponte, mais a janela nativa que o Blazor Hybrid exige.

### 2.1 Já convertido

| Frente | Era WPF | Virou Blazor | Detalhe |
|---|---|---|---|
| Tela do paciente (esta PR) | `PacienteWorkspaceView` e as seções e janelas que só ele abria, a `EnfermagemView`, a lista de pacientes da Recepção | `TelaDoPaciente`, a tela Enfermagem, `ListaDePacientesDoPosto` | §1 |
| Exames e prontuário (`claude/wpf-exames`) | `RegistrarResultadoExameWindow`, `ResultadosDoPedidoWindow`, `ResumoProntuarioWindow`, `EscolherPacienteWindow`, `SessaoDoProntuarioWindow`, `VersoesEvolucaoWindow` | `ResultadoExameForm`, `ResultadosDoPedidoForm`, `ResumoProntuarioForm` → `SessaoDoProntuarioForm` → `VersoesEvolucaoForm`, `EscolherPacienteForm`; na enfermagem, `SessaoMedicaForm` | abaixo |
| Cobrança no balcão | `CobrancaDoPacienteWindow` → `RecebimentoWindow` (`ReceberPagamentoViewModel`, `DadosRecebimentoView`) | `CobrancaDoPacienteForm` → `ReceberPagamentoForm` | a regra em `CobrancaDoPacienteNaTela` / `PagamentosNaTela` |
| Cadastro do paciente (`claude/wpf-cadastro`) | `CadastroPacienteWindow`/`ViewModel`, `CapturaFotoWindow` (+ `CameraServico` e os pacotes AForge), `EscolhaDeConvenioWindow`/`ViewModel` (+ `VinculoDeConvenio`) | `CadastroPacienteForm`, `CapturaFotoForm`, `EscolhaDeConvenioForm`, abertos por `FormulariosDoPaciente` | abaixo |
| Moldura (`claude/wpf-moldura`) | `LoginWindow`, `SetupWindow`, `ShellWindow`, `TelaComAbas.xaml`, `TrocaSenhaWindow`, `TreinamentoView` | `Moldura/*.razor`, `Telas/Treinamento.razor` | §2.3 |
| Recepção (`claude/wpf-balcao`) | a ficha do balcão (`FichaBalcaoView` + `FichaPacienteViewModel`), as janelas da agenda (`AgendaViewModel`, `AgendamentoEdicaoViewModel`), o orçamento livre | `FichaDoBalcao`, os formulários da Grade, `OrcamentoLivreForm` | §4 |

As janelas do shell que essas frentes substituíram ficaram sem chamador quando a ficha do balcão e
a agenda WPF saíram, e foram apagadas (§5).

**Exames e prontuário.** Métodos de ponte que saíram junto: `IJanelasDosExames.RegistrarResultadoAsync`/`VerResultadosAsync`
(e `NovoPedidoAsync` passou a receber o paciente), `IJanelasDosProntuarios.AbrirResumoAsync`,
`IJanelasDaFicha.ReceberDividaAsync`, `IJanelasDoAtendimento.ReceberDividaAsync`,
`IJanelasDoAtendimentoClinico.EnviarAoPacienteAsync`; `IJanelasDaSalaDeInfusao.RegistrarInfusaoExternaAsync`
passou a receber o paciente. Diferença de comportamento assumida: o `EscolherPacienteForm` abre
com a busca VAZIA (o `SeletorDePaciente` da casa não despeja o alfabeto), enquanto a janela WPF
abria com os primeiros da carteira; e a cobrança não oferece "Convênio" como forma (o mesmo corte
do recebimento do balcão).

**Cadastro do paciente, foto e convênio "a definir".**
- **`CadastroPacienteForm`** (novo e edição), **`CapturaFotoForm`** (webcam) e
  **`EscolhaDeConvenioForm`**, em `Clinica.Ui.Blazor/Formularios/Paciente/`. As regras
  (ordem das recusas do Salvar, categoria sugerida pelo convênio, convênio e modalidade que a
  ficha já tem, cópia campo a campo, linhas e frases da escolha do convênio) moram em
  `CadastroPacienteNaTela` / `EscolhaDeConvenioNaTela` (Application, com teste). CPF, e-mail,
  endereço, LGPD/origem, foto gravada só no Salvar e a recusa de CPF repetido (no
  `PacienteService`) ficaram idênticos.
- **Quem abre é `FormulariosDoPaciente`** (Hospedagem): o fluxo PEDE e ESPERA a resposta,
  como no `Dialogos` — a escolha do convênio é perguntada no meio de cinco salvares (horário da
  agenda, Lançar do balcão, Finalizar do médico, Concluir da lista do dia, conciliação), e cinco
  donos de "aberto/fechado" seriam cinco cópias da pergunta. Quem desenha é a
  `CamadaDoPaciente`, ao lado da camada de diálogos (Raiz e PalcoDeTelas); trocar de tela
  desiste do formulário aberto (a regra do `Dialogos.DesistirDeTodos`). Sem camada montada a
  pergunta responde "não abriu" na hora, em vez de esperar para sempre.
- **A webcam é a do navegador** (`js/captura-foto.js`: `getUserMedia` + recorte quadrado no
  canvas, foto cheia ≤ 640 px e miniatura ≤ 160 px — as constantes do `Retrato` do WPF). O
  `NavegadorDaJanela` libera a permissão de **câmera** só para a origem do próprio app
  (`0.0.0.0`); o resto segue o padrão do WebView2. "Escolher arquivo…" continua como saída.
- **Pontes removidas:** `IJanelasDaFicha.EditarCadastroAsync`,
  `IJanelasDoAtendimento.CadastrarPacienteAsync` / `EscolherConvenioAsync`,
  `IJanelasDaAgenda.EscolherConvenioAsync`, `IJanelasDoAtendimentoClinico.EscolherConvenioAsync`
  e `IBalcaoDaRecepcao.CadastrarPacienteAsync` (+ `CadastroDePaciente`), com as implementações
  WPF e "Indisponível".
- 🧑‍💻 **UX:** o cadastro e a escolha do convênio abrem dentro da própria tela, sem janela do
  sistema por cima; a foto mostra o recorte exato que vai para o banco e cada falha de câmera
  (bloqueada, ocupada, sem imagem, ausente) tem a sua frase. **Não verificado com câmera real**:
  a webcam não pôde ser testada neste ambiente — conferir no build de teste (permissão
  automática, primeiro quadro, troca de câmera, luz da webcam apagando ao fechar).
- 🛠️ **DX:** uma camada e um serviço para os três formulários; as regras com teste
  (`CadastroPacienteNaTelaTests`) e os formulários clicados de verdade
  (`FormulariosDoPacienteTests`, bUnit sobre SQLite).
- 🤝 **CX:** sem migration; o risco está na câmera (driver e privacidade do Windows) — o
  arquivo continua como saída.

### 2.2 O que ainda é WPF, e por quê

| O quê | Janelas | Por quê | Até quando |
|---|---|---|---|
| **Área do SafeID e da assinatura** (inclui a execução da enfermagem) | `EscolherCertificadoWindow`, `ColetaDeTermo` / `AssinaturaPacienteWindow`, `EscolherTermoWindow`, `EscolherSessaoDoTermoWindow`, `PrescricaoInternaWindow` (Consultório), `FolhaExecucaoWindow`, `EvolucaoEnfermagemWindow` (+ `EscolherSessaoEnfermagemWindow`), `InfusaoExternaWindow` e as views de enfermagem que elas compõem (`PassagemDeEnfermagemView`, `PassagensDeEnfermagemView`, `ProcessoDeEnfermagemView`, `ConsultaDeEnfermagemView`/`Window`, `CatalogoEnfermagemWindow`) | é a área de [`safeid-congelado.md`](safeid-congelado.md): cada tentativa de assinatura é cobrada pelo PSC, foram sete rodadas em produção até funcionar e os testes não enxergam e-CPF real | **em conversão noutras branches**; até lá as telas Blazor só as chamam por ponte |
| **Emissão de documento** | `DocumentoWindow` / `DocumentoFolha` / `DocumentoOpcoesWindow` (com `AcoesDoDocumento`), `BuscaCidWindow` | a mais delicada: conferência de alergia na receita, prévia, CID sob autorização, ~1.200 linhas e vários chamadores nos cinco apps | **em conversão noutra branch** |
| **Envio ao paciente** | `EnvioAoPacienteWindow` | o `EnvioAoPacienteForm` Blazor já serve à central de Documentos e ao "já saiu hoje"; a janela fica pelo fim da emissão na `DocumentoWindow` (`AcoesDoDocumento.EnviarAsync`) | **em conversão noutra branch**, junto da emissão |
| **Abertura do Faturamento** | `AvisoPendenciasWindow`, `RodadaPendenciasWindow` | **por desenho**: travam o app antes de a área de trabalho existir, e um formulário dentro do navegador travaria só a tela dele (`docs/faturamento-blazor.md`) | ficam |
| **A janela do app** | `JanelaDoApp` (só código, sem XAML), cujo conteúdo é **um** `BlazorWebView` | **por arquitetura**: é o mínimo que o Blazor Hybrid exige no Windows (§2.3) | fica |

Junto da janela do app ficam dois caminhos de baixo, descritos na §2.3: a `PromptWindow` atrás do
`IDialogoService` (quando a pergunta vem de dentro de uma janela WPF modal ou de um evento do
próprio Blazor) e as caixas de mensagem nativas da abertura, antes de existir janela. A
`PromptWindow` some quando as janelas desta tabela forem convertidas.

**Por módulo:** Recepção, Financeiro e Gerente não têm XAML; o Consultório só tem a
`PrescricaoInternaWindow` (área do SafeID); o Faturamento só tem as duas janelas da abertura; o
resto está no shell e é a lista acima.

### 2.3 Por arquitetura: a janela nativa, e só ela

A **moldura dos apps** deixou de ser WPF (branch `claude/wpf-moldura`, out/2026). O que sobra de WPF no
caminho principal é o mínimo que o Blazor Hybrid exige no Windows: **uma `Window` por app**
(`Clinica.Desktop.Shell/Shell/JanelaDoApp.cs`, só código, sem XAML) cujo conteúdo inteiro é **um**
`BlazorWebView`. Tudo o que se vê é Razor em `Clinica.Ui.Blazor/Moldura`:

| Era WPF | Virou Razor | Observação |
|---|---|---|
| `LoginWindow` | `Moldura/Entrada.razor` | os três estados de sempre (primeiro acesso → Gerente, usuário e senha com o travamento do `AcessoService`, troca obrigatória da senha provisória); a recusa do Faturamento ("Ver faturamento") aparece **na própria entrada** |
| `SetupWindow` | `Moldura/ConfiguracaoDoBanco.razor` | testar antes de salvar; editar invalida o teste |
| `ShellWindow` (menu, busca, sino, conta, abas) | `Moldura/MolduraDoApp.razor` | Ctrl+F (busca), Ctrl+B (menu), Esc (fecha o que está por cima); a régua de abas do item composto |
| `TelaComAbas` (UserControl) | `Componentes/TelaComAbas.cs` (estado, sem tela) | a régua é desenhada pela moldura |
| `TrocaSenhaWindow` | `Moldura/TrocaDeSenha.razor` (`Modal`) | "Minha senha" no menu da conta |
| `TreinamentoView` + `TreinamentoPlayer` (MediaElement) | `Telas/Treinamento.razor` (`<video>`) | o catálogo e o recorte continuam no shell (`TreinamentoDaSuite`); as pastas de vídeo viram endereços do navegador |
| `PromptWindow` (atrás do `IDialogoService`) | `Dialogos` (HTML) | ver a exceção abaixo |

**Uma janela para a vida inteira do app.** A entrada e depois a moldura trocam DENTRO do mesmo
navegador (`EstadoDaJanela`): criar um segundo navegador é o instante preto que a clínica filmou.
Por isso o login deixou de ser uma janela à parte. A configuração do banco é a única exceção: ela
acontece antes de o host da clínica existir (não há serviços para dar ao navegador), uma vez por
máquina, e usa uma `JanelaDoApp` própria.

**O ShellViewModel continua sendo quem decide** — o menu filtrado por permissão, a navegação com
histórico, as abas, a busca, o sino, a medição das trocas. A `MolduraDaSuite` só o traduz para o
contrato `IMolduraDoApp` (o `Clinica.Ui.Blazor` é `net8.0` e não conhece o shell) e põe no palco
da janela a tela que ele escolheu. O que `IModuloApp.CriarTela` devolve virou um **descritor**
(`ITelaDoNavegador`: o componente Razor); a `TelaBlazor` dos módulos o implementa e nunca entra
numa árvore WPF na janela do app.

**O que ainda é WPF, e por quê:**
- as **janelas** abertas por cima do app pelas pontes (as da §2.2, que outras branches estão
  convertendo) — elas usam a `JanelaDoApp` como dona (é a `MainWindow`);
- as duas janelas da **abertura do Faturamento** (aviso de pendências e rodada bloqueante), de
  propósito: travam o app antes de a área de trabalho existir;
- as **caixas de mensagem nativas** da abertura (o banco não respondeu, tentar de novo?): acontecem
  antes de existir qualquer janela, quando não há navegador onde desenhá-las;
- a **`PromptWindow` como caminho de baixo** do `IDialogoService`: a pergunta feita com a janela do
  app à frente sai em HTML; quando ela vem de DENTRO de uma janela WPF modal (a principal está
  desabilitada, e o diálogo HTML ficaria atrás, sem clique) ou no meio de um evento do próprio
  Blazor (o renderizador só desenharia o diálogo depois de o evento terminar — e o evento estaria
  esperando a resposta), ela cai na caixa nativa. Some quando as janelas da §2.2 forem convertidas.

Não havia "instância única" para preservar: nenhum dos cinco apps impede duas cópias abertas, e
continua assim.

---

## 3. O que falta, e as decisões da direção

O que falta para a suíte não ter WPF além da janela nativa e das duas janelas da abertura do
Faturamento é exatamente a §2.2: a área do SafeID, a emissão de documento, o envio ao paciente e
a busca de CID — todas em conversão noutras branches, uma PR por parte, mesclada só com o CI
inteiro verde (o ritmo da PR 284).

### Decisões que a direção precisa tomar

1. **A passagem de enfermagem da Sala de infusão.** Ela é aberta de dentro da folha de execução, que é da área do SafeID. Para virar Blazor, a folha teria de chamar a tela Blazor, e isso é mexer na área congelada. Sem autorização expressa, ela fica WPF.
2. **A área do SafeID como um todo.** Reescrevê-la exige autorização expressa e testes de assinatura em homologação, porque cada tentativa é cobrada.

---

## 4. Recepção: a ficha do balcão e o que sobrava de WPF (out/2026, branch `claude/wpf-balcao`)

A frente da Recepção. O módulo `Clinica.Modulo.Recepcao` ficou sem nenhum XAML:
só o módulo (itens, rotas e pontes de navegação) e as duas pontes de pré-preenchimento.

### 4.1 A ficha do balcão é Blazor

- **`Telas/Recepcao/FichaDoBalcao.razor`**, chave `ficha-balcao` (item OCULTO da Recepção, `VerFichaPaciente`).
  Abre no MESMO navegador da janela. Antes era a `FichaBalcaoView` numa `ConsultaContextualWindow` por cima, ou seja um segundo navegador e uma segunda moldura.
- **O crachá é o da tela do paciente** (as classes `tp-*`). Mostra CPF, idade, telefone, convênio e carteirinha, "paciente desde" e os alertas de elegibilidade como selos.
- **As ações do crachá:**
  - Prontuário: pelo posto, só para quem tem `VerProntuario`.
  - Editar cadastro: o `CadastroPacienteForm` Blazor, por `FormulariosDoPaciente` (§2.1).
  - Emitir documento: leva à aba Documentos.
  - Marcar: pela ponte `IMarcacaoDeHorario` → aba Marcar.
- **Cinco abas:**
  - **Resumo**: próximos horários, convênio e pacote (autorização vigente, guias em aberto, pacote em uso), cadastro e linha do tempo.
  - **Agenda e sessões**: próximos horários com "Abrir na agenda" e as sessões realizadas.
  - **Convênio, contatos e LGPD**: a `FichaAdministrativa` da tela do paciente, cascateada com o contexto daqui. Inclui convênio, autorizações (`AutorizacaoForm`), relacionamento, privacidade e termos.
  - **Pacotes e pagamentos**: os pacotes e o "Receber…" pelo `CobrancaDoPacienteForm` Blazor (§2.1).
  - **Documentos**: a central de Documentos embutida com o paciente fixo.
- **A privacidade continua SEM "Registrar autorização"** (a decisão da §1).
- **As portas.** Agenda do dia, Pacientes e Início abrem a ficha por `PedidoDaFichaDoBalcao.AbrirAsync`. Onde a ficha do balcão não existe, cai na ficha do host.
  O host (`TelaBlazor.AbrirFichaDoPacienteAsync`) passou a cair na ficha do balcão quando a tela do paciente não é alcançável (sem `VerProntuario`). Assim o clique no nome do paciente no Início, no Acompanhamento e nas Glosas deixa de responder "não disponível" para a recepcionista.
- **A trilha de leitura** (`AcessoProntuarioService`, origem `FichaPaciente`) é registrada na troca de paciente, como no WPF.
- **O que a tela afirma mora na Application**: `FichaDoBalcao.Cabecalho`, `PacoteDe`, `Autorizacao`, `Comunicacao`, `GuiasEmAberto` e `DocumentoNaLinha`, com teste.
  ⚠️ **Guias em aberto mudou de propósito.** A ficha WPF contava todo código sem baixa, inclusive o `NaoAplicavel` do particular e o do atendimento estornado, que nunca serão baixados.

### 4.2 As janelas da agenda saíram, e o `AgendaViewModel` também

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

### 4.3 O orçamento livre é Blazor

`OrcamentoWindow`/`OrcamentoViewModel` viraram **`Formularios/Recepcao/OrcamentoLivreForm.razor`**. A regra (totais, recusas, montagem dos itens) está em `OrcamentoLivreNaTela`, com teste.
- `IDocumentosDaRecepcao.EmitirOrcamentoAsync` saiu.
- `DocumentosDaRecepcaoWpf` também saiu: a Recepção registra a mesma `DocumentosNoAplicativoWpf` dos outros apps.
- A barreira é a união das portas (`EditarFinanceiro` OU `VenderPacote`).
- Consequência: o orçamento passa a existir na central de Documentos dos três apps, para quem tem o bit. Antes o Consultório dizia "é emitido pela Recepção".

### 4.4 Código morto apagado (conferido sem chamador em produção)

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

⚠️ **O que NÃO foi apagado aqui:** as janelas do shell que a ficha do balcão e a agenda WPF ainda abriam (cadastro, foto, convênio, cobrança, sessão e versões) — saíram logo depois, na §5 — e `BuscaCidWindow`/`BuscaCidViewModel`, que continuam com chamador, a `DocumentoEdicaoViewModel` (a emissão de documento, de outra frente). Saem quando a `DocumentoWindow` virar Blazor.

### 4.5 UX · DX · CX desta etapa

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

## 5. Limpeza final: as janelas do shell que ficaram sem chamador (out/2026)

Com a ficha do balcão (`FichaPacienteViewModel`/`FichaBalcaoView`) e a agenda WPF
(`AgendaViewModel`, `AgendamentoEdicaoViewModel`) apagadas na §4, as janelas do shell que só elas
ainda abriam ficaram sem nenhum chamador em produção — conferido por busca em `src`, `tools` e
`tests` (C#, XAML e registros de DI). Saíram:

- `Componentes/Cadastro/CadastroPacienteWindow` + `CadastroPacienteViewModel`,
  `Controls/CapturaFotoWindow` e `Componentes/CameraServico.cs` — e com eles os pacotes
  **AForge** dos dois `.csproj` (shell e faturamento), que nenhum outro código usava;
- `EscolhaDeConvenioWindow` + `EscolhaDeConvenioViewModel` e `VinculoDeConvenio`;
- `CobrancaDoPacienteWindow` + `CobrancaDoPacienteViewModel`, `RecebimentoWindow` +
  `ReceberPagamentoViewModel`, `IRecebimento` e `DadosRecebimentoView` (que só a janela de
  recebimento compunha);
- `SessaoDoProntuarioWindow` + `SessaoDoProntuarioViewModel` e `VersoesEvolucaoWindow` +
  `VersoesEvolucaoViewModel`.

**Ferramentas acertadas:**
- `tools/Clinica.Verificacao.Desktop`: saiu `RevisaoPr.cs` (só conferia o comportamento da
  `RecebimentoWindow` — cancelar bloqueado durante a gravação —, que é leiaute de janela). A
  conferência do cadastro deixou de montar a janela WPF e passou a exercitar as **regras** pelo
  mesmo caminho do `CadastroPacienteForm`: a conferência do Salvar (`CadastroPacienteNaTela`: CPF
  obrigatório, edição sem endereço) e a gravação pelo `PacienteService` (a carga lê só a ficha,
  edição sem endereço grava nulo, cadastro novo grava e devolve a identidade, reabrir recupera,
  editar preserva os campos não alterados). O que era só da janela (maximizar, rolagem, botões
  dentro da área útil, travas durante a carga e a gravação) saiu: é o leiaute que a prévia
  fotografa e o `TelasBlazorAbremTests` clica.
- `tools/ValidarLayoutWindows`: saiu a foto da `RecebimentoWindow`; a regra do recebimento por
  cartão continua conferida pelo `PagamentosRecepcaoService`.
- `tools/compilar-sombra.py`: o comentário dos avisos deixou de citar o AForge.

Os comentários do código que descreviam essas janelas como vivas (`CadastroPacienteNaTela`,
`FormulariosDoPaciente`, `OpcoesDeConvenio`, `FichaAdministrativaNaTela`, `FichaDoBalcao`,
`IdadeDoPaciente`, `AgendamentoNaTela`, `PagamentosNaTela` e as telas Blazor que citavam a
cobrança e a sessão WPF) passaram a dizer que elas saíram.

- 🧑‍💻 **UX:** nada muda para quem usa — as portas já abriam os formulários Blazor.
- 🛠️ **DX:** ~25 arquivos WPF a menos no shell e uma dependência .NET Framework (AForge) a menos.
- 🤝 **CX:** sem migration; o pacote publicado fica menor sem as DLLs do AForge.

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
  - O que falta da migração é a §2.2, em conversão noutras branches.

### A moldura em Blazor (§2.3), nos três eixos

- 🧑‍💻 **UX:**
  - A entrada aparece na janela do app (maximizada), já com a marca; depois dela a moldura troca no mesmo navegador, sem janela nova nem instante preto.
  - O cabeçalho, o menu, a busca (Ctrl+F), o sino e a conta têm a mesma cara das telas; os popovers fecham com Esc e com o clique fora.
  - O que muda para quem usa: o login deixou de ser uma janela pequena no meio da tela; o snackbar das janelas WPF agora sai nas notificações do canto da tela; o vídeo do Treinamento é o player do navegador (tela cheia de fábrica).
  - Conferido rodando: um harness abriu a `JanelaDoApp` com WebView2 de verdade sobre SQLite, leu a entrada desenhada, entrou por JavaScript e leu a moldura com o menu, as abas e o "Meu dia".
- 🛠️ **DX:**
  - Removidos: `LoginWindow`, `SetupWindow`, `ShellWindow` (~600 linhas de XAML + code-behind), `TrocaSenhaWindow`, `TreinamentoView` + `TreinamentoPlayer`, `TelaComAbas.xaml`.
  - Novos: `JanelaDoApp`, `MolduraDaSuite`, `NavegadorEmbutido` (as regras do WebView2 num lugar só, usadas também pela `NavegadorDaJanela`), `TreinamentoDaSuite`, e o contrato `Clinica.Ui.Blazor/Moldura/Contratos.cs`.
  - A regra continua no `ShellViewModel`; a moldura só desenha. `MolduraBlazorTests` (bUnit) cobre a entrada sobre o `AcessoService` de verdade (senha errada, troca obrigatória, primeiro acesso, recusa do app), a configuração do banco e a moldura com um hospedeiro de mentira.
  - As ferramentas do Windows (`ValidarLayoutWindows`, `Clinica.Verificacao.Desktop`, `VerificarDocumentos`, a captura do treinamento) abrem a `JanelaDoApp` no lugar da `ShellWindow`. O corte de leiaute do cabeçalho deixou de ser medido por elas (é HTML); ele foi conferido no harness, em 1280 px, e **ainda não entrou na prévia** (`tools/PreviaBlazor`) — é o próximo passo para ele ser fotografado em 1024 e 1366 como as telas.
- 🤝 **CX:**
  - Sem migration e sem mudança de regra: login, travamento, troca de senha, permissões do menu, abertura por exe e "Trocar usuário" (que reabre o app) são os mesmos.
  - Risco: a primeira abertura depois da atualização desenha a entrada no WebView2 — máquina sem o runtime do WebView2 já não abria as telas Blazor, e agora não abriria nem a entrada (o mesmo pré-requisito, mais cedo). Antes da release, abrir os cinco apps no build de teste e entrar com um usuário de cada perfil.

