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
  `src/Clinica.Application/Modelos/Crm/ContratosDoCrm.cs` (JÁ ESCRITO em 24/10, namespace
  `Clinica.Crm.Contracts`, records `App*`; **cópia idêntica** do
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

> **Rodada 2 (24/10/2026):** a escolha em Configurações → Integrações, a saída do CRM no
> fechamento do app e o **roteiro da manhã** (`docs/whatsapp-nativo-roteiro-da-manha.md`: publicar
> o CRM, os apps, ligar e conferir) estão entregues — ver §8. A linha do `ModuloRecepcao` e o
> consumo do pedido do sino são da tela (PR 3).

- `ParametrosService.ChaveWhatsAppNativo` (bool, padrão **false**) com o campo em Gerente →
  Configurações → Integrações, ao lado do Endereço do CRM: "WhatsApp dentro do app: tela nova
  (recomendado) / moldura do CRM".
- `ModuloRecepcao` (linha do `ChaveWhatsAppCrm => HospedagemBlazor.Tela(...)`): escolhe
  `ConversasDoWhatsApp` ou `WhatsAppNoCrm` pela configuração. O item do menu é o mesmo.
- O **sino** já navega para a aba com um `PedidoDeConversaNoCrm`; a tela nova o consome na
  abertura e no `AoVoltarAsync` (como a aba de hoje faz) e abre a conversa pelo telefone, liga a
  ficha **só se a conversa não tem ficha** (regra 5) e deixa o texto como rascunho.
- Publicação: CRM (PR 0) → apps (Recepção e Gerente) → a direção liga a configuração antes de
  a clínica abrir e confere. ⚠️ A configuração mora no banco e vale para a CLÍNICA inteira (não há
  "uma máquina primeiro"): cada Recepção passa a usar a escolha quando o app for aberto de novo.
  Rollback = voltar a configuração para a moldura; nenhum release. Passo a passo em
  `docs/whatsapp-nativo-roteiro-da-manha.md`.
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
4. **Áudio e @SemDor** na tela nativa: fase 4, ou nunca (ficam no CRM web)? — o @SemDor entrou em 24/10 (§10).

## 7. Rodada 1 entregue (24/10/2026): PR 0, PR 1 e PR 2

Três agentes em paralelo, juntados na branch `claude/whatsapp-system-integration-ankslw` dos dois
repositórios. Verde na árvore juntada: build `--no-incremental` sem warning, 341 testes das
famílias tocadas (`Crm`, `Ponte`, `EntradaNoCrm`, `ConversasNaTela`, `RetencaoNaTela`,
`ModeloDoWhatsApp`, `Acompanhamento`, `AvisosDeConfirmacao`), `verificar-suite`, e no CRM a suíte
inteira (820 + 20) e os 13 testes do contrato. A suíte completa do Clinica é do CI do PR.

| PR | Onde | O que ficou pronto |
|---|---|---|
| 0 | semdor-crm | `previewDirection`/`previewStatus` na lista; `?desde=` (`ListaDesde`, margem de 5 s porque o `Store.Save` carimba `UpdatedAt` antes de confirmar); `ApiParaOsAppsTests` (reprova campo que some, por reflexão; fluxo do bilhete; SHA-256 do contrato) |
| 1 | Clinica | `IClienteDoCrm` + `ClienteDoCrmHttp` (Singleton; sessão por usuário; entrada por bilhete serializada; 401 e CSRF recusado renovam UMA vez; pausa de 1 min depois de acesso recusado, para não esgotar o limite de login do IP da clínica; logout avisa o CRM sem esperar); `RecusaDoCrm`, `CrmIndisponivel`, `CrmNaoConfigurado`; `ContratosDoCrmTests` |
| 2 | Clinica | `ConversasNaTela`, `RetencaoNaTela`, `ModeloDoWhatsApp`, 83 testes |

**Fatos que a rodada descobriu e que valem para a tela (PR 3):**

- **Status de saída reais** do CRM: `sending`, `accepted`, `simulated`, `uncertain`, `delivered`,
  `seen` (lida), `failed`. `ConversasNaTela.Tick` já mapeia; status desconhecido vira "Envio não
  confirmado" (dizer "enviada" sem confirmação faria a recepção não conferir).
- **`AppReply.Id` e `AppNoteRequest.Id` são Guid "D"** (com hífens). O contrato foi corrigido nos
  dois repositórios (SHA-256 daquela rodada: `ff97010885fc…`; o atual, depois do
  `AppReply.ContactRecordedByApp` de 24/10, era `35284d966f92…673e`; depois do @SemDor (§10) é
  `84200af20226503b53e698e576fd9126e8726331b091342f273ed456fa96dae6`, o mesmo de `ContratosDoCrmTests` e `ApiParaOsAppsTests`).
- **O `?desde=` não traz** a leitura feita noutro aparelho nem a conversa que saiu da visão do
  usuário: a tela relê a lista inteira ao abrir e a cada poucos minutos.
- **Aviso do sino → conversa**: só para conversa com ficha conferida (`PatientId`), pelo mapa
  agendamento → paciente que a tela monta da agenda do período (`ConversasNaTela.AvisosDaConversa`).
- **Os dias do Gerente são UM número** (`ParametrosService.ObterDiasInatividadeRecallAsync`, padrão
  60). Os degraus 30 / 20 / 15 do mockup não existem na configuração: `RetencaoNaTela.Agrupar`
  recebe os limites, e com `[60]` a lista sai num grupo só.
- **"Novos no BSV"** no Acompanhamento são os indicados que ainda NÃO fizeram a 1ª sessão; os textos
  dizem isso (o "como se sentiu" do mockup pressupunha sessão feita).
- **"Retornos pedidos" ficaram fora** da retenção: `RetornoAMarcar` não traz modalidade nem
  autorização, e a tela "Retornos a marcar" já responde a pergunta.
- **Modelos**: `{{n}}` ligado por nome do modelo (`lembrete_sessao`, `retorno_tratamento`,
  `documento_pronto`, `primeiro_contato`, `recall_semdor`, `semdor_confirmacao_24h`); modelo
  desconhecido sai com "Variável n" vazia para a pessoa preencher. Nenhum valor inventado.
- **Encerrar e respostas rápidas** vêm do CRM (`GET /api/workspace`: os seis resultados de
  `Store.Outcomes` e as respostas rápidas). Responde as perguntas 1 e 2 do §6.
- O mockup erra os dias da semana (24/10/2026 é **sábado**); os testes usam o calendário real.

**Mudança de comportamento em produção que veio junto (CRM):** quando não há perfil gravado, o
administrador padrão do CRM passa a ser quem é administrador no Clinica, e não o primeiro da lista
da ponte (`Store.TeamProfiles` agora segue a regra de `WorkspaceStore.Team`).

**Perguntas para a direção antes do PR 4:**

1. Quer os degraus 30 / 20 / 15 dias como configuração nova do Gerente, ou um limite só basta?
2. Quais são os nomes reais dos modelos aprovados na Meta? Os do mockup provavelmente não existem.
3. O "como se sentiu depois da 1ª sessão de BSV" precisa de uma fila nova no Acompanhamento?

**Próxima rodada:** PR 3 (a tela, o CSS, os formulários, a semente e o `IClienteDoCrm` falso da
prévia) e PR 4 (a retenção na tela), sobre `IClienteDoCrm` e as três classes de regras.

## 8. Rodada 2 — a ligação (PR 5, 24/10/2026)

| Peça | Onde | Teste |
|---|---|---|
| Bloco **"WhatsApp dentro do app"** (Tela nova (recomendado) / Moldura do CRM), logo abaixo do Endereço do CRM. Habilitado só com o endereço GRAVADO (diz por quê quando não está); duas barreiras com `GerenciarUsuarios`, o bit do endereço; o Salvar confere o endereço de novo no banco | `ConfiguracoesDaClinica.razor` (aba Integrações), regras e frases em `ConfiguracoesDaClinicaNaTela` | `ConfiguracoesDaClinicaNaTelaTests`; a tela é aberta e clicada pelo `TelasBlazorAbremTests`; fotos em `docs/mockups/whatsapp-nativo/tela-real/configuracoes-whatsapp-*.png` |
| A chave `ChaveWhatsAppNativo`: nasce desligada, grava e relê, só `"true"` liga | `ParametrosService` | `ParametrosServiceTests.WhatsApp_nativo_*` |
| **Saída do CRM no fechamento do app**: `EsquecerSessao` + espera de até 2 s pelo logout (sem a espera o aviso morria com o processo) | `Infrastructure/Crm/SaidaDoCrm`, chamada no `Closed` da `JanelaDoApp` — o ponto único: "Sair" fecha a janela e "Trocar usuário" reabre o processo | `ClienteDoCrmHttpTests.Fechar_o_app_*` |
| O roteiro da direção | `docs/whatsapp-nativo-roteiro-da-manha.md` | — |

**Fatos que a rodada descobriu:**

- A **presença** "online" do CRM vem do pulso `/api/presence`, que só o CRM web manda (lease de
  120 s). O que o logout do app encerra é a **sessão** no servidor (válida por 2 h), não a presença.
- A **escolha não é por máquina**: a configuração da clínica mora no banco. O roteiro manda ligar e
  conferir antes de a clínica abrir.
- Os **modelos em produção** têm prefixo (`semdor_lembrete_sessao`, `semdor_iniciar_atendimento`).
  **Resolvido na mesma noite**: `ModeloDoWhatsApp` passou a conhecer os doze aprovados (a tabela
  `KNOWN` do `template-picker.js` do CRM), e o convite da retenção usa
  `RetencaoNaTela.CandidatosDoConvite` (o `semdor_*` antes do nome do mockup).
- O `docs/producao-out2026.md` do CRM registra `79f823e` (21/10) como a versão no ar; a `main` tem
  depois disso as fases 1–3 e o @SemDor. O roteiro manda conferir o `state.json` da VPS 2 e listar o
  que vai junto antes de publicar.

## 9. Rodada 2 — a tela (PR 3 e PR 4, 24/10/2026)

O que está na branch `claude/whatsapp-system-integration-ankslw` e funciona de ponta a ponta na
prévia (com o CRM de mentira) e no bUnit:

| Peça | Onde |
|---|---|
| O roteador da aba: lê `ObterWhatsAppNativoAsync` e mostra a tela nova ou a moldura; relê ao voltar à aba; se a leitura falhar, fica a moldura | `Telas/Recepcao/WhatsAppDaRecepcao.razor`; `ModuloRecepcao` aponta para ele |
| A tela: lista em dois modos (Conversas / Retenção), conversa (linha do tempo, ticks, aviso que pede ação, janela fechada com modelo, modelos com variáveis e prévia, respostas rápidas, anexo, busca, Ctrl+Enter), assumir/devolver/transferir/encerrar/reabrir, nova conversa, coluna do paciente (ficha, horários com confirmar/não vem/manter/remarcar/cancelar, confirmação de hoje, caso do Acompanhamento com o "Como foi?"), estados sem permissão / não configurado / fora do ar / conflito, releitura de 7 s com `desde` e lista inteira a cada 3 min | `Telas/Recepcao/ConversasDoWhatsApp.razor`, `Ativos/css/telas/conversas.css`, `Formularios/WhatsApp/*` |
| A composição dos dados e as escritas (as MESMAS das telas vizinhas: `CampanhaService`, `AgendaService`, `IAcompanhamentoPacienteService`, `ConsentimentoService.RevogarAsync`); no 409 de responder/nota/modelo relê e repete UMA vez com o mesmo Id | `Servicos/WhatsAppDaRecepcaoService.cs` (18 testes) |
| Duas consultas novas (pacientes dos agendamentos; fichas com o telefone) | repositório + `TraducaoNoNpgsqlTests` |
| Cancelar com motivo na auditoria (o mesmo cancelamento, que suspende as guias) | `AgendaService.CancelarComMotivoAsync` |
| O CRM de mentira e a semente (prévia e `TelasBlazorAbremTests`); `CLINICA_PREVIA_CRM=fora-do-ar` ou `nao-configurado` para fotografar as falhas | `tools/PreviaBlazor/CrmDeMentira.cs`, `Sementes/ConversasDoWhatsApp.cs` |

Fotos da tela real (1024 e 1366, cada estado e formulário, e três fluxos executados):
`docs/mockups/whatsapp-nativo/tela-real/`, refeitas por `node tools/PreviaBlazor/fotos-whatsapp.js`
com a prévia no relógio fixo. O roteiro também reprova página que rola para o lado e filho que vaza
da borda do grupo (seletores, ações do cabeçalho, faixa, rodapé do campo de escrever) e, no celular
(480 px), ação do cabeçalho escondida. Os estados de falha (`40-crm-fora-do-ar-*`,
`41-crm-nao-configurado-*`) saem de uma prévia com `CLINICA_PREVIA_CRM=fora-do-ar` ou `nao-configurado`.

**Verificação na árvore juntada:** build `--no-incremental` de `Clinica.Ui.Blazor`, dos testes e da
prévia sem warning; `compilar-sombra` (13 projetos WPF); `verificar-suite`; as famílias tocadas em
SQLite; **e no Postgres** (`CLINICA_TESTES_POSTGRES`, local): as famílias das consultas e escritas
novas (`WhatsAppDaRecepcao`, `TraducaoNoNpgsql`, `AgendaService`, `Acompanhamento`,
`CampanhaService`, `ParametrosService`) passaram. A suíte inteira roda no CI do PR.

**O que a rodada achou e corrigiu na junção:**

- O `tools/ValidarLayoutWindows` (que o CI do Windows roda) compila as sementes da prévia mas não
  enxergava o `CrmDeMentira.cs`, onde moram `PessoasDoWhatsApp` e `RelogioDaPrevia`: seria build
  vermelho no CI. O `.csproj` passou a incluí-lo, e o arquivo ganhou o `using System.IO` que esse
  projeto não tem implícito. Lição em `docs/agentes/convencoes.md`.
- Com `CLINICA_TESTES_POSTGRES`, o banco das telas (`BancoDasTelas`) também vai ao Postgres, e uma
  semente ANTIGA (horário do Dr. Caio, agenda protegida) cai fora da jornada conforme o dia: o
  gatilho `CK_Agenda_Protegida` recusa. **É anterior a esta rodada** (reproduzido no commit
  `1a828bd`). **Corrigido depois, na mesma noite:** a sessão das férias cai numa terça ou quinta
  (a jornada dele) e é gravada antes do bloqueio; conferido no Postgres nos sete dias da semana.
- O CRM real recusa assumir/transferir/reabrir enquanto há envio em `sending` ("Aguarde o envio em
  andamento"); o CRM de mentira não imita. A tela trata: no 409 relê e mostra a frase.

**O que ficou de fora:** formulário próprio de remarcar (abre a janela do horário da agenda, dois
cliques; a mensagem "Combinado…" fica como rascunho).

**Fechado depois, na mesma noite:**
- **"Cadastrar com este telefone" abre o cadastro com o número da conversa já no campo**
  (`FormulariosDoPaciente.CadastrarAsync(telefone)`, parâmetro opcional: o balcão e a busca de
  pacientes continuam chamando sem ele). A recepção pode corrigir antes de gravar. Teste:
  `FormulariosDoPacienteTests.Cadastro_aberto_pela_conversa_do_WhatsApp_ja_vem_com_o_telefone_e_o_grava`.
  Ele pegou um defeito na hora: sem `@`, o Razor passava a frase `"f.TelefoneInicial"` ao
  parâmetro `string` (lição em `docs/agentes/convencoes.md`).
- **"Ver caso" abre a conversa do caso da pessoa no Acompanhamento**, já na fila do tipo dele
  (`PedidoDoAcompanhamento.PedirCaso`, como o pedido de retornos do Início). Não passa por cima de
  formulário aberto, e o caso que saiu da fila avisa em vez de abrir nada. Teste:
  `VerCasoDoWhatsAppTests`, que reprova se a abertura for retirada.
- **Acabamento visto nas fotos**: o separador que ficava pendurado no fim da linha quando o
  cabeçalho ou a faixa quebravam saiu (o selo já separa); o rodapé da lista mostra "Conectado" em
  cima e o número embaixo; o seletor Conversas/Retenção perdeu os ícones e passou a respeitar o
  próprio padding (o `.segmentado button` comum vencia — lição em `convencoes.md`), e o
  "Retenção 15" deixou de passar por baixo do "+"; a janela de quem nunca escreveu diz "Janela
  fechada: a pessoa ainda não escreveu", sem o "desde sempre"; sem nome, o cabeçalho não repete o
  telefone; as janelas Qual ficha, Encerrar e Como foi perderam a nota do rodapé (ela empurrava o
  botão principal para baixo do Cancelar em 1024; o texto foi para o subtítulo ou para o aviso); e
  o título fixo de grupo da lista fica numa linha (quebrado, a segunda linha aparecia por baixo do
  grupo seguinte); na prévia do modelo, o preenchido se marca por cor e sublinhado, sem a caixinha
  que punha um espaço falso em "Olá, Maria !"; e no celular (≤ 760 px) **Transferir, Buscar e
  Encerrar voltaram** como ícones (estavam escondidos: a secretária não conseguia encerrar nem passar
  a conversa nessa largura), o avatar sai abaixo de 480 px para o nome caber, e a dica do número
  oficial desce para a linha de baixo em vez de quebrar os botões. Medido em 760, 480 e 360 px: as
  cinco ações visíveis, nada vaza, nada rola para o lado. Na busca dentro da conversa, o termo
  achado ganhou sublinhado forte e negrito: no laranja da nota interna o fundo suave sumia.

## 10. @SemDor na tela nova (24/10)

O @SemDor das secretárias (`semdor-crm/docs/semdor-secretarias.md`) na tela nativa, igual ao CRM web: **a IA
lê e redige; quem decide e envia é a secretária** (regra 3 do CRM). A IA continua só no CRM (a chave fica na
VPS 2); o app chama as rotas dele (`semdor-crm/docs/api-para-os-apps.md` §9).

| O que a secretária vê | Onde no código |
|---|---|
| **✨** no rodapé do campo, do tamanho dos vizinhos, com o nome inteiro na dica e no leitor de tela. Campo vazio: a sugestão entra no campo. Com texto: o texto revisado entra, e o **↶** volta ao dela (só enquanto o texto do @SemDor está intacto — depois que ela mexe, voltar apagaria o que escreveu). Nunca envia. "Escrevendo…" com um ponto que pulsa (parado com movimento reduzido). Se ela continuou escrevendo ou passou para a nota interna enquanto a IA pensava, o texto dela vence | `ConversasDoWhatsApp.razor` (`BotoesDoSemDor`, `SugerirComSemDorAsync`, `DesfazerSemDor`); regras em `SemDorNaTela` (`Aplicar`, `PodeDesfazer`, `NomeDoBotao`) |
| **✨ Resumo** no cabeçalho → bloco fechável acima das mensagens: o paciente quer / já foi feito / falta, com "Leitura automática da conversa. Confira antes de agir." O rótulo "Resumo" só aparece com cabeçalho largo (≥ 820 px, `@container`); em 1024 e 1366 fica o ✨, como o buscar — senão o nome do paciente encolhia | `ResumirComSemDorAsync`; `SemDorNaTela.LinhasDoResumo`; `.wa-semdor-resumo` |
| **Etiqueta** na lista (✨ Remarcar, ✨ Dúvida…), no começo da linha da prévia; **vermelha** quando pede atenção (sintoma grave, reação ao tratamento, reclamação). Some quando a clínica responde (o CRM zera; a tela tira na hora em que ela mesma responde ou encerra) | `WhatsAppDaRecepcaoService.Linha` → `SemDorNaTela.Etiqueta`; `Recompor` → `EtiquetaDepoisDaEscrita` |
| **Desligado** no CRM (ou o estado não pôde ser lido): nada disso aparece — nem botão, nem etiqueta, nem texto. **Ligado e falhando**: os botões ficam; o clique mostra a frase do CRM inline ("O @SemDor não respondeu agora. Escreva normalmente…") e o campo fica como estava. A gestão vê o motivo no topo da lista, fechável | `LerAsync` lê `GET /api/semdor/estado` (recusa = desligado, silencioso; falha = desligado + log); `SemDorNaTela.Ligado`, `Falha`, `AvisoDaGestao` |

**Peças**: `IClienteDoCrm` ganhou `EstadoDoSemDorAsync`, `SugerirComSemDorAsync` e `ResumirComSemDorAsync`
(`ClienteDoCrmHttp`: mesmo caminho autenticado, renova no 401/CSRF; prazo próprio de **35 s**, porque o CRM
espera a Anthropic por até 30 s e a escrita comum desiste em 20; o `502` não se repete — cada pedido é uma
chamada paga). `PainelDoWhatsApp.SemDor` (init, lido na leitura inteira e mantido pela releitura de 7 s).
O CRM de mentira responde com textos fixos plausíveis, põe etiquetas em cinco conversas (a da Lúcia, nova
na semente, é a de atenção) e aceita `CLINICA_PREVIA_CRM=semdor-desligado` ou `semdor-falhando`.

**Testes**: `SemDorNaTelaTests` (as regras), `WhatsAppDaRecepcaoServiceTests.SemDor_*` (estado no painel,
etiqueta na linha, desligado/404/fora do ar sem etiqueta, rascunho nulo para sugerir), `ClienteDoCrmHttpTests.O_SemDor_*`
(caminho, csrf, corpo, frase do 409/502, prazo da IA maior que o da escrita), e `SemDorNaTelaDoWhatsAppTests`
(bUnit na `Raiz`, como o `TelasBlazorAbremTests`: etiquetas, ✨ no campo vazio e com texto, ↶, resumo, desligado
sem resquício e falhando com a frase). No CRM, `ApiParaOsAppsTests` cobre as rotas e a etiqueta no `desde`.

**Fotos** (`node tools/PreviaBlazor/fotos-semdor.js [ligado|desligado|falhando]`, aceita `PREVIA_URL`), em
1366 e 1024, em `docs/mockups/whatsapp-nativo/tela-real/`: `50-semdor-lista`, `51-semdor-sugestao`,
`52-semdor-revisao`, `53-semdor-resumo`, `56-semdor-desligado-lista`, `57-semdor-desligado-conversa` e
`58-semdor-falhando`. Duas correções vieram delas: a etiqueta saiu do rodapé da linha (em 300 px ela espremia
o selo da situação até "N…" e empurrava o responsável para fora — o roteiro acusou "vaza do grupo") e o rótulo
"Resumo" passou a ceder por contêiner. Com o ↶ em ≤ 1180 px, a dica "Ctrl + Enter envia" sai para o Enviar
não cair sozinho na linha de baixo.

🧑‍💻 **UX** — a secretária não troca de sistema para ter a ajuda do @SemDor; um toque escreve ou acerta o
texto, e nada sai sem o Enviar dela. A fila mostra o assunto de cada mensagem nova e destaca em vermelho a
que pede atenção. Falhando, ela lê uma frase e escreve como sempre; desligado, a tela é a de antes.
🛠️ **DX** — uma classe de regras (`SemDorNaTela`, pura, com teste), três métodos no cliente, um campo no
painel; no Razor, blocos e métodos novos num lugar só (`BotoesDoSemDor`, `SeloDoSemDor`, o fim do `@code`),
para conviver com as outras mudanças da tela. Classes `wa-semdor-*` e `wa-etiqueta`, só tokens (o violeta é
a cor do @SemDor).
🤝 **CX** — o custo é o do CRM web (uma chamada por toque e por mensagem nova); o risco fica contido: a IA não
vê a ficha nem o prontuário (o CRM manda só o primeiro nome e o texto mascarado), e desligar é uma variável na
VPS 2 — a tela acompanha sem release.

**O que NÃO foi feito, e por quê**

- ~~**A etiqueta de atenção não toca o sino da Recepção.**~~ **Corrigido na junção (24/10):** o sino dos
  apps já recebe os avisos do @SemDor pela ponte (`AvisosDoSemDorShell`, com `CRM_SEMDOR_APPS_ENABLED`); o
  que faltava era o clique, que abria o CRM no navegador. Agora o sino lê o id da conversa do link
  (`AvisosDoSemDorRegras.ConversaDoLink`) e abre a conversa na aba WhatsApp do app
  (`PedidoDeConversa.ConversaId`; teste `PedidoDeConversaDoSinoTests`).
- **Atalho de teclado para o ✨**: o CRM web não tem; ficou de fora para não inventar um.
- **A chamada real à Anthropic** não foi feita aqui (a chave fica na VPS 2), como no CRM: o primeiro uso real
  é em produção, e o aviso da gestão diz o motivo se falhar.
- **A semente ganhou uma conversa** (Lúcia, com a Bruna, sem ficha) para mostrar a etiqueta de atenção: a
  contagem "Todas" das fotos anteriores passa de 8 para 9 abertas na prévia.

## 11. Fase 2 — a conversa ao lado (24/10)

O pedido: *"a conversa vira um painel ao lado da Agenda do dia, da Ficha, de Confirmações e do
Acompanhamento, sem trocar de tela"*. Feito na branch `wa-painel` e juntado depois da fase 3: a aba é
sempre a tela nativa, e o painel só depende de o CRM ter endereço.

### O desenho

Uma **gaveta à direita** (460 px; a tela inteira no celular) com a **mesma tela de conversa** da aba
WhatsApp, no modo painel: sem a lista e sem a coluna da ficha (a tela de trás já é a ficha), direto
na conversa do paciente. Fecha pelo X e pelo Esc; o Tab não sai dela. Trocar de tela pelo menu não
a fecha (a conversa é do paciente, não da tela), menos quando a aba WhatsApp fica à vista.

| Peça | Onde | Teste |
|---|---|---|
| Qual conversa abrir: as LIGADAS à ficha, a mais recente primeiro; sem nenhuma, as do telefone da ficha que ainda não têm ficha (com o aviso de conferir) — nunca a conversa ligada a OUTRA ficha com o mesmo número; as frases do aviso, do seletor e do "sem conversa" | `Application/Servicos/ConversaAoLadoNaTela.cs` | `ConversaAoLadoNaTelaTests` (5) |
| Se o painel serve: o CRM com endereço (a moldura saiu na fase 3) | `WhatsAppDaRecepcaoService.ConversaAoLadoAsync` | `ConversaAoLadoTests` |
| O serviço do painel (Scoped, um por navegador): `AbrirAsync`, `Fechar`, `LigadaAsync` (lida uma vez por minuto para os botões das listas; relida no clique) | `Ui.Blazor/Hospedagem/ConversaAoLado.cs` | idem |
| A gaveta, montada uma vez na `Raiz` e no `PalcoDeTelas`, antes das camadas dos formulários e dos diálogos | `Componentes/CamadaDaConversaAoLado.razor` | idem |
| O modo painel da tela de conversa: o parâmetro `AoLado` (um `PedidoDeConversa`) e três pontos de ligação; o resto num arquivo à parte | `ConversasDoWhatsApp.razor` + `ConversasDoWhatsApp.AoLado.cs`, `TopoDaConversaAoLado.razor`, `SemConversaAoLado.razor` | idem, e o `TelasBlazorAbremTests` clica dentro da gaveta |
| O botão "WhatsApp" das linhas: Agenda do dia, Acompanhamento (onde o passo já não é "Falar no WhatsApp") e o crachá da Ficha do balcão | `Componentes/BotaoDaConversaAoLado.razor` | idem |
| O "Escrever no WhatsApp" que já existia (Confirmações, a aba Convênio da ficha, Retornos a marcar, a janela do horário, a conversa do Acompanhamento) passa a abrir a gaveta | `PedidoDeConversaNoCrm.EscreverAsync` (um ponto só) | idem |
| Onde a aba WhatsApp não existe para quem entrou, o botão some | `IHostDaTela.TemTela` (WPF: `NavegacaoSuite.Existe`; padrão "não") | `O_botao_some_no_app_sem_a_aba_WhatsApp` |
| CSS da gaveta e do modo painel (os `@media` do `conversas.css` olham a JANELA; o modo painel repete o que lá vale em ≤ 760 px) | `Ativos/css/telas/conversa-ao-lado.css` | fotos |

**Paciente sem conversa:** com caso no Acompanhamento, o painel abre o convite da retenção (o mesmo
da aba); sem caso, "Ainda não há conversa com Fulano" e **Começar com um modelo**, que abre a mesma
"Nova conversa" da aba já com o telefone, o nome e a ficha. **Mais de uma conversa** (dois números,
ou WhatsApp e SMS): abre a mais recente e mostra o seletor. **Conversa só do telefone**: o aviso
"Pelo telefone da ficha" fica acima dela, e a ficha não se liga sozinha (diferente do pedido que leva
à aba, que liga): quem confere é a recepção, pelo "Conferir a ficha" que a própria conversa já pede.

Fotos (relógio fixo, `node tools/PreviaBlazor/fotos-conversa-ao-lado.js`): `docs/mockups/whatsapp-nativo/tela-real/`
`50-painel-{agenda,ficha,confirmacoes,acompanhamento}-{1366,1024}.png`, `50-painel-agenda-480.png`,
`51-painel-sem-conversa-*.png` e `52-painel-nova-conversa-*.png`. O roteiro reprova página que rola
para o lado, gaveta que não abriu (ou abriu na conversa errada) e filho que vaza da gaveta.

### As opções comparadas

| Opção | 🧑‍💻 UX | 🛠️ DX | 🤝 CX |
|---|---|---|---|
| Navegar para a aba (o de antes) | troca de tela; voltar perde o lugar na lista | nada novo | a recepção alterna entre telas o dia inteiro |
| **Gaveta POR CIMA, à direita (escolhida)** | a tela de trás não muda de leiaute nem de rolagem; o que fica à esquerda continua clicável; no celular a gaveta é a tela toda | uma camada, um serviço, a mesma tela de conversa; nada depende do leiaute de cada tela | a mesma conversa nas cinco portas; nenhum servidor novo |
| Gaveta que EMPURRA a tela (a tela encolhe 460 px) | nada fica coberto | cada tela teria de caber em ~900 px numa janela de 1366, e os `@media` delas olham a janela: a Agenda do dia já rola por dentro em 1024 | risco de tabela cortada em telas que ninguém fotografou assim |
| Uma segunda tela de conversa, só para o painel | igual à escolhida | duas telas para a mesma pergunta (regra 1), e cada correção feita duas vezes | divergência entre a aba e o painel |

### 🧑‍💻 UX · 🛠️ DX · 🤝 CX

- 🧑‍💻 **UX**
  - Um clique no ícone do WhatsApp da linha (ou no "WhatsApp" do crachá da ficha) abre a conversa
    daquele paciente à direita, no tempo de abrir a aba (a leitura é a mesma, mostrada com o esqueleto). A tela de trás
    fica como estava. Esc ou X fecha. A gaveta só cobre a direita: o cabeçalho da moldura, o menu e o
    lado esquerdo da tela continuam à mão. As notificações e o chat da equipe vão para a esquerda da
    gaveta em vez de cobrir o Enviar.
  - O que a conversa já sabe fazer está lá: responder, nota interna, modelos, respostas rápidas,
    janela de 24 h, assumir, transferir, encerrar, o aviso que pede ação (remarcar, confirmou por
    telefone, conferir a ficha, "Como foi?").
  - Sem o bit `EditarAgenda` o ícone nasce apagado e diz por quê; sem telefone no cadastro, também.
    No app, quem não tem o bit nem tem a aba, e o ícone não aparece.
- 🛠️ **DX**
  - A regra de qual conversa abrir e as frases moram em `ConversaAoLadoNaTela` (puro, com teste); a
    tela de conversa ganhou um parâmetro, um `@class`, dois `@if` e uma linha no `OnInitializedAsync`
    — o resto mora em `ConversasDoWhatsApp.AoLado.cs`, para as outras frentes que mexem na tela não
    esbarrarem.
  - Botão novo que abra a conversa do paciente: `<BotaoDaConversaAoLado PacienteId Nome Telefone />`.
    Botão que já chamava `PedidoDeConversaNoCrm.EscreverAsync` ganhou a gaveta sem mudar.
  - O `TelasBlazorAbremTests` passou a ter a aba WhatsApp no hospedeiro do teste: os ícones aparecem
    e a gaveta é explorada como um formulário. O `PalcoTrocaRepetidaTests` ganhou o CRM de mentira (a
    semente liga a tela nativa, e sem ele a aba ia ao HTTP de verdade e acusava "o CRM não respondeu").
- 🤝 **CX**
  - **Custo:** nenhuma migration, nenhum servidor, nenhuma chamada nova ao CRM (as mesmas da aba).
    Releases: Recepção e Gerente (os dois apps com a aba WhatsApp). O Financeiro e o Consultório
    levam o código (as telas são compartilhadas), mas lá a aba não existe e nada aparece.
  - **Risco:** sem o endereço do CRM, o caminho é o de antes (o wa.me). Defeito no painel se
    corrige com release; até lá, a aba WhatsApp e o CRM no navegador continuam.

### Achado de passagem (corrigido)

Nas fotos, o menu "⋯" de uma linha **cancelada** da Agenda do dia não abria nenhum item: a opacidade
da linha apagada (`tr.apagada td{opacity:.72}`) abria um contexto de empilhamento, e o menu (fixo, z 30)
ficava abaixo do fundo que o fecha (z 25). A célula das ações saiu do apagado (`rec-dia.css`). Lição em
`docs/agentes/convencoes.md`.

### O que NÃO foi feito, e por quê

- **A gaveta não empurra a tela** (ver a comparação). Numa janela de 1024 ela cobre a coluna de ações
  da linha: para abrir a conversa de outro paciente, fecha-se (Esc) e clica-se na outra linha.
- **Rascunho não sobrevive ao fechar** a gaveta nem à troca de paciente nela: vive na tela, como na aba.
  Fechar com texto digitado não pergunta (a gaveta não enxerga o rascunho da conversa sem mexer mais
  na tela que outras frentes estão mudando).
- **Na Ficha do balcão há duas portas** para a mesma conversa: o "WhatsApp" do crachá (novo) e o da
  aba "Convênio, contatos e LGPD" (o de antes, que a tela do paciente do Consultório também usa).
  Tirar o de lá mexeria na tela do Consultório; fica para quando a ficha administrativa for revista.
- **Confirmações não ganhou ícone novo**: os dois botões que ela já tinha (Avisar/Lembrar e o "Abrir a
  conversa" da linha que respondeu) abrem a gaveta. O "Avisar" continua registrando o aviso no clique,
  como já fazia com a aba e com o wa.me — com a tela nativa, o aviso de verdade sai quando a secretária
  envia; alinhar o registro ao envio é mudança de regra da rodada, não de tela.
- **O destino da tela Acompanhamento** (funil e taxa de retorno) continua com a direção: a gaveta não o
  decide, e a fila dela segue igual.
- **Dentro da própria aba WhatsApp**, o "Confirmar pelo WhatsApp" da janela do horário abre a gaveta por
  cima da aba (raro: a conversa já está à vista). Não fechado porque o serviço do painel não conhece a
  tela atual; fecha-se pelo Esc.
- **A suíte no Postgres não rodou**: nada de consulta, coluna ou migration nova (a única leitura nova é
  a configuração que já existia).
