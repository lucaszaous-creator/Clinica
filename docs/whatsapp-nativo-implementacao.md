# WhatsApp nativo na Recepção — roteiro de implementação (out/2026)

> **Para quem vai implementar.** Este documento é o contrato de trabalho: o que construir, onde
> cada peça mora, em que ordem, como se testa e quando está pronto. A decisão e a comparação
> estão em `docs/whatsapp-nativo-na-recepcao.md`; o desenho aprovado pela direção é o mockup
> interativo em `docs/mockups/whatsapp-nativo/` (abra o `index.html`; o `app.js` é o mapa de
> estados e ações que a tela Blazor tem de reproduzir). A metade do CRM está em
> `semdor-crm/docs/api-para-os-apps.md`.
>
> Leia antes de escrever a primeira linha: `CLAUDE.md` (a lista de conferência NA ESCRITA e as
> regras de tela de quem atende), `docs/telas-blazor.md` (o esqueleto de uma tela, os
> componentes, as fotos) e `docs/crm-na-recepcao.md` (a moldura de hoje, o bilhete e a ponte).

## 0. O que está decidido e não se reabre

| Decisão | Quem decidiu | Onde está escrito |
|---|---|---|
| A tela é **nativa Blazor**, sobre a **API do CRM**; o CRM continua o único que fala com a Meta e com o SMS | direção, 24/10 | proposta, §2 |
| Visual: **WhatsApp reconhecível, nos tokens da casa**; verde só no que é do canal | direção, 24/10 | proposta, §3 |
| A **retenção** (Acompanhamento: Recall e Novo BSV) é o **segundo modo da mesma lista** | direção, 24/10 ("estou sentindo falta da retenção") | proposta, §3 |
| Ficha, agenda, confirmação e Acompanhamento vêm **do próprio app**, nunca do CRM | arquitetura | proposta, §4 |
| O que a tela **afirma** mora na Application, puro e com teste | `CLAUDE.md` | telas-blazor §3.11 |
| A fase 1 entra **ao lado da moldura**, escolhida por configuração do Gerente; a moldura só sai na fase 3 | proposta | proposta, §4 |
| O item do menu continua **"WhatsApp"** (`ModuloRecepcao.ChaveWhatsAppCrm`, `Principal = 6`, exige `EditarAgenda`) | proposta | proposta, §4 |
| **Sem gravação de áudio e sem @SemDor** na fase 1 (ficam no CRM web) | proposta | proposta, §6 |

## 1. O mapa: cada parte do mockup → o código que a sustenta

A tela tem três colunas e dois modos. A tabela diz, para cada parte, **de onde vem o dado** e
**onde mora a regra**. "CRM" = `IClienteDoCrm`; "app" = os serviços que a Recepção já usa.

