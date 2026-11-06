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
  `Tipo`; `Consentimento == false` ganha o selo "Sem autorização · ligar" (desde 25/10: "Pediu para não
  receber", e `Consentimento` é falso só para quem RECUSOU — §17); só `Tratamento`
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
- Quem não autorizou (`Consentimento == false`; desde 25/10, só quem recusou — §17): o único botão é "Já liguei" → "Como foi a
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
  `AppReply.ContactRecordedByApp` de 24/10, era `35284d966f92…673e`; depois do @SemDor (§10) e do
  `AppSession.RecordingEnabled` (§12) era `7dc0717ed14a…6642`; depois do `AppMessage.Sticker` (§13, a figurinha) é `49d26d079134fede2d2fa122a545c0fed780f55d4725b51e54434c2f22651da3`, o mesmo de `ContratosDoCrmTests` e `ApiParaOsAppsTests`).
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
à aba, que liga): quem confere é a recepção, pelo "Conferir a ficha" que a própria conversa já pede
(desde 25/10, com UMA ficha só, pelo "É {Nome}?" com "Ligar a ficha" em um clique — §17).

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
- ~~**Rascunho não sobrevive ao fechar a gaveta.**~~ **Resolvido na junção (24/10):** os rascunhos são
  da janela (`Hospedagem/RascunhosDoWhatsApp`, Scoped), não da tela: fechar a gaveta ou trocar de paciente
  nela não apaga, e a aba e a gaveta mostram o mesmo texto da mesma conversa. Teste
  `ConversaAoLadoTests.O_rascunho_sobrevive_a_fechar_a_gaveta_e_reabrir` (reprova com o rascunho na tela).
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

## 12. Mídia e áudio (24/10/2026)

Pedido da direção: *"Importantíssimo as possibilidades de ver imagens, ouvir áudios e baixar itens
enviados dentro da tela do CRM no sistema"*. Até aqui o balão mostrava só o nome do arquivo.

| Peça | Onde | Teste |
|---|---|---|
| A regra da mídia: tipo pelo `Type` do CRM, a ROTA por direção (recebida `/api/conversations/{id}/media/{messageId}`, enviada `/api/attachments/{attachmentId}`, sempre `download=true`), o teto de 16 MB, o que vira `data:` (só o formato que a mensagem diz), a duração lida do próprio arquivo (OGG/Opus da nota de voz e WAV), os rótulos ("Foto", "Áudio 0:42", "PDF enviado · 12 KB"), o nome e o filtro para salvar, a prévia da lista ("AUDIO" → "Áudio") e a memória da tela (64 MB, descarta a usada há mais tempo) | `Application/Servicos/MidiaDaConversa.cs` (`MidiaDaConversa`, `MemoriaDeMidia`); a linha do tempo leva `ItemDaConversa.Midia` e, com mídia, o texto é só a legenda | `MidiaDaConversaTests` |
| `IClienteDoCrm.MidiaAsync` (bytes + tipo + nome do Content-Disposition; GET sem csrf, prazo do envio; 401 renova; acima do teto é recusa) e `AnexarAsync(..., gravacao)` (o campo `recording=true` do CRM web) | `ClienteDoCrmHttp`, `WhatsAppDaRecepcaoService.MidiaAsync` | `ClienteDoCrmHttpTests` (7 novos), `WhatsAppDaRecepcaoServiceTests` |
| No balão: foto (miniatura com esqueleto, clique amplia), áudio (tocador do navegador + Baixar), vídeo (capa com ▶, carrega no clique) e documento (cartão com a sigla, Abrir e Baixar); falha com "Tentar de novo" | `Componentes/MidiaNoBalao.razor`, `Formularios/WhatsApp/MidiaAmpliadaForm.razor`, `Telas/Recepcao/ConversasDoWhatsApp.Midia.cs` (a parte da mídia da tela, num arquivo à parte), `css/telas/conversas-midia.css` | `MidiaDoWhatsAppNaTelaTests` (bUnit, CRM de mentira) e o `TelasBlazorAbremTests` |
| Gravar áudio: microfone no rodapé ("Áudio"; só o ícone abaixo de 1180 px), faixa com o tempo, Cancelar e Enviar; até 2 min (para e espera o Enviar); sobe como gravação e sai como resposta com o anexo; falhou o envio, o áudio fica para Enviar de novo com o MESMO anexo e Id | `Componentes/GravadorDeAudio.razor` + `js/gravacao-de-audio.js` (o MediaRecorder do CRM web) | `MidiaDoWhatsAppNaTelaTests` (o navegador simulado: sobe com a gravação e aparece na hora; microfone negado diz o que fazer) |
| O microfone liberado no WebView2 só para a origem do app (a mesma via da câmera da foto do paciente) | `Desktop.Shell/Shell/NavegadorEmbutido.cs` | `compilar-sombra` |
| Prévia: foto (PNG), nota de voz (WAV de 6,5 s) e dois PDFs gerados em código; Roberto manda a foto e o áudio e recebe o PDF; João manda o pedido em PDF e um vídeo que o "provedor" não entrega | `tools/PreviaBlazor/MidiaDeExemplo.cs`, `CrmDeMentira.cs` | fotos 50–55 e 33 (`FOTOS=50,51,52,53,54,55,33`) |

As regras do anexo valem para o áudio sem cópia: o botão só existe onde o Anexar existe (WhatsApp,
janela de 24 h aberta, anexos ligados no CRM); conversa de outra pessoa, janela fechada no meio e
SMS o CRM recusa, e a frase aparece na faixa de recado. Baixar e abrir passam pelo `IHostDaTela`
(`PerguntarOndeSalvarAsync` + `GravarArquivoAsync`; `SalvarEAbrirAsync`), com a barreira da aba
(`EditarAgenda`).

🧑‍💻 **UX** — a recepção vê a foto e ouve o áudio sem sair da conversa; o arquivo chega sozinho
(foto e áudio) ou num clique (vídeo, documento), com esqueleto enquanto chega e "Tentar de novo"
quando falha. A releitura de 7 s não baixa nada de novo (memória da tela). Gravar é um clique,
Enviar outro — nada sai sem o clique. Sem microfone ou sem permissão, a tela diz onde liberar e
lembra do Anexar.

🛠️ **DX** — uma classe de regra com teste (`MidiaDaConversa`) e três componentes pequenos; a tela
ganhou quatro linhas no Razor e um arquivo parcial (`ConversasDoWhatsApp.Midia.cs`), para a
mídia crescer sem conflitar com a conversa. O CRM não mudou (as rotas e o `recording` já
existiam). Três lições novas em `docs/agentes/convencoes.md` (ordem dos campos estáticos, bytes do
navegador em fluxo, `Func` não redesenha a tela).

🤝 **CX** — o pedido da direção, sem trocar de sistema; sem custo novo (a mídia já estava no CRM,
que a guarda e a converte). Risco: a mídia passa pela memória do app (até 64 MB por aba aberta);
o arquivo maior que isso não é guardado e volta a ser pedido no clique.

**O que NÃO foi feito, e por quê:**

- ~~**Saber se o CRM tem o conversor de gravações.**~~ **Feito na junção (24/10):** o `AppSession`
  ganhou `RecordingEnabled` no fim (padrão `false`; o CRM já mandava `recordingEnabled` na
  `/api/session`), os dois contratos idênticos com o SHA-256 novo
  (`7dc0717ed14a…6642`; o atual é o de `ContratosDoCrmTests`). O microfone só aparece com o
  conversor (ffmpeg) no CRM; sem ele, a secretária anexa um arquivo de áudio como antes.
- **Ouvir a gravação antes de enviar** (o cadeado do WhatsApp): gravar → Enviar ou Cancelar, como
  o CRM web. Pedido "simples, mas funcional".
- **Quadro do vídeo como capa**: baixar até 16 MB só para a capa; a capa é escura com ▶.
- **Tamanho do documento antes de baixar**: o `AppMessage` não traz o tamanho; ele aparece depois
  do primeiro Abrir/Baixar.
- **Vídeo que toca na prévia**: gerar MP4 em código não cabe; o vídeo da semente é o que o provedor
  não entregou (a foto do "Tentar de novo"). Na prévia o áudio enviado fica em WebM (o CRM real
  converte para OGG/Opus), e o Chromium mostra "0:00" no tocador até tocar.
- **As fotos antigas da tela** (02–25, 31, 32) não foram refeitas: o rodapé agora tem o microfone.
  Refazer com o roteiro depois da junção com os outros PRs da tela (PNG conflita).
- **Microfone real no Windows**: só a sombra compilou o hospedeiro. Conferir na clínica: abrir uma
  conversa com janela aberta, gravar 3 s e enviar; negar o microfone em Configurações do Windows →
  Privacidade → Microfone e ver a frase.

## 13. Figurinhas e fotos de perfil (25/10/2026)

Pergunta da direção: *"Figurinhas e fotos de perfis estão inclusos?"*. Antes desta rodada a
figurinha chegava como uma foto comum (no balão, com "Ampliar", e "Foto" na lista) e o avatar era
sempre as iniciais.

**Figurinha.** A Meta manda `type: "sticker"` (uma imagem WEBP, às vezes animada). O CRM continua a
gravá-la como `IMAGE` — o app antigo segue mostrando-a como foto, nada quebra no meio da
publicação — e agora com a marca `Sticker`:

| Peça | Onde | Teste |
|---|---|---|
| A marca na entrada (`Incoming.Sticker` → `Message.Sticker`), a prévia da lista "Figurinha" e o "[figurinha]" que o @SemDor lê | semdor-crm: `MetaCloud.cs`, `Store.cs`, `Models.cs`, `Program.cs`, `SemDorSecretaria.cs`; o CRM web a desenha solta (`app.js`, `.message-sticker`) | `MidiaRecebidaTests` (2), `ApiParaOsAppsTests.Figurinha_chega_marcada_na_conversa_e_a_lista_diz_Figurinha`, `SemDorSecretariaTests` |
| O contrato: `AppMessage.Sticker` no FIM, padrão `false` (SHA-256 `49d26d07…51da3`, os dois arquivos idênticos) | `ApiParaOsApps.cs` ≡ `ContratosDoCrm.cs` | `ApiParaOsAppsTests`, `ContratosDoCrmTests` |
| A regra: `MidiaDaMensagem.Figurinha` (imagem com a marca) — carrega, baixa e falha como foto, mas se chama "Figurinha" em toda frase ("Figurinha recebida", "figurinha-….png", "Não foi possível carregar a figurinha.") | `Application/Servicos/MidiaDaConversa.cs` | `MidiaDaConversaTests.Figurinha_e_imagem_que_se_chama_figurinha_em_toda_frase` |
| Na tela: 128 px, sem balão e sem "Ampliar"; a hora numa pílula, como no WhatsApp | `MidiaNoBalao.razor`, `ConversasDoWhatsApp.razor` (`wa-balao-figurinha`), `conversas-midia.css` | `MidiaDoWhatsAppNaTelaTests.A_figurinha_aparece_solta_e_a_ficha_ligada_poe_o_retrato_no_avatar` |

