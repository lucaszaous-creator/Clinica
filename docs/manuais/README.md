# Manuais de uso — os cinco aplicativos

Um PDF por aplicativo, escrito para **quem vai usar** (as funcionárias da clínica), e não
para quem programa. Linguagem simples, passo a passo, uma figura por assunto e uma tabela
“o que cada botão faz”.

| Manual | Para quem | Págs |
|---|---|---|
| [`pdf/manual-recepcao.pdf`](pdf/manual-recepcao.pdf) | recepcionistas e balcão (e a enfermagem, quando usa este exe) | ~42 |
| [`pdf/manual-consultorio.pdf`](pdf/manual-consultorio.pdf) | médicos, fisioterapeutas, acupunturistas, enfermagem | ~30 |
| [`pdf/manual-financeiro.pdf`](pdf/manual-financeiro.pdf) | quem cuida do dinheiro | ~26 |
| [`pdf/manual-gerente.pdf`](pdf/manual-gerente.pdf) | direção | ~24 |
| [`pdf/manual-faturamento.pdf`](pdf/manual-faturamento.pdf) | faturista | ~24 |

## Gerar de novo

```bash
node tools/gerar-manuais.js
```

Exige Playwright + Chromium (já presentes no ambiente de trabalho).

## O PDF é interativo

| Recurso | Como funciona |
|---|---|
| **Marcadores** (painel lateral do leitor) | `outline: true` no gerador, montado dos `<h1>`/`<h2>` — dá ~70 entradas por manual |
| **Sumário clicável** | cada item é `<a href="#cap-N">` e salta para o capítulo |
| **Voltar ao sumário** | link no cabeçalho de cada capítulo |
| **Remissões** | “Veja o capítulo 8” é link |
| **Texto pesquisável e marcado** | `tagged: true` — árvore de estrutura para leitor de tela e reflow |
| **Rodapé com “página X de Y”** | desenhado pelo gerador, não por CSS: o Chromium não implementa as caixas de margem do `@page` |

## As figuras: encaixe para a captura real

⚠️ **As figuras de hoje NÃO são capturas de tela**, e cada uma sai com um selo laranja
dizendo isso. O WPF só roda no Windows (`net8.0-windows` + `UseWPF` nos dez projetos) e os
manuais são gerados no ambiente de desenvolvimento, que é Linux — o SDK de Linux nem traz
o `Microsoft.NET.Sdk.WindowsDesktop`, então aqui não há como **nem construir** os `.exe`.

Cada `<figure data-captura="nome">` é um **encaixe**. O gerador procura
`capturas/<nome>.png` (ou `.jpg`):

- **achou** → a figura sai com a foto e **sem** o selo;
- **não achou** → sai a reprodução com o selo.

Nenhum HTML precisa ser editado. O roteiro do que capturar, com o nome exato de cada
arquivo e o estado em que cada tela deve estar, é
[`capturas/ROTEIRO.md`](capturas/ROTEIRO.md) — inclusive a regra de **não capturar com
paciente real** e como rodar um build portátil contra um banco de teste.

O que mantém as reproduções honestas enquanto elas existem:

1. **Os rótulos vêm do XAML.** Nome de botão, cabeçalho de coluna, rótulo de campo, texto
   de dica — lidos dos `*.xaml` de cada módulo, não escritos de memória.
2. **As cores vêm dos tokens.** O `_estilo.css` repete os valores de
   `src/Clinica.Desktop.Shell/Styles/Tokens.xaml`. Mudar um lá sem mudar aqui torna a
   figura mentirosa — é o mesmo débito das duas cópias do design system (parcela 7).
3. **O conteúdo é de exemplo.** Pacientes, valores e datas são inventados de propósito;
   nenhum dado real entra num documento que vai por e-mail.

## Quando uma tela mudar

O manual é a **promessa mais barata de quebrar** do repositório: ele não compila contra
nada, e uma frase que descreve um botão que não existe é a garantia aparente em prosa —
com o agravante de que quem a encontra é o cliente. Por isso o texto fala de **regras
estáveis** (a guia nasce quando o atendimento entra, prontuário não se apaga, previsto ≠
realizado, “—” é não medido) e cita leiaute só onde ele é o assunto.

**Parcela que renomear item de menu, botão ou aba mexe no manual daquele módulo no mesmo
commit.** É mais barato do que descobrir pelo cliente.
