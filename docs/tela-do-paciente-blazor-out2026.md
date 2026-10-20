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
| `InfusaoExternaWindow` | ~180 linhas | Sala de infusão, prescrição | meio dia, com as duas abaixo |
| ~~`EscolhaDeConvenioWindow` (`VinculoDeConvenio`)~~ | ~310 linhas | Finalizar, fila, Novo atendimento | **convertida** (§2.2.2) |
| ~~`CadastroPacienteWindow`~~ (+ `CapturaFotoWindow`) | ~530 linhas | Ficha, Pacientes, Agenda | **convertida** (§2.2.2) |
| `DocumentoWindow` (emitir receita, atestado, laudo) | ~1.200 linhas, 9 chamadores | os cinco apps | **1 dia**: a mais delicada (conferência de alergia na receita, prévia, CID sob autorização) |
| `EvolucaoEnfermagemWindow` | já há o equivalente Blazor (`FolhaDaPassagem`) | a folha de execução | **depende de decisão** (§4) |

As cinco últimas são do **shell** e servem também à Recepção e ao Gerente: convertê-las resolve os três módulos de uma vez.

### 2.2.1 Convertidas depois da PR 285 (branch `claude/wpf-exames`)

| Janela WPF | Virou | Onde abre | Saiu do código? |
|---|---|---|---|
| `RegistrarResultadoExameWindow` + VM | `ResultadoExameForm` (já existia, da seção Exames e anexos) | Exames → "Anexar laudo" | **Sim** |
| `ResultadosDoPedidoWindow` + VM | `ResultadosDoPedidoForm` | Exames → "Ver resultados" | **Sim** |
| `ResumoProntuarioWindow` + VM | `ResumoProntuarioForm` (abre o `SessaoDoProntuarioForm`, que abre o `VersoesEvolucaoForm`) | Prontuários → "Abrir" | **Sim** |
| `EscolherPacienteWindow` | `EscolherPacienteForm` (o `SeletorDePaciente` num `Modal`) | Exames → "Novo pedido"; Sala de infusão → infusão externa | **Sim** |
| `CobrancaDoPacienteWindow` → `RecebimentoWindow` | `CobrancaDoPacienteForm` → `ReceberPagamentoForm` | Ficha (aba administrativa) e Lançar/Marcar | **Não**: a ficha do balcão WPF (`FichaPacienteViewModel`) ainda as abre |
| `EnvioAoPacienteWindow` | `EnvioAoPacienteForm` (+ ponte `IEntregaAoPaciente` para o WhatsApp deste computador) | Documentos → "Enviar ao paciente…"; "já saiu hoje" do atendimento | **Não**: a ficha do balcão e o fim da emissão na `DocumentoWindow` ainda passam por `AcoesDoDocumento.EnviarAsync` |
| `SessaoDoProntuarioWindow`, `VersoesEvolucaoWindow` | (o resumo Blazor já usa os formulários) | — | **Não**: a ficha e o Prontuário da Recepção WPF ainda as abrem |

Métodos de ponte que saíram junto: `IJanelasDosExames.RegistrarResultadoAsync`/`VerResultadosAsync`
(e `NovoPedidoAsync` passou a receber o paciente), `IJanelasDosProntuarios.AbrirResumoAsync`,
`IJanelasDaFicha.ReceberDividaAsync`, `IJanelasDoAtendimento.ReceberDividaAsync`,
`IJanelasDoAtendimentoClinico.EnviarAoPacienteAsync`; `IJanelasDaSalaDeInfusao.RegistrarInfusaoExternaAsync`
passou a receber o paciente. As janelas que ficaram somem quando a ficha do balcão e a
`DocumentoWindow` virarem Blazor — aí os seus últimos chamadores saem. Diferença de comportamento
assumida: o `EscolherPacienteForm` abre com a busca VAZIA (o `SeletorDePaciente` da casa não
despeja o alfabeto), enquanto a janela WPF abria com os primeiros da carteira; e a cobrança não
oferece "Convênio" como forma (o mesmo corte do recebimento do balcão).
### 2.2.2 Cadastro do paciente, foto e convênio "a definir" (convertidos, out/2026)

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
- **O que continua WPF, e por quê:** `CadastroPacienteWindow`/`ViewModel` + `CapturaFotoWindow`
  (AForge) ficam só porque a **ficha WPF do balcão** (`FichaPacienteViewModel`, aberta por
  `JanelasDaAgendaWpf.AbrirFichaAsync`) ainda edita o cadastro por ela; `EscolhaDeConvenioWindow`
  + `VinculoDeConvenio.Garantir` ficam só pelo **formulário WPF do horário**
  (`AgendamentoEdicaoViewModel`, da agenda WPF da Recepção). WPF não abre formulário do
  navegador; saindo esses dois chamadores, as três janelas, o `VinculoDeConvenio`, o
  `CameraServico` e os pacotes AForge saem junto. O `CadastroPacienteViewModel` passou a usar
  o mesmo `CadastroPacienteNaTela` para conferir e copiar, para as duas portas não divergirem
  enquanto convivem.
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

### 2.3 Por arquitetura
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
- as **janelas** abertas por cima do app pelas pontes (a área congelada da §2.1 e as da §2.2 que as
  outras frentes ainda estão convertendo) — elas usam a `JanelaDoApp` como dona (é a `MainWindow`);
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

## 3. O que ainda é WPF nos outros módulos

| Módulo | O que resta |
|---|---|
| Gerente, Financeiro | Nada |
| Faturamento | As duas janelas da abertura (aviso de pendências e rodada bloqueante), WPF de propósito: travam o app antes de a área de trabalho existir |
| Recepção | 16 arquivos XAML. Vários (agendamento, bloqueio, lista de espera, próximas vagas, autorização) já têm o formulário Blazor equivalente e podem ser só código morto, a conferir um a um. A tela de verdade é a **ficha do balcão** (`FichaBalcaoView`) com as quatro abas, que pode reaproveitar a aba administrativa Blazor desta PR |
| Shell | A moldura (janela, menu, login, configuração do banco, troca de senha, treinamento) é Blazor (§2.3). Restam os componentes WPF das janelas: parte é da área congelada, parte serve às janelas da §2.2 e sai com elas |

---

## 4. Plano e estimativa

A estimativa se baseia no ritmo desta PR: as sete seções, as listas e a aba administrativa
saíram em cerca de um dia de trabalho, com três frentes em paralelo.

| Etapa | Escopo | Desenvolvimento | Até a release |
|---|---|---|---|
| **PR A: Consultório completo** | as janelas da §2.2 | 2 a 3 dias | ~1 semana, com o CI e a conferência no build de teste |
| **PR B: Recepção** | ficha do balcão + limpeza dos XAML mortos | ~3 a 5 dias | ~1,5 a 2 semanas para a suíte inteira, somando as duas PRs |
| 100% literal | área congelada (a moldura já é Blazor, §2.3) | não é possível sem mudar a decisão da §2.1; a janela nativa da §2.3 é o que o Blazor Hybrid exige no Windows | — |

Mesmo ritmo da PR 284: **uma PR por parte, mesclada só com o CI inteiro verde**.

### Decisões que a direção precisa tomar

1. **A passagem de enfermagem da Sala de infusão.** Ela é aberta de dentro da folha de execução, que está congelada. Para virar Blazor, a folha congelada teria de chamar a tela Blazor, e isso é mexer na área congelada. Sem autorização expressa, ela fica WPF.
2. **A área congelada como um todo.** Reescrevê-la exige autorização expressa e testes de assinatura em homologação, porque cada tentativa é cobrada.

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