**Foto de perfil.** ⚠️ **A foto do perfil do WhatsApp não existe para a clínica:** a API oficial da
Meta (Cloud API) entrega o nome do perfil (`contacts[].profile.name`, que o CRM já usa) e nunca a
foto. Não há rota da Meta que a devolva a uma empresa; buscá-la por fora seria usar o WhatsApp
fora dos termos — e o número oficial da clínica é o que se arrisca. O que a clínica TEM é o
**retrato da ficha**, tirado no balcão (`Paciente.FotoMiniatura`), e é ele que aparece:

| Peça | Onde | Teste |
|---|---|---|
| As miniaturas de várias fichas numa ida, só a coluna (`ObterMiniaturasPacientesAsync` → `PacienteService.ObterMiniaturasAsync`) | `ClinicaRepositorio.ConsultaMiniaturas`, `IClinicaRepositorio` | `PacienteServiceTests.Miniaturas_vem_so_de_quem_tem_retrato_e_lista_vazia_nao_vai_ao_banco`, `TraducaoNoNpgsqlTests.Miniaturas_traduzem_em_projecao_sem_a_ficha` |
| O avatar com o retrato na lista, na Retenção, no cabeçalho da conversa e no cartão do paciente; o selo do canal continua por cima | `Telas/Recepcao/ConversasDoWhatsApp.Fotos.cs` (`Avatar`, `PedirFotos`), `LinhaDaConversa.PacienteId` (no fim), `conversas.css` (`.avatar.com-foto`) | `MidiaDoWhatsAppNaTelaTests` (Roberto e João com retrato, Carlos nas iniciais) |
| Prévia: retratos DESENHADOS (silhuetas, nunca o rosto de alguém) no João e no Roberto; uma figurinha (rosto amarelo, fundo transparente) que o Roberto manda depois do "Serve sim" | `tools/PreviaBlazor/MidiaDeExemplo.cs`, `Sementes/ConversasDoWhatsApp.cs`, `CrmDeMentira.cs` | fotos 56 e 57 |

As regras do retrato:

- **Só da ficha LIGADA** (`PatientId`, a que a recepção conferiu) — o telefone não é identidade, e
  a candidata ainda não conferida não ganha rosto. Na Retenção, o caso é da própria clínica.
- **Só com `VerFichaPaciente`** — o retrato é da ficha; sem o bit, iniciais.
- **Uma pergunta por ficha por abertura da tela**: a releitura de 7 s não volta ao banco; ficha
  recém-ligada entra na batida seguinte. Retrato trocado no balcão aparece na próxima abertura.
- **Falhou, iniciais**: a falha vai ao log e a tela não muda por ela.

🧑‍💻 **UX** — a figurinha parece figurinha (pequena, solta, sem um "Ampliar" que não serve), e a
lista diz "Figurinha" em vez de "Foto". O rosto da ficha na conversa ajuda a recepção a reconhecer
quem escreve — e ele só aparece depois de a ficha ser conferida, então nunca sugere a pessoa errada.

🛠️ **DX** — figurinha: uma marca no contrato e uma propriedade derivada na regra; nenhum tipo novo
de mídia (o `TipoDeMidia` não ganhou valor, então nenhum `switch` ficou para trás). Retrato: um
método de repositório com teste que o executa e com a rede do Npgsql, e um arquivo parcial da tela.

🤝 **CX** — custo zero (a foto já está no banco, a figurinha no CRM); a leitura extra é de ~10 KB
por ficha, uma vez por abertura da tela. Ordem de publicação: CRM primeiro (o app antigo ignora o
`sticker` e continua mostrando a figurinha como foto); o app novo com o CRM antigo mostra a
figurinha como foto e a lista "Foto", como antes.

**O que NÃO foi feito, e por quê:**

- **A foto do perfil do WhatsApp**: não há como (acima). Se um dia a Meta a oferecer, ela entra
  como mais uma fonte do mesmo `Avatar`, atrás do retrato da ficha.
- **Mandar figurinha**: a Cloud API aceita, mas a figurinha precisa ser WEBP 512×512, e o pedido é
  "simples, mas funcional" — a recepção responde com texto e emoji.
- **Nada a fazer na figurinha animada**: o WebView2 (Chromium) anima o WEBP sozinho, como o WhatsApp.

## 14. A tela diz a verdade: regras, frases e transferência (25/10/2026)

O pedido da direção depois das revisões de 25/10 (`3-casos-de-uso.md`, R1–R7 e D11; `2-profissional.md`, itens 6, 7,
15, 16 e 22): *"corrigir todas, sem perguntas, profissional, sem falhas nem redundâncias"*. Esta frente cuidou das
REGRAS e das FRASES; a aparência, o teclado, a lista e a linha do tempo foram de outra frente, e as capacidades novas
(marcar pela conversa, guardar arquivo na ficha, perguntar ao profissional, presença e passar o turno), de uma terceira.