| No mockup (`app.js`) | Na tela Blazor | Dado | Regra (Application, com teste) |
|---|---|---|---|
| Seletor **Conversas / Retenção** com contagens | `.wa-modo` (`segmentado`) | CRM (`ListarConversasAsync`) + app (`IAcompanhamentoPacienteService.ListarAsync`) | `ConversasNaTela.Contagens`, `RetencaoNaTela.Contagens` |
| Filtros **Novas / Minhas / Todas / Encerradas**, busca, "Não lidas" | `.wa-filtros` | CRM | `ConversasNaTela.Filtrar` (quem é "nova": sem `AgentId`, aberta, sem menu pendente) |
| Linha da conversa: avatar com selo do canal, nome, hora, prévia com tick, não lidas, **selo da situação**, responsável | `.wa-linha` | CRM (`unread`, `preview`, `agentId`, `channel`, `lastIncoming`) + app para o selo | `ConversasNaTela.Selo` — o selo de confirmação (Não vem, Sem resposta, Confirmou, Pediu cancelar) vem de `AvisosDeConfirmacaoService.ListarAsync`, **a mesma lista do sino**; os demais (novo contato, 2 fichas, aguardando) vêm da conversa |
| Cabeçalho da conversa: nome, telefone, canal, **selo da ficha** (conferida / 2 fichas / sem ficha) | `.wa-cab` | CRM (`patientId`, `patientName`) + app (`IClinicaRepositorio` busca por telefone) | `ConversasNaTela.EstadoDaFicha` (0 fichas, 1 ficha = `patientId`, N fichas = conferir) |
| **Assumir / Com você / Devolver**, **Transferir**, **Encerrar / Reabrir**, buscar | `.wa-cab-acoes` | CRM (`claim`, `reassign`, `transfer`, `close`, `reopen`) | — (só as duas barreiras: `Pode(EditarAgenda)` + `Exigir`) |
| Faixa: fila, responsável, **janela de 24 h**, próximo horário, retenção | `.wa-faixa` | CRM (`lastIncoming`) + app (agenda) | `ConversasNaTela.Janela(lastIncoming, agora)` → aberta até / fechada desde |
| Mensagens: dia, evento, recebida, enviada com ticks, nota interna, divisória "N novas", agrupamento | `.wa-mensagens` | CRM (`GET /api/conversations/{id}`: `Messages`, `Notes`, `Audit`) | `ConversasNaTela.LinhaDoTempo(conversa, lidasAte)` — intercala mensagens, notas e auditoria em ordem; decide "seguida"; traduz `Status` (`sent/delivered/read/failed`) para o tick |
| **Aviso que pede ação** acima do campo (Não vem → Remarcar/Manter; Sem resposta → Confirmou por telefone/Não vem; 2 fichas → Conferir; Novo contato → Buscar/Cadastrar; retenção → Preparar convite/Já liguei; respondeu → Como foi?) | `.wa-acao` | app | `ConversasNaTela.PedeAcao(conversa, avisos, acompanhamento)` devolve UM `AcaoPedida` (tipo, frase, botões) |
| Escrever: **Responder / Nota interna**, Anexar, **Modelos**, Respostas rápidas, Ctrl+Enter | `.wa-escrever` | CRM (`reply`, `notes`, `attachments`, `templates/{sender}`, `quick-replies`) | `ConversasNaTela.PodeEscreverLivre(janela, canal)` |
| **Janela fechada**: campo some, "Escolher modelo" | `.wa-janela-fechada` | — | idem |
| **Formulário do modelo**: variáveis preenchidas pelo contexto, prévia | popover `.wa-popover` | CRM (`WhatsAppTemplate`) + app | `ModeloDoWhatsApp.Sugestoes(template, paciente, horario, caso)` |
| Coluna 3 — **Paciente** (cabeçalho da ficha, telefone confere, carteirinha, autorização de mensagens, tratamento) | `.wa-secao` | app | `FichaDoBalcao.Cabecalho` (já existe) + `AutorizacaoService` (autorização do termo) |
| **Horários** com Confirmar / Remarcar / Cancelar | `.wa-horario` | app (`AgendaService.NoPeriodoAsync`, `RemarcarAsync`, `CancelarAsync`, registrar resposta pelo `CampanhaService`, como a tela Confirmações faz) | as regras já existentes da agenda; **nenhuma nova** |
| **Confirmação de hoje** (linha do tempo) | `.wa-tempo` | app (`AvisosDeConfirmacaoService`, `RodadaDeConfirmacao`) | idem |
| **Acompanhamento** / **o caso** (tratamento, última sessão, sessões, autorização, situação, contatos) | `.wa-caso` | app (`IAcompanhamentoPacienteService.ListarAsync`, `HistoricoAsync`) | `ConversaDoAcompanhamento` (já existe: `Cabecalho`, `Autorizacao`, `TempoTexto`, `Trilho`) |
| **Como foi?** (Quer marcar, Pediu outra data, Aguardando plano, Não respondeu, Não quer voltar + PARAR) | `.wa-desfechos` | app (`AtualizarAsync` com `AtualizarAcompanhamento`; PARAR pela `AutorizacaoService`) | `ConversaDoAcompanhamento.Pedido(linha, desfecho, …)` (já existe) |
| **Qual ficha é esta pessoa?** | `Formularios/ConferirFichaDaConversa.razor` | app (busca por telefone) + CRM (`POST …/patient`) | `ConversasNaTela.CandidatasPorTelefone` |
| **Sino** → abre a conversa certa | `MolduraDoApp` (já existe o item "Abrir a conversa no CRM") | app | o clique deixa um `PedidoDeConversaNoCrm` e navega para a aba (já existe); a tela nova o consome |
| Lista de **Retenção**: grupos (Responderam · 30 · 20 · 15 dias · Novos no BSV · Retornos pedidos), filtros Para hoje / Aguardando / Fechados, linha com tratamento, última sessão, sessões, autorização | `.wa-grupo`, `.wa-linha` | app (`ListarAsync`; dias do Gerente em `ParametrosService`) | `RetencaoNaTela.Agrupar(linhas, diasDoGerente, hoje)` e `RetencaoNaTela.Selo(linha)` |
| Conversa de retenção **sem mensagens**: bloco "Primeira mensagem: um modelo" / "Sem autorização: ligar" | `.wa-convite` | app | `RetencaoNaTela.Abertura(linha)` |
| Notificações passageiras | `Notificacoes` (já existe) | — | — |

