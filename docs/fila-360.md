# Fila 360: o sistema de retenção (nov/2026)

Pedido da direção: um sistema de retenção de verdade, fora da tela do WhatsApp. A Patrycia
(gerente) cuida de quem está em tratamento antes que desista e traz de volta quem parou, **só por
mensagem de WhatsApp**. O protótipo aprovado está no artefato "Sistema de Retenção SemDor". Esta é
a **Fase 1**.

## Onde fica

**Relacionamento → Fila 360**, no Gerente, na Recepção, no Faturamento e no portal `/gestao`. É
a mesma chave de antes (`retorno-pacientes`): a Fila 360 **substituiu** o "Acompanhamento de
pacientes" (uma porta por pergunta). O "Ver caso" do WhatsApp e os atalhos que levavam ao
Acompanhamento continuam chegando nela.

## O que ela faz

| Parte | O que mostra | Onde mora a regra |
|---|---|---|
| **Hoje** (abre nela) | Só o que pede ação hoje, agrupado pela ação (Responderam, Ninguém pegou ainda, Mandar mensagem, Querem marcar, Acompanhar o plano, Conferir na agenda, Pedir autorização no balcão), do maior risco para o menor | `Fila360NaTela.Hoje` |
| **Responderam** | Quem escreveu depois do convite; vem antes de tudo | `Fila360NaTela.QueResponderam`, a mesma regra do "Respondeu" da conversa (`RetencaoNaTela.Respondeu`) |
| **Risco** | Nota de 0 a 100, faixa (baixo, médio, alto) e o motivo que mais pesou; todos os motivos com os pontos no title e na ficha | `Fila360NaTela.Risco` |
| **Carteira** | A fila inteira de antes (funil, filtros, CSV), com a coluna Risco no lugar do trilho e o atalho "Risco alto" | `AcompanhamentoNaTela` + `Fila360NaTela` |
| **Ficha 360** | A conversa do paciente (trilho, "Como foi?", histórico) com o painel "Por que está em risco" | `ConversaDoPacienteForm` |

**O risco é explicável:** cada sinal soma pontos, e a tela mostra quais.

| Sinal | Pontos |
|---|---|
| Sem vir há 30, 60, 90 ou 120 dias | +10, +20, +25 ou +30 |
| Indicado ao BSV sem a 1ª sessão: até 6 dias, 7 dias ou mais, 14 dias ou mais | +5, +15 ou +25 |
| Tratamento longo (5 sessões ou mais) | +15 |
| Pacote com saldo | +15 |
| Faltou | +15 |
| Cancelou | +10 |
| Tentativas sem resposta: 1, ou 2 e mais | +5 ou +10 |
| Contato atrasado | +10 |
| Disse que quer marcar | −15 |
| Já tem sessão marcada | −30 |

Alto é a partir de 60 pontos; médio, a partir de 35. Só usa campos que a `LinhaAcompanhamento` já
traz: nenhuma consulta a mais no banco.

**A mensagem sai pelo WhatsApp da clínica, sem sair da Fila 360**: o botão do passo abre a conversa
ao lado (a gaveta), e o convite marca o caso. Com o CRM novo (semdor-crm#50), a resposta do
paciente volta para quem convidou, na fila Retenção, longe da recepção.

## 🧑‍💻 UX

- A Patrycia abre a Fila 360 e vê só o que é de hoje, já na ordem de quem mais pode parar.
- Quem respondeu aparece em verde, no topo.
- O risco diz o porquê: "Sem vir há 99 dias", "Pacote com saldo parado". Ninguém precisa adivinhar a nota.
- Nada que a tela fazia sumiu: assumir, filtros, gerar recall, configurar, a visão por responsável e o CSV (que agora leva o risco e o motivo).

## 🛠️ DX

- Uma classe pura nova (`Fila360NaTela`) com testes (`Fila360NaTelaTests`).
- A tela é a do Acompanhamento evoluída (`git mv`), e o formulário é o mesmo, com um bloco a mais.
- O "Responderam" lê as conversas do CRM em fila, no mesmo escopo. Se o CRM falhar, a fila abre sem o grupo e a falha vai ao log.
- O catálogo do portal foi regenerado por `tools/gerar-catalogo-gestao-web.py`.

## 🤝 CX

- Sem migration, sem dado novo e sem custo novo.
- O nome muda no menu de quatro apps e do portal. O Faturamento só muda quando for republicado, e não precisa ser agora.

## O que não está nesta fase, e por quê

- **"Como está" (sentimento) e a Escuta pós-sessão.** O "Como está" é dado de saúde: exige coluna nova, a permissão do prontuário e a trilha de acesso. Entra na Fase 3, com a Escuta e o alerta ao profissional.
- **Réguas de cuidado** (sequências automáticas por fato da agenda): Fase 2.
- **Painel com metas e taxa de retenção mês a mês:** Fase 3. O funil da Carteira já mostra quem voltou em 30 dias e a taxa.
- **A tela de conversas própria da Patrycia** e a saída do modo Retenção da aba WhatsApp: próximo PR. Até lá, a conversa abre na gaveta ao lado, e o modo antigo continua existindo.