| Peça | Onde | Teste |
|---|---|---|
| **R1** — Confirmar e "Não vem" pela conversa sem a rodada de Confirmações: o contato de confirmação do horário é obtido ou criado com a MESMA origem `AGD:{id}` da ponte (`ClinicAgendaBridge`), num `SaveChanges` só com a resposta; só para horário marcado e futuro. O cartão do horário tem **Confirmar** e **Não vem** sempre que a ficha está conferida e o horário é futuro | `CampanhaService.RegistrarRespostaDoHorarioAsync` (+ `ContatoDoHorarioAsync`), `WhatsAppDaRecepcaoService.ConfirmouPorTelefoneAsync`/`NaoVemAsync`/`Horarios`, o cartão em `ConversasDoWhatsApp.razor` | `WhatsAppDaRecepcaoServiceTests.Confirmar_e_nao_vem_sem_rodada_criam_o_contato_do_horario_e_o_aviso_chega_ao_sino`, `Horario_que_ja_passou_ou_saiu_da_agenda_nao_ganha_resposta_inventada`, `Horario_futuro_sem_rodada_tem_confirmar_e_nao_vem_na_coluna` |
| **R2** — "Sem resposta" com a regra do CRM: o lembrete AUTOMÁTICO (gravado pela ponte com `RodadaDeConfirmacao.LembreteAutomatico`, sem link) pede resposta como o link do SMS pedia. O lembrete à mão continua fora. A frase do painel do Gerente que falava de SMS e link foi junto | `AvisosDeConfirmacao` (`ContatoParaAvisoDeConfirmacao.PedeResposta`), a projeção em `ClinicaRepositorio.ConsultaContatosParaAvisosDeConfirmacao`, a ponte usa a constante, `PainelDirecaoService` | `AvisosDeConfirmacaoTests.O_lembrete_automatico_do_WhatsApp_pede_resposta_e_o_manual_nao`, `TraducaoNoNpgsqlTests.Contatos_para_avisos_de_confirmacao_traduzem_sem_foto` (agora com `EnviadoPor`) |
| **R3** — as frases que prometiam o que não acontece: (a) Transferir para uma PESSOA é "pedido enviado; passa quando ela aceitar" (o setor passa na hora), no formulário e na notificação; (b) a remarcação só promete o lembrete com o automático LIGADO (`AppSession.RemindersEnabled`) e sessão a partir de amanhã; (c) Confirmações não fala mais do SMS com link; (d) o envio de documento diz "aparece na conversa do paciente no WhatsApp da clínica", não "CRM → Envios do Clínico" | `TransferirConversaForm.razor`, `ConversasNaTela.FraseDaTransferencia`, `WhatsAppDaRecepcaoService.RascunhoDeRemarcacao`, `ConfirmacoesDeAgenda.razor`, `EnvioAoPacienteService.QuandoSair`; no grep pedido ("SMS", "lembrete", "CRM →"): o aviso "A resposta pelo SMS não cancela", o "SMS sem resposta desde" do sino e o alerta do Gerente | `ConversasNaTelaTests.A_frase_da_transferencia_diz_se_foi_pedido_ou_se_passou`, `WhatsAppDaRecepcaoServiceTests.Rascunho_de_remarcacao_diz_o_que_a_clinica_fez_e_so_promete_o_lembrete_ligado`, `AvisosDeConfirmacaoTests` (as frases) |
| **R7** — Aceitar/Recusar a transferência na tela: o pedido pendente no contrato (`AppPendingTransfer` no fim de `AppConversation` e `AppConversationSummary`), a rota `POST /api/transfers/{id}/{accept\|reject\|cancel}`; no app, `IClienteDoCrm.DecidirTransferenciaAsync`, o aviso "Fulana quer passar esta conversa para você · Aceitar / Recusar" (antes de tudo, para quem recebe), "Aguardando o aceite de Fulana · Cancelar o pedido" (para quem pediu, depois dos avisos de confirmação), o selo "Transferência para você" / "Aguardando aceite · Fulana", Transferir apagado com pedido pendente, e o formulário com pessoa OU setor, sem nada marcado (item 20) | `ApiParaOsApps.cs` ≡ `ContratosDoCrm.cs`, `Store.PendingTransferOf` (CRM), `ClienteDoCrmHttp`, `WhatsAppDaRecepcaoService.Aceitar/Recusar/CancelarTransferenciaAsync`, `ConversasNaTela.PedeAcao/Selo`, o CRM de mentira | `ApiParaOsAppsTests.Transferir_para_uma_pessoa_vira_pedido_*`, `Quem_pediu_cancela_*`, `Sessao_diz_se_o_lembrete_automatico_esta_ligado`; `ClienteDoCrmHttpTests.A_decisao_da_transferencia_*`, `A_recusa_da_transferencia_*`; `ConversasNaTelaTests.O_pedido_de_transferencia_*`, `O_selo_diz_*`; `WhatsAppDaRecepcaoServiceTests.Transferir_para_uma_pessoa_*`, `Quem_recebe_o_pedido_*`; `RegrasDoWhatsAppNaTelaTests` (bUnit: aceitar, pedir, cancelar) |
| **D11** — o "avisado" do Avisar de Confirmações é gravado no ENVIO do modelo de lembrete pela conversa (o horário é o primeiro marcado e futuro da ficha conferida — o mesmo que preenche o "Dia e hora"), não no clique que abre a conversa; o lembrete repetido não apaga a resposta que o paciente já deu. Pelo wa.me do computador o clique continua sendo o registro (o app não vê o envio). Fechada a gaveta, Confirmações relê | `CampanhaService.RegistrarLembreteDoHorarioAsync`, `ModeloDoWhatsApp.Lembretes`, `WhatsAppDaRecepcaoService.HorarioDoLembrete` + `EnviarModeloAsync(..., lembreteDoHorario)`, `ConfirmacoesDeAgenda.razor` | `WhatsAppDaRecepcaoServiceTests.O_lembrete_enviado_pela_conversa_marca_o_horario_como_avisado_e_o_clique_sozinho_nao`, `O_lembrete_repetido_nao_apaga_a_resposta_que_o_paciente_ja_deu` |
| **D11 (configuração)** — ligar o pedido de confirmação automático: recomendado, com o custo (mensagem de utilidade cobrada pela Meta) | `docs/whatsapp-nativo-roteiro-da-manha.md` §4.1 | — |
| **R6** — "Para resolver" na coluna do paciente: os alertas da Ficha do balcão (`ElegibilidadeService` → `FichaAdministrativaNaTela.AlertaDe`), lidos DEPOIS de a conversa estar desenhada, uma vez por troca de paciente, com o bit da Ficha do balcão (`VerFichaPaciente` — o serviço não filtra; sem o bit nada é lido); três estados (lendo, não conferiu, lista/"Nada pendente na ficha") | `WhatsAppDaRecepcaoService.ParaResolverAsync`, `DadosDaConversa.ParaResolver` (no fim), `ConversasDoWhatsApp.ParaResolver.cs`, `Componentes/ParaResolverNaConversa.razor` | `WhatsAppDaRecepcaoServiceTests.Para_resolver_traz_os_alertas_da_ficha_do_balcao_e_nada_sem_o_bit_da_ficha`; a semente (Maria: carteirinha vence em 20 dias) |
| **Item 6** — uma porta por pergunta: o que o aviso da conversa já mostra sai da coluna, BOTÃO A BOTÃO e só para aquele horário (Cancelar continua); o selo da ficha no cabeçalho é estado quando o aviso tem "Conferir a ficha"/"Buscar ficha"; "Cadastrar paciente" é o nome único; o mesmo verbo nos dois lugares ("Confirmar", "Manter o horário") | `ConversasNaTela.AvisoOferece`, `RotuloDeCadastrar`, `ConversasDoWhatsApp.razor` | `ConversasNaTelaTests.O_que_o_aviso_oferece_sai_da_coluna_so_para_aquele_horario`, `RegrasDoWhatsAppNaTelaTests.Sem_resposta_e_a_mesma_palavra_*` |
| **Item 7** — "Sem resposta" na linha, na coluna e no aviso, de um lugar só | `RodadaDeConfirmacao.SemResposta` | `ConversasNaTelaTests.Sem_resposta_e_pediu_cancelar_pedem_os_botoes_da_agenda`, bUnit |
| **Item 15** — o canal certo do lembrete (a semente do Carlos tinha o contato como SMS); o nome de quem fez o contato do caso (não o login); "Responsável pelo acompanhamento" rotulado (e a grade de pares com o rótulo em no máximo 45%); conversa sem telefone sem "WhatsApp", janela e selo do canal; a semente com a Bruna Reis dos dois lados | `WhatsAppDaRecepcaoService.ComNomeDeQuemFez`, `ConversaDoWhatsApp.SemTelefone`, `conversas.css` (`.wa-pares`), `Sementes/ConversasDoWhatsApp.cs` | `WhatsAppDaRecepcaoServiceTests.O_caso_sem_telefone_abre_sem_WhatsApp_e_o_contato_mostra_o_nome_de_quem_fez`, bUnit (o canal do Carlos) |
| **Item 16** — sem ficha conferida, o título é o telefone e o nome do perfil vem rotulado ("Terezinha Souza (nome no WhatsApp)"; no SMS, "nome na conversa"); as iniciais viram "?" | `WhatsAppDaRecepcaoService.Compor`, `ConversaDoWhatsApp.NomeNoWhatsApp`/`NomeDoPerfil` | `Sem_ficha_conferida_o_titulo_e_o_telefone_e_o_nome_do_perfil_vai_rotulado`, bUnit (Lúcia) |
| **Item 22** — "Como foi?" com botões neutros, e na coluna só depois de um convite ou ligação (`Tentativas > 0`) e quando o aviso ainda não o mostra | `RetencaoNaTela.BotoesDoComoFoi`/`ComoFoiNaColuna` | `RetencaoNaTelaTests.Os_cinco_como_foi_*`, `O_como_foi_da_coluna_so_aparece_*` |
| **Item 8** — a fila do recall se renova quando a aba lê a Retenção, uma vez por dia no app (dividida com a tela Acompanhamento), pelo MESMO `GerarRecallAsync` (que não duplica caso); só para quem tem `GerenciarCampanhas`; a falha vai ao log e libera o dia | `RecallDoDia` (singleton), `WhatsAppDaRecepcaoService.RetencaoAsync`, `AcompanhamentoDePacientes.razor` | `WhatsAppDaRecepcaoServiceTests.Ler_a_aba_renova_a_fila_do_recall_uma_vez_por_dia_sem_duplicar_caso` |

**O contrato** (os dois arquivos idênticos, SHA-256 `dbbf45cdc079cf755db7d9f39ef3d1c078ed61d28d89901a18d0a7db33cd7ad9` nas duas
constantes): `AppSession.RemindersEnabled`, `AppConversationSummary.PendingTransfer`, `AppConversation.PendingTransfer`
(todos no fim, com padrão) e os records `AppPendingTransfer`, `AppTransferDecision`, `AppTransferDecided`. A metade do CRM
está em `semdor-crm/docs/api-para-os-apps.md` §11. **Ordem de publicação: CRM primeiro**; o app novo com o CRM antigo
não mostra Aceitar nem promete lembrete (o comportamento seguro).

**Fotos** (relógio fixo, `FOTOS=60,61,62,63,64,65,66,67 node tools/PreviaBlazor/fotos-whatsapp.js`), em 1366 e 1024, em
`docs/mockups/whatsapp-nativo/tela-real/`: `60-transferencia-para-voce`, `61-transferir-pessoa-ou-setor`,
`62-aguardando-aceite`, `63-sem-resposta-uma-palavra`, `64-para-resolver`, `65-nome-no-whatsapp`,
`66-retencao-sem-telefone` e `67-retencao-responsavel`. Três correções vieram delas: o rótulo "Responsável pelo
acompanhamento" espremia os valores até uma letra por linha (a grade de pares passou a limitar o rótulo); a primeira
versão do item 6 escondia o Cancelar do horário (passou a ser botão a botão); e, em 1024, o formulário de Transferir
continuava aberto depois de gravar até a batida de 7 s — os formulários da tela a chamam por `Func`, e fechar não a
redesenhava (`FecharForm` agora redesenha; vale para todos os formulários da tela). Lições em `docs/agentes/convencoes.md`.

### 🧑‍💻 UX · 🛠️ DX · 🤝 CX

- 🧑‍💻 **UX** — o paciente diz "confirmo" na conversa e a secretária confirma ali, com ou sem rodada (antes: Confirmações
  → Gerar rodada → achar a linha). O "Avisar" deixa de marcar avisado quem não recebeu nada. Quem recebe uma
  transferência aceita na própria tela; quem pede vê que está esperando. A coluna responde "a carteirinha venceu?", "tem
  guia glosada?" sem abrir a ficha. Cada estado tem uma palavra, cada gesto uma porta, e nenhuma frase ao paciente
  promete lembrete que não sai.
- 🛠️ **DX** — as regras moram na Application, puras e com teste (`AvisoOferece`, `ComoFoiNaColuna`, `PedeAcao`/`Selo`
  com o pedido, `HorarioDoLembrete`, `FraseDaTransferencia`); a tela ganhou um arquivo à parte (`ParaResolver.cs`) e
  um componente, para conviver com as outras frentes. Nenhuma migration; uma condição na projeção do sino (com a rede
  do Npgsql); um método no cliente do CRM; um singleton de uma data.
- 🤝 **CX** — sem custo novo ligado por padrão; ligar o lembrete automático é decisão com custo (Meta, utilidade) e
  está no roteiro como recomendado. A passagem entre pessoas deixa de ficar pendurada até alguém abrir o CRM web, e o
  "não vem" contado na conversa chega ao painel do Gerente. Releases: CRM primeiro, depois Recepção e Gerente.

### O que NÃO foi feito, e por quê

- **A pílula da AGENDA continua "Não confirmou".** Trocar para "Sem resposta" no app inteiro tornaria as videoaulas e o
  catálogo de treinamento (que citam "Não confirmou") uma aula sobre uma tela que não existe mais. A palavra nova vale
  no WhatsApp, de um lugar só (`RodadaDeConfirmacao.SemResposta`); a agenda troca quando as aulas forem regravadas.
- **O lembrete à mão não vira "sem resposta" no sino**, e o CRM web o mostra como "sem resposta" (ele não sabe quem
  mandou o lembrete: a ponte só diz "notified"). Alinhar exige a ponte dizer, no snapshot, se o lembrete pede resposta
  (`ClinicContracts.cs`, outro contrato) — ficou fora desta rodada; a regra do app segue a decisão de que ali quem
  julga é a recepção.
- **"Fulana está nesta conversa"** (presença na conversa), **passar o turno** e **aviso de espera fora da aba** são da
  frente de capacidades novas.
- **O pedido para uma pessoa de outra fila** só aparece para quem vê aquela fila (`AccessPolicy` do CRM); a recepção
  trabalha numa fila só.