## 2. Arquitetura, em uma figura

```
Recepção (BlazorWebView)                              VPS 2 — CRM (semdor-crm)
┌──────────────────────────────────────────┐          ┌─────────────────────────────┐
│ ConversasDoWhatsApp.razor                │  HTTPS   │ /api/login/ticket           │
│   ├─ ConversasNaTela / RetencaoNaTela    │ ───────► │ /api/session (csrf)         │
│   │    (Application, puro, com teste)    │  cookie  │ /api/conversations …        │
│   ├─ IClienteDoCrm ─► ClienteDoCrmHttp   │ ◄─────── │ /api/templates/{sender}     │
│   │    (Infrastructure)                  │          │ (ver api-para-os-apps.md)    │
│   └─ os serviços da Recepção:            │          └──────────────┬──────────────┘
│        AgendaService, AvisosDeConfirmacao│                         │ ponte (VPS 1)
│        IAcompanhamentoPacienteService,   │                         ▼
│        FichaDoBalcao, AutorizacaoService │          ┌─────────────────────────────┐
│        IEntradaNoCrmService (bilhete)    │          │ /crm/login/ticket → confere │
└──────────────────────────────────────────┘          │ o bilhete no banco da clínica│
                                                      └─────────────────────────────┘
```

- O app **nunca** lê o banco do CRM; o CRM **nunca** lê o banco da clínica (só a ponte, com o
  papel `ponte_crm`). Isso já é assim e continua.
- A sessão do CRM é por **usuário logado no app**: o bilhete diz quem é a pessoa, e o CRM
  aplica a `AccessPolicy` dele (a mesma do CRM web). Duas pessoas no mesmo PC = dois
  `CookieContainer`, um por `SessaoUsuario`.

## 3. A ordem: PR a PR

Cada PR é pequeno, tem o próprio teste e sobe sozinho. Nenhum PR troca o que a clínica vê até
o PR 5 ligar a configuração.

### PR 0 — no `semdor-crm`: o contrato da API para os apps

Roteiro em `semdor-crm/docs/api-para-os-apps.md`. Entrega: o arquivo `ApiParaOsApps.cs` com
os records de resposta das rotas que a tela usa, o teste de contrato (`ApiParaOsAppsTests`:
o JSON de cada rota desserializa no record, sem campo sobrando que a tela precise), e o
`GET /api/conversations?desde=<UpdatedAt>` opcional para a releitura a cada 7 s. **Sem este PR
a fase 1 não começa**: hoje `/api/*` é interno do front do CRM e muda sem aviso.

### PR 1 — `IClienteDoCrm` + `ClienteDoCrmHttp` (sem tela)

