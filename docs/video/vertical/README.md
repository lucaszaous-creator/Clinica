# Vertical 9:16 — amostra de estilo

`amostra-estilo.html` — 1080×1920, ~16s, quatro batidas. **Não é o vídeo
final**: existe para julgar a LINGUAGEM antes de escrever um roteiro inteiro.

Abra e aperte espaço. `C` liga o modo captura, `?still=1` congela tudo no
estado final para conferir enquadramento.

## De onde veio

De um vídeo de referência trazido pelo cliente: um post do TikTok
**@oto.criativo** mostrando, filmado do monitor, um brand video da marca
"Zeiz" aberto no Premiere. Dele veio a **linguagem**, nunca o material — a
marca é de outra empresa.

O que foi extraído: fundo off-white quente, **um** azul saturado, tipografia
pesada com ênfase por peso e cor dentro da mesma frase, traço desenhado à mão
sublinhando a palavra-chave, forma orgânica que respira, e fecho em logo sobre
cor cheia. O azul-royal da referência é quase o `#123A9E` do nosso
`Tokens.xaml`, então a transposição não pediu marca nova.

## Por que isto NÃO é gerado por IA

O estilo é **dirigido por tipografia** — não há um quadro sem texto. Geração
de vídeo por IA erra pior exatamente aí: devolve grafia embaralhada. Aqui o
texto é texto de verdade, em vetor, e o mesmo motor da versão 16:9
(`../cenas-animadas.html`) desenha as duas.

## Decisões que o código não conta sozinho

- **Off-white, não branco.** Branco puro endurece o azul e dá cara de slide.
- **Palavra a palavra, não linha a linha.** A entrada escalonada com leve
  rotação é o que dá a batida do reference; em bloco vira PowerPoint.
- **`border-radius` animado em vez de morph de path.** Um `<div>` resolve, e
  os raios têm de ser bem assimétricos **já no repouso** — perto de 50% o
  blob vira círculo, e círculo não se lê como forma orgânica.
- **`white-space:nowrap` no valor do cartão.** A métrica muda com a fonte
  instalada na máquina; um cartão que cabe aqui quebra em duas linhas noutra,
  e aí a fileira sai com a base serrilhada.
- **Sem mascote de olhinhos.** O reference é de app de compras; aqui é o
  sistema que fatura uma clínica. A forma orgânica ficou, a carinha não —
  mas é decisão de tom, e se reverte em duas linhas.

⚠️ **As capturas de conferência deste ambiente usam fonte SUBSTITUTA** (não há
Segoe UI aqui). No Windows o texto sai mais estreito que nos PNGs de teste.
