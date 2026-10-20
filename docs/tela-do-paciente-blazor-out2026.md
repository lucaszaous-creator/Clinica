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

### 2.1 Área da assinatura: virou Blazor em out/2026, só a camada de tela

A direção autorizou em 19/10/2026 converter estas janelas (eram congeladas). O motor continua congelado: [`safeid-congelado.md`](safeid-congelado.md). O roteiro de homologação está em [`safeid-telas-blazor-out2026.md`](safeid-telas-blazor-out2026.md).

| Era (WPF) | Agora (Blazor) |
|---|---|
| `PrescricaoInternaWindow` + `PrescricaoInternaEdicaoViewModel` | `Formularios/Infusao/PrescricaoDeInfusaoForm` (estado em `Application/Modelos/PrescricaoDeInfusaoEmEdicao`) |
| `FolhaExecucaoWindow` + VM | `Formularios/Infusao/FolhaDeExecucaoForm` (estado em `FolhaDeExecucaoNaTela`) |
| `InfusaoExternaWindow` | `Formularios/Infusao/InfusaoExternaForm` |
| `EvolucaoEnfermagemWindow` e as views que ela hospedava | `Formularios/Infusao/EvolucaoDaFolhaForm` (reusa `FolhaDaPassagem`) |
| `EscolherCertificadoWindow` | `Formularios/Assinatura/EscolherCertificadoForm` (porta `IAssinaturaNaTela`) |
| `AssinaturaPacienteWindow`, `EscolherTermoWindow`, `EscolherSessaoDoTermoWindow` | `Formularios/Termo/*` (porta `ColetaDeTermoNaPagina`) |

A ordem das chamadas de assinatura mora em `Application/Modelos/AssinaturaNaTela.cs`, com teste (`AssinaturaNaTelaTests`). O código WPF que ainda pede um destes formulários (a ficha do balcão, a ponte dos Prontuários, a emissão de documento) os abre na **janela de transição** `JanelaDeFormularioBlazor`.

### 2.2 Podem virar Blazor

| Janela | Tamanho (XAML + VM) | Onde é usada | Esforço |
|---|---|---|---|
| `ResultadosDoPedidoWindow` + `RegistrarResultadoExameWindow` | ~300 + ~350 linhas | Exames (Consultório) | meio dia, as duas |
| `ResumoProntuarioWindow` | ~500 linhas | Prontuários (Consultório) | meio dia |
| `EscolhaDeConvenioWindow` (`VinculoDeConvenio`) | ~310 linhas | Finalizar, fila, Novo atendimento | |
| `CobrancaDoPacienteWindow` | ~350 linhas | Ficha, Novo atendimento | |
| `CadastroPacienteWindow` | ~530 linhas | Ficha, Pacientes, Agenda | meio dia |
| `DocumentoWindow` (emitir receita, atestado, laudo) | ~1.200 linhas, 9 chamadores | os cinco apps | **1 dia**: a mais delicada (conferência de alergia na receita, prévia, CID sob autorização) |

As quatro últimas são do **shell** e servem também à Recepção e ao Gerente: convertê-las resolve os três módulos de uma vez.

### 2.3 Por arquitetura

A **moldura dos apps** (janela principal, menu, login) é WPF em todos os módulos: o Blazor Hybrid roda dentro dela (um navegador por janela, `NavegadorDaJanela` + `Palco`).

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
| 100% literal | moldura | não é possível sem mudar a decisão da §2.3 (a área da assinatura já virou Blazor, §2.1) | — |

Mesmo ritmo da PR 284: **uma PR por parte, mesclada só com o CI inteiro verde**.

### Decisões da direção (tomadas em 19/10/2026)

1. ✅ **A passagem de enfermagem da Sala de infusão** virou Blazor junto com a folha de execução (`EvolucaoDaFolhaForm`).
2. ✅ **A área congelada**: autorizada a conversão **só da camada de tela**. O motor (assinatura, PDF/PAdES, certificado, token, bytes assinados) continua congelado, e os testes de assinatura em homologação ficam com a clínica. Ver [`safeid-telas-blazor-out2026.md`](safeid-telas-blazor-out2026.md).

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
