# Roteiro — vídeo de entrega, 90 segundos

**Público:** a Clínica SemDor (cliente atual).
**Tom:** entrega, não venda. Não é "compre"; é "veja o que foi construído".
**Formato:** 16:9, 1920×1080, 30 fps.
**Duração alvo:** 90s (1:30).

Locução calibrada em **~2,6 palavras/segundo** (≈155 wpm), que é o ritmo calmo do
português falado. O texto abaixo tem 226 palavras — se você gravar mais rápido que isso,
sobra tempo no fim; mais devagar, estoura. **Grave a locução primeiro** e ajuste o vídeo
a ela, nunca o contrário.

---

## Mapa de cenas

| # | Tempo | Duração | Cena | Fonte da imagem |
|---|---|---|---|---|
| 1 | 0:00–0:09 | 9s | Abertura | `cenas-animadas.html` |
| 2 | 0:09–0:20 | 11s | A dor — o 2º código | `cenas-animadas.html` |
| 3 | 0:20–0:36 | 16s | O painel com semáforo | `cenas-animadas.html` |
| 4 | 0:36–0:48 | 12s | A rodada bloqueante | `cenas-animadas.html` |
| 5 | 0:48–1:03 | 15s | Cinco apps, um banco | `cenas-animadas.html` |
| 6 | 1:03–1:18 | 15s | Conformidade | `cenas-animadas.html` |
| 7 | 1:18–1:30 | 12s | Fecho | `cenas-animadas.html` |

---

## CENA 1 · Abertura — 0:00–0:09

**Locução (22 palavras)**
> Toda clínica de convênio conhece esta cena: o atendimento acabou, o paciente foi
> embora — e a segunda guia ficou para amanhã.

**Texto em tela**
`A guia que fica para amanhã` *(entra em 0:04)*

**Imagem** — Fundo navy (`#071F5C`). Símbolo da marca entra em escala com fade,
frase abaixo dele em Segoe UI 800.

**Nota de direção:** silêncio nos primeiros 0,5s. Começar com som já tocando é o
que faz o espectador perder a primeira frase.

---

## CENA 2 · A dor — 0:09–0:20

**Locução (28 palavras)**
> Amanhã vira semana. Quando alguém lembra, o prazo já correu. Não é descuido: é uma
> tarefa que nasce vinte e quatro horas depois, quando o dia já acabou.

**Texto em tela**
`+24h` *(pulsa junto com a palavra "vinte e quatro")*

**Imagem** — Linha do tempo horizontal: `Atendimento` → `+24h` → `2ª guia`.
O bloco da 2ª guia entra e vai perdendo opacidade até 35% enquanto os dias correm
ao fundo (14/07 → 15/07 → 16/07). O contador de atraso sobe de `0` para `+3`.

**Nota de direção:** este é o único momento do vídeo em que algo *piora* na tela.
Deixe respirar — não corte antes de o `+3` assentar.

---

## CENA 3 · O painel — 0:20–0:36

**Locução (42 palavras)**
> Por isso o sistema começa pelo fim. A primeira tela do dia é o que está em aberto —
> cada guia com a sua cor. Verde, dentro do prazo. Amarelo, vence hoje. Vermelho, já
> passou. Ninguém precisa procurar: a lista procura por você.

**Texto em tela** — nenhum. A tela do app é o texto.

**Imagem** — Painel de Pendências. Sequência:

| Momento | O que acontece |
|---|---|
| 0:20 | Janela entra de baixo, com sombra |
| 0:22 | Sidebar aparece; item "Pendências" acende com a barra azul |
| 0:23 | Faixa vermelha de alerta desce |
| 0:24 | KPIs contam de 0 até o valor (`6`, `3`, `2`, `8`) |
| 0:26–0:32 | Linhas da tabela entram escalonadas, 180ms entre elas |
| 0:28 / 0:30 / 0:32 | Os pontos do semáforo pulsam na ordem da narração: verde, amarelo, vermelho |

**Nota de direção:** a narração nomeia as cores em ordem *crescente* de urgência e o
pulso na tela segue essa ordem. Se dessincronizar, a cena perde a única coisa que ela
precisa comunicar.

---

## CENA 4 · A rodada bloqueante — 0:36–0:48

**Locução (29 palavras)**
> E quando o prazo vence, o sistema não avisa. Ele para. Cada guia exige uma decisão:
> dar baixa, ou dizer por que não deu. Nada some sem alguém responder.

