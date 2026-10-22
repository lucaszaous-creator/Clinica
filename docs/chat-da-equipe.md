# Chat da equipe (out/2026)

Mockup aprovado: `docs/mockups/chat-da-equipe.html` (v3). Vale para os quatro apps da suíte
(Recepção, Consultório, Financeiro, Gerente Geral). O Faturamento fica de fora: ele não usa a
moldura Blazor.

## O que é

Uma conversa interna da clínica, dentro do próprio sistema, em três posições:

- **Bolinha** no canto de baixo à direita, com o número de não lidas. Quando chega mensagem
  direta, menção ou pedido da enfermagem, uma prévia aparece por 8 segundos acima dela. Dá
  para ARRASTAR a bolinha para qualquer lugar da tela (mais de 6 px de movimento é arrasto;
  menos é clique).
- **Janelinha** no canto, por cima da tela, ao clicar na bolinha. Arrasta-se pelo cabeçalho.
- **Painel encostado** na lateral direita. A tela de trás encolhe para dar lugar.
- **Ampliado**: o próprio chat cresce até quase a tela inteira, com o campo de escrever alto —
  é onde se escreve o laudo. Ampliado, Enter quebra a linha e Ctrl+Enter envia; "Reduzir" ou
  Esc voltam ao tamanho de antes.

A posição (onde a bolinha e a janelinha foram deixadas), a conversa aberta e as preferências de
som ficam guardadas por pessoa, neste computador. A posição guardada nunca põe o chat fora da
janela do app (ela pode ter ficado menor) nem por cima do rodapé de um formulário aberto.

## Conversas

- **Três canais fixos**: `# Geral` (toda a equipe), `# Recepção` (balcão, enfermagem e
  direção) e `# Enfermagem` (enfermagem, quem atende e direção). Quem vê cada canal sai do
  PERFIL de acesso (`ChatEquipeRegras.ParticipaDoCanal`). Não há grupo livre.
- **Diretas** entre quaisquer duas pessoas ativas. A lista mostra toda a equipe ativa, com
  presença: online (verde), em atendimento (laranja, com a hora prevista de fim) e fora.
- **Quem saiu da equipe** (usuário desativado) continua na lista, no fim e apagado, se havia
  conversa com a pessoa: a direta abre para LEITURA ("saiu da equipe · só leitura"), sem campo
  de escrever, e o serviço recusa mensagem nova. O histórico continua na busca.

## O que dá para mandar

- **Texto sem limite**. Mensagem longa (mais de 500 caracteres ou 8 linhas) chega recolhida,
  com "Ver inteiro", "Copiar texto" e a contagem de caracteres. "Ampliar" aumenta o próprio
  chat para escrever o laudo (ver "Ampliado" acima).
- **Paciente anexado**: quem recebe abre a ficha num clique (com `VerFichaPaciente`).
- **Documento emitido** do paciente anexado: quem recebe abre a 2ª via idêntica. Só aparecem
  os documentos que quem escreve pode ver, e abrir registra o acesso ao prontuário. O SERVIÇO
  confere de novo (não só a tela): quem não vê a ficha não anexa paciente, quem não vê o tipo do
  documento não o anexa, o documento tem de ser do paciente anexado e não pode estar cancelado.
  Quem recebe e não tem acesso ao tipo vê só "Documento clínico", sem o número e sem abrir; a
  prévia da bolinha e da lista diz "documento clínico" para todos.
- **Arquivo** de até 10 MB, só documento, planilha, imagem e texto
  (`ChatEquipeRegras.ExtensoesPermitidas`). Quem recebe abre o anexo direto no posto, então
  programa, atalho, script e compactado não vão pelo chat — conferido no envio e de novo ao abrir.
- **@menção** com sugestão: ao escrever "@" a tela lista as pessoas da conversa (setas, Enter
  ou Tab escolhem; Esc fecha). A escolha escreve "@Ana" quando o primeiro nome é só dela, e
  "@Ana_Souza" quando há outra Ana — "@Ana" digitado à mão chama as duas, de propósito (citar
  não pode silenciar quem foi chamado). Só toca para quem enxerga a conversa.
- **Correção** da própria mensagem: ela ganha "(editada)" e a versão anterior fica guardada.
  No chat nada se apaga.

## Pedidos da enfermagem no # Recepção

