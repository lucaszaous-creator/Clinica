# O CRM dentro da Recepção: a aba "WhatsApp" (out/2026)

> ⛔ **A moldura saiu na fase 3 do WhatsApp nativo (24/10/2026, decisão da direção).** A aba
> WhatsApp abre a tela nativa (`ConversasDoWhatsApp`, `docs/whatsapp-nativo-implementacao.md`).
> Saíram a `WhatsAppNoCrm`, o roteador da aba, a escolha em Configurações, o endereço com
> `?modo=recepcao` e, no CRM, o modo Recepção. **Ficaram** o bilhete de entrada
> (`EntradaNoCrm`, `BilhetesEntradaCrm`, a ponte), o "Escrever no WhatsApp" das outras telas
> (`PedidoDeConversaNoCrm`, agora consumido pela tela nativa) e o contato que volta ao
> Acompanhamento. O texto abaixo é o registro de como a moldura funcionava.

> **Fase 1** da unificação Recepção × CRM. A direção pediu para **reduzir ao máximo as
> redundâncias** entre os dois sistemas. A metade do CRM (o modo Recepção) está em
> `semdor-crm/docs/modo-recepcao.md`.

## O plano inteiro (decidido em 23/10/2026)

O princípio: **o Clinica guarda os dados e as regras** (quem é paciente, quem sumiu, quantos
dias contam), **o CRM é o único que fala com o paciente** (WhatsApp, SMS, conversas), e cada
tarefa tem **uma tela só**.

| Fase | O quê | Onde |
|---|---|---|
| **1 (esta)** | Aba WhatsApp na Recepção com o CRM no modo Recepção, sem outra senha. No modo Recepção o CRM esconde Agenda, Meu dia, Relacionamento e o sino de confirmações, e fica **um só aviso** de sem resposta (o sino da Recepção). | este PR + PR do CRM |
| 2 | Uma tela de relacionamento só: o **Acompanhamento** escreve pelo CRM e registra o contato sozinho, ganha "Consulta a renovar" e absorve "Retornos a marcar". O Relacionamento do CRM continua **no celular**. | depois |
| 3 | Um motor de envio só: recall com os dias do Gerente, só da fila e **só com autorização** (colhida no termo do BSV); uma configuração de SMS e um PARAR; confirmação **só pelo WhatsApp no dia**; a campanha de SMS sai. | sessão própria |

## Como funciona

1. A Gerência informa o **Endereço do CRM** em Gerente → Configurações → **Integrações**
   (`ParametrosService.ChaveEnderecoDoCrm`; só `https`, sem caminho).
2. A Recepção ganha o item **WhatsApp** na barra do topo (`ModuloRecepcao.ChaveWhatsAppCrm`,
   `Principal = 6`). Ele exige `EditarAgenda`, o mesmo bit que a ponte exige para entrar no CRM
   (`ClinicTeamBridge.Requer`).
3. A tela `WhatsAppNoCrm` emite um **bilhete** (`IEntradaNoCrmService.EmitirAsync`) e abre
   `https://<crm>/?modo=recepcao#entrada=<bilhete>` numa moldura (iframe no navegador único da
   janela; nenhum `BlazorWebView` novo).
4. O CRM manda o bilhete à ponte (`POST /crm/login/ticket`). `ClinicTeamBridge.EnterByTicket`
   **gasta** o bilhete (`ConferirAsync`) e confere `PodeEntrar` no usuário de AGORA. Usuário
   desativado ou que perdeu o bit depois de o bilhete nascer recebe 403.

### O bilhete (`EntradaNoCrm`, `BilhetesEntradaCrm`)

- São 32 bytes aleatórios. O banco guarda **só o SHA-256**, e o bilhete em si só existe na URL
  da moldura. Quem só **lê** o banco (a ponte, a web de leitura, um backup) não entra no CRM com
  o que leu. Foi por isso que se descartou o HMAC com segredo guardado no banco.
- Vale **60 s** e **uma vez só**. O uso único é uma escrita condicional
  (`UsadoEm IS NULL`): dois pedidos com o mesmo bilhete não entram os dois.
- **Folga de 2 min** para o relógio do PC do balcão (que grava o vencimento) discordar do da
  VPS da ponte (que confere). PC atrasado além disso só faz a pessoa cair no login normal.
- O app apaga os bilhetes com mais de um dia quando emite um novo. A ponte não apaga: o papel
  `ponte_crm` só tem `UPDATE ("UsadoEm")` nessa tabela (`PontePapelRestritoTests`).

## Publicação (ordem)

1. **Banco**: migration `BilhetesEntradaCrm`, que o Gerente aplica ao abrir. Depois rode de novo
   `deploy/ponte-crm/papel-ponte-crm.sql` (ele dá a `ponte_crm` o UPDATE da coluna `UsadoEm`).
2. **Ponte** (VPS 1): a rota `/crm/login/ticket`.
3. **CRM** (VPS 2): o modo Recepção e `CRM_EMBED_ORIGINS=https://0.0.0.0` no `production.env`.
4. **Apps**: Recepção (a aba) e Gerente (o campo do endereço e a migration).
5. Gerente → Configurações → Integrações → Endereço do CRM = `https://crm.clinicasemdormacae.com.br`.

Fora de ordem, nada quebra: sem endereço, a aba diz como ligar; sem a variável no CRM, a moldura
fica em branco; ponte antiga, o CRM responde "Atualize a ponte" e o login normal aparece.

## UX · DX · CX

