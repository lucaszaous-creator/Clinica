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
