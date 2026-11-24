# CRM com cara de WhatsApp — F1: a tela (24/11/2026)

O dono aprovou em 24/11/2026 as propostas v2, v3 e v4 ("Perfeito! Pode prosseguir e implementar!"). Esta frente é a F1, a
tela da aba WhatsApp da Recepção, do Design SemDor. A F1b (corretor, acesso, notificação e atalhos) é do Raio-X e já está na
`main` (#378); a F2 (servidor do CRM e contrato) é do Arquiteto. O plano das frentes está em
`Shared/Clinica/crm-ux/IMPLEMENTACAO.md`.

## O que mudou na tela

| Antes | Agora (como no WhatsApp) | Onde a regra mora |
|---|---|---|
| Segmentado Todas · Minhas · Encerradas + chip "Não lidas" | Busca em pílula e as pílulas **Tudo · Não lidas · Minhas · Sem resposta**; "Encerradas" é a linha de cima da lista, como as Arquivadas | `ConversasNaTela.Pilulas`, `ContagemDaPilula`, `PilulaAtual` |
| — | **Sem resposta** (a direção): todas as conversas abertas em que a última fala é do paciente, de quem quer que sejam, a mais antiga primeiro, com quem atende e há quanto tempo na linha | `ConversasNaTela.EsperaResposta` (a regra do sino sem o filtro de dono e sem o mínimo de 5 min) |
| Cinco botões escritos no cabeçalho e o ícone da ficha | Avatar, nome (abre os Dados do contato), subtítulo curto, lupa e **⋮** com Assumir/Devolver, Transferir, Nota, Buscar, Dados do contato, Resumo e Encerrar | `MenuDoWhatsApp.DoCabecalho` |
| Faixa com fila, responsável, janela, próximo horário e caso | Faixa curta: com quem está e a janela de 24 h. Fila, próximo horário e caso foram para os Dados do contato | — |
| O aviso que pede ação, acima do campo | **Próximo passo fixado** no topo, como a mensagem fixada: até 2 botões à vista e o resto no "Mais" | `ConversasNaTela.DividirBotoes` |
| Balões com borda, papel pontilhado, "N novas mensagens" com linhas verdes | Papel claro liso, balão verde da clínica (#D9FDD3), branco do paciente, nota amarela centrada, "1 mensagem não lida" na pílula branca, ✓✓ lidos no azul de ação | `ConversasNaTela.LinhaDoTempo` (o texto) |
| Abas Responder · Nota interna; Anexar, Modelos e Respostas rápidas escritos; Enviar escrito | 😊 📎 [Mensagem] ✨ e o botão redondo: microfone sem texto, enviar com texto. Anexar, Modelos e Respostas rápidas no 📎; **"/" no começo do campo** busca respostas e modelos numa lista só (no máximo 8); a nota entra pelo ⋮, pelo balão ou pela faixa e pinta o campo de amarelo | `MenuDoWhatsApp.DoClipe`, `ConversasNaTela.TermoDaBarra`, `OpcoesDaBarra`, `ComEmoji` |
| Botão "Perguntar ao profissional" ao lado do balão; menu do botão direito com 9 a 12 itens | A **setinha do balão** abre o mesmo menu do botão direito, que começa por Responder, Copiar, Nota para a equipe e Perguntar ao profissional | `MenuDoWhatsApp.DaMensagem` |
| Coluna 3 fixa ("Do sistema da clínica") | **Dados do contato**: o painel abre pelo nome ou pelo ⋮ e fecha no ✕. Leva tudo o que a coluna tinha (caso e "Como foi?", ficha, para resolver, horários com status e botões, confirmação) | — |
| Esc tratado em quatro lugares | Os atalhos da F1b: **Ctrl+F** busca (na conversa; sem conversa, na lista), **Ctrl+Alt+N** começa uma conversa, **Esc** fecha uma coisa por vez: o "/" ou o popover, a busca, os Dados do contato, a conversa. Menu e formulário fecham com o próprio Esc | `AoAtalhoAsync` (tela) |

Nenhuma função saiu: o que era botão está no ⋮, no 📎, no balão, na faixa ou nos Dados do contato. Reagir entra quando a F2
entregar o envio de reação.

## 🧑‍💻 UX

- A recepção usa o que já sabe do WhatsApp no celular: a mesma lista, o mesmo cabeçalho, o mesmo campo, a mesma setinha.
- Menos texto à vista: o cabeçalho tinha até 6 botões e o rodapé até 6; agora são lupa e ⋮ em cima, e 😊 📎 ✨ e o botão
  redondo embaixo. Quem não pode fazer algo vê o item apagado com o motivo, como antes.
- O que pede ação continua na frente: a faixa fixada fica no topo da conversa, sem rolar.
- A direção acha quem espera sem abrir conversa por conversa: a pílula "Sem resposta" traz todas, a mais antiga primeiro.
- O "/termo" nunca vai ao paciente: o Enter com o "/" aberto escolhe a opção, não envia.
- Contraste medido (texto 4,5:1, ícone 3:1): cinza `#54656F` 5,37–6,06; nota `#8A6100` no amarelo 5,12; ação `#1E62AC`
  5,42–6,18; número de não lidas branco no `#008069` 4,89; ícone branco no botão `#00A884` 3,03.
- Fotos em 1366, 1024, 760 e 390 px: `Shared/Clinica/crm-ux/fotos-f1/`.

## 🛠️ DX

- Regras novas na Application, puras e com teste (`CampoComCaraDeWhatsAppTests`): a pílula e o filtro "Sem resposta", o ⋮,
  o 📎, o "/", os emojis e a divisão dos botões da faixa. `MenuDoWhatsAppTests` e `ConversasNaTelaTests` acompanham.
- A tela ganhou um arquivo (`ConversasDoWhatsApp.CaraDeWhatsApp.cs`): os três menus novos usam o `MenuDeContexto` que já
  existia, e cada item chama o handler que já existia. O componente `AcoesDaMensagemRecebida` saiu inteiro (o item foi para o
  menu do balão).
- Cores: um bloco `--wa-*` em `tokens/web-extras.css`. A tela troca `--acento` pelo azul de ação dentro de `.pagina.wa`, então
  os botões da casa (`.botao.primario`, foco) ficam no #1E62AC só nesta aba. O violeta do @SemDor saiu (regra do dono).
- O Esc mora num lugar só (o atalho Fechar). O script repassa o Esc na captura, antes do campo; por isso menu e formulário,
  que têm o próprio Esc, fazem o atalho não agir.
- Testes de tela atualizados para o desenho novo (pílula "Tudo", ⋮, setinha do balão, atalhos pelo `SinalDeVolta`).

## 🤝 CX

- Parece o WhatsApp que a clínica já usa, com as cores da SemDor e sem cara de template.
- Nada muda no servidor nem no contrato: só a tela. Publicar com a Recepção e o Gerente, depois da F1b.
- Risco baixo: as escritas são as mesmas, pelos mesmos serviços. O que mudou é onde cada botão mora.

## O que não foi feito

- Reagir, etiquetas, fixar, ★, rascunho na lista, "escrevendo", áudio 1,5x e o resto da v3 e da v4: dependem da F2 (contrato)
  e entram na F3.
- O CRM web no celular (F4).
- Não testado num Windows de verdade: as fotos são do Chromium do Linux na prévia, com o relógio fixo.