- **O cabeçalho em si** (rótulos "Com você", ✓ do encerrar — item 5) é da frente de aparência. Desta frente entrou só
  uma regra nele: "Assumir" nasce apagado quando a conversa está com OUTRA pessoa (o CRM recusaria com 409; o caminho é
  ela transferir, e quem recebe aceita no aviso), e "Transferir" apagado com um pedido pendente.
- **A suíte no Postgres não rodou aqui** (os três agentes disputariam o mesmo banco-modelo): a consulta do sino mudou e
  a tradução está na rede do Npgsql; quem junta as branches roda `CLINICA_TESTES_POSTGRES`.

## 15. Acabamento profissional da tela (25/10/2026)

A revisão "a aba WhatsApp está profissional?" (nota 7/10: nada bloqueava; incomodavam o ruído, o jargão e
o teclado) virou esta rodada. A direção pediu "corrigir todas, sem perguntas, sem falhas nem
redundâncias". Esta parte cuidou da LISTA, da LINHA DO TEMPO, do CABEÇALHO, do TECLADO, dos MODELOS, da
MÍDIA e das FALHAS; as regras da coluna "Do sistema da clínica" e dos avisos que pedem ação (itens 6, 7,
15, 16 e 22 da revisão) e as capacidades novas são de outras frentes da mesma rodada.

| Item da revisão | O que mudou | Onde | Teste |
|---|---|---|---|
| 1 · modelos com nome técnico e `{{1}}` | Nome da clínica por modelo ("Confirmação da consulta"; o técnico só na dica); desconhecido sem prefixo e sem `_`. `{{n}}` vira `[nome]`, `[data]`… pelos MESMOS rótulos do formulário. Ordem: os que podem sair primeiro; com horário HOJE, os de confirmação; no caso aberto da retenção, os convites. Sem o selo de categoria (o "Marketing" saiu da tela). A notificação e o recado do convite também falam pelo nome da clínica | `ModeloDoWhatsApp.NomeNaTela`, `TextoNaLista`, `Ordenar` | `ModeloDoWhatsAppNaListaTests` |
| 2 e 11 · eventos tomando a conversa | O "pendente" sai quando a agenda já registrou o fim (cada resposta da agenda resolve um); "assumiu/reabriu ao responder" sai quando o balão ao lado, a menos de 2 min, é da mesma pessoa; eventos seguidos no mesmo minuto viram uma nota, com o tom mais forte. O evento é texto cinza de 12 px centralizado, sem caixa nem ícone (o de atenção, na cor do aviso). As mensagens assentam perto do campo (`margin-top:auto`), e o caso sem conversa não mostra "Hoje · …" sozinho | `ConversasNaTela.Acabamento.cs` (`Natureza`, `Limpar`), `LinhaDoTempo`, `WhatsAppDaRecepcaoService.Compor`; `.wa-evento` | `ConversasNaTelaAcabamentoTests` (5) |
| 3 · erro do CRM e do provedor cru na tela | Mídia: "O WhatsApp não entregou este vídeo. Peça à pessoa que o envie de novo." / "Sem ligação com o WhatsApp da clínica agora: …" / acima de 16 MB; a frase do CRM vai ao `Diagnostico`. Mídia que falhou perde o Baixar e o Abrir; o "Tentar de novo" só quando tentar pode resolver (sem ligação). Escritas, modelos, anexo, áudio e nova conversa: `FalhaNaEscrita` ("Sem ligação com o WhatsApp da clínica agora. Nada foi alterado; …"). A mensagem que não saiu diz "Não entregue. Confira o número ou envie de novo." em vez do texto do provedor. O @SemDor sem ligação diz a frase dele, não a do transporte | `MidiaDaConversa.Falha`, `ConversasNaTela.FalhaNaEscrita`/`ErroDaEntrega`, `SemDorNaTela.Falha`, `MidiaNoBalao` (`PodeTentarDeNovo`) | `MidiaDaConversaTests`, `MidiaDoWhatsAppNaTelaTests` (o vídeo do João), `SemDorNaLinhaTests` |
| 4 · teclado | Enter envia (a nota, guarda) e Shift+Enter quebra a linha — o C# decide o envio (`EnviaPeloEnter`), o `js/conversa-do-whatsapp.js` só impede a quebra que o Enter faria. Foco no campo ao abrir a conversa e no campo da busca ao abri-la; Esc fecha a busca, os popovers (Modelos, Respostas rápidas) e o ⓘ da retenção — na gaveta, sem fechar a gaveta junto. A dica do teclado mora no próprio campo ("Mensagem · Enter envia, Shift+Enter quebra a linha"), em toda largura | `ConversasDoWhatsApp.Acabamento.cs`, `js/conversa-do-whatsapp.js` | `AcabamentoDoWhatsAppNaTelaTests` (Enter/Shift+Enter, Esc) |
| 5 · cabeçalho ambíguo | "Com você" virou **Devolver à fila** (ícone de seta para trás); **Encerrar** com o nome a partir de 1180 px; **Resumo** com o nome a partir de 640 px de cabeçalho (em 1366, sim). Para o nome do paciente caber, o Transferir fica no ícone ⇄ (com a dica) abaixo de 820 px de cabeçalho, os botões são compactos e o nome ganha reticências de verdade | `ConversasDoWhatsApp.razor`, `conversas.css`, `Icone` (`devolver`) | `AcabamentoDoWhatsAppNaTelaTests` |
| 8 · selos cortados | O selo nunca encolhe; quem cede é o responsável. Textos curtos: "Sem ficha", "2 fichas", "Autorizou", "Sem autorização" (desde 25/10: "Pode convidar", "Pediu para não receber" — §17), "Sem telefone", "Indicado ao BSV", "Respondeu", "Aguardando · 30/10", "Respondeu ao convite"; os títulos dos grupos da retenção cabem numa linha. Sem responsável, a linha não escreve "Sem responsável" (o selo e a aba "Novas" já dizem) | `ConversasNaTela.Selo`, `RetencaoNaTela.Selo`/`Agrupar`, `.wa-linha-rodape` | `ConversasNaTelaTests`, `RetencaoNaTelaTests`, `WhatsAppDaRecepcaoServiceTests`, fotos (o roteiro mede selo cortado) |
| 9 · ✨ repetindo o selo | A etiqueta de assunto só aparece com o selo neutro da clínica; a de atenção, sempre | `SemDorNaTela.EtiquetaNaLinha` | `SemDorNaLinhaTests`, `SemDorNaTelaDoWhatsAppTests` |
| 10 · notificação cobrindo o Enviar | A sugestão já aparece no campo (com o ↶): nenhuma notificação. A que não entrou (ela continuou escrevendo) diz por quê no recado, acima do campo | `SugerirComSemDorAsync` | `SemDorNaTelaDoWhatsAppTests` |
| 12 · vazios fora de contexto | Sem conversa aberta, a coluna da ficha não ocupa lugar (o centro fica com ela). Um vazio por modo: "Escolha uma conversa" / "Escolha um paciente para preparar o convite", sem "7 segundos" | `ConversasNaTela.SemConversaAberta`, `.pagina.wa.sem-conversa` | `ConversasNaTelaAcabamentoTests`, `AcabamentoDoWhatsAppNaTelaTests` |
| 13 · rodapé da retenção | O rodapé da lista é o estado da ligação nos dois modos ("Conectado", "Sem ligação desde 10:14", "WhatsApp desligado"); a explicação foi para o ⓘ "Quem aparece aqui?" ao lado da busca | `ConversasNaTela.EstadoDaLigacao`, `RetencaoNaTela.QuemApareceAqui` | idem |
| 14 · ligação que cai no meio do uso | Faixa amarela no topo da conversa: "Sem ligação com o WhatsApp desde 10:14. O que você enviar agora pode não sair; tentando de novo…" — some na próxima leitura que der certo. Abertura que falha: "Sem ligação com o WhatsApp da clínica agora. Enquanto isso, atenda pelo celular da recepção e avise o suporte." (a falha do próprio app diz outra coisa) | `ConversasNaTela.FaixaSemLigacao`, `FalhaDeAbertura`, `FalhaDaConversa`; `_semLigacaoDesde` | `ConversasNaTelaAcabamentoTests`, `AcabamentoDoWhatsAppNaTelaTests` (o CRM cai e ela tenta enviar) |
| 17 · tocador de áudio | Botão de velocidade à vista (1× → 1,5× → 2×; o `playbackRate` só se alcança por JS) e sem "KB" no rodapé do áudio | `MidiaDaConversa.ProximaVelocidade`/`RotuloDaVelocidade`/`Descricao`, `MidiaNoBalao` | `MidiaDaConversaTests`, `AcabamentoDoWhatsAppNaTelaTests` |
| 18 · hora da figurinha | Estava à direita, mas solta embaixo (parecia centralizada): agora fica no canto de baixo à direita, por cima da figurinha, como no WhatsApp | `conversas-midia.css` | foto 56 |
| 19 · contraste | Rodapé dos balões verdes e texto secundário da linha selecionada no `--cinza-700` (9,3:1; antes 4,4 e 4,3) | `conversas.css` | medido no roteiro |
| 20 · Transferir pré-marcado | Ninguém vem marcado; o Transferir nasce desligado | `TransferirConversaForm` | `AcabamentoDoWhatsAppNaTelaTests` |
| 21 · gaveta sem marcar a linha | A linha do paciente da gaveta fica destacada (o botão dela leva `.aberta`), e um véu leve — a sombra da própria gaveta, que não pega clique — diz que o que ficou na borda está POR TRÁS dela | `BotaoDaConversaAoLado`, `conversa-ao-lado.css` | fotos 50-painel |
| 23 · cabeçalhos fixos da retenção | Cada grupo num bloco: o título fixo vale só dentro do próprio grupo | `ConversasDoWhatsApp.razor` (`.wa-grupo-bloco`) | fotos |
| 24 · botões do modelo | Confirmar · Remarcar · Cancelar (o `AppTemplate.Buttons`) desenhados como etiquetas sob o balão que saiu pelo modelo. A lista de modelos do número é lida UMA vez por aba (a mesma do popover) | `ModeloDoWhatsApp.Botoes`, `PedirModelosDosBotoes` | `ModeloDoWhatsAppNaListaTests`, `AcabamentoDoWhatsAppNaTelaTests` |
| 25 · não lidas × "N novas" | Conferido: a mesma fonte. A divisória da conversa sai das não lidas da LINHA no clique (`RecebidasJaLidas`), e a linha zera ao abrir — como o WhatsApp, que mantém a divisória enquanto a conversa está aberta. A foto da revisão tinha a Maria lida antes por outra foto da mesma prévia | — | `ConversasNaTelaTests.A_divisoria_conta_as_nao_lidas…` |
| R4 · quem espera | No canto da hora da linha: "há 2 h" (laranja a partir de 1 h) e, faltando menos de 4 h para a janela fechar, "fecha às 14:10"; a frase inteira ("espera há 2 h", "janela fecha às 14:10") na dica e no leitor de tela. Só quando a última fala é do paciente, aberta e fora do menu; respondeu, a linha deixa de esperar NA HORA (a prévia passa a ser o que a clínica mandou, com o tick — `LinhaDepoisDaEscrita`, antes um `with` no Razor). Em "Novas", a mais ANTIGA primeiro, com o que pede ação (selo vermelho ou laranja) na frente | `ConversasNaTela.Espera`, `Filtrar`, `OrdenarAsNovas`, `LinhaDepoisDaEscrita`; `WhatsAppDaRecepcaoService.Linhas`/`Linha` | `ConversasNaTelaAcabamentoTests` (5) |

