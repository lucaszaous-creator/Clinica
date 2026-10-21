# Chat da equipe (out/2026)

Mockup aprovado: `docs/mockups/chat-da-equipe.html` (v3). Vale para os quatro apps da suíte
(Recepção, Consultório, Financeiro, Gerente Geral). O Faturamento fica de fora: ele não usa a
moldura Blazor.

## O que é

Uma conversa interna da clínica, dentro do próprio sistema, em três posições:

- **Bolinha** no canto de baixo à direita, com o número de não lidas. Quando chega mensagem
  direta, menção ou pedido da enfermagem, uma prévia aparece por 8 segundos acima dela.
- **Janelinha** no canto, por cima da tela, ao clicar na bolinha.
- **Painel encostado** na lateral direita. A tela de trás encolhe para dar lugar.

A posição, a conversa aberta e as preferências de som ficam guardadas por pessoa, neste
computador.

## Conversas

- **Três canais fixos**: `# Geral` (toda a equipe), `# Recepção` (balcão, enfermagem e
  direção) e `# Enfermagem` (enfermagem, quem atende e direção). Quem vê cada canal sai do
  PERFIL de acesso (`ChatEquipeRegras.ParticipaDoCanal`). Não há grupo livre.
- **Diretas** entre quaisquer duas pessoas ativas. A lista mostra toda a equipe ativa, com
  presença: online (verde), em atendimento (laranja, com a hora prevista de fim) e fora.

## O que dá para mandar

- **Texto sem limite**. Mensagem longa (mais de 500 caracteres ou 8 linhas) chega recolhida,
  com "Ver inteiro", "Copiar texto" e a contagem de caracteres. "Ampliar" abre um editor de
  tela grande para escrever o laudo.
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
- **@menção** pelo primeiro nome. Só toca para quem enxerga a conversa.
- **Correção** da própria mensagem: ela ganha "(editada)" e a versão anterior fica guardada.
  No chat nada se apaga.

## Pedidos da enfermagem no # Recepção

Os pedidos aparecem como cartões entre as mensagens do `# Recepção`. Eles NÃO são copiados
para o chat: o cartão lê o mesmo pedido da tela "Pedidos da enfermagem". "Abrir pedido" leva
à mesma tela que o sino abre, e "Marcar como visto" chama o mesmo serviço. O número vermelho
ao lado do canal conta os pedidos novos.

## Som e avisos

Mensagem direta e menção tocam um toque curto, e pedido da enfermagem toca um toque grave.
Mensagem comum de canal só sobe o número. Cada toque pode ser desligado, e "Silenciar por 1
hora" cala tudo sem parar a contagem. O som é sintetizado no navegador da janela, sem
arquivo de áudio.

## Entrega

O gatilho `clinica_avisar_mensagens_equipe` avisa as outras máquinas na hora (o mesmo
`IAvisosDoBanco` do sino). Um relógio de fundo continua relendo: a conversa aberta a cada 4 s
("está escrevendo", "Lida") e a lista a cada 16 s. Aviso perdido não deixa o chat parado.

A releitura da conversa aberta é INCREMENTAL (`AtualizarAsync`): traz só as mensagens depois da
última na tela, as correções recentes e até onde cada um leu, e a tela as mescla
(`ChatEquipeRegras.Mesclar`). Assim as mensagens anteriores que a pessoa carregou continuam na
tela, e o laudo já mostrado não atravessa a rede de novo a cada 4 s. As correções são pedidas com
5 minutos de folga, porque a hora da correção é a do posto de quem corrigiu. Chegaram mais de 60
de uma vez: a tela relê a conversa inteira.

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
  `IChatEquipeService`. A conversa é uma chave de texto (`canal:geral`,
  `direta:{menor}:{maior}`), sem tabela de conversa.
- Tela: `Clinica.Ui.Blazor/Moldura/ChatDaEquipe.razor`, CSS só de tokens em
  `telas/chat-equipe.css`, ponte JS `window.clinicaChat` em `janela.js`.
- O componente se desliga sozinho onde o hospedeiro não o serve (prévia sem login, testes da
  moldura). Na prévia, `?usuario=carla.previa` na página da aula liga o chat com dados
  fictícios (`Sementes/ChatDaEquipe.cs`).

## 🤝 CX

- Tira do WhatsApp pessoal o recado interno e o laudo: o texto fica no sistema da clínica,
  com autoria, hora e versão anterior das correções.
- Custo: cinco tabelas novas (migration aditiva). Por máquina, a leitura da lista a cada 16 s
  (canais, diretas, presença e quem está em atendimento hoje) e, só com o chat aberto, a
  releitura incremental da conversa a cada 4 s — poucas linhas quando nada mudou.
- O que NÃO tem, de propósito: apagar mensagem, voz, vídeo, figurinha e grupo livre.

## Desvios declarados do mockup

- O som é sintetizado no navegador em vez de arquivos `.wav` tocados pelo hospedeiro.
- Os pedidos antigos (já tratados) continuam aparecendo no `# Recepção`, na ordem em que
  foram feitos, como o histórico da tela de pedidos.
