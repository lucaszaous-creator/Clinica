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
  os documentos que quem escreve pode ver, e abrir registra o acesso ao prontuário.
- **Arquivo** de até 10 MB.
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
- Custo: cinco tabelas novas (migration aditiva) e uma consulta pequena por máquina a cada
  16 s, mais uma a cada 4 s só com o chat aberto.
- O que NÃO tem, de propósito: apagar mensagem, voz, vídeo, figurinha e grupo livre.

## Desvios declarados do mockup

- O som é sintetizado no navegador em vez de arquivos `.wav` tocados pelo hospedeiro.
- Os pedidos antigos (já tratados) continuam aparecendo no `# Recepção`, na ordem em que
  foram feitos, como o histórico da tela de pedidos.