**Fotos** (`docs/mockups/whatsapp-nativo/tela-real/`, refeitas com prévia recém-iniciada no relógio fixo):
`fotos-whatsapp.js` (01–25, 30–33, 50–57), `fotos-conversa-ao-lado.js` (50–52-painel) e `fotos-semdor.js`
(50–58-semdor). O roteiro do WhatsApp passou a achar o modelo pelo nome da clínica ("Horário remarcado") e
ganhou duas reprovações: **selo cortado na lista** e **tela rolada para o lado por dentro** (`scrollLeft > 0`
em qualquer elemento). A segunda nasceu provada: o foco no campo ao abrir a conversa, no celular, rolava a
`.pagina.wa` (`overflow:hidden` também rola) enquanto a conversa ainda deslizava — a foto 31 saiu com a
coluna da ficha à mostra e o `scrollWidth` da página não acusou. Corrigido com `FocusAsync(preventScroll: true)`;
lição em `docs/agentes/convencoes.md`. As fotos que não saem de roteiro e mudaram (03, 20, 22-fora-do-ar, 26
e 40, tiradas à mão) foram refeitas com o `foto.js` nos mesmos passos; as de "não configurado" (22 e 41) não
mudaram.

### 🧑‍💻 UX · 🛠️ DX · 🤝 CX

- 🧑‍💻 **UX** — A conversa é a conversa: os balões têm o peso, o sistema é nota de rodapé, e o que se
  repetia sumiu. A secretária digita e aperta Enter, como no WhatsApp Web; abre uma conversa e já
  escreve; Esc fecha o que abriu. A fila diz quem espera e há quanto tempo, e a janela que vai fechar
  antes de virar mensagem paga. O cabeçalho diz o que cada botão FAZ. Os modelos se chamam como a
  clínica fala e vêm na ordem do que serve agora. Quando a ligação cai, ela vê na hora, no topo da
  conversa, e sabe que o que enviar pode não sair; quando a tela nem abre, sabe o que fazer enquanto isso.
- 🛠️ **DX** — Tudo o que a tela afirma de novo é função pura com teste: `ConversasNaTela.Acabamento.cs`
  (linha do tempo limpa, espera, ordem de "Novas", frases de falha), `ModeloDoWhatsApp` (nome, texto,
  ordem, botões), `MidiaDaConversa` (falha, velocidade), `SemDorNaTela.EtiquetaNaLinha`. Na tela, um
  arquivo parcial novo (`ConversasDoWhatsApp.Acabamento.cs`: teclas, foco, faixa, botões do modelo) e
  mudanças localizadas no Razor, para a junção com as outras frentes ser limpa. Um módulo JS pequeno, só
  para o que o Blazor não alcança (impedir a quebra do Enter; `playbackRate`). Três lições em
  `docs/agentes/convencoes.md` (tecla: estado × própria tecla; regra compacta no fim do CSS; `focus()` que
  rola contêiner `overflow:hidden`).
- 🤝 **CX** — Parece ferramenta de atendimento madura: sem `semdor_confirmacao_24h`, sem `{{1}}`, sem
  "CRM" nas frases, sem a frase do provedor. Menos modelos pagos por janela perdida (a fila avisa antes).
  Custo: uma leitura a mais de `/api/templates` por número e por abertura da aba, quando a conversa tem
  mensagem de modelo. Nada de migration, contrato ou servidor; releases: Recepção e Gerente.

### O que NÃO foi feito, e por quê

- **Itens 6, 7, 15, 16 e 22** (a mesma ação em dois lugares, uma palavra por estado, contradições da
  semente, "Responsável por…", "Como foi?") são da frente das REGRAS e da COLUNA, na mesma rodada.
  Consequência visível aqui até a junção: a coluna do caso mostra "Mensagens: autorizou" e
  "Situação: Autorizou" (o selo encurtado), e o aviso de "sem ficha" ainda diz "Novo contato".
- **O "Abra uma conversa…" da coluna da ficha** ficou inalcançável (a coluna some sem conversa), mas
  não foi apagado: o bloco é da coluna, e apagar no meio dela conflitaria com a outra frente. Sai na junção.
- **A espera conta da ÚLTIMA mensagem do paciente**, não da primeira sem resposta: o contrato não traz
  `waitingSince`, e o pedido era sem campo novo. Quem escreveu sábado e de novo segunda aparece "há 2 h".
- **"Mais antigas primeiro" é a ordem de "Novas", não uma opção**: duas ordens para a mesma fila seriam
  duas respostas à mesma pergunta.
- **O detalhe do provedor numa mensagem que não saiu não vai ao log**: não é exceção da tela, é dado do
  CRM, e registrá-lo a cada releitura de 7 s encheria o log. Ele continua no CRM web.
- **Tocador de áudio próprio** (onda, arrastar): ficou o do navegador com a velocidade à vista —
  "simples, mas funcional".
- **A lista de modelos não se relê sozinha**: o modelo aprovado ou pausado na Meta durante o dia aparece
  na próxima abertura da aba (como já era no popover).
- **A suíte no Postgres não rodou**: nenhuma consulta, coluna ou migration nova.

## 16. A conversa resolve: marcar, guardar, perguntar, avisar e passar o turno (25/10/2026)

O pedido da direção, depois da revisão dos casos de uso (R5, R8, R9, R10 e D12): *"corrigir todas, sem
perguntas, profissional, sem falhas nem redundâncias"*, com duas decisões — **o app passa a contar como
conectado no CRM** e **a troca de turno vira um gesto**. Cada item tira a secretária de uma ida a outra tela
ou de uma dependência da memória; nada sai ao paciente sem o Enviar dela.

