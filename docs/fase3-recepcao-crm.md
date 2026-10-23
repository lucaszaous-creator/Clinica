# Fase 3 da unificação Recepção×CRM — recall com autorização, um PARAR, um SMS (23/10/2026)

> Decisão da direção: tirar toda redundância entre a Recepção (Clinica) e o CRM. **O Clinica é a
> verdade** dos dados e das regras (quem está no recall, quantos dias, quem autorizou mensagens);
> **o CRM é o único que fala com o paciente** (WhatsApp e SMS); **uma tela por tarefa**. A fase 1
> (modo Recepção dentro do CRM e o aviso único de "sem resposta") e a fase 2 (Acompanhamento ×
> Relacionamento) são de outras entregas. Esta é a 3ª, com um PR em cada repositório.

Decisões já tomadas pela direção antes desta entrega:

1. a confirmação do horário é **só pelo WhatsApp, no dia** (o pedido do CRM às 06:00);
2. a **Campanha de SMS** do Clinica sai;
3. a tela **Relacionamento** do CRM fica (uso no celular);
4. o recall volta a **exigir autorização de mensagem** (a Meta exige aceite para mensagem iniciada
   pela empresa, e o recall usa o modelo de MARKETING `semdor_recall`), colhida como declaração
   no **termo do BSV** — por onde passa a maior parte dos pacientes.

## 1. O que mudou

| # | Peça | Onde |
|---|---|---|
| 1 | Declaração **"AUTORIZAÇÃO DE MENSAGENS (opcional) — Autorizo a Clínica SemDor a me enviar mensagens por WhatsApp e SMS sobre minhas sessões, retornos e novidades do tratamento."** + "Posso cancelar a qualquer momento respondendo PARAR…" | `ModelosTermoBsv.AutorizacaoDeMensagens`, acrescentada na EMISSÃO de todo termo exigido para o BSV (TCLE e Termo da sessão) por `DocumentoClinicoService.AutorizacaoDeMensagensAsync` — balcão/consultório e portal do tablet |
| 1 | "Sim" grava `ConsentimentoLgpd` `ComunicacaoEMarketing` vigente, versão = **número do termo**; "Não" grava a recusa | `TermoConsentimento.Decisoes` (agora lê o termo de procedimento, só esse código) e `AssinaturaDoPacienteService.AplicarConsentimentosAsync` |
| 1 | "Não" não acende alerta; a declaração aparece em destaque e fica fora do "Marcar todas como Sim" | `RespostaDeclaracao.RequerAtencao`, `DeclaracaoItem.AutorizacaoDeMensagens`, `AssinaturaDoPacienteForm` |
| 2 | Recall da ponte: **só a fila de recall** (`Acompanhamentos` tipo Recall); "Sem autorização de mensagem" bloqueia; os **dias do Gerente** bloqueiam e voltam em `RecallSnapshot.InactivityDays` | `ClinicRecallBridge.Snapshot`, `ClinicContracts.cs` |
| 2 | CRM: saem as "regras por tipo" com dias (`RecallRule`); confere consentimento e fila também do lado dele; a tela mostra os dias do Clinica | `semdor-crm`: `RecallFlow.cs`, `app.js`, `index.html` |
| 3 | **Um PARAR só**: o PARAR no CRM agenda `consent-optout`, que revoga a autorização no Clinica | `POST /crm/v1/consent/optout` → `ClinicRecallBridge.OptOut`; `semdor-crm`: `RecallFlow.ApplyRecallResponse`, `ClinicConnector` |
| 4 | **Um SMS só**: saem o lembrete de 24h, os relógios, a configuração do provedor, a campanha e a tela Canal de SMS | ver `docs/canal-sms.md` §0 |
| 5 | Confirmação só pelo WhatsApp no dia: o pedido das 06:00 já cobre "pending" e "notified"; nada depende do SMS | `semdor-crm/ClinicFlow.PedeConfirmacaoAgora` / `LembreteAindaVale` (só comentários) |

## 2. As decisões desta entrega, com a comparação

### 2.1 Onde a declaração entra