Os pedidos aparecem como cartões entre as mensagens do `# Recepção`. Eles NÃO são copiados
para o chat: o cartão lê o mesmo pedido da tela "Pedidos da enfermagem". "Abrir pedido" leva
à mesma tela que o sino abre, e "Marcar como visto" chama o mesmo serviço. O número vermelho
ao lado do canal conta os pedidos novos.

## Busca

A lupa procura nas mensagens de todas as conversas que a pessoa vê, da mais recente para a
mais antiga (30 de cada vez), sem acento e sem diferença de maiúscula: "lucia laudo" acha
"Laudo da Lúcia". Cada resultado mostra a conversa, quem escreveu, quando e o trecho com a
palavra; clicar abre a conversa NA mensagem, destacada, com "Ir para as mais recentes".

## Som e avisos

Mensagem direta e menção tocam um toque curto, e pedido da enfermagem toca um toque grave.
Mensagem comum de canal só sobe o número. Cada toque pode ser desligado, e "Silenciar por 1
hora" cala tudo sem parar a contagem. O som é sintetizado no navegador da janela, sem
arquivo de áudio. O que toca também faz a janela do app PISCAR na barra de tarefas quando ela
está atrás de outro programa (`IHostDaTela.ChamarAtencaoAsync`, `FlashWindowEx`), como o Teams.

Quem usa leitor de tela ouve o que chega (região `aria-live`), e Esc fecha o que o chat tem
por cima, um de cada vez.

## Entrega

O gatilho `clinica_avisar_mensagens_equipe` avisa as outras máquinas na hora (o mesmo
`IAvisosDoBanco` do sino), e o chat responde com o PULSO. Um relógio de fundo continua: aviso
perdido não deixa o chat parado.

## ⚡ Desempenho — por que o chat não trava com anos de conversa

**A pergunta certa não é "quantas mensagens", é "quantas idas ao banco".** O banco fica a
~250 ms do posto (`docs/desempenho-leituras-2026-10-15.md`: 186–288 ms de ping). Uma consulta
que lê 40 linhas por índice custa praticamente o mesmo que uma que lê 1; dez consultas em fila
custam 2,5 s. É por isso que o WhatsApp e o Telegram não travam:

1. **O aparelho guarda o que já viu** e pergunta ao servidor só "o que há depois da última que
   eu tenho". Aqui: o pulso (`PulsoAsync`, Id > X) e a releitura (`AtualizarAsync`); a
   conversa já aberta reaparece NA HORA, da memória da tela, enquanto a releitura corre por baixo.
2. **Nada lê o histórico inteiro**: tudo vai por índice — a chave da mensagem, (Conversa, Id),
   (Conversa, EditadaEm), a LISTA DE CONVERSAS (`ConversasEquipe`, uma linha por conversa com a
   última mensagem — a "lista de chats" deles) e um índice de texto para a busca.
3. **Página pequena e texto sob demanda**: a conversa abre com as últimas 40, as anteriores vêm
   por página, e de cada mensagem vem só o começo (600 caracteres) — o laudo inteiro vem no
   "Ver inteiro", no "Copiar" ou no "Corrigir" (o "Ler mais" deles).
4. **A busca vai a um índice**, não às mensagens: no Postgres, `to_tsvector('simple',
   "TextoBusca")` com índice GIN; `TextoBusca` é o texto minúsculo e sem acento, gravado junto
   com a mensagem. Procurar em dez anos custa o mesmo que em um dia.
5. **O que é da mesma pergunta vem na mesma ida**: uma consulta traz quem pode ver a conversa,
   a presença, quem está escrevendo e até onde cada um leu (`ConsultaParticipantes`); as não
   lidas são CONTADAS no banco, a partir da leitura de cada conversa (sem trazer as linhas e sem
   teto que deixe o número errado); enviar grava a mensagem, a lista de conversas e a presença
   num `SaveChanges` só.
6. **O chat não disputa a abertura do sistema**: nenhuma leitura até 2 s depois de a moldura
   abrir; a lista completa (nomes, presença) só quando o chat abre; e o chat não usa as vagas do
   `ILeituraParalela`, que são das telas.

Idas medidas (`ChatEquipeIdasAoBancoTests`, que reprova se alguma passar do orçamento):