- 🧑‍💻 **UX**: agenda, paciente e conversa na mesma janela, sem Alt+Tab nem segunda senha. A aba
  fica montada ao trocar de tela (`PreservarAoNavegar`), então a conversa aberta não se perde.
  "Entrar de novo" resolve a sessão vencida do CRM. Sem endereço, sem permissão ou com falha de
  leitura, a tela diz o porquê (`EstadoVazio` / `FalhaDeLeitura`) em vez de ficar em branco.
- 🛠️ **DX**:
  - **Peças novas**: a regra `EntradaNoCrm` (Application, `EntradaNoCrmTests`), o serviço
    `EntradaNoCrmService` (Infrastructure, `PonteClinicTeamTests`), a entidade e a migration,
    a tela `WhatsAppNoCrm` (semente `SementeWhatsAppNoCrm`, aberta pelo `TelasBlazorAbremTests`),
    o bloco em Configurações e a rota da ponte.
  - **Contrato**: `ClinicTicketLogin` entrou no fim de `ClinicContracts.cs`, idêntico nos dois
    repositórios.
- 🤝 **CX**:
  - **Custo**: nenhum servidor novo.
  - **Segurança**: o CRM continua sem guardar senha. Com uma credencial só de leitura do banco
    não se fabrica entrada. O CRM só aceita ser emoldurado pela origem dos apps.
  - **Celular**: continua como está.

## Fase 2: uma tela só de relacionamento (out/2026)

- **"Escrever no WhatsApp" pelo número oficial.** Cinco botões da Recepção que abriam o wa.me
  (o WhatsApp do computador, sem registro) passam pelo CRM: a conversa do Acompanhamento, o
  "Convidar" dos retornos, o aviso das Confirmações, o "Confirmar" do horário na agenda e o
  WhatsApp da ficha.
  - Cada um deixa um `PedidoDeConversaNoCrm` e navega até a aba WhatsApp. A aba troca só o
    fragmento da moldura (`ConversaNoCrm.Endereco`, com bilhete novo, o pedido e o número da
    vez).
  - O CRM então abre ou começa a conversa, liga a ficha escolhida aqui (só se a conversa ainda não
    tem ficha) e deixa a mensagem como rascunho. Quem envia é a recepcionista.
  - Onde a aba não existe (app sem o item, CRM sem endereço, usuário sem `EditarAgenda`), o
    botão continua no wa.me de antes.
- **O contato entra sozinho no Acompanhamento.** A mensagem que a equipe manda pelo CRM a um
  paciente identificado vira um `ClinicContactNotice`, um por conversa por dia. A ponte
  (`ClinicContactBridge`, `POST /crm/relationship/contact`) o grava no histórico de cada caso
  aberto do paciente, com o nome de quem mandou.
  - Não mexe em etapa, data nem responsável: o desfecho continua no "Como foi?".
  - É idempotente pelo CommandId, como o recall.
- **"Retornos a marcar" virou a fila "Retornos pedidos" do Acompanhamento.**
  - É a mesma tela, embutida (`RetornosAMarcar Embutida`), com as mesmas ações.
  - O item e a aba saíram do menu (e da `OrganizacaoNavegacao` e da verificação Windows).
  - O contador "Retornos" do Início abre o Acompanhamento já nessa fila (`PedidoDoAcompanhamento`).
- **"Consulta a renovar" não ganhou uma terceira cópia**: na Recepção ela já mora em "Consultas de
  convênio". No CRM do celular ela continua no Relacionamento.

### UX · DX · CX da fase 2

- 🧑‍💻 **UX**: um clique leva da fila à conversa certa, com a mensagem pronta e a ficha ligada. O
  histórico do caso mostra que a mensagem saiu sem ninguém digitar "falei pelo WhatsApp". Há uma
  fila a menos no menu, e os retornos ficam ao lado do recall e dos novos BSV. Fotografado em
  1024 e 1366: o seletor de filas e a lista cabem sem rolar para o lado.
- 🛠️ **DX**: a regra do endereço mora em `ConversaNoCrm` (`ConversaNoCrmTests`). A ponte nova é
  testada em `PonteClinicContactTests`; a gravação usa `ContatosAcompanhamento`, onde a
  `ponte_crm` já tem INSERT. No CRM, o pedido (`modo-recepcao.js`, `reception.js escrever`) é
  testado em `modo-recepcao.test.cjs` e o registro em `RelationshipContactTests`. O contrato novo
  `ClinicContactNotice` entrou no fim de `ClinicContracts.cs`, idêntico nos dois repositórios.
- 🤝 **CX**: as mensagens da recepção saem pelo número oficial, que é o que a Meta enxerga e
  avalia, e não pelo WhatsApp de cada computador. O histórico deixa de depender de alguém
  lembrar de registrar. Nenhum servidor novo.

## O que NÃO foi feito, e por quê

- **Número de não lidas no item WhatsApp**: o sino da Recepção já avisa ("Atenção no
  WhatsApp"). Um contador pediria outra leitura periódica do CRM; fica para quando a direção
  pedir.
- **Fase 3**: PR próprio (ver a tabela acima).
- **Cobrança e pendências do faturamento** ainda abrem o wa.me: são do Financeiro e do Faturamento,
  que não têm a aba WhatsApp. A mudança é a mesma chamada, quando a direção quiser.
- **A videoaula "Retornos a marcar"** (`catalogo.json`) deixa de aparecer, porque a tela saiu do
  menu. Ela precisa ser regravada dentro do Acompanhamento.