| Opção | 🧑‍💻 UX | 🛠️ DX | 🤝 CX |
|---|---|---|---|
| Linha nova no texto dos dois modelos + migration que a acrescenta nos modelos gravados | a mesma pergunta | migration que edita TEXTO da clínica; o "Escrever termos…" do Gerente pode apagá-la sem aviso, e o código não saberia qual linha é a autorização (o `ItemModelo` não tem código) | reescreve um texto que o advogado da clínica aprovou |
| Só nos modelos de fábrica (`ModelosTermoBsv`) | — | uma linha | **as instalações existentes nunca perguntariam** |
| **Na emissão, a todo termo exigido para o BSV** (escolhida) | aparece no fim, separada e em destaque | uma função, duas portas (balcão e tablet); o código da finalidade viaja no `ItemDocumento.Codigo`, como no termo LGPD | nenhum texto da clínica é tocado, e toda instalação pergunta já na próxima emissão |

### 2.2 A quem perguntar, e o que o "Não" faz

| Opção | 🧑‍💻 UX | 🛠️ DX | 🤝 CX |
|---|---|---|---|
| Perguntar em todo termo | o paciente responde a mesma coisa a cada sessão de BSV | nada | cansa; o "Não" apressado de um dia vira regra |
| Só no TCLE | — | — | quem já assinou o TCLE (a maioria dos pacientes em tratamento) **nunca** seria perguntado |
| **Em todo termo do BSV, só para quem NUNCA respondeu** (escolhida) | uma vez na vida do paciente; quem pediu PARAR não é perguntado de novo | lê o histórico de consentimento na emissão | colhe dos pacientes atuais na próxima sessão e respeita quem recusou |

- **"Não" grava a recusa e nunca revoga.** Gravar a recusa é o que impede a pergunta de voltar;
  revogar é do termo LGPD e do PARAR. Como a pergunta só aparece para quem nunca respondeu, um
  consentimento vigente só existiria se chegasse por outro caminho entre a emissão e a assinatura
  — e aí o "Não" do termo do procedimento não o desfaz (decisão a confirmar, §5).
- **A resposta é obrigatória (Sim ou Não)**, como as outras declarações: "opcional" quer dizer que
  o paciente escolhe livremente, não que o balcão pode pular. Pular é o que deixaria a pergunta
  sem ser feita.

### 2.3 Quem o CRM convida

| Opção | 🧑‍💻 UX | 🛠️ DX | 🤝 CX |
|---|---|---|---|
| Manter as listas da ponte fora da fila ("sem retorno", "sem BSV", "sem acupuntura") | a recepção vê no Acompanhamento uma lista e o CRM convida outra | duas definições de "quem sumiu", com dias diferentes (CRM 60, Gerente N) | o paciente pode receber convite sem estar na fila que a equipe trabalha |
| Pôr essas listas na geração da fila (Clinica) | igual | mexe no `GerarRecallAsync`, que já usa os dias do Gerente por modalidade | nada a ganhar: a fila já É isso |
| **Só a fila** (escolhida — a menor mudança correta) | a fila do Acompanhamento é a lista do CRM | uma regra, uma tabela | o que a equipe vê é o que sai |

⚠️ **A fila só se renova quando alguém abre o Acompanhamento** (é lá que `GerarRecallAsync`
roda). A ponte só lê. Se ninguém abrir a tela, o CRM fica sem convidados novos — falha segura.
Uma geração diária pela ponte (a ponte já pode gravar `Acompanhamentos`) é o próximo passo se a
clínica não abrir a tela todo dia (§5).

### 2.4 O PARAR

| Opção | 🧑‍💻 UX | 🛠️ DX | 🤝 CX |
|---|---|---|---|
| PARAR só no CRM (como era) | — | — | o Clinica continua "autorizado"; uma troca de servidor do CRM ou uma conversa nova perderia o pedido |
| **PARAR revoga no Clinica, por telefone, todas as fichas** (escolhida) | ninguém precisa saber de duas listas | um registro na clínica, como o `recall-notice`: sem mensagem, repete até a ponte responder | quem segura o aparelho pediu para parar; o envio é para o número, então nenhuma ficha é escolhida (o telefone não é identidade) |