| operação | antes | agora | quando |
|---|---|---|---|
| bolinha (pulso) | 9 | **1** | a cada 16 s e a cada mensagem de qualquer pessoa |
| lista | 9 | 2 (bolinha) / 4 (chat aberto) | só quando o pulso acha algo, e a cada 2 min |
| abrir conversa | 6 + 3 (marcar) | **3** (já marca lida) | ao abrir; reabrir mostra na hora e relê com 2 |
| releitura da conversa aberta | 7 | **2** | a cada 4 s, só com o chat aberto |
| enviar | 7 + 7 | **2** (+2 da releitura) | ao enviar |
| presença | 3 | **1** | a cada 30 s |
| buscar | — | 2 | uma vez, 350 ms depois de parar de digitar |

Com 250 ms por ida: abrir uma conversa leva ~0,8 s na primeira vez e é instantâneo nas
seguintes; a bolinha fechada custa 0,25 s a cada 16 s, em segundo plano, sem travar nada.

A releitura da conversa aberta mescla o que chegou no que está na tela
(`ChatEquipeRegras.Mesclar`), então as mensagens anteriores que a pessoa carregou continuam lá.
As correções são pedidas com 5 minutos de folga, porque a hora da correção é a do posto de quem
corrigiu. Chegaram mais de 60 de uma vez: a tela pede as próximas páginas (até 5).

Enter envia e Shift+Enter quebra a linha, decidido na página (`clinicaChat.ligarCampo`) na hora
da tecla — o `@onkeydown:preventDefault` do Blazor só valia no render seguinte.

## 🧑‍💻 UX

- A bolinha não tira lugar de ninguém e sobe por cima dos rodapés de formulário.
- Prévia e som só para o que não está à vista: a conversa aberta não toca.
- "Novas mensagens" separa o que chegou desde a última leitura, e as mensagens novas entram
  deslizando. "Está escrevendo…" aparece para os outros no máximo a cada 3 s de digitação.
- Falha de leitura não derruba a moldura: a bolinha mantém o último número e a lista diz que
  não leu.

## 🛠️ DX

- Regras puras em `Clinica.Application/Servicos/ChatEquipeRegras.cs` (com testes): canais,
  quem vê, menções, recorte do texto, som, presença, "Lida por".
- Serviço em `Clinica.Infrastructure/ChatEquipeService.cs`, contrato em
  `IChatEquipeService` (cada método diz quantas idas custa). A conversa é uma chave de texto
  (`canal:geral`, `direta:{menor}:{maior}`); `ConversasEquipe` é só a LISTA (última mensagem e
  quem participa da direta), gravada no mesmo `SaveChanges` do envio.
- ⚠️ Mudou uma consulta do chat? Rode `ChatEquipeIdasAoBancoTests` (o orçamento de idas) e
  `TraducaoNoNpgsqlTests` (a tradução para o Postgres e a expressão do índice de busca). Quem
  grava mensagem fora do serviço (a semente da prévia) preenche `TextoBusca` e `ConversasEquipe`.
- A tela tem teste próprio (`ChatDaEquipeBlazorTests`): texto longo, @, busca, Esc, só leitura
  e a prévia com o pisca.
- Tela: `Clinica.Ui.Blazor/Moldura/ChatDaEquipe.razor`, CSS só de tokens em
  `telas/chat-equipe.css`, ponte JS `window.clinicaChat` em `janela.js`.
- O componente se desliga sozinho onde o hospedeiro não o serve (prévia sem login, testes da
  moldura). Na prévia, `?usuario=carla.previa` na página da aula liga o chat com dados
  fictícios (`Sementes/ChatDaEquipe.cs`).

## 🤝 CX

- Tira do WhatsApp pessoal o recado interno e o laudo: o texto fica no sistema da clínica,
  com autoria, hora e versão anterior das correções.
- Custo: seis tabelas novas (migration aditiva) e os índices de busca. Por máquina, uma ida a
  cada 16 s com a bolinha fechada; com o chat aberto, duas a cada 4 s (ver ⚡ Desempenho).
- O que NÃO tem, de propósito: apagar mensagem, voz, vídeo, figurinha e grupo livre.

## Desvios declarados do mockup

- O som é sintetizado no navegador em vez de arquivos `.wav` tocados pelo hospedeiro.
- Os pedidos antigos (já tratados) continuam aparecendo no `# Recepção`, na ordem em que
  foram feitos, como o histórico da tela de pedidos.
