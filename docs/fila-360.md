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

## O que ela faz (igual ao protótipo)

Um módulo com **menu lateral próprio**: Painel, Fila de hoje, Conversas, Carteira e, em Configurar, Gerar
recall, Por responsável e Configurar. No rodapé do menu fica quem voltou em 30 dias.

| Página | O que mostra | Onde mora a regra |
|---|---|---|
| **Painel** | Casos abertos, risco alto, para hoje, quem respondeu e quem voltou em 30 dias (com a taxa); o gráfico "Voltaram, mês a mês"; o resumo de hoje; por que pararam; a equipe | `Fila360NaTela.Numeros`, `VoltaramPorMes`, `Equipe` |
| **Fila de hoje** (abre nela) | O progresso do dia ("3 de 12 feitos") e só o que pede ação hoje, agrupado pela ação (Responderam, Ninguém pegou ainda, Mandar mensagem, Querem marcar, Acompanhar o plano, Conferir na agenda, Pedir autorização no balcão), do maior risco para o menor, com a resposta do paciente ou o motivo do risco | `Fila360NaTela.Hoje`, `ProgressoDeHoje` |
| **Conversas** | A caixa da retenção: as conversas dos casos abertos com ficha conferida, quem respondeu primeiro; a conversa do WhatsApp nativo no meio (o mesmo modo painel da gaveta); o risco e as ações à direita | `Fila360NaTela.ConversasDaFila` |
| **Carteira** | Todos os casos, do maior risco para o menor, com o "Por quê", as visões (Todos, Risco alto, Novos no BSV, Sem autorização de mensagem, Encerrados), a busca, "Mais filtros" e o CSV | `Fila360NaTela.Carteira` |
| **Ficha 360** (página) | Cabeçalho com os selos e as ações, os cinco fatos, por que está em risco (o anel e cada motivo com os pontos), a jornada (sessões, faltas e o que está marcado nos últimos 6 meses), as mensagens e acontecimentos e a próxima mensagem | `Fila360NaTela.Risco`, `Jornada`; `IAcompanhamentoPacienteService.JornadaAsync` |

Nas **Conversas**, a página ocupa a área inteira, como a aba WhatsApp: sem cabeçalho e sem margem. O Atualizar
fica no rodapé da lista. O "Como foi?" sai do meio da conversa e vai para a coluna da direita, com os cinco
desfechos. Cada um abre o formulário já com o desfecho escolhido. Até 1280 px, onde a coluna some, o cartão
volta para o meio da conversa.

O "Como foi?" continua sendo o mesmo formulário (`ConversaDoPacienteForm`, agora com `DesfechoInicial`), aberto pelo
"Registrar como foi". Ele ganhou o bloco do risco e perdeu o "Copiar o telefone para ligar": a
retenção é só por mensagem.

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
- **Metas e a taxa de retenção mês a mês:** Fase 3. Elas pedem histórico guardado e meta cadastrada. O Painel já mostra quem voltou, mês a mês, e a taxa dos contatados em 30 dias.
- **As Réguas de cuidado e a Escuta** não estão no menu ainda. Elas entram quando tiverem banco e regras de verdade: nenhum botão para função que não existe.
- **A saída do modo Retenção da aba WhatsApp:** próximo PR. A Fila 360 já faz o trabalho dele.