| Peça | Onde | Teste |
|---|---|---|
| **R5 — Marcar horário pela conversa.** Com a ficha conferida, a seção Horários aparece SEMPRE (antes, só com horário marcado) e oferece "Marcar horário" — o formulário da AGENDA (`AgendamentoForm`, ganhou `PacienteId`: a ficha travada) em modal, sem trocar de tela; o profissional sugerido é o da próxima sessão ou o do caso. Na volta, a tela compara os horários (o número de agendamento novo) e, com a janela aberta e o campo vazio, deixa "Pronto, Maria! Marquei a sua acupuntura para …". Cancelar pela conversa também deixa "Maria, cancelei o seu horário de …". Nenhuma frase promete lembrete | `RascunhosDaConversa` (Application); `ConversasDoWhatsApp.Resolve.cs`, `Componentes/MarcarHorarioNaConversa.razor`, `Formularios/Recepcao/AgendamentoForm.razor` | `ConversaResolveTests` (frases, horário novo), `ConversaResolveNaTelaTests` (o formulário abre com a ficha travada; o cancelamento deixa o rascunho e nada é enviado) |
| **R8 — Paciente esperando vira aviso fora da aba.** A regra `ConversasNaTela.PedemResposta`: conversa aberta, fora do menu automático, última mensagem do paciente, sem resposta há 5 min ou mais, e sem responsável ou de quem está logado. O leitor é da JANELA (a aba para o próprio relógio quando sai de vista): a lista inteira ao abrir e a cada 10 min, e no meio o `?desde=` a cada 60 s. O número aparece no item "WhatsApp" do menu (e no grupo do submenu, no Gerente); o sino ganha a seção "Esperando resposta no WhatsApp", UMA linha por espera (a chave carrega a hora da mensagem: a mesma espera não pisca de novo a cada minuto; a mensagem nova depois da resposta é outra), e o clique abre a conversa pela `PedidoDeConversa.ConversaId`. A notificação passageira sai só para a espera NOVA, fora da aba; a primeira leitura semeia (abrir o app não é rajada) | `ConversasNaTela.Espera.cs`, `VigiaDoWhatsApp` (Application); `Desktop.Shell/Shell/EsperaNoWhatsAppShell.cs` (relógio, sino, marcas de leitura em `LeituraDoSinoShell`), `MolduraDaSuite` (o número e a seção), `Moldura/MolduraDoApp.razor` + `Contratos.cs` (`ItemDaBarra.Contador`, `EsperaNoSino`) | `EsperaNoWhatsAppTests` (a regra, as frases, o vigia sobre o CRM de mentira: semeia, anuncia uma vez, desligado sem endereço, fora do ar sobe), `MolduraBlazorTests.Quem_espera_resposta_poe_o_numero_no_menu…`; o shell só pela `compilar-sombra` |
| **R9 — "Guardar na ficha" o arquivo recebido.** No balão da foto ou do documento que o PACIENTE mandou (a figurinha, não): um `Modal` com o que é (Carteirinha, Pedido médico, Exame, Outro) e a data do documento (obrigatória, nunca futura). Grava pelo `AnexoPacienteService` (o mesmo teto, a trilha no mesmo SaveChanges, o operador de quem entrou, "não se apaga, cancela-se com motivo"), com a observação "Recebido pelo WhatsApp da clínica.", e deixa a nota interna "Guardado na ficha por …" quando a conversa é de quem guardou (a nota no CRM é de quem está com a conversa). Os bytes vêm da memória da tela. **A permissão é a das outras portas de anexar** (a aba Prontuário da ficha, a Enfermagem, o Consultório, o tablet): `EditarProntuario` ou `RegistrarEvolucaoEnfermagem` — duas barreiras, e só com a ficha CONFERIDA | `GuardarNaFichaService` (Application); `Componentes/MidiaNoBalao.razor`, `Formularios/WhatsApp/GuardarNaFichaForm.razor` | `ConversaResolveTests` (regras, o arquivo na ficha com a trilha e o operador, as recusas sem nada gravado), `ConversaResolveNaTelaTests` (a foto do Roberto vai à ficha dele e a nota fica na conversa) |
| **R10 — "Perguntar ao profissional".** Ao lado do balão do paciente (aparece ao passar o mouse ou com o foco; sempre, em tela de toque): um `Modal` que escolhe quem responde (sugestão: o profissional da próxima sessão; sem ela, o do caso; a secretária troca) e manda pelo CHAT DA EQUIPE uma direta com o trecho citado e o paciente ANEXADO (só com a ficha conferida e `VerFichaPaciente`, a barreira do chat). Nada sai do banco da clínica e não há IA. O formulário diz numa linha que o relato não entra no prontuário | `PerguntaAoProfissionalService` (Application); `Componentes/AcoesDaMensagemRecebida.razor`, `Formularios/WhatsApp/PerguntarAoProfissionalForm.razor` | `ConversaResolveTests` (quem pode responder, a sugestão, o texto com teto, a direta gravada com o paciente), `ConversaResolveNaTelaTests` (a mensagem da Lúcia chega à direta) |
| **D12 — Presença: o app conta como conectado.** O pulso `POST /api/presence` a cada 30 s (vale 120 s) enquanto a pessoa está logada, para quem tem a aba e com o CRM configurado. O contrato ganhou só `AppPresence` no fim (SHA-256 `ea3d21f73d7e5898a6453b81afbe2f566723f32e08760ad4870f38fd6f2b3c52`, os dois arquivos idênticos). O vigia para no `PararAvisosGlobais`, antes do `SairDoCrm` da janela, e cancela o pulso em voo | `IClienteDoCrm.PulsoDePresencaAsync`, `ClienteDoCrmHttp`, `VigiaDoWhatsApp`, `EsperaNoWhatsAppShell` | `ClienteDoCrmHttpTests.O_pulso_de_presenca_vai_com_o_csrf_e_renova_a_sessao_vencida`, `EsperaNoWhatsAppTests.O_pulso_vai_ao_CRM_e_para_de_vez…`; no CRM, `ApiParaOsAppsTests.Pulso_de_presenca_do_app_segue_o_contrato_e_acaba_com_a_saida` |
| **D12 — "Passar o turno".** No filtro "Minhas", a faixa "N conversas com você · Passar o turno": um `Modal` com a lista do que vai e a nota obrigatória (5 letras ou mais), e cada conversa volta à fila dela, uma a uma, sem parar no primeiro erro. No 409 relê e repete UMA vez, só se a conversa ainda é de quem sai. Todas passaram: fecha com "N conversas voltaram à fila"; alguma ficou: o formulário fica aberto com o relato e só as que ficaram | `TurnoNoWhatsApp`, `WhatsAppDaRecepcaoService.Turno.cs` (Application); `Componentes/BarraDoTurno.razor`, `Formularios/WhatsApp/PassarOTurnoForm.razor` | `ConversaResolveTests` (a lista, a nota, o relato), `WhatsAppDaRecepcaoServiceTests.Passar_o_turno_*` (a mesma nota em todas, a falha no meio, o 409), `ConversaResolveNaTelaTests` (as quatro da Ana voltam à fila) |

CSS em `Ativos/css/telas/conversas-resolve.css` (só tokens, prefixo `wa-`). A prévia: o CRM de mentira responde ao
pulso (`Pulsos`) e ganhou `ChegouDoPaciente` (os testes da espera); a página da aula com `?espera=true` liga o
número e a seção do sino na moldura (sem o parâmetro, as videoaulas continuam com o sino quieto).

**Fotos** (relógio fixo, `PREVIA_URL=… FOTOS=70,…,79 node tools/PreviaBlazor/fotos-whatsapp.js`; a 78 pede a
prévia com `CLINICA_NAVEGACAO` apontando para um `navegacao.json` com o app `recepcao` e a chave `whatsapp-crm`),
em 1366 e 1024, em `docs/mockups/whatsapp-nativo/tela-real/`: `70-marcar-horario`, `71-marcar-sem-horario`,
`72-guardar-na-ficha`, `73-perguntar-acao`, `74-perguntar-ao-profissional`, `75-passar-o-turno-faixa`,
`76-passar-o-turno-form`, `78-contador-no-menu`, `78-sino-esperando`; os fluxos executados (1366):
`77-cancelou-rascunho` e `79-turno-passado`.

### Achados no caminho (corrigidos)

- **O "Com você → Devolver à fila" ia pela rota da GESTÃO.** O app chamava `POST …/reassign`, que no CRM é o
  "Reorganizar" e recusa com 403 quem não é admin nem supervisora ("Somente a gestão pode reorganizar…"); o
  CRM de mentira aceitava porque a "ana" da prévia é supervisora. A dona devolve pelo `/transfer` sem
  responsável e para a mesma fila, como o CRM web. O "Passar o turno" usa a mesma devolução. Lição em
  `docs/agentes/convencoes.md`.
- **Os formulários chamam a tela por `Func`, e `Func` não redesenha a tela.** Na foto 79 a notificação dizia
  "4 conversas voltaram à fila" com o formulário ainda aberto e a lista velha — até a releitura de 7 s. Os três
  formulários desta rodada passam por `Redesenhando(...)`; os testes da tela esperam o fechamento em 3 s (com
  20 s, a releitura escondia o defeito; com 3 s, eles reprovaram antes da correção).

### 🧑‍💻 UX · 🛠️ DX · 🤝 CX

- 🧑‍💻 **UX**
  - O paciente novo é atendido numa tela: cadastrar, conferir, **marcar** e escrever "Marquei a sua …" sem ir à
    Agenda (de ~12–15 cliques em 3 telas para ~5 numa). O cancelamento também deixa a mensagem pronta.
  - Quem espera resposta aparece com a secretária em outra tela: o número no "WhatsApp" do menu, a linha no sino
    e uma notificação para a espera nova — sem repetir a cada minuto.
  - A carteirinha e o pedido médico vão do balão à ficha em 3 cliques (antes ~8–10, passando pelo disco).
  - A pergunta clínica chega a quem trata em 2 cliques, com a ficha anexada e o trecho citado.
  - A troca de turno é um gesto: uma nota, e todas as conversas voltam à fila; o que não passou é dito, conversa
    por conversa.
- 🛠️ **DX**
  - As regras moram na Application, puras e com teste: `PedemResposta`, `RascunhosDaConversa`, `TurnoNoWhatsApp`,
    `GuardarNaFichaService`, `PerguntaAoProfissionalService`, `VigiaDoWhatsApp`. O Razor ganhou só pontos de
    ligação (a seção Horários, o balão recebido, o filtro "Minhas", a camada dos formulários e o rascunho do
    cancelar); o resto mora em `ConversasDoWhatsApp.Resolve.cs` e em componentes novos.
  - `ConversasNaTela` e `WhatsAppDaRecepcaoService` viraram `partial` para as partes novas ficarem em arquivos
    próprios (`ConversasNaTela.Espera.cs`, `WhatsAppDaRecepcaoService.Turno.cs`).
  - Um record no fim do contrato (`AppPresence`) e um método no `IClienteDoCrm`; nenhuma rota nova no CRM.
- 🤝 **CX**
  - **Custo**: por posto logado com a aba, um POST pequeno a cada 30 s (o CRM web manda a cada 25 s) e um
    `?desde=` a cada 60 s; a lista inteira a cada 10 min. Nenhuma migration, nenhuma tabela nova.
  - **Mudança de operação (decidida pela direção)**: com o app novo, quem tem a aba WhatsApp (Recepção e Gerente)
    passa a contar como conectado, e a distribuição automática do CRM passa a entregar conversas a essas pessoas
    (setor, folga, expediente e limite continuam valendo). O painel "responsável sem sinal" da gestão deixa de ser
    ruído.
  - **Conformidade**: o arquivo guardado é registro clínico (`AnexoPaciente`, já na exportação e na guarda), não
    se apaga, cancela-se com motivo, com trilha e o login de quem guardou; a pergunta ao profissional não sai do
    banco da clínica (art. 33 não se aplica) e não vira prontuário.
  - **Publicação**: Recepção e Gerente (os apps com a aba). O CRM não precisa de release para o pulso (a rota já
    existia); o contrato novo só dá nome à resposta.

### O que NÃO foi feito, e por quê

- **O perfil padrão da Recepção não guarda na ficha.** A permissão é a das outras portas de anexar arquivo à
  ficha (dado de saúde, art. 5º, II; regra 5 da conformidade). O botão aparece apagado e diz por quê; quem
  decide liberar é a direção, em Acessos (por pessoa).
- **O relato do paciente não vira registro de prontuário** (decisão de 25/10): registrar é ato médico; quem
  trata lê no chat e registra, se for o caso.
- **A conversa de OUTRA colega não conta na espera.** Avisar a todos sobre a conversa de alguém ensinaria a
  ignorar o aviso; quem sai sem passar o turno aparece agora, com o pulso, no painel "responsável sem sinal" da
  gestão.
- **A espera conta da ÚLTIMA mensagem do paciente**, que é o que o contrato traz (`LastIncoming`): com três
  mensagens seguidas, o aviso sai cinco minutos depois da terceira. O `waitingSince` do CRM não está no
  contrato; entra quando a fila "mais antigas primeiro" (R4) precisar dele.
- **"Passar o turno" devolve à FILA, não a uma pessoa.** Transferir para uma pessoa é um pedido com aceite
  (R7, outra frente); o turno não espera aceite. O resumo do @SemDor por conversa na passagem e o "Reorganizar"
  da gestão na tela ficaram de fora: o CRM web faz o segundo, e a nota única cobre o primeiro.
- **O "Marcar horário" não usa o `IMarcacaoDeHorario`** (a aba Marcar da Recepção): ele troca de tela, e a
  volta à conversa dependeria de lembrar de voltar. O formulário da agenda em modal tem as mesmas regras e
  avisos de guia, e a tela compara os horários na hora em que ele grava.
