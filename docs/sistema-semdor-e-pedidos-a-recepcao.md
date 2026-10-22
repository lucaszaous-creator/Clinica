# Sistema SemDor e pedidos à recepção no chat da equipe (out/2026)

Continuação do [chat da equipe](chat-da-equipe.md). O pedido da direção foi este: quando o médico
escrever, a resposta deve vir do sistema. Exemplos: a agenda do dia com quem confirmou e quem pediu
para remarcar, ou pedir à recepção para mudar o horário de um paciente. E isso precisa chegar à
secretária. (Depois, a direção esclareceu: o médico pergunta **pelo WhatsApp da clínica** — ver abaixo.)

São três peças, e todas usam o mesmo chat:

1. **Sistema SemDor**: cada pessoa tem uma conversa com o sistema (`sistema:{id}`), só dela. Ali ela
   pergunta e recebe a resposta em **cartão**, com os mesmos dados e as mesmas palavras da agenda do
   balcão.
2. **Pedido à recepção**: quem atende pede um gesto sobre a agenda de um paciente (remarcar,
   cancelar, encaixar, avisar). O pedido tem dono e desfecho. Ele aparece como cartão no `# Recepção`,
   e quem pediu recebe a resposta na conversa com o sistema, sem precisar perguntar.
3. **Pelo WhatsApp da clínica**: quem é da equipe manda mensagem ao número da clínica (*"qual a
   minha agenda de hoje?"*) e o sistema responde ali mesmo. O CRM reconhece o celular cadastrado em
   Acessos, não abre conversa de paciente e pergunta à ponte em nome daquele login. Ver
   [O sistema pelo WhatsApp](#o-sistema-pelo-whatsapp-da-clínica). **O chat da equipe NÃO abre no
   CRM** (decisão da direção, out/2026): ele fica nos apps.

## O que o Sistema SemDor responde

| Pergunta (texto livre, sem acento e sem caixa) | Resposta |
|---|---|
| "minha agenda de hoje", "agenda de amanhã", "sexta", "24/10", "dia 25" | Cartão da agenda. Mostra os números do dia (horários, confirmaram, remarcar, não confirmaram, na clínica, atendidos, faltas) e uma linha por horário com o selo |
| "agenda da Dra. Helena amanhã" | A agenda daquele profissional. Exige `VerAgenda`, a não ser que seja a própria pessoa |
| "quem confirmou", "quem não confirmou", "quem pediu para remarcar", "faltas de ontem" | O mesmo cartão, filtrado |
| "próximo paciente", "quem já chegou" | O balcão agora (`Agendamento.Etapa`) |
| "paciente Maria José" | Os próximos horários (60 dias) de quem tem o nome. Exige `VerFichaPaciente` |
| "meus pedidos" / "pedidos" | Quem pede vê os seus dos últimos 14 dias. Quem trata vê os abertos |
| "ajuda", ou o que não entendeu | A lista do que dá para perguntar, com botões. **Nunca um palpite** |

Os selos usam as palavras do balcão e do CRM: **Confirmou**, **Não vem — remarcar**,
**Pediu para cancelar**, **Não confirmou**, **Sem lembrete**, **Chegou**, **Em atendimento**,
**Atendido** e **Faltou**. A classificação é a de `RodadaDeConfirmacao.Classificar`, e nada foi
recalculado. A agenda é a do profissional ligado ao login (`UsuarioSistema.ProfissionalId`). Quem
não tem profissional e tem `VerAgenda` vê a agenda da clínica, com o nome do profissional em cada
linha.

Cada linha da agenda que ainda pode virar pedido (horário marcado, futuro, paciente ainda não chegou)
tem o botão **Pedir à recepção**, que abre o formulário já com o paciente e o horário.

## O pedido à recepção

- **Tipos**: Remarcar, Cancelar, Encaixar, Avisar o paciente e Outro. Remarcar e cancelar exigem o
  horário.
- **Quem pede**: qualquer pessoa ativa. **Quem trata**: quem tem `EditarAgenda`, o mesmo bit de quem
  marca e remarca na agenda.
- **Situações**: Aberto → Em andamento ("Deixa comigo") → Concluído ou Não foi possível. "Não foi
  possível" exige o motivo. Encerrado não volta: um novo pedido é outro recado.
- **Nada mexe na agenda.** Quem remarca é a recepção, pela agenda do balcão, com as regras
  do `AgendaService`.
- O desfecho vira mensagem do sistema na conversa de quem pediu, com o toque de uma direta. Exemplo:
  "Rayssa Lima concluiu o seu pedido (Remarcar · Maria · qui, 22/10 às 10:00). Remarcada para ter,
  27/10 às 09:00."
- **Idempotente**: o formulário manda uma chave (`Idempotencia`, índice único), então o duplo clique
  não cria dois pedidos. Dois cliques de pessoas diferentes também não se atropelam: `Versao` é token
  de concorrência.
- **Nada se apaga.** Cada mudança fica em `EventosPedidoRecepcao` (quem, quando, o quê).

## Como funciona por dentro

- `MensagemEquipe` ganhou duas colunas:
  - `DoSistema`: a linha é resposta do sistema. O `AutorId` é o da pessoa, porque a chave estrangeira
    pede um usuário.
  - `Cartao`: o JSON de `CartaoChat`.
- O `Texto` continua sendo o resumo legível, e é ele que a busca, a prévia e quem não desenha o cartão
  leem. Um app antigo vê o texto do pedido no `# Recepção` e não vê a conversa com o sistema. Nada
  quebra.
- O cartão é **um formato só**: título, números, linhas com selo, rodapé e perguntas prontas.
  O Blazor (`CartaoDoChat.razor`) desenha o cartão; pelo WhatsApp ele vira texto (`AssistenteWhatsAppRegras.Texto`).
- O pedido muda de situação → os **dois cartões** (no `# Recepção` e na conversa de quem pediu) são
  atualizados com `EditadaEm`. A releitura de quem está com a conversa aberta traz o cartão novo, e a
  tela não escreve "(editada)" em cartão.
- A resposta do sistema é gravada **no mesmo `EnviarAsync` da pergunta** e já sai lida: quem perguntou
  está olhando. Se a leitura da agenda falhar, a pergunta não se perde e a resposta diz que não deu
  para ler agora.

Peças:

| Onde | O quê |
|---|---|
| `Clinica.Application/Servicos/SistemaEquipeRegras.cs` | Como a pergunta é lida, o selo de cada horário e o cartão. Puro, com teste |
| `Clinica.Application/Servicos/PedidosRecepcaoRegras.cs` | Tipos, quem trata, transições, cartão e desfecho do pedido |
| `Clinica.Application/Abstracoes/CartaoChat.cs` | O formato do cartão (JSON) |
| `Clinica.Infrastructure/SistemaEquipeService.cs` | Lê a agenda e os contatos de confirmação (2 a 4 idas) |
| `Clinica.Infrastructure/PedidosRecepcaoService.cs` | Cria, lista, assume e encerra pedidos, em transação sob a estratégia de execução |
| `Clinica.Infrastructure/GravacaoDoChat.cs` | Grava a mensagem que não nasce de alguém digitando, com o texto de busca e a lista de conversas |
| `Clinica.Ui.Blazor/Moldura/CartaoDoChat.razor` | O cartão na tela |
| `Clinica.Ui.Blazor/Moldura/ChatDaEquipe.razor` | O sistema no topo da lista, as perguntas prontas e o formulário "Pedir à recepção" |
| `Clinica.Application/Servicos/AssistenteWhatsAppRegras.cs` | O pedido escrito numa frase e a resposta como texto de WhatsApp. Puro, com teste |
| `Clinica.Infrastructure/AssistenteWhatsAppService.cs` | Responde ao celular da equipe, grava na conversa com o sistema e guarda a fila dos desfechos |
| `Clinica.Crm.Bridge/Program.cs` (`/crm/v1/staff/*`) | As rotas que o CRM chama |
| Migration `20261022023705_SistemaEPedidosDaEquipe` | Duas colunas e duas tabelas, só aditiva, mais o gatilho de aviso dos pedidos |

## O sistema pelo WhatsApp da clínica

O pedido da direção, nas palavras dela: *"eu envio uma mensagem para o WhatsApp da clínica: qual a
minha agenda de hoje? O sistema busca na ponte e me responde com a agenda."*

1. A direção cadastra o **celular para o WhatsApp do sistema** no usuário (Acessos → usuário). Um
   número responde por uma pessoa só, e tem de ser celular brasileiro.
2. A mensagem chega à Meta e ao CRM. O CRM compara o remetente com a lista da ponte
   (`GET /crm/v1/staff/numbers`). A Meta às vezes entrega o celular sem o nono dígito, e a chave
   (`TelefoneDaEquipe.Chave`) repõe o 9.
3. Sendo da equipe, a mensagem **não abre conversa de paciente**. O CRM chama
   `POST /crm/v1/staff/ask`, e a ponte responde em nome daquele login, com as mesmas leituras e
   barreiras do chat. A resposta volta como texto pelo WhatsApp.
4. A pergunta e a resposta ficam na conversa da pessoa com o sistema, no app, já lidas.

O que se pode mandar é o mesmo do chat (a tabela acima), e mais o **pedido escrito**:

| Mensagem | O que acontece |
|---|---|
| "Remarcar Maria Silva 24/10 10h", "cancelar o João de amanhã" | Procura o horário na agenda que a pessoa alcança (a dela, ou a da clínica para quem vê a agenda e não atende), nos próximos 30 dias ou no dia citado. Um só: o pedido nasce, com o horário. Vários: a resposta lista e diz como escrever para escolher. Nenhum: diz onde procurou e não cria nada |
| "Encaixar Ana hoje à tarde", "Avisar Maria que vou atrasar" | Com horário achado, liga ao horário. Sem horário, liga à ficha quando o nome acha uma só. Sem ficha, vai como recado com o texto inteiro |
| "Recepção: ligar para o laboratório" | Recado livre, sem paciente |

Só é pedido quando o verbo vem **no começo** ("quem pediu para remarcar" é pergunta). A chave do
pedido é o id da mensagem da Meta: a mesma mensagem entregue duas vezes não cria dois pedidos.

**O desfecho volta pelo WhatsApp.** Quando a recepção conclui, o pedido entra na fila
`GET /crm/v1/staff/notices`. O CRM manda o texto e confirma (`POST /crm/v1/staff/notices/ack`),
entregue ou não. O resultado fica nos eventos do pedido (`aviso-whatsapp`), e o desfecho continua na
conversa do app. Só entra na fila pedido criado **depois** de o número ser ligado (sem isso, ligar o
número despejaria os desfechos antigos) e encerrado nos últimos 3 dias.

⚠️ **Janela de 24 h da Meta**: mensagem livre só vai a quem escreveu à clínica nas últimas 24 h.
Desfecho fora da janela não sai pelo WhatsApp (fica no app e no evento). Para sair sempre, falta um
modelo aprovado na Meta (ex.: `semdor_aviso_equipe`).

### 🧑‍💻 UX · 🛠️ DX · 🤝 CX do WhatsApp

- 🧑‍💻 O médico pergunta do celular, sem abrir o sistema, e recebe a agenda com as mesmas palavras do
  balcão. O pedido sai numa frase, e a resposta da recepção chega no mesmo lugar.
- 🛠️ A leitura da frase e o texto moram na Application, com teste (`AssistenteWhatsAppRegrasTests`).
  O ciclo no banco está em `AssistenteWhatsAppServiceTests`. Pergunta nova continua sendo uma linha
  em `SistemaEquipeRegras.Interpretar`, e vale para o chat e para o WhatsApp.
- 🤝 Nenhum serviço novo: é o mesmo número e a mesma conta da Meta que a clínica já paga.
  ⚠️ **LGPD**: a agenda (nome do paciente, horário, procedimento e resposta ao lembrete) passa
  pela Meta até o celular da pessoa. É o mesmo canal que a clínica já usa com os pacientes, e não vai
  prontuário. Mesmo assim, é a direção que decide quem recebe. Por isso o número é cadastrado um a um,
  em Acessos, por quem administra acessos.
- **Migration** `WhatsAppDaEquipe`, só aditiva: `Usuarios.TelefoneWhatsApp`, `WhatsAppLigadoEm` e
  `PedidosRecepcao.AvisoWhatsAppEm`. ⚠️ Rode o `papel-ponte-crm.sql` de novo depois de publicar.

## Comparação dos caminhos (o que foi decidido e por quê)

### Quem responde: regras ou um modelo de linguagem?

| | Regras sobre os dados (escolhido) | Modelo de linguagem (LLM) |
|---|---|---|
| 🧑‍💻 UX | Resposta igual para a mesma pergunta, imediata, em cartão com botões. O que não entende vira a lista do que dá para perguntar | Entende frases mais soltas, mas pode errar com confiança ("a Maria confirmou" quando não confirmou) |
| 🛠️ DX | Regras puras com teste. Pergunta nova = uma linha em `Interpretar` e um teste | Prompt, avaliação, custo por chamada e testes não determinísticos |
| 🤝 CX | Nada sai da clínica (LGPD, art. 33). A direção **tirou a IA do CRM**, e isto não a traz de volta. Custo zero | Agenda de paciente num serviço externo é transferência internacional. Custo mensal e risco de resposta errada sobre paciente |

### Onde mora o pedido: tabela própria ou só mensagem no chat?

| | Tabela própria + cartão no chat (escolhido) | Só mensagem ("@Rayssa remarca a Maria") |
|---|---|---|
| 🧑‍💻 UX | Tem dono, situação e desfecho. Quem pediu sabe quando foi feito, inclusive pelo WhatsApp | Some na rolagem do canal. Ninguém sabe se alguém pegou |
| 🛠️ DX | Duas tabelas e um serviço com transições testadas | Nada novo, mas nada a medir |
| 🤝 CX | O recado do médico não se perde. É o que faz parecer profissional | O WhatsApp pessoal de antes, só que dentro do sistema |

### Como o CRM vê o chat: pela ponte ou com cópia? (superado: o chat saiu do CRM em out/2026)

| | Pela ponte, em nome do login (escolhido) | O CRM guarda uma cópia |
|---|---|---|
| 🧑‍💻 UX | A mesma conversa nos apps e no CRM, ao mesmo tempo | Duas conversas que divergem |
| 🛠️ DX | Os mesmos serviços e as mesmas regras de acesso. Rotas finas na ponte | Segunda verdade e sincronização |
| 🤝 CX | Os dados ficam no banco da clínica | Dado de paciente em dois lugares |

## 🧑‍💻 UX

- O médico pergunta em português e recebe a agenda com os selos, sem abrir a agenda. Um toque em
  "Pedir à recepção" na linha do horário e o pedido está feito.
- A secretária vê o pedido no `# Recepção` dos apps, com
  "Deixa comigo", "Concluir" e "Não foi possível". Quem pediu recebe a resposta sozinho, com toque.
- As perguntas prontas ficam numa fileira acima do campo, e a resposta já traz as próximas
  ("Agenda de amanhã", "Quem não confirmou").

## 🛠️ DX

- A regra mora na Application, com teste: `SistemaEquipeRegrasTests` (intenção, dia, profissional,
  selos, cartão) e `SistemaEPedidosDaEquipeTests`, que cobre o ciclo inteiro no banco: perguntar,
  pedir, assumir, concluir, o desfecho tocando como direta e a conversa alheia recusada.
- Tela: `ChatDaEquipeBlazorTests.Sistema_responde_com_cartao_e_o_pedido_a_recepcao_nasce_da_tela`.
  WhatsApp: `AssistenteWhatsAppRegrasTests` e `AssistenteWhatsAppServiceTests`. Tradução para o Postgres: `TraducaoNoNpgsqlTests`. Orçamento de
  idas: `ChatEquipeIdasAoBancoTests` (inalterado).
- Pergunta nova para o sistema: acrescente a intenção em `IntencaoDoSistema`, a leitura em
  `Interpretar` (atenção à ordem: "não confirmou" antes de "confirm") e o caso no teste.
- ⚠️ A ponte grava no chat com o papel restrito `ponte_crm`. As tabelas novas entraram em
  `deploy/ponte-crm/papel-ponte-crm.sql`. **Rode o script de novo depois da atualização**; sem ele,
  a pergunta pelo WhatsApp falha ao gravar (42501 no log da ponte) e o CRM responde que o sistema não
  respondeu agora.
- ⚠️ Transação explícita só pela estratégia de execução (`AtomicoAsync`). O `EnableRetryOnFailure`
  recusa `BeginTransaction` solto (o mesmo achado de `TransacaoDaPonte`).

## 🤝 CX

- O recado do médico deixa de depender de alguém lembrar. Tem dono, prazo visível e resposta.
- A clínica não paga nada novo: nenhuma chamada a serviço externo e nenhum dado de paciente fora.
- **Risco em produção**: a migration é aditiva (colunas novas com padrão e tabelas novas). App antigo
  continua funcionando: ele não lista a conversa com o sistema e vê o pedido como texto.

## O que NÃO foi feito, e por quê

- **Remarcar direto pelo chat do médico**: a recepção remarca pela agenda, com as regras dela. O
  médico pede e não mexe. Esse é o desenho da casa ("a agenda é do Clinica", "nada sai para o
  paciente sem revisão humana").
- **Pedido automático quando o paciente diz "não vou"**: o sino da recepção já avisa ("Não vem —
  remarcar"). Um pedido a mais seria um segundo aviso do mesmo fato.
- **Assinar alertas** ("me avise quando alguém da minha agenda desmarcar"): é uma boa próxima etapa.
  Precisa decidir com a direção se o médico quer ser interrompido no consultório.
- **Anexo e correção de mensagem pelo WhatsApp**: ficam nos apps, onde abrir a ficha e o documento
  registra o acesso ao prontuário.
