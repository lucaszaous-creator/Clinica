# Tokens de design

Fonte da verdade: `src/Clinica.Desktop/Styles/Tokens.xaml` (espelhado em `tokens/*.css`). Use sempre os **brushes semânticos** (`Brush.*`); as cores primitivas (`Cor.*`) existem só para compor os semânticos.

## Cores semânticas

| Chave XAML | CSS | Valor | Uso |
|---|---|---|---|
| `Brush.Acento` | `--acento` | `#123A9E` | Ação primária, links, seleção ativa |
| `Brush.Acento.Hover` | `--acento-hover` | `#0A2E86` | Hover da ação primária |
| `Brush.Acento.Suave` | `--acento-suave` | `#EEF3FC` | Fundo de item ativo/seleção |
| `Brush.Acento.Tint` | `--acento-tint` | `#D8E3F7` | Texto sobre azul forte |
| `Brush.Foco` | `--foco` | `#3F62C9` | Anel de foco do teclado |
| `Brush.Fundo` | `--fundo` | `#F8FAFC` | Fundo do app |
| `Brush.Superficie` | `--superficie` | `#FFFFFF` | Cartões, tabelas, campos, topbar |
| `Brush.Superficie.Hover` | `--superficie-hover` | `#F1F5F9` | Hover de linhas/itens; cabeçalho de tabela |
| `Brush.Borda` | `--borda` | `#E5E7EB` | Bordas padrão |
| `Brush.Borda.Hover` | `--borda-hover` | `#D1D5DB` | Borda em hover |
| `Brush.Texto.Primario` | `--texto-primario` | `#111827` | Títulos e conteúdo |
| `Brush.Texto.Secundario` | `--texto-secundario` | `#6B7280` | Rótulos, legendas, dicas |
| `Brush.Sucesso` / `.Forte` / `.Suave` | `--sucesso*` | `#16A34A` / `#15803D` / `#DCFCE7` | Estados de sucesso (`.Forte` é a cor de texto) |
| `Brush.Aviso.Texto` / `.Erro.Texto` / `.Info.Texto` | `--*-texto` | `#C2410C` / `#B91C1C` / `#0369A1` | **Texto** semântico sobre fundo suave ou branco — o tom 600/500 não fecha AA |
| `Brush.Aviso` / `.Suave` | `--aviso*` | `#EA580C` / `#FFEDD5` | Avisos |
| `Brush.Erro` / `.Hover` / `.Suave` | `--erro*` | `#DC2626` / `#B91C1C` / `#FEE2E2` | Erros, ações destrutivas |
| `Brush.Info` / `.Suave` | `--info*` | `#0EA5E9` / `#E0F2FE` | Informação |
| `Brush.Sidebar.*` | `--sidebar-*` | — | Fundo/hover/ativo/texto da sidebar |
| `Brush.Snackbar.Sucesso` / `.Erro` | — | `#4ADE80` / `#F87171` | Ícones de estado sobre o fundo escuro do snackbar |
| `Brush.Visor.Fundo` / `.Texto` / `.Guia` | `--visor-*` | `#111827` / `#D1D5DB` / branco 40% | Visor escuro de mídia (preview da webcam na captura da foto) |

Semáforo de urgência do domínio (`UrgenciaParaCorConverter`): verde `#2E7D32`, amarelo `#F9A825`, vermelho `#C62828` — não usar fora do semáforo.

## Tipografia (Segoe UI)

| Chave | Tamanho | Peso | Uso |
|---|---|---|---|
| `Fonte.H1` / estilo `H1` | 24 | Bold | Título de página |
| `Fonte.H2` / estilo `H2` | 20 | SemiBold | Título de cartão/seção |
| `Fonte.H3` / estilo `H3` | 18 | SemiBold | Subseção, título de diálogo |
| `Fonte.Corpo` | 14 | Regular | Texto, campos, botões |
| `Fonte.Tabela` | 13 | Regular | Células e rótulos (`Rotulo`) |
| `Fonte.Legenda` | 12 | Regular | Dicas (`TextoSuave`), badges |