- **Os outros formulários da aba** (Transferir, Encerrar, Cancelar horário, Conferir ficha, Como foi, Nova
  conversa) também chamam a tela por `Func` e podem ficar abertos até a releitura de 7 s depois de gravar — o
  mesmo defeito da foto 79. Não mexidos aqui (são de outras frentes desta rodada); a correção é a mesma
  (`Redesenhando`).
- **A suíte no Postgres não rodou**: nenhuma consulta nova (a equipe vem do `UsuariosAsync` que já existia;
  o anexo e a mensagem do chat são os serviços de sempre).
- **No Windows** (só a `compilar-sombra` compilou o shell): conferir o número no "WhatsApp" do menu, a seção do
  sino, o clique que abre a conversa e, na gestão do CRM, a secretária do app como conectada; e que, ao sair do
  app, ela deixa de aparecer conectada.


## 17. Mensagens sem trava, ficha em um clique e o telefone com 0 (25/10/2026)

Três decisões da direção do mesmo dia. A primeira muda a regra da fase 3 e está explicada, com a
comparação e o UX · DX · CX, em `docs/fase3-recepcao-crm.md` §6; aqui fica o que ela muda nesta tela
e as outras duas.

### 17.1 Sem registro de autorização conta como autorizado

- A tela (retenção, aviso da conversa, cartão do paciente) e o Acompanhamento leem
  `IClinicaRepositorio.PacientesComMensagensLiberadasAsync`: só quem RECUSOU (último registro
  `ComunicacaoEMarketing` não vigente — "Não" no termo, PARAR no CRM, revogação na ficha) fica sem
  convite. As campanhas do Gerente (`CampanhaService`) continuam exigindo o "Sim".
- Os textos: "não autorizou mensagens" → **"pediu para não receber mensagens"**; o selo da retenção
  "Autorizou" / "Sem autorização" → **"Pode convidar"** / **"Pediu para não receber"**; o cartão diz
  **"Liberadas"** ou **"Pediu para não receber · a ação é ligar"**; o título do convite não diz mais
  "e autorizou mensagens".
- O PARAR que chega pelo CRM (`ClinicRecallBridge.OptOut`) grava a recusa também para quem nunca
  respondeu; sem a linha, ele continuaria "liberado".

### 17.2 Ficha única em um clique

Quando só UMA ficha tem o telefone da conversa, o aviso deixa de ser "Uma ficha tem este telefone" +
"Conferir a ficha" (que abria a janela "Qual ficha é esta pessoa?" com uma linha só) e passa a ser
**"É {Nome}?"**, com o nascimento e o fim do documento, e dois botões: **"Ligar a ficha"**
(`BotaoDaAcao.LigarFicha`, liga direto pelo `LigarFichaAsync`) e **"É outra pessoa"** (a busca).
Com duas ou mais fichas, segue "Conferir a ficha" e a janela.

A regra 4 da direção não mudou: o telefone não é identidade, e quem confere é a recepção — ela
pergunta à pessoa e clica. O que saiu foi a janela no meio, que só repetia o que o aviso já podia
mostrar. Regra em `ConversasNaTela` (o aviso que pede ação), com teste.

### 17.3 Telefone com 0 na frente

Bug visto na clínica: a ficha com "022998999334" ia para a Meta como estava, e a mensagem não era
entregue (o WhatsApp do celular entende o mesmo número como +55 22 99899-9334). Novo
`Telefone.SemPrefixoDeDiscagem` (Clinica.Domain): tira o 0 do interurbano e, com ele, o código de
operadora ("0 15 22 …"); "00" (internacional) e número sem 0 ficam como estão. Aplicado em:

| Onde | Para quê |
|---|---|
| `ClinicAgendaBridge.Phone` (ponte) | o número que vai ao CRM: 55 + DDD + número, também no PARAR e no recall |
| `ConversasNaTela.TelefoneNaTela` e `MesmoTelefone` | mostrar "(22) 99899-9334" e casar a conversa com a ficha que guarda o 0 |
| `ConversaNoCrm.Telefone` | o número da conversa nova aberta pelo app |
| `WhatsAppDaRecepcaoService.Variantes` | a busca da ficha no banco compara dígitos, então inclui a forma "0" + DDD + número |

Testes: `TelefoneTests` (`SemPrefixoDeDiscagem`, `Ponte_manda_o_numero_com_o_55_sem_o_zero`,
`A_conversa_acha_a_ficha_que_guarda_o_zero`).

⚠️ **As conversas que o CRM já abriu com o número errado ficam lá** (não há migração de conversa); as
novas vão para 55 + DDD + número. A ficha não é corrigida: o número gravado continua como a
recepção digitou.

### 🧑‍💻 UX · 🛠️ DX · 🤝 CX

- 🧑‍💻 **UX** — a retenção deixa de mostrar "Sem autorização · ligar" em quase todo mundo; ligar a
  ficha única é um clique em vez de três (aviso → janela → escolher → ligar); a mensagem para quem
  tem "0" na ficha chega, e o telefone aparece no formato de sempre.
- 🛠️ **DX** — uma pergunta nova no repositório (com o nome que a separa da das campanhas), um botão
  novo no enum (`LigarFicha`) e uma função de domínio usada nas quatro portas por onde o telefone
  passa. Telefone novo que vá ao CRM ou compare com a ficha passa por `SemPrefixoDeDiscagem` antes
  de pôr o 55 — a lição está em `docs/agentes/convencoes.md`.
- 🤝 **CX** — mensagem que não chegava passa a chegar, sem a recepção reescrever fichas. Nenhuma
  migration, contrato idêntico. Publicar: a ponte na VPS 1 (o número e o recall do CRM) e os apps
  Recepção e Gerente.

### O que NÃO foi feito, e por quê

- **Corrigir os telefones gravados com 0** nas fichas: é dado da clínica, e a regra é não reescrevê-lo
  sem pedido. A normalização acontece na saída.
- **Juntar as conversas antigas do número errado** com as novas: são registros do CRM, e a Meta nunca
  entregou nada para aquele número; ficam como histórico.
- **A dúvida da autorização de quem nunca passou por um termo do BSV**: `docs/fase3-recepcao-crm.md`
  §6.5.


## 18. O botão direito: o menu de contexto (25/10/2026)

O pedido da direção: *"possíveis interações com o clique no botão direito"* na aba WhatsApp. Até aqui o
botão direito não fazia nada de útil na tela (abria o menu do próprio WebView2). Agora ele abre um menu
em dois lugares: numa **conversa da lista** e numa **mensagem** (o balão recebido ou enviado; notas
internas e eventos da trilha não têm menu). Nada sai ao paciente sem a pessoa confirmar, e não há regra
nova: cada item leva ao que a tela já fazia.

| Peça | Onde | Teste |
|---|---|---|
| **A regra** — qual item aparece, em que ordem, e por que um deles está desabilitado | `MenuDoWhatsApp` (Application): `ItemDeMenu<TAcao>` (ação, rótulo, ícone, `Motivo`, `Perigo`, `GrupoNovo`; habilitado = sem motivo), `AcaoNaConversa`, `AcaoNaMensagem`, `AcessoNoMenu` (o bit de escrever, `VerFichaPaciente`, as permissões do `GuardarNaFichaService`, o @SemDor ligado), `DaConversa`, `DaMensagem`, `MotivoParaNaoReenviar` e os textos `ComDataEHora`, `ComecoDaNota`, `DetalhesDaEntrega` | `MenuDoWhatsAppTests` (11 casos: a fila, a minha, a de outra pessoa, a encerrada, a leitura, a ficha, citar, reenviar, a mídia, os textos e a citação) |
| **O componente** — desenha, posiciona, foca e fecha | `Componentes/MenuDeContexto.razor` (genérico em `TAcao`), `Ativos/js/menu-de-contexto.js` (`posicionar`, as setas, `selecaoEm`), `.menu-contexto*` em `clinica.css` (só tokens) | pelos testes da tela, abaixo |
| **A ligação na tela** | `Telas/Recepcao/ConversasDoWhatsApp.MenuDeContexto.cs` (arquivo próprio); no Razor, só o `@oncontextmenu` da linha e do balão (com `data-mensagem` para achar a seleção) e os dois `<MenuDeContexto>` | `RegrasDoWhatsAppNaTelaTests.Botao_direito_na_lista_mostra_o_menu_da_conversa_e_marca_como_nao_lida` e `Botao_direito_na_mensagem_responde_citando_e_a_citacao_vai_ao_CRM` |
| **A citação** ("Responder citando") | `CitacaoNaConversa` + `ConversasNaTela.Citacao` (o primeiro nome de quem escreveu e o começo do texto, até `LetrasDaCitacao` = 90 letras; sem texto, o tipo do arquivo); `ItemDaConversa.Citacao` (o balão mostra o que cita, quando a citada está na conversa); `RascunhoDaConversa.Citada` (a faixa "Respondendo a …" acima do campo, com × para tirar; nota interna não cita); `WhatsAppDaRecepcaoService.ResponderAsync(..., citada)` manda o `ReplyToId` que o contrato já tinha (no SMS, recusa) | `MenuDoWhatsAppTests.A_citacao_mostra_quem_escreveu_e_o_comeco_e_o_balao_a_carrega`; o teste da mensagem acima (o `ReplyToId` chega ao CRM de mentira) |
| **A leitura** | `WhatsAppDaRecepcaoService.MarcarNaoLidaAsync` (→ `IClienteDoCrm.MarcarNaoLidaAsync`, `POST /api/conversations/{id}/unread`) e `MarcarLidaAsync` (lê a conversa e manda o `/read` de sempre, sem abri-la na tela); `ClienteDoCrmHttp` e o CRM de mentira da prévia | o teste da lista acima |

**Fotos** (`tools/PreviaBlazor/fotos-whatsapp.js`), em 1366 e 1024, em
`docs/mockups/whatsapp-nativo/tela-real/`: `80-menu-da-conversa`, `81-menu-da-mensagem`,
`82-responder-citando`, `83-menu-no-canto` (o menu aberto perto da borda, puxado para dentro) e
`84-respondeu-citando`.

### O menu de uma conversa da lista

