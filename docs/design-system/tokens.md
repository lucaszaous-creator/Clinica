# Tokens de design

Fonte da verdade: `src/Clinica.Desktop/Styles/Tokens.xaml` (espelhado em `tokens/*.css`). Use sempre os **brushes semânticos** (`Brush.*`); as cores primitivas (`Cor.*`) existem só para compor os semânticos.

## Cores semânticas

| Chave XAML | CSS | Valor | Uso |
|---|---|---|---|
| `Brush.Acento` | `--acento` | `#2563EB` | Ação primária, links, seleção ativa |
| `Brush.Acento.Hover` | `--acento-hover` | `#1D4ED8` | Hover da ação primária |
| `Brush.Acento.Suave` | `--acento-suave` | `#EFF6FF` | Fundo de item ativo/seleção |
| `Brush.Acento.Tint` | `--acento-tint` | `#DBEAFE` | Texto sobre azul forte |
| `Brush.Foco` | `--foco` | `#3B82F6` | Anel de foco do teclado |
| `Brush.Fundo` | `--fundo` | `#F8FAFC` | Fundo do app |
| `Brush.Superficie` | `--superficie` | `#FFFFFF` | Cartões, tabelas, campos, topbar |
| `Brush.Superficie.Hover` | `--superficie-hover` | `#F1F5F9` | Hover de linhas/itens; cabeçalho de tabela |
| `Brush.Borda` | `--borda` | `#E5E7EB` | Bordas padrão |
| `Brush.Borda.Hover` | `--borda-hover` | `#D1D5DB` | Borda em hover |
| `Brush.Texto.Primario` | `--texto-primario` | `#111827` | Títulos e conteúdo |
| `Brush.Texto.Secundario` | `--texto-secundario` | `#6B7280` | Rótulos, legendas, dicas |
| `Brush.Sucesso` / `.Forte` / `.Suave` | `--sucesso*` | `#16A34A` / `#15803D` / `#DCFCE7` | Estados de sucesso |
| `Brush.Aviso` / `.Suave` | `--aviso*` | `#EA580C` / `#FFEDD5` | Avisos |
| `Brush.Erro` / `.Hover` / `.Suave` | `--erro*` | `#DC2626` / `#B91C1C` / `#FEE2E2` | Erros, ações destrutivas |
| `Brush.Info` / `.Suave` | `--info*` | `#0EA5E9` / `#E0F2FE` | Informação |
| `Brush.Sidebar.*` | `--sidebar-*` | — | Fundo/hover/ativo/texto da sidebar |
| `Brush.Snackbar.Sucesso` / `.Erro` | — | `#4ADE80` / `#F87171` | Ícones de estado sobre o fundo escuro do snackbar |

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
