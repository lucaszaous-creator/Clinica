# Treinamento — as videoaulas dos cinco apps

A aba **Treinamento** de cada app mostra uma videoaula por tela que o app publica (o recorte
é o `ShellViewModel.TelasDoTreinamento`). O catálogo embutido é
`src/Clinica.Desktop.Shell/Treinamento/catalogo.json`; os vídeos são baixados da release
`AcervoTreinamento.ReleaseDosVideos`, nomeados pelo SHA-256 e conferidos antes de tocar.

## Como os vídeos são feitos (out/2026) — tudo no GitHub

Nada roda na máquina da clínica nem na de quem programa. O workflow
`.github/workflows/treinamento.yml` faz, em ordem:

1. **Navegação** (`captura/`, Windows): monta os MESMOS módulos e o MESMO `ShellViewModel`
   dos executáveis e grava `navegacao.json` — o menu do topo, as abas e o componente Razor
   de cada chave.
2. **Inventário** (`gravacao/inventario.js`, Linux): abre cada tela na prévia
   (`tools/PreviaBlazor`, página `/aula/{app}/{chave}` — a moldura real do app em volta da
   tela real, com os dados fictícios das sementes) e grava foto e controles de cada uma. É
   o material para escrever e conferir roteiro.
3. **Gravação** (`gravacao/gravar.js`, Linux, 20 máquinas em paralelo): para cada roteiro em
   `gravacao/roteiros/<chave>.json`, sobe uma prévia NOVA, executa cada função da tela de
   verdade e grava a tela limpa mais a linha do tempo (onde está cada elemento, quando foi o
   clique).
4. **Motion design** (`gravacao/renderizar.js` + `gravacao/motion/`, Remotion): abertura
   animada, a janela do app sobre o fundo da marca, câmera que vai até cada função e volta
   para mostrar o resultado, destaque e cursor animados, painel com a função da vez e o
   índice da aula, fechamento com o resumo. Sai o MP4 1920×1080 e a entrada do catálogo, com
   um capítulo por função. Roda em 20 máquinas do GitHub, uma por fatia da gravação.
   `renderizar-lambda.js` faz o mesmo na AWS Lambda, e só compensa com a cota da conta
   acima de ~100 execuções simultâneas (a conta nova tem 10, e cada aula pede ~20).
5. **Juntar** (`gravacao/juntar.js`): um catálogo só e os MP4 renomeados pelo SHA-256 — o
   artefato `videoaulas`.

Como escrever um roteiro: `gravacao/ROTEIROS.md`.

**Regravar só o que falhou**: ponha as chaves em `gravacao/regravar.txt` (uma por linha) e o
número da rodada anterior em `gravacao/reaproveitar.txt`. A montagem pega as gravações daquela
rodada (elas vivem 7 dias) e a aula regravada vence a antiga. Esvazie os dois depois de publicar.
A aula que falha deixa `<chave>.erro.png` no artefato da fatia: a tela no instante da falha.

## Publicar

1. Baixar o artefato `videoaulas` do run verde.
2. Criar a release `ReleaseDosVideos` (se ainda não existir) e subir os arquivos de
   `videoaulas/release/` **sem sobrescrever** nenhum já publicado.
3. Copiar `videoaulas/catalogo.json` para `src/Clinica.Desktop.Shell/Treinamento/catalogo.json`.
4. Publicar os apps. A release antiga continua no ar: o app que ainda não atualizou baixa os
   vídeos dela.
