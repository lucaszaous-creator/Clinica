# Manuais de uso — os cinco aplicativos

Um PDF por aplicativo, escrito para **quem vai usar** (as funcionárias da clínica), e não
para quem programa. Linguagem simples, passo a passo, com uma **ilustração de tela** por
assunto e uma tabela “o que cada botão faz”.

| Manual | Para quem | Páginas |
|---|---|---|
| [`pdf/manual-recepcao.pdf`](pdf/manual-recepcao.pdf) | recepcionistas e balcão (e a enfermagem, quando usa este exe) | ~43 |
| [`pdf/manual-consultorio.pdf`](pdf/manual-consultorio.pdf) | médicos, fisioterapeutas, acupunturistas, enfermagem | ~30 |
| [`pdf/manual-financeiro.pdf`](pdf/manual-financeiro.pdf) | quem cuida do dinheiro | ~26 |
| [`pdf/manual-gerente.pdf`](pdf/manual-gerente.pdf) | direção | ~24 |
| [`pdf/manual-faturamento.pdf`](pdf/manual-faturamento.pdf) | faturista | ~24 |

## Como gerar de novo

```bash
node tools/gerar-manuais.js
```

Lê os `manual-*.html` desta pasta e imprime os PDFs em `pdf/`. Exige Playwright + Chromium
(já presentes no ambiente de trabalho). O rodapé com “página X de Y” é desenhado pelo
gerador, e não por CSS — o Chromium não implementa as caixas de margem do `@page`.

## Como as ilustrações de tela funcionam

⚠️ **Elas não são capturas de tela.** O WPF só roda no Windows, e estes manuais são
gerados no ambiente de desenvolvimento (Linux). As figuras são **reproduções em HTML/CSS**
da tela real, e o que as mantém honestas é o seguinte:

1. **Os rótulos vêm do XAML.** Nome de botão, cabeçalho de coluna, rótulo de campo, texto
   de dica — tudo foi lido dos `*.xaml` de cada módulo, não escrito de memória.
2. **As cores vêm dos tokens.** O `_estilo.css` repete os valores de
   `src/Clinica.Desktop.Shell/Styles/Tokens.xaml`. Mudar um lá sem mudar aqui torna o
   manual mentiroso — é o mesmo débito das duas cópias do design system (parcela 7).
3. **O conteúdo é de exemplo.** Os pacientes, valores e datas das figuras são inventados
   de propósito; nenhum dado real da clínica entra num documento que vai por e-mail.

Quando alguém puder rodar os cinco apps no Windows, o caminho de melhoria é trocar as
figuras por capturas de verdade — a estrutura dos manuais não muda, só o `<figure>`.

## O que fazer quando uma tela mudar

O manual é a **promessa mais barata de quebrar** do repositório: ele não compila contra
nada, e uma frase que descreve um botão que não existe é a garantia aparente em prosa
(a mesma razão por que a tela de Ajuda do sistema fala de regras, e não de leiaute).

Por isso os manuais falam de **regras estáveis** (a guia nasce quando o atendimento entra,
prontuário não se apaga, previsto ≠ realizado) e citam leiaute só onde ele é o assunto.
Ainda assim: **parcela que renomear item de menu, botão ou aba mexe no manual do módulo
dela no mesmo commit.** É mais barato do que descobrir pelo cliente.