- `src/Clinica.Application/Abstracoes/IClienteDoCrm.cs`: um método por rota da tabela acima,
  todos `Async` com `CancellationToken`, devolvendo os records de
  `src/Clinica.Application/Modelos/Crm/ContratosDoCrm.cs` (**cópia idêntica** do
  `ApiParaOsApps.cs` do CRM, pela mesma regra do `ClinicContracts.cs`: campo novo entra no
  fim, com valor padrão). Toda recusa do CRM (4xx com `{ error }`) vira `RecusaDoCrm(string
  Mensagem, int Status)` — exceção tipada que a tela mostra inline; 5xx/timeout viram
  `CrmIndisponivel` (a tela mostra `FalhaDeLeitura`, nunca lista vazia).
- `src/Clinica.Infrastructure/Crm/ClienteDoCrmHttp.cs`:
  - `HttpClient` com `CookieContainer` **por `UsuarioId`** (`ConcurrentDictionary`), `BaseAddress`
    = `ParametrosService.ObterEnderecoDoCrmAsync()` (já validado: só https, sem caminho).
  - **Entrar**: `GET /api/session` (recebe o cookie antiforgery e o `csrf`) → `POST
    /api/login/ticket` com `{ ticket }` e o cabeçalho `X-CSRF-TOKEN` → sessão. O bilhete vem de
    `IEntradaNoCrmService.EmitirAsync(usuarioId)` (já existe; 60 s, uso único).
  - **Renovar sozinho**: qualquer 401 → entra de novo com bilhete novo e repete a chamada **uma
    vez**. A pessoa nunca vê "Entrar de novo". Dois 401 seguidos = `CrmIndisponivel` com a
    frase "O CRM não aceitou o acesso. Fale com a direção."
  - **Escritas** sempre com `X-CSRF-TOKEN` (relido do `/api/session` depois de cada login) e com
    a `Version` da conversa (o CRM recusa 409 quando alguém mexeu antes: a tela relê e avisa).
  - Timeouts: leitura 10 s, escrita 20 s, upload 60 s. Sem retry em escrita (idempotência é do
    CRM, pela `Version`).
- Registro em `DependencyInjection.AddClinica`: `AddSingleton<IClienteDoCrm, ClienteDoCrmHttp>`
  (um por processo; os cookies são por usuário dentro dele). **Nunca** Scoped: a tela abre
  escopo por operação e o cliente precisa sobreviver a eles.
- **Testes** (`tests/Clinica.Tests/Crm/ClienteDoCrmHttpTests.cs`) com `HttpMessageHandler`
  falso (há precedente em `PonteClinicTeamTests`): entra com bilhete; 401 renova uma vez;
  dois 401 viram `CrmIndisponivel`; `error` do CRM vira `RecusaDoCrm` com a frase; CSRF vai em
  toda escrita; dois usuários não compartilham cookie.

### PR 2 — `ConversasNaTela` e `RetencaoNaTela` (puros, sem tela)

`src/Clinica.Application/Servicos/ConversasNaTela.cs` e `RetencaoNaTela.cs`, estáticos como os
irmãos (`ConfirmacoesNaTela`, `AcompanhamentoNaTela`). Cada função da coluna "Regra" da tabela
do §1 é um método público com teste em `ConversasNaTelaTests` / `RetencaoNaTelaTests`:

- `Janela(DateTimeOffset? lastIncoming, DateTimeOffset agora, string canal)` → `{ Aberta, Ate |
  Desde, Texto }`. SMS = sem janela. Teste: 23 h 59 aberta, 24 h 01 fechada, nunca escreveu =
  fechada "desde sempre".
- `Selo(conversa, avisosDeConfirmacao, linhaDoAcompanhamento)` → `(Tom, Texto)`. Teste por
  tipo, e **o mesmo aviso do sino dá o mesmo selo** (compara com `AvisoDeConfirmacao.Tipo`).
