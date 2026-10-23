# O canal de SMS (out/2026) — ⛔ SAIU na fase 3 Recepção×CRM (23/10/2026)

> **Decisão da direção (unificação Recepção×CRM, fase 3):** o CRM é o ÚNICO que fala com o
> paciente. A confirmação do horário é **só pelo WhatsApp, no dia** (o pedido do CRM às 06:00), e
> o SMS existe só no CRM — na conversa por SMS, no recall por SMS e quando o WhatsApp falha.
> Mapa da fase e comparação das opções: `docs/fase3-recepcao-crm.md`.

## 0. O que saiu, o que ficou

| Peça | Situação |
|---|---|
| Lembrete de SMS 24h antes (`LembreteSmsService.RodarAsync`), relógio dos apps (`RelogioDoSms`) e do servidor (`RelogioDoSmsNoServidor`, chave `LembreteSms__Ligado` na ponte e na web) | **saiu** — apague `LembreteSms__Ligado` do ambiente da ponte na VPS 1 (sem leitor, ela é ignorada) |
| Configuração do provedor no Clinica (Relacionamento → Canal de SMS → Configuração; chaves `Sms*` em `Configuracoes`; `src/Clinica.Application/Sms/`) | **saiu** — o provedor é o do CRM (`CRM_SMS_*`, VPS 2). As linhas `Sms*` (com o token cifrado) ficam no banco sem leitor: nenhuma migration apaga configuração da clínica |
| Campanha de SMS (prévia, público por convênio/modalidade, envio) | **saiu** — o CRM não é ferramenta de marketing (`semdor-crm/CLAUDE.md`) |
| A tela Canal de SMS (configuração, teste, histórico, campanha) e a faixa de SMS em Confirmações ("Buscar respostas do SMS", "Enviar lembretes por SMS") | **saiu** — do menu, das telas, da prévia, do catálogo de aulas e do roteiro da aula de Confirmações |
| Tabela `MensagensSms`, colunas `TokenResposta`/`Canal` dos contatos | **ficam** (sem migration destrutiva). As sessões já avisadas por SMS continuam aparecendo como "SMS enviado" em Confirmações — é histórico |
| `ConfirmacaoPorLink` e `TextoSms` (Domínio) | ficam, sem chamador de produção — são a leitura do histórico e o vocabulário do link |
| Rota `/c/TOKEN` do `tools/worker-clinica.js` | fica no ar: os links já enviados respondem até a hora da sessão. ⚠️ As respostas dadas por esses links DEPOIS da publicação não são mais trazidas para a agenda (o "Buscar respostas" saiu) — publique depois de passar o dia dos últimos SMS, ou confira à mão |

O que segue abaixo é o desenho de out/2026, mantido como histórico das decisões (a regra do
"não vou não cancela sozinho", §3.3, continua valendo para a resposta do WhatsApp).


> Pedido da cliente: *"canal de comunicação com os clientes (vamos contratar um serviço de API
> de SMS para confirmações de consultas e afins)… enviar SMS para os pacientes 24h antes da
> consulta para que o paciente possa clicar no link e confirmar se irá ou não, além de que no
> futuro poderemos utilizar do canal para que possamos enviar SMS de promoções e afins."*

## 1. O que existe agora

| Peça | Onde | O que faz |
|---|---|---|
| Momento do lembrete, token, caminhos | `src/Clinica.Domain/ConfirmacaoPorLink.cs` | 24h antes (configurável); sábado/domingo adiantam para a sexta; nada sai a menos de 2h |
| Texto do SMS | `src/Clinica.Domain/TextoSms.cs`, `MensagensDeContato.ConfirmacaoPorSms` | sem acento (GSM-7 = 160 caracteres por SMS), cabe em UM segmento com o link |
| Registro de cada SMS | `MensagemSms` (tabela `MensagensSms`) | o que saiu, para quem, quantos segmentos, id no provedor, erro — e a chave ÚNICA que impede envio duplo |
| A rodada | `LembreteSmsService.RodarAsync` | gera a rodada de confirmação do dia (a MESMA do balcão), publica o link, envia, grava |
| A resposta | `LembreteSmsService.SincronizarRespostasAsync` | lê do balde o que o paciente respondeu e grava no contato |
| A página do paciente | `tools/worker-clinica.js`, rota `/c/TOKEN` | "Confirmo, estarei lá" / "Não poderei ir" — write-once |
| Provedores | `src/Clinica.Application/Sms/` | Zenvia, Twilio e **Genérico** (qualquer API JSON com token) |
| Relógio | `RelogioDoSms` (Recepção e Gerente, a cada 5 min) e, no servidor, `RelogioDoSmsNoServidor` — ligado na **ponte do CRM** da VPS da clínica (`docs/vps-do-crm.md`) | nos apps, roda enquanto estão abertos; na ponte, 24h por dia |
| Campanha | `LembreteSmsService.PreverCampanhaAsync/EnviarCampanhaAsync` | só para quem tem consentimento de comunicação VIGENTE e celular |

## 2. Como liga (roteiro para a clínica)

1. Contratar o provedor e pegar o **token da API** e o **remetente** (Zenvia: o "from" da conta;
   Twilio: Account SID + Auth Token + número; outro provedor: o endereço HTTPS da API, o nome do
   cabeçalho do token e o modelo do corpo com `{telefone}` e `{mensagem}`).
2. Em **Relacionamento → Canal de SMS → Configuração**, escolher o provedor, preencher, salvar e
   **Enviar SMS de teste** para um celular da equipe.
3. Atualizar o Worker: colar o `tools/worker-clinica.js` no worker do endereço público (o mesmo
   das receitas e do termo pelo WhatsApp). A rota `/c/*` passa a responder no mesmo hostname.