**Texto em tela**
`O sistema para até alguém responder` *(entra em 0:44)*

**Imagem** — O painel escurece e desfoca; a janela "Rodar pendências" entra por cima
com um leve overshoot. As três guias vencidas aparecem, cada uma com os botões
`Dar baixa` e `Não conformidade`.

**Nota de direção:** a palavra "para" cai exatamente no frame em que a janela trava
no lugar. É a batida mais importante do vídeo.

---

## CENA 5 · Cinco apps, um banco — 0:48–1:03

**Locução (39 palavras)**
> O que começou no faturamento virou a clínica inteira. Cinco programas, um banco. A
> recepção marca, o consultório registra, o financeiro concilia, a direção enxerga. O
> que um grava, o outro lê — na hora, sem ninguém digitar duas vezes.

**Texto em tela**
`Faturamento · Recepção · Consultório · Financeiro · Direção`

**Imagem** — Cinco cartões em arco, cada um com seu ícone. Ao serem nomeados na
locução, acendem um a um. Depois, linhas descem de cada cartão até um cilindro de
banco de dados no centro, e um pulso viaja pelas linhas nos dois sentidos.

**Nota de direção:** o pulso precisa ir **nos dois sentidos**. É literalmente o que
distingue o produto de cinco programas separados.

---

## CENA 6 · Conformidade — 1:03–1:18

**Locução (39 palavras)**
> E o que a lei exige está no código, não no manual. O prontuário não se apaga:
> corrige-se, e a versão anterior fica guardada. Quem abre, fica registrado. E a
> receita sai assinada em ICP-Brasil, conferível no validador do ITI.

**Texto em tela** — três cartões, entrando com a narração:

1. `O prontuário não se apaga` — Lei 13.787/2018, guarda de 20 anos
2. `Quem abriu, ficou registrado` — trilha de acesso, LGPD art. 5º, II
3. `Assinatura ICP-Brasil` — PAdES-B, conferível no validador do ITI

**Nota de direção:** ⚠️ **Não escreva "certificado SBIS/CFM" nem "PAdES-LT".** O sistema
não tem nem um nem outro, e afirmar isso num vídeo é exatamente a garantia aparente que
o projeto recusa desde a parcela 3. O que está escrito acima é o que o código entrega.

---

## CENA 7 · Fecho — 1:18–1:30

**Locução (27 palavras)**
> Cinco aplicativos. Oitenta e quatro serviços. Mil quinhentos e oitenta e dois testes
> automatizados, rodando a cada mudança. Clínica SemDor — construído tela por tela, com
> quem usa.

**Texto em tela** — três números contando, depois a marca:

| Número | Rótulo |
|---|---|
| 5 | aplicativos, um por perfil |
| 84 | serviços de negócio |
| 1.582 | testes automatizados |

**Imagem** — Volta ao navy da abertura. Números contam em 1,2s cada, escalonados.
Aos 1:26 eles saem e entra o logo com a assinatura.

**Nota de direção:** deixe **1,5s de marca parada e silêncio** no fim. Vídeo que corta
no último fonema parece que acabou a bateria.

---

## Verificação dos números

Todos conferidos no repositório em 15/08/2026 — não são estimativa:

```bash
grep -rE '^\s*\[Fact\]' tests/ | wc -l        # 1373
grep -rE '^\s*\[InlineData' tests/ | wc -l    #  209  → total 1582
grep -l "WinExe" src/*/*.csproj                #    5 executáveis
ls src/Clinica.Application/Servicos/*.cs | wc -l  # 84 serviços
```

⚠️ **Recontar antes de renderizar.** O número cresce a cada parcela, e um vídeo que
diz 1.582 quando já são 1.640 não fica errado — fica desatualizado, que num material de
entrega é pior.

---

## O que este roteiro deliberadamente NÃO diz

Escrito aqui para não ser reintroduzido por engano numa revisão:

- **Nenhum valor em reais.** O sistema é de faturamento, não de recebíveis — não há
  campo de dinheiro por guia. "Recuperamos R$ X" seria um número que o sistema não tem
  como produzir.
- **Nenhuma promessa de percentual** ("reduza 90% das glosas"). Não foi medido.
- **Nada sobre SBIS/CFM, PAdES-LT ou substituição do papel.** Ver Cena 6.
- **Nenhum dado de paciente real.** Todos os nomes nas telas são fictícios.
