# Biblioteca de componentes

Todos em `src/Clinica.Desktop/Styles/Componentes/`. Estados cobertos por padrão: normal, hover, focus (anel azul), disabled (opacidade 0.5–0.7); loading onde indicado.

## Botões (`Botoes.xaml`)

Template único (`TemplateBotaoBase`): hover/pressed por véu escuro sobreposto (funciona sobre qualquer cor), anel de foco externo, spinner de loading.

| Estilo | Uso |
|---|---|
| implícito (`Button`) | Primário azul — a ação principal da tela (uma por tela) |
| `BotaoSecundario` | Contorno branco — ações secundárias |
| `BotaoFantasma` | Sem fundo — ações discretas |
| `BotaoPerigo` | Vermelho — destrutivas (excluir, estornar, cancelar) |
| `BotaoPequeno` (+`BotaoAcaoGrid`, `BotaoAcaoGridSecundario`, `BotaoAcaoGridPerigo`) | Linhas de tabela e barras densas |
| `BotaoIcone` | Só glifo, quadrado 36px (topbar). Exige `ToolTip` e `AutomationProperties.Name` |

**Loading**: `ctrl:Ajudantes.EstaCarregando="{Binding Salvando}"` — mostra spinner e bloqueia o clique.

```xml
<Button Content="Salvar" Command="{Binding SalvarCommand}"
        ctrl:Ajudantes.EstaCarregando="{Binding Salvando}" />
```

## Campos (`Campos.xaml`)

- `TextBox` implícito: raio 4, foco azul 2px, placeholder via `ctrl:Ajudantes.Placeholder="…"`.
- `CampoPesquisa`: TextBox com lupa à esquerda (pesquisa instantânea = `UpdateSourceTrigger=PropertyChanged` + filtro no VM).
- `ComboBox`/`ComboBoxItem`: flat, popup com sombra leve, seleção azul-suave.
- `DatePicker`: alinhado aos campos (36px).

## Seleção (`Selecao.xaml`)

- `CheckBox` implícito: caixa 16px, check branco sobre azul.
- `RadioButton` implícito: círculo + ponto azul.
- `Switch` (aplicar a um CheckBox): trilho 38×20 com bolinha animada 150ms — para preferências liga/desliga.

## Tabelas (`Tabelas.xaml`)

`DataGrid` implícito: cabeçalho cinza-100 40px clicável com **seta de ordenação** (`SortDirection`), linhas 36px alternadas, hover cinza, seleção azul-suave, virtualização de linhas ativa. Ações por linha com `BotaoAcaoGrid*`. Não envolver DataGrid em ScrollViewer (quebra a virtualização).

## Navegação (`Navegacao.xaml`)

- `TabControl`/`TabItem`: abas sublinhadas (2px azul no ativo).
- Breadcrumb: `BreadcrumbItem` (clicável), `BreadcrumbAtual`, `BreadcrumbSeparador` (chevron).
- `BotaoPaginacao`: blocos 32px para paginação (uso pleno na fase 2).

## Feedback (`Feedback.xaml`)

- **Badges**: `Badge` (neutro), `Badge.Sucesso`, `Badge.Aviso`, `Badge.Erro`, `Badge.Info` — pílula com fundo suave + texto forte. `BadgeContador` para contadores (sino da topbar). `Pilula` é legado.
- **Alertas**: `AlertaPerigo` (faixa vermelha), `AlertaAviso`, `AlertaSucesso`, `AlertaInfo`.
- **EmptyState** (`ctrl:EmptyState`): ícone + título + descrição + ação opcional. Sobrepor à DataGrid com `Panel.ZIndex="1"` e trigger em `Itens.Count == 0` (exemplos em DashboardView e ConsultaGuiasView).
- **Loading**: `Spinner` (Control girando) e `Skeleton` (Border pulsante para placeholders).
- **Snackbar**: host único no MainWindow bindado ao `SnackbarService`; nos VMs, injete `ISnackbarService` e chame `Sucesso/Erro/Info("…")`. Auto-dispensa em 4s. Confirmações Sim/Não continuam em diálogo.

## Sobreposição (`Sobreposicao.xaml`)

- `ToolTip` implícito escuro (obrigatório em botões só-ícone e na sidebar recolhida).
- `ScrollBar` fina 8px.
- `DialogoTitulo` para cabeçalho de janelas modais; todo diálogo tem botão `IsDefault` e `IsCancel`.

## Cartões (`Cartoes.xaml`)

- `Card`: branco, borda 1px, raio 12, padding 16.
- `CardKpi` + `CardKpi.Rotulo` + `CardKpi.Valor`: indicadores do painel (variantes coloridas trocando `Background` pelos tints semânticos).
- `Expander` implícito: accordion com chevron animado.