- `PedeAcao(...)` → `AcaoPedida?`. Teste: Não vem → Remarcar/Manter; fechado → nada; retenção
  sem mensagens → nada (o bloco de abertura responde).
- `LinhaDoTempo(conversa, lidasAte)` → lista intercalada, com `Seguida` e a divisória. Teste:
  ordem por `At`, nota entre mensagens, divisória antes da primeira não lida.
- `EstadoDaFicha(conversa, fichasComOTelefone)` → Conferida / Duvida(n) / Nenhuma. Teste com
  0, 1 e 2 fichas.
- `RetencaoNaTela.Agrupar(linhas, diasDoGerente, hoje)` → grupos na ordem do mockup; teste:
  quem respondeu vem primeiro; 30 antes de 20 antes de 15; Novo BSV e Retorno pedido pelo
  `Tipo`; `Consentimento == false` ganha o selo "Sem autorização · ligar"; só `Tratamento`
  (`ModalidadeAtendimento != Consulta`).
- `ModeloDoWhatsApp.Sugestoes(template, contexto)` → valores iniciais por nome do parâmetro.
  Teste: "sessão de BSV" para BSV, "acupuntura" em minúsculo, dias do caso.

### PR 3 — a tela, o CSS, os formulários e a semente (ainda desligada)

- `src/Clinica.Ui.Blazor/Telas/Recepcao/ConversasDoWhatsApp.razor` (`@inherits TelaBase`,
  implementa `IAtualizarAoVoltar`). Esqueleto e CSS: **copie** `docs/mockups/whatsapp-nativo/
  conversas.css` para `Ativos/css/telas/conversas.css` (ele já é só token e já tem o prefixo
  `wa-`) e registre no `clinica.css`. O HTML do Razor segue o `corpo.html` + o que o `app.js`
  renderiza (`renderLista`, `renderConversa`, `renderFicha`, `renderEscrever`).
- Estado da tela: `_modo`, `_filtro`, `_busca`, `_atual` (id da conversa), `_rascunhos`
  (por conversa: texto, modo, anexo), `_geracao`. **Timer de 7 s** ligado no `Loaded` e
  desligado no `Unloaded` (regra do `CLAUDE.md`: quem liga é a view); a releitura é silenciosa
  (sem "Carregando", sem limpar, com `?desde=`), publicada depois do último `await`, e **não**
  mexe no rascunho de quem digita.
- Formulários em `Formularios/`: `TransferirConversa`, `EncerrarConversa` (os quatro
  resultados do mockup → `ServiceClose.Outcome`), `ConferirFichaDaConversa`,
  `CancelarHorarioPelaConversa` (motivo obrigatório, como no mockup), `NovaConversa`. **Não**
  crie formulário de remarcar: a Recepção já tem a marcação (`IMarcacaoDeHorario` /
  `rec-agendamento-form`) e a tela Confirmações já remarca pela agenda; chame o que existe.
  "Qual ficha é esta pessoa?" usa `SeletorDePaciente` / `BuscaDePacienteNaTela` para a busca.
- Popovers de **Modelos** e **Respostas rápidas** são componentes da tela (`ModelosDoWhatsApp`,
  `RespostasRapidas`), posicionados `position:absolute` sobre a área de escrever (o palco não
  corta: `.wa-conversa` é `position:relative`).
- **As duas barreiras** em cada botão que grava: `disabled="@(!Pode(Permissao.EditarAgenda))"`
  e `if (!Exigir(Permissao.EditarAgenda, "responder pelo WhatsApp da clínica")) return;`.
  Cancelar horário e registrar desfecho usam os bits que as telas Confirmações e Acompanhamento
  já usam (copie dali, não invente).
- **Terceiro estado**: CRM fora → `FalhaDeLeitura` com `AoTentarDeNovo`; sem endereço do CRM →
  `EstadoVazio` "ainda não foi ligado" (a frase da moldura de hoje); sem permissão → o
  `EstadoVazio` com cadeado (igual à aba de hoje).