## Espaçamento (múltiplos de 8)

`Espaco.1`=4 · `Espaco.2`=8 · `Espaco.3`=12 · `Espaco.4`=16 · `Espaco.6`=24 · `Espaco.8`=32 · `Espaco.10`=40 · `Espaco.12`=48 · `Espaco.16`=64.
Compostos: `Margem.Pagina`=24, `Padding.Card`=16, `Padding.Campo`=12,8, `Padding.Botao`=12,8, `Padding.BotaoPequeno`=8,4.

## Raios

`Raio.Pequeno`=4 (campos) · `Raio.Medio`=8 (botões, popups) · `Raio.Grande`=12 (cartões) · `Raio.Pilula`=999 (badges).

## Movimento

`Duracao.Rapida`=100ms (hover) · `Duracao.Normal`=150ms (sidebar, switch, chevrons). Nunca acima de 150ms.

## Iconografia

`FonteIcones` = "Segoe Fluent Icons, Segoe MDL2 Assets" (nativas do Windows; nunca emoji). Estilo `Icone` para TextBlocks de glifo. Glifos em uso: pesquisa `E721`, refresh `E72C`, sino `EA8F`, adicionar `E710`, chevrons `E70D/E70E/E76B/E76C`, hambúrguer `E700`, impressora `E749`, check `E73E`, erro `E783`, info `E946`, salvar/exportar `E74E`, mensagem/WhatsApp `E8BD`, pessoa `E77B`.

### A cor da agenda por modalidade (ago/2026)

O traço de 3 px do cartão de horário diz a **família** da modalidade (pedido da direção:
"blocos coloridos por tipo"). A cor sai do enum, nunca do rótulo — a variante cadastrada
herda a cor de quem deriva (a regra do convênio). Estado vence categoria: encaixe segue
laranja e cancelado/falta seguem apagados.

| família | brush | cor |
|---|---|---|
| AcupunturaSimples | `Brush.Acento` (o padrão de sempre) | `#123A9E` |
| AcupunturaComEletro | `Brush.Modalidade.AcupunturaEletro` | `#7C3AED` |
| BsvApenas | `Brush.Modalidade.Bsv` | `#0D9488` |
| BsvComAcupuntura | `Brush.Modalidade.BsvAcupuntura` | `#0EA5E9` |
| Consulta | `Brush.Modalidade.Consulta` | `#DB2777` |

A agenda do **faturamento** não recebe isto, e é decisão: a tarja do cartão de lá
significa STATUS desde sempre, e repintá-la apagaria informação de um app em produção.

### O glifo semântico do CardKpi (ago/2026)

Todo `CardKpi` leva `CardKpi.Icone` (15 px, à esquerda do rótulo, na mesma linha; a cor
repete a do NÚMERO do cartão). O glifo é escolhido pela **métrica**, e a mesma métrica leva
o mesmo glifo em todo o sistema — cartão novo consulta esta tabela antes de inventar:

| métrica | glifo | | métrica | glifo |
|---|---|---|---|---|
| entrada / recebido | `E896` | | saída / enviado | `E898` |
| vencido / falta / urgente | `E7BA` | | baixado / atendido / feito | `E73E` |
| pendente / previsto | `E823` | | guia / documento-página | `E7C3` |
| tempo / espera | `E916` | | NPS / avaliação | `E734` |
| calendário / agenda | `E787` | | pessoa (um) | `E77B` |
| pessoas (vários) | `E716` | | dinheiro | `E825` / `E8C7` |
| glosa / cancelado | `E711` | | taxas | `E9F9` |
| gráfico / indicador | `E9D9` | | dor / enfermagem | `E95E` |
| tendência | `EB05` | | atendimento em curso | `EB51` |
| encaixe / novo | `E710` | | lista / fila | `E8FD` |
| consultas | `E8A5` | | contas | `E8F1` |
| ajuda | `E897` | | | |
