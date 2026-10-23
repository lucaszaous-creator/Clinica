# @SemDor — o assistente da clínica no chat e no WhatsApp (out/2026)

O "Sistema SemDor" do chat da equipe (`docs/sistema-semdor-e-pedidos-a-recepcao.md`) vira o
**@SemDor**: o mesmo bot, com o símbolo da clínica como avatar (opção 2A, animado) e com três
capacidades novas, entregues em fases, cada uma no seu PR:

| Fase | O que entrega | Precisa de |
|---|---|---|
| **1 · Remarcação pedida pelo paciente** | O paciente toca REMARCAR no lembrete → cartão no # Recepção com horários livres → a recepção escolhe até 3 → o paciente recebe os botões no WhatsApp → escolhe → um clique da recepção remarca e confirma ao paciente | Clinica + CRM (contrato da ponte) |
| **2 · IA para texto livre** | "não vou conseguir quinta, tem sexta de tarde?" entra no mesmo fluxo da fase 1; o @SemDor entende pergunta livre no chat e no WhatsApp da equipe | Chave da API, decisão LGPD (abaixo), chave liga/desliga |
| **3 · @SemDor em qualquer conversa** | Marcar @SemDor num canal ou numa direta responde ali, para todos, com as permissões de quem marcou | Clinica |
| **4 · Documentos pelo WhatsApp** | O médico pede atestado/declaração/receita; sai sem assinatura (marcado como tal) ou com A1 por link de uso único que abre a página de assinatura do portal | Autorização da direção (abaixo), homologação com A1 real |

## Decisões da direção (22/10/2026)

Registradas aqui porque revertem ou abrem exceção a decisões anteriores:

1. **A IA volta, revogando "a direção retirou a IA do CRM".** A direção autorizou por escrito, na
   conversa de 22/10/2026, que texto de conversa vá a um modelo de linguagem da Anthropic (Claude).
   ⚠️ **Isso reabre a transferência internacional** que a clínica tinha ELIMINADO ao trazer o banco
   para a Locaweb (`docs/conformidade-lgpd.md`, item 10): a Anthropic processa nos EUA. O que a
   construção garante, e só isso:
   - **O mínimo vai ao modelo**: o texto da mensagem e, quando preciso, primeiro nome e horários.
     Nunca CPF, documento, convênio, prontuário, evolução ou anexo.
   - **O modelo não decide nada**: ele classifica a intenção e redige. Remarcar, cancelar, emitir
     e assinar continuam passando pelo clique de quem atende (as regras do CRM e da clínica).
   - **Liga e desliga por variável** (nada sai sem a variável — regra do CRM), e desligada é o
     comportamento de hoje (regras e botões).
   - ❌ **O que NÃO está resolvido e não é código**: o contrato com a Anthropic (DPA) e a base legal
     do art. 33 (cláusulas-padrão da ANPD ou outra hipótese). Até isso existir, o item 10 do placar
     de conformidade volta a ⚠️ por este motivo, e é assim que se responde a quem auditar.
2. **Assinatura: só o link.** A direção autorizou o link de uso único que abre a página de
   assinatura A1 que já existe no portal. O motor (`Assinatura/`, cofre do A1, PAdES, hash)
   continua congelado; a senha do A1 nunca passa pelo WhatsApp. Teste com A1 real em homologação.
3. **Ordem**: remarcação primeiro.

## Fase 1 — remarcação pedida pelo paciente

### O fluxo

1. O lembrete sai como hoje (`semdor_lembrete_sessao`, botões Confirmar · Remarcar · Cancelar).
2. O paciente toca **Remarcar**. Hoje isso só grava "pediu para remarcar" na confirmação
   (`ClinicAgendaBridge.DecideCore`). Agora a MESMA transação cria um **pedido à recepção** do tipo
   remarcar, de origem **paciente** (não há usuário autor), e o cartão aparece no # Recepção.
3. O cartão traz os **próximos horários livres do mesmo profissional** (`BuscaDeVagasService`,
   os mesmos critérios da ponte) e a recepção marca até **3** e toca **Oferecer ao paciente**.