4. Pronto: com a Recepção aberta, cada sessão recebe o SMS 24h antes.
5. Na VPS da clínica, ligar `LembreteSms__Ligado=true` na **ponte do CRM**
   (`docs/vps-do-crm.md` §4): o lembrete sai na hora certa e a resposta do link chega à
   agenda mesmo com os computadores desligados (noite, fim de semana, feriado). A web de
   leitura aceita a mesma chave, mas o caminho escolhido em out/2026 é a ponte, que já roda lá.

## 3. As decisões, cada uma com a comparação

### 3.1 Onde o "24h antes" roda

| Opção | 🧑‍💻 UX | 🛠️ DX | 🤝 CX |
|---|---|---|---|
| Só na abertura do app (como o e-mail) | a sessão das 17h é lembrada às 8h do dia anterior, a das 9h às 8h — "24h" vira "na véspera" | nada novo | não é o que a cliente pediu |
| **Relógio de 5 min nos apps abertos** (escolhida) + adiantar o fim de semana para a sexta | o SMS sai na hora da sessão do dia seguinte; segunda é lembrada na sexta | uma classe pequena no shell | funciona sem servidor nenhum |
| + **serviço na VPS** (desligado por padrão; out/2026: **ligado na ponte do CRM**) | exato, inclusive sábado e domingo | o mesmo `BackgroundService` (`RelogioDoSmsNoServidor`) na ponte ou na web | a ponte já roda na VPS da clínica, sem site aberto — ver `docs/vps-do-crm.md` |

As duas formas convivem: a reserva da mensagem é de BANCO (índice único), então desktop e VPS
juntos não mandam dois SMS.

### 3.2 Como a resposta do paciente chega

| Opção | 🧑‍💻 UX | 🛠️ DX | 🤝 CX |
|---|---|---|---|
| Resposta por SMS ("responda 1 para confirmar") | o paciente digita; erra | webhook de entrada por provedor, cada um diferente | SMS de volta custa; provedor precisa de número dedicado |
| Worker consultando o banco da clínica | página rápida | uma credencial do Postgres na borda | **inaceitável**: vazamento da borda expõe o banco |
| **Balde + Worker sem banco** (escolhida, o desenho do termo pelo WhatsApp) | dois botões grandes, um toque | reusa o Worker e o balde que já existem | nenhuma credencial na borda; o pedido no ar tem só primeiro nome, dia e hora |

### 3.3 "Não vou" cancela o horário sozinho?

| Opção | 🧑‍💻 UX | 🛠️ DX | 🤝 CX |
|---|---|---|---|
| Cancelar automático | a vaga abre na hora | um caminho a mais no `AgendaService` | o horário some sem ninguém falar com o paciente; as guias são suspensas sem ninguém ver |
| **Marcar "Não vem — remarcar" e a recepção decide** (escolhida) | selo vermelho na agenda, no Início e nas Confirmações | só leitura nova | a recepção liga, remarca e oferece a vaga a outra pessoa na MESMA ligação |

### 3.4 E-mail e SMS juntos

Com o SMS ligado, o e-mail da abertura **cede** para quem tem celular (configurável): o e-mail
sai dias antes e tiraria o contato de "pendente", e o SMS — que a clínica paga para ter — nunca
sairia. Quem não tem celular continua recebendo e-mail.

### 3.5 Promoção (o "futuro" do pedido)

A campanha já existe na tela, com as regras que o CLAUDE.md fixou desde a parcela 5: **só para
quem tem consentimento de comunicação vigente**, quem fica de fora aparece CONTADO, o texto leva
"Não quer receber? Avise a clínica", e o mesmo lote clicado duas vezes não manda duas vezes.

## 4. Custo

Cada linha de `MensagensSms` guarda os segmentos cobrados. O texto do lembrete foi desenhado
para caber em UM segmento (sem acento, primeiro nome, link curto de 12 caracteres): um acento só
transformaria 160 caracteres em 70 e triplicaria o custo de toda sessão. A tela mostra os
segmentos do mês.

## 5. O que fica para depois, com o motivo

- **WhatsApp oficial (Meta) e o `clinica-crm`.** O CRM vai confirmar, desmarcar e marcar pela API
  oficial do WhatsApp. O contrato para ele conviver com este canal é o MESMO contato
  (`ContatosCampanha`, origem `AGD:{agendamento}`): o CRM grava a resposta ali (status
  Respondido + `RodadaDeConfirmacao.RespostaRemarcar` ou "confirmou"), e o SMS, ao ver o contato
  já tratado, não sai. Um canal que escrevesse num lugar próprio daria duas verdades sobre a
  mesma sessão.
- **Mudar de ideia pelo link.** É write-once: a primeira resposta vale; depois disso, telefone.
  A recepção pode já ter oferecido a vaga a outra pessoa.
- **Status de entrega do provedor (DLR).** Hoje registramos "aceito pelo provedor"; o retorno de
  entrega exige webhook de entrada, e é o mesmo problema da resposta por SMS.

## 6. UX · DX · CX

- 🧑‍💻 **UX** — quem atende vê a resposta onde já trabalha (agenda, Início, Confirmações), com o
  selo "Não vem — remarcar" vermelho; o paciente toca um botão.
- 🛠️ **DX** — uma regra por lugar: momento e token no domínio, texto em `MensagensDeContato`,
  envio e resposta num serviço só, idempotência no banco. `CanalSmsTests` cobre a rodada, a
  resposta, a falha e a campanha sem provedor real.
- 🤝 **CX** — o custo é visível por mês; nada sai sem configuração; a borda não guarda credencial
  do banco; a promoção respeita o consentimento que a clínica já colhe.
