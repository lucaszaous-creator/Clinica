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
| ~~`DocumentoWindow` (emitir receita, atestado, laudo)~~ | — | — | **Feito** (out/2026): virou o `DocumentoForm` Blazor — ver §2.4 |
| `EvolucaoEnfermagemWindow` | já há o equivalente Blazor (`FolhaDaPassagem`) | a folha de execução | **depende de decisão** (§4) |

As cinco últimas são do **shell** e servem também à Recepção e ao Gerente: convertê-las resolve os três módulos de uma vez.

### 2.3 Por arquitetura

A **moldura dos apps** (janela principal, menu, login) é WPF em todos os módulos: o Blazor Hybrid roda dentro dela (um navegador por janela, `NavegadorDaJanela` + `Palco`).

### 2.4 Emitir documento: virou Blazor (out/2026)

A janela de emissão de papel ESCRITO (receita, atestado, laudo, declaração de comparecimento,
pedido de exame) era a peça mais delicada da lista. Ela virou o **`DocumentoForm`**
(`Clinica.Ui.Blazor/Formularios/Documentos/`), aberto DENTRO da tela que o pede.

| Porta | Antes | Agora |
|---|---|---|
| Central de Documentos (todos os apps) | `IDocumentosDaRecepcao.EmitirEscritoAsync` → janela | o formulário na própria tela |
| "Alterar (nova versão)" do laudo | `AcoesDoDocumento.NovaVersaoAsync` → janela | o formulário com o laudo anterior |
| Atendimento médico (Entregar agora) | `IJanelasDoAtendimentoClinico.EmitirAsync` → janela | o formulário com a herança da sessão |
| Exames (Novo pedido) | `EscolherPacienteWindow` + janela | o formulário pergunta QUEM e abre a folha |
| Ficha do balcão (WPF) | `EscolhaDeDocumentoWindow` + janela | leva à central de Documentos já no paciente (`PedidoDeDocumento`) — ponte fina até a ficha virar Blazor |

**O que a folha decide mora na Application, com teste** (`RascunhoDeDocumento` e
`EmissaoDeDocumentoNaTela`, 43 testes puros + 5 bUnit):
- a **conferência de alergia na receita**: avisa, exige o "eu vi" e reconfere no clique de emitir;
  editar o texto desfaz a confirmação; a colisão que some leva a confirmação junto;
- o **CID** só sai com a autorização expressa, com a descrição ao lado e o aviso de omissão;
- a **herança da sessão** só preenche campo vazio (CID, horários, indicação do pedido) e diz de onde veio;
- a **nova versão** cancela o laudo anterior no mesmo gravar;
- **quem assina** é quem está logado, nunca o primeiro da lista;
- a ordem do clique é a da janela WPF: permissão do tipo → assinante → texto → alergia →
  endereço da receita (antes de gastar número) → conferência legal → emitir → PDF → enviar.

**O que continua WPF:** só a janela de envio ao paciente (`IEnvioDoDocumentoAoPaciente` →
`AcoesDoDocumento.EnviarAsync`, a mesma de todas as portas). Assinar com e-CPF não mudou.

**Saíram:** `DocumentoWindow`, `DocumentoFolha`, `DocumentoEdicaoViewModel`,
`DocumentoOpcoesWindow`, `EscolhaDeDocumentoWindow`, `BuscaModeloClinico`,
`SugestaoTextoClinico`, `IconeClinico`, o dicionário `PaginaDocumentos.xaml`, os estilos
`BotaoDocumento*`, o token `Fonte.DocumentoTitulo`, os métodos de ponte que só abriam a janela e
a ferramenta `tools/VerificarDocumentos`.

**Diferenças deliberadas (escritas, não cometidas em silêncio):**
- depois de emitir, a folha **não oferece emitir de novo** (a janela WPF deixava o botão aceso
  quando só a impressão falhava — o segundo clique gastaria outro número);
- as **sugestões** de modelo da receita eram calculadas e nunca desenhadas: saíram (a aba Modelos
  faz o mesmo);
- a conferência legal também reconfere ao trocar o tipo e quem assina (antes só ao digitar);
- a indicação do pedido herdada da sessão ganhou a frase que diz de onde veio (era gravada sem leitor);
- os campos de exame têm o teto das colunas do banco (300 e 200 caracteres).

**UX · DX · CX**
- 🧑‍💻 **UX:** a folha abre na mesma tela, sem janela do sistema por cima; o papel continua em
  abas (Folha · Dados da emissão · Observações · Modelos). **Não foi visto rodando**: nem no app
  nem fotografado na prévia.
- 🛠️ **DX:** cerca de 1.800 linhas de WPF removidas; as regras ganharam teste pela primeira vez.
- 🤝 **CX:** sem migration. O risco está no fluxo: antes da release, emitir no build de teste uma
  receita com alergia registrada, um atestado com CID, um laudo e a nova versão dele.

---

## 3. O que ainda é WPF nos outros módulos

| Módulo | O que resta |
|---|---|
| Gerente, Financeiro | Nada |
| Faturamento | As duas janelas da abertura (aviso de pendências e rodada bloqueante), WPF de propósito: travam o app antes de a área de trabalho existir |
| Recepção | 16 arquivos XAML. Vários (agendamento, bloqueio, lista de espera, próximas vagas, autorização) já têm o formulário Blazor equivalente e podem ser só código morto, a conferir um a um. A tela de verdade é a **ficha do balcão** (`FichaBalcaoView`) com as quatro abas, que pode reaproveitar a aba administrativa Blazor desta PR |
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