4. A clínica põe a oferta numa fila; o CRM a busca pela ponte e manda ao paciente uma mensagem com
   **botões** (o WhatsApp aceita até 3 — por isso o teto). A janela de 24 h está aberta porque o
   paciente acabou de responder; se fechou, o CRM devolve "janela fechada" e o cartão diz isso.
5. O paciente toca um horário. O CRM manda a escolha pela ponte; o cartão vira
   **"João escolheu sex 15h30 · [Remarcar e confirmar]"**.
6. Um clique da recepção remarca pelo `AgendaService.RemarcarAsync`, com os conflitos conferidos
   DE NOVO (o horário pode ter sido ocupado). Ocupado → o cartão avisa e volta ao passo 3. Feito →
   o pedido se encerra e a confirmação vai ao paciente pela mesma fila.

### O que NÃO foi feito na fase 1

- Texto livre ("quinta não dá") não entra no fluxo: é a fase 2.
- Fora da janela de 24 h a oferta não sai (falta o modelo aprovado pela Meta para oferecer
  horários; `semdor_vaga_disponivel` existe para outro uso).
- O cancelamento com reaproveitamento da vaga fica para depois.

## 🧑‍💻 UX

- A recepção não abre o CRM: o pedido do paciente chega ao chat dos apps, com os horários
  prontos, e cada passo é um clique.
- O paciente não digita nada: toca o botão do lembrete e depois o do horário.
- Nada é remarcado sem o clique da recepção, e o paciente só recebe "remarcado" quando foi.

## 🛠️ DX

- Reaproveita o pedido à recepção (tabela, cartão, transições, testes), a busca de vagas e a
  remarcação da agenda; o que é novo é a ORIGEM do pedido, a oferta e duas rotas na ponte.
- `ClinicContracts.cs` muda igual nos dois repositórios (campos novos no fim, com padrão).
- A regra de quais horários oferecer mora na Application, com teste.

## 🤝 CX

- É a demonstração mais visível do produto: o paciente remarca pelo WhatsApp em dois toques e a
  recepção resolve sem telefonema.
- Risco em produção: só depois do lembrete com botões já em uso; liga por variável no CRM.
- Custo: nenhum serviço novo na fase 1 (Meta já paga, mensagem dentro da janela é gratuita).

## Fase 2 — a IA lê o texto livre (o lado da clínica)

A IA mora no CRM (`semdor-crm/docs/semdor-ia.md`) e **só lê e reescreve**. Aqui, na ponte, entram:

- **`ClinicDecision.Note`**: no "reschedule" que veio de texto livre, a preferência que a IA leu.
  Ela vai para o pedido do paciente (`RemarcacaoPeloPacienteRegras.DetalheDaPreferencia`):
  "Preferência do paciente (lida pelo @SemDor): sexta à tarde". É uma leitura de máquina, e a
  recepção confere na conversa.
- **`StaffAsk.Probe` / `StaffAnswer.Understood`**: a ponte responde se as regras ENTENDEM o texto,
  sem gravar nada (`AssistenteWhatsAppService.EntendeAsync`). O CRM só chama a IA quando elas não
  entendem; a pergunta reescrita volta pelo caminho normal, e quem responde é a clínica.

Os dois campos estão no fim dos records, com padrão. CRM antigo e ponte nova (ou o contrário)
continuam se entendendo.

### 🧑‍💻 UX · 🛠️ DX · 🤝 CX

- **UX**: o pedido que o paciente escreveu chega ao # Recepção com a preferência dele.
- **DX**: dois campos no contrato, um método de leitura sem efeito colateral e testes em
  `RemarcacaoPeloPacienteTests` e `AssistenteWhatsAppServiceTests`.
- **CX**: a ponte não chama serviço externo nenhum. A IA e a chave ficam só na VPS 2.

## Fase 3 — @SemDor em qualquer conversa

Marcar **@SemDor** num canal ou numa direta faz o sistema responder **ali**, com as mesmas regras
da conversa com o Sistema SemDor (`SistemaEquipeService`) e com as permissões de quem perguntou.