"Sem o bit" é sempre a dica **"Seu acesso não alcança escrever no WhatsApp"**; "só com você" é
**"Só quem está com a conversa: assuma antes"** (`MenuDoWhatsApp.SoComVoce`).

| Item | Quando aparece | Quando vem desabilitado (a dica) |
|---|---|---|
| Abrir | sempre | nunca |
| Reabrir | conversa encerrada | sem o bit |
| Devolver à fila | aberta e com quem vê | sem o bit |
| Assumir | aberta e não está com quem vê | sem o bit; com outra pessoa: "Está com outra pessoa: peça a transferência" |
| Transferir… | aberta | sem o bit; "só com você"; já há pedido: "Já há um pedido de transferência aguardando o aceite" |
| Nota interna | aberta | sem o bit; "só com você" |
| Marcar como lida | há não lidas | nunca (a marca de leitura é de quem vê: não pede o bit) |
| Marcar como não lida | não há não lidas | sem recebida: "Ainda não há mensagem recebida" |
| Copiar telefone | sempre | "Sem telefone" |
| Abrir a ficha | a conversa tem ficha ligada | sem `VerFichaPaciente`: "Seu acesso não alcança a ficha do paciente" |
| Ligar a ficha… | sem ficha ligada | sem o bit |
| Resumo do @SemDor | o @SemDor ligado | sem o bit |
| Encerrar… (vermelho) | aberta | sem o bit; "só com você" |

### O menu de uma mensagem

"Janela" é a de 24 h do WhatsApp; "com você" é a conversa estar com quem vê.

| Item | Quando aparece | Quando vem desabilitado (a dica) |
|---|---|---|
| Responder citando | sempre | sem o bit; SMS: "O SMS não cita mensagens"; janela fechada: "Janela de 24 h fechada: só modelo, que não cita"; sem id: "Esta mensagem ainda não tem o número do WhatsApp" |
| Copiar a seleção | só com texto selecionado DENTRO deste balão no instante do clique | — |
| Copiar o texto | sempre | "A mensagem não tem texto" (foto sem legenda, áudio) |
| Copiar com data e hora | sempre | idem; copia "[21/10 07:34] Alice: Confirmar" |
| Reenviar… | enviada que **falhou** ou que o WhatsApp **não confirmou** | sem o bit; modelo: "Modelo se reenvia escolhendo de novo em Modelos"; arquivo: "Arquivo se reenvia anexando de novo"; sem texto; janela fechada: "Janela de 24 h fechada: só modelo"; "só com você" |
| Usar como rascunho | enviada | sem o bit; sem texto ou modelo: "Só texto escrito vira rascunho (modelo se escolhe em Modelos)" |
| Salvar o arquivo… | a mensagem tem mídia | — |
| Guardar na ficha | mídia que o paciente mandou | sem ficha: "Ligue a ficha antes: o telefone não é identidade"; sem a permissão: "Guardar na ficha pede "editar prontuário" (Acessos)" |
| Nota interna sobre esta mensagem | sempre | sem o bit; "só com você". Com o campo vazio, a nota começa com "Sobre a mensagem das 17:02 ("…"): " |
| Detalhes da entrega | enviada com tick | — (um aviso com Situação, Enviada, Por, Modelo e O que fazer) |

### As decisões

- **Desabilitado com o motivo, em vez de sumir.** Item que some ensina que a ação não existe; o item
  apagado com a dica ensina onde ela mora e o que falta ("assuma antes", "ligue a ficha antes"). É o
  desenho dos botões da conversa (§16). Só some o que não faz sentido naquele objeto: "Reabrir" numa
  conversa aberta, "Reenviar" numa mensagem entregue, "Guardar na ficha" sem arquivo.
- **`aria-disabled`, e não `disabled`.** O botão `disabled` não mostra o `title` no Chromium (o
  WebView2): a dica, que é a razão de o item estar ali, não apareceria. Com `aria-disabled="true"` o
  leitor de tela anuncia "indisponível", o CSS apaga o item, o componente ignora o clique
  (`EscolherAsync` confere `Habilitado`) e as setas o pulam.
- **O menu é ATALHO, nunca uma segunda regra.** A regra 1 ("uma porta por pergunta") vale para a
  decisão: cada item chama o MESMO handler do botão do topo, do aviso ou do balão (`AssumirAsync`,
  `DevolverAsync`, os formulários de Transferir e Encerrar, `AbrirGuardar`, `BaixarMidiaAsync`,
  `ResumirComSemDorAsync`…), com o mesmo `Exigir`. Na lista, o que precisa da conversa aberta primeiro
  a abre e depois chama o handler; copiar o telefone, abrir a ficha e a leitura não abrem nada.
- **Reenviar é um envio NOVO, com pergunta.** O CRM nunca repete sozinho um envio incerto (é o que
  torna o envio idempotente pelo `IdDoEnvio`): mandar de novo com o mesmo id não reenviaria nada. O
  reenvio usa um id novo e, por isso, se a primeira tiver chegado, a pessoa recebe duas — a confirmação
  diz isso ("Se a primeira chegou, a pessoa recebe duas vezes. Confira com ela antes, se puder."). Só
  texto escrito, com a janela aberta e a conversa com quem vê; modelo e arquivo se reenviam pelo
  caminho deles.
- **Marcar como não lida fecha a conversa, se ela estiver aberta.** Aberta, a próxima leitura a
  marcaria lida de novo, e o clique não teria efeito visível. A lista acompanha na hora (a linha volta
  a ter não lida) e a releitura confirma. O `/unread` volta só a ÚLTIMA recebida, só para quem pediu;
  a colega não vê diferença.
- **"Copiar a seleção" só com seleção dentro daquele balão.** O JS (`selecaoEm`) confere que a seleção
  está dentro do `[data-mensagem]` clicado: texto selecionado noutro lugar da tela não vira um item
  que copia outra coisa.
- **Posição e foco no JS, depois de desenhar.** O menu nasce invisível no ponto do clique; o
  `posicionar` mede, puxa para dentro da janela (margem de 8 px), mostra e foca o primeiro item
  habilitado. Fecha no Esc, no Tab, no clique ou botão direito fora, na rolagem do mouse e sempre
  depois de escolher.

### Dependência do CRM: publique o CRM ANTES dos apps

"Marcar como não lida" usa a rota nova **`POST /api/conversations/{id}/unread`**, implementada no
`semdor-crm` na branch `claude/marcar-como-nao-lida` (commit `51ad693`): a marca de leitura de quem
pede vira recebidas − 1; 400 sem recebida; não muda a versão da conversa nem entra na auditoria; o
`?desde=` passa a trazer a conversa de quem mudou a marca. O contrato (`ApiParaOsApps.cs`) **não
mudou** (mesmo SHA-256), e a citação usa o `ReplyToId` que ele já tinha.

**Ordem:** juntar e publicar o CRM na VPS 2, depois Recepção e Gerente. Com o app novo e o CRM
antigo, o resto do menu funciona e só "Marcar como não lida" falha (404; a tela diz que nada foi
alterado). "Marcar como lida" usa o `/read` de sempre e não depende da publicação.

### 🧑‍💻 UX · 🛠️ DX · 🤝 CX

- 🧑‍💻 **UX**
  - O que a secretária faz numa conversa sem precisar lê-la (copiar o telefone, abrir a ficha, marcar
    como lida ou não lida) é um clique a partir da lista; o resto abre a conversa e já faz a ação.
  - Responder citando é o gesto do WhatsApp do celular: a faixa "Respondendo a Roberto" acima do campo,
    o × para desistir, e o paciente recebe a resposta citada. O balão da tela mostra a citação também.
  - Mensagem que não saiu se reenvia sem redigitar, com a pergunta que evita a duplicata às cegas.
  - O item apagado diz por quê, no próprio menu. Aberto o menu, o teclado anda nele (setas, Home, End,
    Enter; Esc e Tab fecham), e o leitor de tela ouve "Ações da conversa" / "Ações da mensagem".
- 🛠️ **DX**
  - A regra mora em `MenuDoWhatsApp` (Application, pura, com teste); a tela ganhou um arquivo próprio
    (`ConversasDoWhatsApp.MenuDeContexto.cs`) e, no Razor, dois `@oncontextmenu` e dois componentes.
  - `MenuDeContexto` é componente da casa, genérico em `TAcao`, com o JS e o CSS dele
    (`docs/telas-blazor.md` §4): o próximo menu de contexto não redesenha nada.
  - Um método novo no `IClienteDoCrm` (`MarcarNaoLidaAsync`), sem mudar o contrato. Lição em
    `docs/agentes/convencoes.md` ("Menu de contexto no Blazor…").
- 🤝 **CX**
  - **Custo**: nenhum envio sem a pessoa clicar; o reenvio é UMA mensagem, com confirmação. "Marcar
    como lida" pela lista lê a conversa e marca (uma ida e volta a mais que abri-la).
  - **Risco**: o reenvio pode duplicar se a primeira tiver chegado — por isso a pergunta, e por isso
    só para "falhou" e "não confirmada". Nenhuma migration.
  - **Publicação**: o CRM na VPS 2 (a rota `/unread`) primeiro, depois Recepção e Gerente.

### O que NÃO foi feito, e por quê

- **As linhas do modo Retenção não têm menu.** Só a lista de Conversas ganhou o `@oncontextmenu`; o
  caso da retenção é outra pergunta (o Acompanhamento), com os botões dele na coluna. Aberto o caso,
  os balões da conversa têm o menu da mensagem como sempre.
- **Notas internas e eventos da trilha não têm menu**: não são mensagem ao paciente.
- **Não há atalho de teclado para ABRIR o menu** (Shift+F10, a tecla Menu). O componente não trata
  essas teclas, e se o WebView2 dispara o `contextmenu` na linha focada não foi conferido; o balão
  nem recebe foco. Aberto o menu, o teclado anda nele.
- **O foco não volta à linha ou ao balão depois de fechar.** Quem abriu pelo mouse não sente; quem
  usa teclado recomeça do topo.
- **Toque longo (tela de toque) não abre o menu.** O toque segue com os botões que já aparecem sempre.
- **"Perguntar ao profissional" e "Marcar horário" não estão no menu**: já são um botão ao lado do
  balão e na coluna da ficha.
- **Reenviar modelo ou arquivo**: o modelo se escolhe de novo em Modelos e o arquivo se anexa de novo
  — reenviar por aqui pularia as conferências desses caminhos.