## 3. Ordem de publicação

1. **Clinica (este PR)** — a ponte primeiro, na VPS 1:
   - publicar `Clinica.Crm.Bridge`;
   - rodar de novo `deploy/ponte-crm/papel-ponte-crm.sql` (a ponte passa a gravar
     `Consentimentos` para o PARAR e deixa de gravar `MensagensSms`);
   - **apagar `LembreteSms__Ligado`** do ambiente da ponte (e da web de leitura, se estiver lá);
   - a ponte nova com o CRM antigo é segura: o CRM antigo só convida o que vier "existing" e sem
     bloqueio — e a ponte nova já bloqueia sem autorização e pelos dias do Gerente.
2. **Apps**: Recepção, Gerente, Clínico e Financeiro (a Application e a moldura são dos quatro); o
   faturamento não precisa. ⚠️ **Os postos com a versão antiga continuam mandando o SMS da véspera
   até atualizar** (o relógio é do app): se algum posto demorar, desligue o provedor em
   Relacionamento → Canal de SMS da versão antiga antes de publicar.
3. **Posto do tablet** (`Clinica.Assinaturas.Api`, onde o `PortalTabletService` roda): publicar
   para o termo colhido no tablet também perguntar. (`Clinica.Assinaturas.Tests` e os testes Python
   de `deploy/tablet` não rodam no CI — rodar localmente antes de publicar o posto.)
4. **CRM (`semdor-crm`)**, VPS 2, depois da ponte: `CRM_SMS_*` **ficam** (o CRM é o único que manda
   SMS); nenhuma variável nova. Com a ponte antiga, o PARAR para em "bloqueado" até a ponte nova
   chegar, e a gestão repete na tela de automações.
5. Meta: o modelo `semdor_recall` não muda. O recall continua desligado até a direção ligar; com ele
   ligado, só sai para quem autorizou.

## 4. UX · DX · CX

- 🧑‍💻 **UX** — a recepcionista lê a pergunta uma vez por paciente, em destaque, e o "Marcar todas"
  não responde por ele; a tela de Confirmações perdeu a faixa e os dois botões de SMS; o menu perdeu
  o Canal de SMS. No CRM, a tela de recall diz de onde vêm os dias e a autorização, e a fila é a
  mesma do Acompanhamento.
- 🛠️ **DX** — uma regra de recall (a fila do Clinica), uma configuração de SMS (a do CRM), um
  registro de autorização (o do Clinica); ~2.000 linhas a menos (serviço, gateway, relógios, tela,
  testes). Testes novos: `AutorizacaoDeMensagensNoTermoTests`, `PonteClinicRecallBridgeTests`
  (fila, dias, trava, PARAR) e, no CRM, `RecallTests` (consentimento, fora da fila, PARAR uma vez).
- 🤝 **CX** — a clínica deixa de pagar dois provedores de SMS e de configurar o SMS em dois
  lugares; o recall volta a cumprir a regra da Meta; nenhuma tabela é apagada e o histórico de SMS
  continua legível nas Confirmações.

## 5. O que NÃO foi feito, e o que a direção precisa confirmar

- **Geração diária da fila de recall pela ponte** — não foi feita (§2.3). Confirmar se a recepção
  abre o Acompanhamento todo dia; se não, é o próximo passo.
- **As linhas `Sms*` (com o token cifrado) continuam em `Configuracoes`**, sem leitor. Apagá-las é
  uma migration de dados simples; não foi feita porque a regra é não apagar configuração da
  clínica sem pedido.
- **"Não" no termo do BSV não revoga** uma autorização vigente (§2.2) — confirmar.
- **A videoaula de Confirmações** ainda mostra a faixa de SMS: o roteiro foi corrigido, a gravação
  precisa ser refeita (`tools/treinamento`). A aula do Canal de SMS saiu do catálogo.
- **As respostas pelo link de SMS dadas depois da publicação** não chegam mais à agenda
  (`docs/canal-sms.md` §0).
