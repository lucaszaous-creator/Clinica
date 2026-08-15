# Vertical 9:16 — 41s

`vertical-41s.html` — 1080×1920, 41s, oito batidas. Peça curta para WhatsApp e
Instagram. Ela **soma** ao vídeo de 90s em 16:9 (`../cenas-animadas.html`); não
o substitui — aquele é a entrega formal, com as telas em tela cheia.

Abra e aperte espaço. `C` liga o modo captura, `←` `→` pulam batidas, `?t=25`
abre num segundo, `?still=1` congela tudo no estado final para conferir
enquadramento (nunca grave assim).

## As oito batidas

| # | s | Batida |
|---|---|---|
| 1 | 0,0–4,5 | "A guia que fica para amanhã" — o gancho |
| 2 | 4,5–9,0 | "+24h" — 1º baixado, 2º em aberto |
| 3 | 9,0–14,0 | Os dias correndo, "+3" |
| 4 | 14,0–19,5 | "Cada guia tem cor" — o semáforo |
| 5 | 19,5–25,5 | **A tela real**, dentro de mockup |
| 6 | 25,5–30,5 | "Passou do prazo? o sistema para" |
| 7 | 30,5–36,0 | "Cinco programas, um banco" |
| 8 | 36,0–41,0 | Fecho — marca |

Gravar: OBS → fonte **Navegador**, arquivo local, **1080×1920**, 30fps, URL
terminando em `?c=1&play=1`. Comece a gravar e use "Atualizar cache da página
atual" para disparar.

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
- **A batida 5 tem um recorte PRÓPRIO da tela, não a de 16:9 encolhida.**
  Reduzida para caber em 1080 de largura, a fonte da tabela ficaria ilegível —
  e tela que não se lê não prova que o produto existe, que é a única razão da
  batida. Por isso ali há KPIs e quatro linhas, com corpo grande, em vez da
  tela inteira.

⚠️ **As capturas de conferência deste ambiente usam fonte SUBSTITUTA** (não há
Segoe UI aqui). No Windows o texto sai mais estreito que nos PNGs de teste.