- **Registro de acesso ao prontuário**: a coluna 3 mostra ficha e agenda, não prontuário.
  Se algum dia mostrar evolução, entra `AcessoProntuarioService.RegistrarAsync` na troca de
  paciente (ponto 4 da conformidade).
- Semente `tools/PreviaBlazor/Sementes/ConversasDoWhatsApp.cs`: paciente com horário hoje e
  aviso "Não vem", um Novo BSV, um Recall de 30 dias sem autorização, o endereço do CRM
  configurado. **A prévia não tem CRM**: registre em `tools/PreviaBlazor` um
  `IClienteDoCrm` falso em memória (`ClienteDoCrmDeMentira`) com as conversas do `app.js`,
  para a foto e para o `TelasBlazorAbremTests` abrirem a tela e clicarem tudo. O mesmo falso
  serve aos testes da tela.
- Fotos em 1024 e 1366, com cada formulário e cada popover aberto, antes do push (§5 de
  `docs/telas-blazor.md`). Compare lado a lado com as fotos `01`–`20` do mockup: a diferença
  aceitável é a fonte (Segoe no Windows).

### PR 4 — a retenção dentro da tela

- O modo Retenção lê `IAcompanhamentoPacienteService.ListarAsync(usuarioId)` e agrupa com
  `RetencaoNaTela.Agrupar` (dias do Gerente: a chave de "Recall: dias sem vir" em
  `ParametrosService`, a mesma que a fase 3 usa).
- Clique numa linha: se a linha tem telefone e o CRM já tem conversa com ele →
  abre; senão cria **em memória** (sem chamar o CRM) e só chama `POST /api/conversations` +
  `POST …/patient` no primeiro envio (é o que o mockup faz com `oculta`). Conversa criada pela
  clínica nasce com janela fechada → só modelo (`PodeEscreverLivre` já decide).
- Enviar o modelo = `AtualizarAsync` com `ConversaDoAcompanhamento.Pedido(linha, Contatar,
  canal WhatsApp, …)` **no mesmo clique** (o contato entra sozinho, como a fase 2 já faz pela
  ponte: aqui o app grava direto porque ele é o autor).
- "Como foi?" = os cinco `DesfechoContato`; "Quer marcar" abre a marcação da Recepção; "Não quer
  voltar" exige observação (`ConversaDoAcompanhamento.ExigeObservacao`) e, com o PARAR marcado,
  chama a revogação da autorização (`AutorizacaoService`, a mesma do PARAR da fase 3) e
  registra o evento.
- Quem não autorizou (`Consentimento == false`): o único botão é "Já liguei" → "Como foi a
  ligação?" (os mesmos desfechos, canal Telefone). **Nenhum caminho de envio** pode existir
  para ela: teste que `PedeAcao` e `Abertura` não devolvem botão de modelo.
- Testes: `RetencaoNaTelaTests` (PR 2) + o teste de tela clicando um desfecho de cada e
  conferindo o `AtualizarAcompanhamento` que chegou ao serviço falso.

### PR 5 — ligar na clínica, ao lado da moldura

- `ParametrosService.ChaveWhatsAppNativo` (bool, padrão **false**) com o campo em Gerente →
  Configurações → Integrações, ao lado do Endereço do CRM: "WhatsApp dentro do app: tela nova
  (recomendado) / moldura do CRM".
- `ModuloRecepcao` (linha do `ChaveWhatsAppCrm => HospedagemBlazor.Tela(...)`): escolhe
  `ConversasDoWhatsApp` ou `WhatsAppNoCrm` pela configuração. O item do menu é o mesmo.
- O **sino** já navega para a aba com um `PedidoDeConversaNoCrm`; a tela nova o consome na
  abertura e no `AoVoltarAsync` (como a aba de hoje faz) e abre a conversa pelo telefone, liga a
  ficha **só se a conversa não tem ficha** (regra 5) e deixa o texto como rascunho.
- Publicação: CRM (PR 0) → apps (Recepção e Gerente) → a direção liga a configuração numa
  máquina, depois nas outras. Rollback = desligar a configuração; nenhum release.