- **A resposta só fica à vista quando todos ali podem vê-la**: cada participante ativo precisa de
  `VerAgenda` e `VerFichaPaciente` (`ChatEquipeRegras.RespostaPodeFicarAVista`). Basta um que não
  tenha (o # Geral tem o financeiro), e a resposta vai para a conversa de quem perguntou com o
  sistema. O canal recebe só o aviso "Respondi a … em particular", sem paciente nem horário.
- A menção é a palavra inteira depois do @ (`contato@semdor.com.br` e `@SemDorinha` não contam).
  O campo de escrever sugere o @SemDor, com o avatar dele, ao digitar "@se…".
- Na conversa com o próprio sistema nada muda: ali toda mensagem é pergunta.

### O que NÃO foi feito na fase 3

- Texto livre continua lido pelas regras (as perguntas da ajuda); a IA é a fase 2.
- O @SemDor não cria pedido à recepção a partir do canal: o pedido continua pelo botão "Pedir à
  recepção" ou pela conversa com o sistema.

### 🧑‍💻 UX · 🛠️ DX · 🤝 CX

- **UX**: quem está no # Recepção pergunta sem trocar de conversa, e a equipe toda vê a resposta.
- **DX**: duas regras puras com teste (a menção e a visibilidade) e um ramo no `EnviarAsync`;
  nenhuma tabela nova. Testes: `SemDorNaConversaTests`.
- **CX**: nada sai da clínica (são as regras, sem IA), e nenhum dado de paciente aparece para quem
  não poderia vê-lo na tela.

## Fase 4 — documento pelo WhatsApp, com link de uso único (desligada)

O médico escreve ao número da clínica, do celular ligado ao login dele em Acessos:

| Frase | O que nasce |
|---|---|
| `atestado Maria Silva 2 dias` | Atestado, 2 dias de afastamento, texto padrão para revisar |
| `comparecimento Maria Silva` | Declaração de comparecimento (o PDF já escreve a frase da declaração) |
| `receita Maria Silva: dipirona 500 mg…` | Receita com o texto depois dos dois-pontos, como foi escrito |
| `exame Maria Silva: hemograma` · `relatório Maria Silva: …` | Pedido de exame · relatório |

1. **A ponte grava um RASCUNHO** (`PedidosDocumentoWhatsApp`), nunca o documento. A paciente é
   procurada só na agenda DE QUEM PEDIU (60 dias para trás, 30 para a frente); duas pacientes com
   o nome citado → a resposta lista e pede mais do nome. Receita, exame e relatório sem conteúdo,
   ou atestado sem os dias, voltam com a pergunta — o sistema não inventa conduta.
2. **A resposta leva o link**: `https://<portal>/profissional/assinar.html#<token>`. O token
   (32 bytes) vai depois do `#`, que o navegador não manda ao servidor nem ao Referer; no banco fica
   só o SHA-256. Vale **15 minutos e abre uma vez só** (`AbertoEm`). No chat do app, a mesma
   resposta fica gravada SEM o link.
3. **No portal** (`RotasLinkWhatsApp`, `LinkDocumentoTabletService`), abrir o link cria uma sessão
   `modo = "link"` de 15 minutos, num cookie próprio (`__Host-clinica.tablet.link`), presa ao
   rascunho: ela só alcança `/api/link/*` — o rascunho, o documento que ele gerou, o PDF desse
   documento e o envio dele. As rotas da equipe (`/api/clinico`, `/api/posto`…) continuam pedindo
   o tablet cadastrado e o login, e recusam essa sessão.
4. **O médico revisa e emite** pela MESMA emissão do atendimento no tablet
   (`AtendimentoTabletService.EmitirConteudoAsync`), datada de hoje, só para paciente que tem ou
   teve horário com ele. Repetir o toque devolve o mesmo documento.
5. **Assina com o A1** pelas MESMAS chamadas do portal (`A1TabletService.Preparar` → `Assinar`),
   com a senha digitada só na página. O A1 precisa estar cadastrado antes em **Meu certificado**.
   Sem A1 (ainda não homologado, ou por escolha), **"Concluir sem assinatura digital"** deixa o
   documento emitido como qualquer documento do tablet que se imprime e assina à mão.
6. **Envio ao paciente (opcional)**: o botão põe o PDF na fila do CRM
   (`EnvioAoPacienteService.EnfileirarWhatsAppAsync`), a mesma do "Enviar ao paciente" do desktop.
   Sai quando o CRM estiver com o envio clínico ligado (`CRM_CLINICAL_DISPATCH_ENABLED`) e o
   modelo aprovado pela Meta.

A sonda da fase 2 (`EntendeAsync`) conta o pedido de documento como ENTENDIDO mesmo com a fase 4
desligada: o texto clínico de uma receita ou de um relatório nunca vai à IA para ser reescrito.

### Como ligar (nesta ordem, depois da homologação do A1)

1. Publicar a ponte e o portal com a migration `20261022192147_DocumentoPeloWhatsApp` e rodar os
   grants: `deploy/ponte-crm/papel-ponte-crm.sql` (a ponte: INSERT no rascunho) e o
   `deploy/tablet/atualizar-posto.py` (o portal: SELECT/UPDATE no rascunho; SELECT/INSERT na fila
   de envios).
2. Portal: `Portal__LinkWhatsApp__Habilitado=true` (e `Portal__A1__Habilitado=true` para assinar).
3. Ponte (VPS 1): `CRM_STAFF_DOCUMENTS_ENABLED=true` e
   `CRM_STAFF_DOCUMENTS_PORTAL_URL=https://portal.clinicasemdormacae.com.br`.
4. clinica-site: publicar o pacote do portal com `profissional/assinar.html`.

Sem qualquer um dos três interruptores, a frase segue o caminho de hoje (pergunta ou pedido à
recepção) e as rotas `/api/link` não existem.

### O que NÃO foi feito na fase 4

- **SafeID pelo link**: só o A1. O SafeID é o motor congelado com cobrança por tentativa
  (`docs/safeid-congelado.md`) e não foi autorizado para esta porta.
- **Cadastrar o A1 pelo link**: o cadastro continua só no portal da equipe, com o tablet.
- **Receita de controle especial e infusão**: não entram pelo WhatsApp (pedem endereço e
  conferência que o rascunho não tem).
- **Aviso ao médico pelo WhatsApp depois de assinar**: a própria página mostra o desfecho.
- **A suíte contra o Postgres** não rodou nesta máquina (precisa da senha do `postgres`). A
  migration, o `ExecuteUpdate` e o `pg_advisory_xact_lock` do rascunho rodaram só no SQLite.

### 🧑‍💻 UX

- O médico pede do celular em uma linha e recebe o link na hora; a página cabe no celular (320 px
  com fonte em 200%) e tem três passos à vista: revisar e emitir, assinar, enviar.
- Falta de informação (dias, conteúdo, nome repetido) volta como pergunta curta, com o exemplo.
- Link vencido ou já usado diz isso e o que fazer ("peça de novo pelo WhatsApp").

### 🛠️ DX

- Uma entidade (`PedidoDocumentoWhatsApp`), uma regra pura (`DocumentoPeloWhatsAppRegras`), um
  serviço de porta (`LinkDocumentoTabletService`), uma emissão de 10 linhas sobre a que já existe
  (`AtendimentoTabletService.Link.cs`) e um arquivo de rotas. Nenhum serviço de assinatura mudou;
  a única linha mexida na autorização é aceitar o modo `link` em `AtendimentoTabletService.AutorizarAsync`.
- Testes: `DocumentoPeloWhatsAppTests` (regras e ponte), `LinkWhatsAppHttpTests` (abrir uma vez,
  vencido, sessão do link fora das rotas da equipe, emitir uma vez, A1 fictício com PDF íntegro,
  concluir sem A1) e `clinica-site/ferramentas/testar-portal-link-whatsapp.mjs` (a página).

### 🤝 CX

- Atestado e declaração sem o médico abrir o computador — e com a mesma trilha (auditoria do link
  aberto e da emissão) e a mesma assinatura do consultório.
- Risco contido: desligado por padrão, o rascunho não é documento, o link é de uso único e curto,
  e a senha do A1 não passa pelo WhatsApp nem pelo CRM.
- Custo: nenhum serviço novo. O envio ao paciente é a mesma mensagem paga da Meta de hoje, e só
  sai quando o médico toca o botão.

## IA nos apps — o chat e o sino da Recepção (out/2026, Clinica#307)

A IA continua morando só no CRM (VPS 2); **a chave nunca vem às máquinas da clínica**. O que liga
os dois lados são três tabelas, lidas e gravadas pela ponte (migration `SemDorNosApps`, aditiva):

1. **Chat dos apps.** Quando as regras (`SistemaEquipeRegras`) não entendem a pergunta feita ao
   Sistema SemDor — ou ao @SemDor num canal — e o CRM leu a fila há menos de 2 min
   (`SemDorNoCrm.PerguntasLidasEm`), a pergunta entra em `PerguntasAoSemDor` e a resposta imediata
   diz que o @SemDor está relendo. O CRM lê a fila a cada 5 s (`GET /crm/v1/staff/questions`),
   reescreve com a IA e devolve (`POST /crm/v1/staff/questions/answer`); a ponte responde pela
   **mesma** `SistemaEquipeService`, com as permissões de quem perguntou, começando por
   *Entendi como "agenda de amanhã"*. Sem pergunta conhecida, ou depois de 2 min, vai a ajuda de
   sempre (`PerguntasAoSemDorService.VencerAsync`). Sem o CRM passando, nada muda: a ajuda sai na hora.
2. **Sino da Recepção.** O CRM manda a cada minuto a lista dos avisos "Atenção"
   (`ReceptionNotices.DoSemDor`: sintoma grave, reação ao tratamento, reclamação, sem resposta) para
   `AvisosDoSemDor` (`POST /crm/v1/semdor/alerts`). O sino da Recepção e do Gerente mostra a seção
   **"Atenção no WhatsApp"** antes de tudo, com o primeiro nome e o assunto; o clique abre a
   conversa no CRM. Lista com mais de 10 min (o CRM parou) não é mostrada como atual.
3. **Documento.** "emitir atestado…" ou "emitir documento" deixou de cair no "não entendi" (relato
   da clínica, 23/10). No WhatsApp com a fase 4 desligada, o @SemDor diz onde emitir
   (`DocumentoPeloWhatsAppRegras.PorAquiNao`); no chat dos apps ele **emite** (seção abaixo). O
   pedido sem tipo pergunta qual documento. Texto de documento nunca vai à IA.

### Como ligar (nesta ordem)

1. Publicar a ponte nova na VPS 1 e reaplicar `deploy/ponte-crm/papel-ponte-crm.sql` **depois** de
   o Gerente criar as tabelas (abrir o Gerente uma vez).
2. Publicar os apps (Recepção, Gerente, Financeiro e Consultório: o chat e o sino são da moldura).
3. No CRM: `CRM_SEMDOR_APPS_ENABLED=true` (com a IA já ligada e `CRM_UI_URL` em https).

Ponte antiga com CRM novo: o CRM fica desligado até a variável. Ponte nova com CRM antigo: ninguém
lê a fila, e os apps respondem a ajuda de sempre.

### O que NÃO foi feito

- **A IA não responde nada sozinha** nem cria pedido à recepção a partir da releitura: "remarcar
  Maria" relido vira a instrução de usar "Pedir à recepção", como hoje no chat.
- **O aviso do sino não responde ao paciente**: a resposta continua no CRM.
- **A suíte contra o Postgres** não rodou nesta máquina (precisa da senha do `postgres`); o teste
  `PontePapelRestritoTests.SemDor_nos_apps_fila_e_avisos` cobre os privilégios novos quando rodar.

### 🧑‍💻 UX

- Quem pergunta do seu jeito ("tem gente pra amanhã cedo?") recebe a resposta da agenda em poucos
  segundos, dizendo como a pergunta foi entendida — e nunca fica esperando um "relendo" sem fim.
- A recepção vê no sino do balcão quem escreveu algo grave e ainda não teve resposta, sem abrir o
  CRM; um clique leva à conversa.

### 🛠️ DX

- Três tabelas pequenas, uma regra pura (`SemDorNosAppsRegras`, `AvisosDoSemDorRegras`), um serviço
  (`PerguntasAoSemDorService`) que a ponte e o chat usam, duas rotas e uma seção do sino que segue o
  desenho das respostas dos pacientes. O contrato (`ClinicContracts.cs`) ganhou records no fim,
  idêntico nos dois repositórios. Testes: `SemDorNosAppsTests`, `MolduraBlazorTests`,
  `DocumentoPeloWhatsAppTests` e, no CRM, `SemDorNosAppsTests`.

### 🤝 CX

- A chave fica num servidor só; à IA vai só o texto da pergunta que as regras não entenderam
  (a resposta imediata diz isso). O item 10 da conformidade continua ⚠️ até o DPA com a Anthropic.
- Custo: uma chamada à IA por pergunta não entendida — a maioria das perguntas nem chega lá.
- Risco contido: desligado até a variável do CRM, e cada lado funciona sozinho se o outro faltar.

## Documento pelo @SemDor no chat dos apps (out/2026 — decisão da direção de 23/10)

"O sistema faz todo o trâmite interno e traz o documento em PDF para a conversa." A frase é a da
fase 4 — `atestado Maria Silva 2 dias`, `comparecimento Maria Silva`, `receita` (ou `prescrição`)
`Maria Silva: …`, `exame Maria Silva: …`, `relatório` (ou `laudo`) `Maria Silva: …` — escrita ao
Sistema SemDor ou com @SemDor num canal. `DocumentoPeloChatService`:

1. Só quem prescreve, com o profissional ligado ao login; a paciente é procurada só na agenda
   de quem pediu (60 dias para trás, 30 para a frente), a MESMA busca do WhatsApp.
2. Falta algo (dias do atestado, conteúdo da receita, nome repetido ou não achado)? Volta a
   pergunta, e nada é emitido.
3. O documento nasce pela MESMA emissão do Consultório (`DocumentoClinicoService.EmitirAsync`:
   número, código de conferência e auditoria no mesmo SaveChanges), datado de hoje e ligado ao
   horário achado. A resposta vem com o documento ANEXADO — o "Abrir" do chat é a 2ª via em PDF.
4. Sem assinatura digital (o A1 não está homologado), e a resposta diz isso: imprime-se e assina-se
   à mão, ou assina-se com o certificado na central de Documentos.
5. No canal, o documento vai SEMPRE para a conversa de quem pediu com o sistema; o canal recebe só
   o aviso, sem nome de paciente.

**No WhatsApp** a fase 4 foi ligada em 23/10 **sem A1** (`deploy-vps/publicacao-20261023/ligar-fase4.sh`):
o link abre o portal, o médico revisa, emite e toca "Concluir sem assinatura digital".

### O que NÃO foi feito
- **Prescrição de infusão** não sai por frase: tem itens, diluente, via e tempo, e continua no
  Consultório e no tablet. "prescrição" no chat é a RECEITA.
- **Revisar antes de emitir** não existe no chat: o texto do atestado é o padrão e o da receita é o
  que foi escrito. Errou? Cancela-se com motivo na central de Documentos e pede-se de novo.

### 🧑‍💻 UX · 🛠️ DX · 🤝 CX
- **UX**: o médico escreve uma linha e recebe o PDF na conversa, sem abrir a ficha nem a tela de
  emissão; dúvida vira pergunta curta com o exemplo.
- **DX**: um serviço (`DocumentoPeloChatService`) sobre a emissão que já existe, regras puras em
  `DocumentoPeloChatRegras`, a busca da paciente compartilhada com o WhatsApp e o anexo de documento
  que o chat já tinha. Testes: `DocumentoPeloChatTests`.
- **CX**: mesmo número, mesma trilha e mesmo PDF do Consultório; nada vai à IA nem sai da clínica;
  o documento de paciente não aparece no canal.