- O PR diz em **UX · DX · CX** como cada regra de tela da direção foi atendida (as seis do
  `CLAUDE.md`), e o que ficou de fora (áudio, @SemDor).

### Fase 2 (depois do PR 5 em produção): a conversa como painel

`PainelDaConversa` = a coluna 2 extraída como componente, com a mesma `ConversasNaTela`. As
telas que hoje chamam `PedidoDeConversaNoCrm` e navegam (Agenda do dia, Ficha do balcão,
Confirmações, Acompanhamento) passam a abrir o painel ao lado, sem trocar de tela. Decidir
então o destino da tela Acompanhamento (funil da equipe e taxa de retorno não cabem em 340 px:
ou ela vira só o painel de números, ou vai para o Gerente).

### Fase 3: a moldura sai inteira

`WhatsAppNoCrm.razor`, `whatsapp-crm.css`, `SementeWhatsAppNoCrm`, a configuração do PR 5, e
no CRM o `modo=recepcao` (`modo-recepcao.js`, `CRM_EMBED_ORIGINS`, o teste `ModoRecepcaoHttpTests`).
A entrada por bilhete **fica**: é o que o `ClienteDoCrmHttp` usa. "Função que saiu, sai inteira."

## 4. Critério de pronto, por PR

- [ ] `python tools/verificar-suite.py` verde; `dotnet build <projeto> --no-incremental` sem warning novo.
- [ ] Testes da família com `--filter` verdes **e** o `TelasBlazorAbremTests` abre a tela e clica cada botão (PR 3+).
- [ ] Fotos em 1024 e 1366 no PR, cada formulário e popover aberto; nenhum erro no console da prévia.
- [ ] Nenhuma regra nova no Razor: tudo que a tela afirma tem um método em `ConversasNaTela` /
      `RetencaoNaTela` com teste.
- [ ] Nenhum número solto no CSS (só token); nenhuma classe sem o prefixo `wa-`.
- [ ] O PR descreve UX · DX · CX e diz o que **não** fez.
- [ ] Contrato do CRM: `ContratosDoCrm.cs` **idêntico** ao `ApiParaOsApps.cs` (há um teste de
      igualdade de arquivo para o `ClinicContracts.cs` em `tests/Clinica.Tests/PonteCrm`; copie o padrão).

## 5. O que a tela NÃO faz (e por quê)

- **Não calcula** quem não vem, quem sumiu ou quantos dias: lê `AvisosDeConfirmacaoService` e
  `IAcompanhamentoPacienteService`. Dois cálculos dariam dois números para a mesma agenda.
- **Não manda** mensagem sem a pessoa clicar Enviar, nem para mais de uma pessoa por vez.
  Não há "enviar para todos".
- **Não cria ficha**: "Cadastrar paciente" abre o cadastro da Recepção com o telefone
  preenchido (`FormulariosDoPaciente`).
- **Não troca** a ficha de uma conversa que já tem ficha (regra 5). A troca é um ato explícito
  no formulário "Qual ficha é esta pessoa?", com evento.
- **Não guarda** nada do CRM no banco da clínica, além do que a fase 2 já grava pela ponte
  (o contato no Acompanhamento). Rascunho vive na tela.
- **Não mostra prontuário**. Se um dia mostrar, registra o acesso (conformidade, ponto 4).

## 6. Perguntas abertas para a direção (não bloqueiam o PR 0–3)

1. Os quatro resultados de **Encerrar** (Resolvido, Agendado, Sem resposta, Encaminhado) bastam,
   ou o CRM web tem outros que a recepção usa?
2. **Respostas rápidas**: as do CRM (`/api/quick-replies`) ou um catálogo do Gerente no app?
   O mockup usa as do CRM.
3. O **destino da tela Acompanhamento** depois da fase 2.
4. **Áudio e @SemDor** na tela nativa: fase 4, ou nunca (ficam no CRM web)?
